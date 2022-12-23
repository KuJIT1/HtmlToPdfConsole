namespace ConverterWorker.EventHandling
{
    using System;
    using System.Threading.Tasks;

    using ConverterWorker.Events;

    using EventBus.Abstractions;

    public class StartConvertHtmlFileIntegrationEventHandler : IIntegrationEventHandler<StartConvertHtmlFileIntegrationEvent>
    {

        public StartConvertHtmlFileIntegrationEventHandler()
        {

        }

        public Task Handle(StartConvertHtmlFileIntegrationEvent @event)
        {
            throw new NotImplementedException();
        }
    }
}
