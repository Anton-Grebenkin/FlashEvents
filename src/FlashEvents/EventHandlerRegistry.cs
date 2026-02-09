using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlashEvents
{
    internal class EventHandlerRegistry : IEventHandlerRegistry
    {
        private readonly Dictionary<Type, List<Type>> _handlerMappings = new();
        private readonly ILogger<EventHandlerRegistry>? _logger;

        public EventHandlerRegistry(ILogger<EventHandlerRegistry>? logger = null)
        {
            _logger = logger;
        }

        public void RegisterByType(Type eventType, Type handlerType)
        {
            if (!_handlerMappings.TryGetValue(eventType, out var handlerTypes))
            {
                handlerTypes = new List<Type>();
                _handlerMappings[eventType] = handlerTypes;
            }

            handlerTypes.Add(handlerType);

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
                _logger.LogDebug("Registered handler {HandlerType} for event {EventType}.", handlerType, eventType);
        }

        public ICollection<Type> GetHandlerTypesFor<TEvent>() where TEvent : IEvent
        {
            return _handlerMappings.TryGetValue(typeof(TEvent), out var handlerTypes)
                ? handlerTypes
                : Array.Empty<Type>();
        }

        public ICollection<Type> GetHandlerTypesFor<TEvent, THandlerInterface>()
            where TEvent : IEvent
            where THandlerInterface : IEventHandler
        {
            if (!_handlerMappings.TryGetValue(typeof(TEvent), out var handlerTypes) || handlerTypes.Count == 0)
                return Array.Empty<Type>();

            List<Type>? filtered = null;
            foreach (var handlerType in handlerTypes)
            {
                if (!typeof(THandlerInterface).IsAssignableFrom(handlerType))
                    continue;

                (filtered ??= new List<Type>()).Add(handlerType);
            }

            return filtered is null ? Array.Empty<Type>() : filtered;
        }

        public static EventHandlerRegistry GetOrCreateRegistry(IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventHandlerRegistry));

            if (descriptor?.ImplementationInstance is EventHandlerRegistry existingRegistry)
            {
                return existingRegistry;
            }

            var newRegistry = new EventHandlerRegistry();
            services.AddSingleton<IEventHandlerRegistry>(newRegistry);

            return newRegistry;
        }
    }
}
