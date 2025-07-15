using System;

namespace FlashEvents.Abstractions
{
    public interface IEventHandler<in T> : IEventHandler where T : IEvent
    {
        Task Handle(T @event, CancellationToken ct = default);
    }

    public interface IEventHandler
    {

    }
}
