using FlashEvents.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FlashEvents.Internal
{
    internal interface IHandlerWrapperCache
    {
        IHandlerWrapper GetOrAdd(Type eventType);
    }

    internal sealed class HandlerWrapperCache : IHandlerWrapperCache
    {
        private readonly ConcurrentDictionary<Type, IHandlerWrapper> _cache = new();
        private readonly ILogger<HandlerWrapperCache>? _logger;

        public HandlerWrapperCache(ILogger<HandlerWrapperCache>? logger = null)
        {
            _logger = logger;
        }

        public IHandlerWrapper GetOrAdd(Type eventType)
        {
            var added = false;
            var wrapper = _cache.GetOrAdd(
                eventType,
                notificationType =>
                {
                    added = true;
                    try
                    {
                        var wrapperType = typeof(HandlerWrapper<>).MakeGenericType(notificationType);
                        var instance = Activator.CreateInstance(wrapperType)
                            ?? throw new InvalidOperationException($"Could not create wrapper for type {notificationType}");
                        return (IHandlerWrapper)instance;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to create handler wrapper for event type {EventType}.", notificationType);
                        throw;
                    }
                });

            if (added && _logger?.IsEnabled(LogLevel.Debug) == true)
                _logger.LogDebug("Created handler wrapper for event type {EventType}.", eventType);

            return wrapper;
        }
    }
}
