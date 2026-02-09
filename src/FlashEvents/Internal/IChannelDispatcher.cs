using FlashEvents.Abstractions;

namespace FlashEvents.Internal;

internal interface IChannelDispatcher
{
    ValueTask EnqueueAsync(IEvent @event, IServiceProvider serviceProvider, IEventHandlerRegistry registry, CancellationToken ct);
}
