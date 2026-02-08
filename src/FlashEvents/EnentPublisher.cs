using FlashEvents.Abstractions;
using FlashEvents.Internal;

namespace FlashEvents
{
    internal class EventPublisher : IEventPublisher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHandlerWrapperCache _handlerWrapperCache;
        private readonly IEventHandlerRegistry _registry;

        public EventPublisher(IServiceProvider serviceProvider, IEventHandlerRegistry registry, IHandlerWrapperCache handlerWrapperCache)
        {
            _serviceProvider = serviceProvider;
            _registry = registry;
            _handlerWrapperCache = handlerWrapperCache;
        }

        public async Task PublishAsync<T>(T notification, CancellationToken ct = default) where T : class, IEvent
        {
            var handlerWrapper = _handlerWrapperCache.GetOrAdd(notification.GetType());
            await handlerWrapper.Handle(notification, _serviceProvider, _registry, ct);
        }
    }
}
