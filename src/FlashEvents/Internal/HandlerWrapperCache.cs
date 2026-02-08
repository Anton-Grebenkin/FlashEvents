using FlashEvents.Abstractions;
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

        public IHandlerWrapper GetOrAdd(Type eventType)
        {
            return _cache.GetOrAdd(
                eventType,
                static notificationType =>
                {
                    var wrapperType = typeof(HandlerWrapper<>).MakeGenericType(notificationType);
                    var wrapper = Activator.CreateInstance(wrapperType)
                        ?? throw new InvalidOperationException($"Could not create wrapper for type {notificationType}");
                    return (IHandlerWrapper)wrapper;
                });
        }
    }
}
