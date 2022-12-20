namespace EventBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using EventBus.Abstractions;
    using EventBus.Events;

    public interface IEventBusSubscriptionsManager
    {
        void AddSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        string GetEventKey<T>(T? @event);

        IEnumerable<Type>? GetHandlersForEvent(string eventName);

        Type? GetEventType(string eventName);

        bool HasSubscriptionsForEvent(string eventName);
    }
}
