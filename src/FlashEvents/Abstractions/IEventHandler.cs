using System;

namespace FlashEvents.Abstractions
{
    public interface IEventHandler
    {
    }

    public interface ISerialEventHandler<in T> : IEventHandler where T : IEvent
    {
        Task Handle(T @event, CancellationToken ct = default);
    }

    public interface IParallelInMainScopeEventHandler<in T> : IEventHandler where T : IEvent
    {
        Task Handle(T @event, CancellationToken ct = default);
    }

    public interface IParallelInDedicatedScopeEventHandler<in T> : IEventHandler where T : IEvent
    {
        Task Handle(T @event, CancellationToken ct = default);
    }

    public interface IChannelEventHandler<in T> : IEventHandler where T : IEvent
    {
        Task Handle(T @event, CancellationToken ct = default);
    }
}
