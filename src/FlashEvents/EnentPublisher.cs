using FlashEvents.Abstractions;
using FlashEvents.Internal;
using Microsoft.Extensions.Logging;

namespace FlashEvents
{
    internal class EventPublisher : IEventPublisher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHandlerWrapperCache _handlerWrapperCache;
        private readonly IEventHandlerRegistry _registry;
        private readonly ILogger<EventPublisher>? _logger;

        public EventPublisher(
            IServiceProvider serviceProvider,
            IEventHandlerRegistry registry,
            IHandlerWrapperCache handlerWrapperCache,
            ILogger<EventPublisher>? logger = null)
        {
            _serviceProvider = serviceProvider;
            _registry = registry;
            _handlerWrapperCache = handlerWrapperCache;
            _logger = logger;
        }

        public async Task PublishAsync<T>(T notification, CancellationToken ct = default) where T : class, IEvent
        {
            var eventType = notification.GetType();

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
                _logger.LogTrace("Publishing event {EventType}...", eventType);

            try
            {
                var handlerWrapper = _handlerWrapperCache.GetOrAdd(eventType);
                await handlerWrapper.Handle(notification, _serviceProvider, _registry, ct).ConfigureAwait(false);

                if (_logger?.IsEnabled(LogLevel.Trace) == true)
                    _logger.LogTrace("Published event {EventType}.", eventType);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                    _logger.LogDebug("Publishing event {EventType} was cancelled.", eventType);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unhandled exception while publishing event {EventType}.", eventType);
                throw;
            }
        }
    }
}
