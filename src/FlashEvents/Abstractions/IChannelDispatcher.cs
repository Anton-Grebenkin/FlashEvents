namespace FlashEvents.Abstractions;

internal interface IChannelDispatcher
{
    ValueTask EnqueueAsync(IEvent @event, IServiceProvider serviceProvider, IEventHandlerRegistry registry, CancellationToken ct);
}
