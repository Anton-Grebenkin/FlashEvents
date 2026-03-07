using FlashEvents.Abstractions;
using FlashEvents.Internal;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FlashEvents
{
    internal class EventPublisher : IEventPublisher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHandlerWrapperCache _handlerWrapperCache;
        private readonly IEventHandlerRegistry _registry;
        private readonly FlashEventsMetrics _metrics;
        private readonly ILogger<EventPublisher>? _logger;

        public EventPublisher(
            IServiceProvider serviceProvider,
            IEventHandlerRegistry registry,
            IHandlerWrapperCache handlerWrapperCache,
            FlashEventsMetrics metrics,
            ILogger<EventPublisher>? logger = null)
        {
            _serviceProvider = serviceProvider;
            _registry = registry;
            _handlerWrapperCache = handlerWrapperCache;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task PublishAsync<T>(T notification, CancellationToken ct = default) where T : class, IEvent
        {
            var eventType = notification.GetType();

            _logger?.PublishingEvent(eventType);

            var startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                var handlerWrapper = _handlerWrapperCache.GetOrAdd(eventType);
                await handlerWrapper.Handle(notification, _serviceProvider, _registry, ct).ConfigureAwait(false);

                _metrics.EventPublished(eventType.Name);
                _logger?.PublishedEvent(eventType);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger?.PublishingCancelled(eventType);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.PublishingFailed(ex, eventType);
                throw;
            }
            finally
            {
                _metrics.RecordPublishDuration(
                    Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                    eventType.Name);
            }
        }
    }
}
