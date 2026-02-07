using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FlashEvents
{
    internal class EventHandlerRegistry : IEventHandlerRegistry
    {
        private readonly Dictionary<Type, List<Type>> _handlerMappings = new();

        public void RegisterByType(Type eventType, Type handlerType)
        {
            if (!_handlerMappings.TryGetValue(eventType, out var handlerTypes))
            {
                handlerTypes = new List<Type>();
                _handlerMappings[eventType] = handlerTypes;
            }

            handlerTypes.Add(handlerType);
        }

        public ICollection<Type> GetHandlerTypesFor<TEvent>() where TEvent : IEvent
        {
            return _handlerMappings.TryGetValue(typeof(TEvent), out var handlerTypes)
                ? handlerTypes
                : Array.Empty<Type>();
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
