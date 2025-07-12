using FlashEvents.Abstractions;
using System.Collections.Concurrent;

namespace FlashEvents
{
    internal class EventPublisher : IEventPublisher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<Type, IHandlerWrapper> _notificationHandlers = new();

        public EventPublisher(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task PublishAsync<T>(T notification, CancellationToken ct = default) where T : class, IEvent
        {
            var handlerWrapper = _notificationHandlers.GetOrAdd(notification.GetType(), static notificationType =>
            {
                var wrapperType = typeof(HandlerWrapper<>).MakeGenericType(notificationType);
                var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {notificationType}");
                return (IHandlerWrapper)wrapper;
            });

            await handlerWrapper.Handle(notification, _serviceProvider, ct);
        }
    }
}
