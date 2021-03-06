namespace MassTransit.WebJobs.ServiceBusIntegration
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.ServiceBus.Core.Configuration;
    using Azure.ServiceBus.Core.Transport;
    using Microsoft.Azure.ServiceBus;
    using Saga;


    public class MessageReceiver :
        IMessageReceiver
    {
        readonly IServiceBusBusConfiguration _busConfiguration;
        readonly IAsyncBusHandle _busHandle;
        readonly IServiceProvider _provider;
        readonly ConcurrentDictionary<string, IBrokeredMessageReceiver> _receivers;

        public MessageReceiver(IServiceProvider provider, IAsyncBusHandle busHandle, IServiceBusBusConfiguration busConfiguration)
        {
            _provider = provider;
            _busHandle = busHandle;
            _busConfiguration = busConfiguration;

            _receivers = new ConcurrentDictionary<string, IBrokeredMessageReceiver>();
        }

        public Task Handle(string entityName, Message message, CancellationToken cancellationToken)
        {
            var receiver = CreateBrokeredMessageReceiver(entityName, cfg =>
            {
                cfg.ConfigureConsumers(_provider);
                cfg.ConfigureSagas(_provider);
            });

            return receiver.Handle(message, cancellationToken);
        }

        public Task HandleConsumer<TConsumer>(string entityName, Message message, CancellationToken cancellationToken)
            where TConsumer : class, IConsumer
        {
            var receiver = CreateBrokeredMessageReceiver(entityName, cfg =>
            {
                cfg.ConfigureConsumer<TConsumer>(_provider);
            });

            return receiver.Handle(message, cancellationToken);
        }

        public Task HandleSaga<TSaga>(string entityName, Message message, CancellationToken cancellationToken)
            where TSaga : class, ISaga
        {
            var receiver = CreateBrokeredMessageReceiver(entityName, cfg =>
            {
                cfg.ConfigureSaga<TSaga>(_provider);
            });

            return receiver.Handle(message, cancellationToken);
        }

        public Task HandleExecuteActivity<TActivity>(string entityName, Message message, CancellationToken cancellationToken)
            where TActivity : class
        {
            var receiver = CreateBrokeredMessageReceiver(entityName, cfg =>
            {
                cfg.ConfigureExecuteActivity(_provider, typeof(TActivity));
            });

            return receiver.Handle(message, cancellationToken);
        }

        IBrokeredMessageReceiver CreateBrokeredMessageReceiver(string entityName, Action<IReceiveEndpointConfigurator> configure)
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentNullException(nameof(entityName));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            return _receivers.GetOrAdd(entityName, name =>
            {
                var endpointConfiguration = _busConfiguration.HostConfiguration.CreateReceiveEndpointConfiguration(entityName);

                var configurator = new BrokeredMessageReceiverConfiguration(_busConfiguration, endpointConfiguration);

                configure(configurator);

                return configurator.Build();
            });
        }

        public void Dispose()
        {
        }
    }
}
