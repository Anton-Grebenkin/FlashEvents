using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FlashEvents
{
    internal class EventHandlerRegistry : IEventHandlerRegistry
    {
        private readonly Dictionary<Type, List<Type>> _handlerMappings = new();
        internal void RegisterByType(Type eventType, Type handlerType)
        {
            if (!_handlerMappings.TryGetValue(eventType, out var handlerTypes))
            {
                handlerTypes = new List<Type>();
                _handlerMappings[eventType] = handlerTypes;
            }

            handlerTypes.Add(handlerType);
        }

        public IEnumerable<Type> GetHandlerTypesFor<TEvent>() where TEvent : IEvent
        {
            if (_handlerMappings.TryGetValue(typeof(TEvent), out var handlerTypes))
            {
                return handlerTypes;
            }

            return Enumerable.Empty<Type>();
        }

        internal static EventHandlerRegistry GetOrCreateRegistry(IServiceCollection services)
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
