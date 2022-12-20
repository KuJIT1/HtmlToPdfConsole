namespace EventBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using EventBus.Abstractions;
    using EventBus.Events;

    // Не потокобезопасно
    public class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager
    {
        private readonly Dictionary<Type, List<Type>> handlers = new();
        public void AddSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventType = typeof(T);
            var handlerType = typeof(TH);

            if (!this.handlers.ContainsKey(eventType))
            {
                this.handlers.Add(eventType, new());
            }

            if (this.handlers[eventType].Any(h => h == handlerType))
            {
                return;
            }

            this.handlers[eventType].Add(handlerType);
        }

        public string GetEventKey<T>(T? @event)
        {
            return typeof(T).Name;
        }

        public Type? GetEventType(string eventName)
        {
            return this.handlers.Keys.FirstOrDefault(k => k.Name == eventName);
        }

        public IEnumerable<Type>? GetHandlersForEvent(string eventName)
        {
            var key = this.GetEventType(eventName);
            if (key == null)
            {
                return null;
            }

            return this.handlers[key].ToArray();
        }

        public bool HasSubscriptionsForEvent(string eventName)
        {
            return this.GetEventType(eventName) is not null;
        }
    }
}
