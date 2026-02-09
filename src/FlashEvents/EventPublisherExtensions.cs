using FlashEvents.Abstractions;
using FlashEvents.Internal;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FlashEvents
{
    public static class EventPublisherExtensions
    {
        public static IServiceCollection AddEventHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
        {
            var registry = EventHandlerRegistry.GetOrCreateRegistry(services);

            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i =>
                    i.IsGenericType && (
                        i.GetGenericTypeDefinition() == typeof(ISerialEventHandler<>) ||
                        i.GetGenericTypeDefinition() == typeof(IParallelInMainScopeEventHandler<>) ||
                        i.GetGenericTypeDefinition() == typeof(IParallelInDedicatedScopeEventHandler<>) ||
                        i.GetGenericTypeDefinition() == typeof(IChannelEventHandler<>)
                    )));

            foreach (var handlerType in handlerTypes)
            {
                var eventHandlerInterfaces = handlerType.GetInterfaces()
                    .Where(i => i.IsGenericType && (
                        i.GetGenericTypeDefinition() == typeof(ISerialEventHandler<>) ||
                        i.GetGenericTypeDefinition() == typeof(IParallelInMainScopeEventHandler<>) ||
                        i.GetGenericTypeDefinition() == typeof(IParallelInDedicatedScopeEventHandler<>) ||
                        i.GetGenericTypeDefinition() == typeof(IChannelEventHandler<>)
                    ));

                foreach (var interfaceType in eventHandlerInterfaces)
                {
                    services.AddTransient(handlerType);

                    var eventType = interfaceType.GetGenericArguments()[0];
                    registry.RegisterByType(eventType, handlerType);
                }
            }

            return services;
        }

        public static IServiceCollection AddEventPublisher(this IServiceCollection services)
        {
            EventHandlerRegistry.GetOrCreateRegistry(services);

            services.AddSingleton<IHandlerWrapperCache, HandlerWrapperCache>();
            services.AddSingleton<IChannelDispatcher, ChannelDispatcher>();
            services.AddScoped<IEventPublisher, EventPublisher>();

            return services;
        }

        public static IServiceCollection AddEventHandler<TInterface, THandler>(this IServiceCollection services)
            where TInterface : IEventHandler
            where THandler : class, TInterface
        {
            var handlerType = typeof(THandler);

            var eventType = typeof(TInterface).GetGenericArguments()[0];

            services.AddTransient<THandler>();

            var registry = EventHandlerRegistry.GetOrCreateRegistry(services);

            registry.RegisterByType(eventType, handlerType);

            return services;
        }
    }
}
