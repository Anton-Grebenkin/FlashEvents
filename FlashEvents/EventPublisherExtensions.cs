using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FlashEvents
{
    public static class EventPublisherExtensions
    {
        public static IServiceCollection AddEventHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
        {
            var multiOpenInterface = typeof(IEventHandler<>);

            var handlerTypes = assembly.GetExportedTypes()
                .Where(t =>
                    !t.IsAbstract &&
                    !t.IsInterface &&
                    t.GetInterfaces().Any(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == multiOpenInterface))
                .ToList();

            foreach (var handlerType in handlerTypes)
            {
                var handlerInterfaces = handlerType.GetInterfaces()
                    .Where(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == multiOpenInterface);

                foreach (var handlerInterface in handlerInterfaces)
                {
                    services.AddTransient(handlerType);
                    services.AddTransient(handlerInterface, sp => sp.GetRequiredService(handlerType));
                }
            }

            return services;
        }

        public static IServiceCollection AddEventPublisher(this IServiceCollection services)
        {
            services.AddSingleton<IEventPublisher, EventPublisher>();

            return services;
        }

        public static IServiceCollection AddEventHandler<TInterface, THandler>(this IServiceCollection services)
            where TInterface : class, IEventHandler
            where THandler : class, TInterface
        {
            services.AddTransient(typeof(THandler));
            services.AddTransient(typeof(TInterface), sp => sp.GetRequiredService(typeof(THandler)));

            return services;
        }
    }
}
