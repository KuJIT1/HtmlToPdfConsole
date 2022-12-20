namespace EventBus.Abstractions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using EventBus.Events;

    public interface IEventBus
    {
        void Publish(IntegrationEvent @event);

    }
}
