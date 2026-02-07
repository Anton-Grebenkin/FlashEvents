using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FlashEvents
{
    internal class HandlerWrapper<TEvent> : IHandlerWrapper where TEvent : IEvent
    {
        private struct HandlerGroups
        {
            public List<Type>? Serial;
            public List<Type>? ParallelMain;
            public List<Type>? ParallelDedicated;
        }

        public async Task Handle(IEvent @event, IServiceProvider serviceProvider,
            IEventHandlerRegistry registry, CancellationToken ct)
        {
            var handlerTypes = registry.GetHandlerTypesFor<TEvent>();
            if (handlerTypes.Count == 0)
                return;

            var typedEvent = (TEvent)@event;
            var groups = GroupHandlers(handlerTypes);

            if (groups.Serial?.Count > 0)
            {
                await HandleSerialAsync(serviceProvider, groups.Serial, typedEvent, ct);
            }

            var parallelCount = (groups.ParallelMain?.Count ?? 0) + (groups.ParallelDedicated?.Count ?? 0);
            if (parallelCount == 0)
                return;

            var parallelTasks = new List<Task>(parallelCount);

            if (groups.ParallelMain is { Count: > 0 })
            {
                foreach (var handlerType in groups.ParallelMain)
                {
                    parallelTasks.Add(ProcessHandlerAsync(serviceProvider, handlerType, typedEvent, ct));
                }
            }

            if (groups.ParallelDedicated is { Count: > 0 })
            {
                foreach (var handlerType in groups.ParallelDedicated)
                {
                    parallelTasks.Add(ProcessHandlerInDedicatedScopeAsync(serviceProvider, handlerType, typedEvent, ct));
                }
            }

            await Task.WhenAll(parallelTasks);
        }

        private static HandlerGroups GroupHandlers(ICollection<Type> handlerTypes)
        {
            var groups = new HandlerGroups();

            foreach (var handlerType in handlerTypes)
            {
                if (typeof(ISerialEventHandler<TEvent>).IsAssignableFrom(handlerType))
                {
                    (groups.Serial ??= new List<Type>()).Add(handlerType);
                }
                else if (typeof(IParallelInMainScopeEventHandler<TEvent>).IsAssignableFrom(handlerType))
                {
                    (groups.ParallelMain ??= new List<Type>()).Add(handlerType);
                }
                else if (typeof(IParallelInDedicatedScopeEventHandler<TEvent>).IsAssignableFrom(handlerType))
                {
                    (groups.ParallelDedicated ??= new List<Type>()).Add(handlerType);
                }
            }

            return groups;
        }

        private static async Task HandleSerialAsync(IServiceProvider serviceProvider,
            List<Type> handlerTypes, TEvent @event, CancellationToken ct)
        {
            foreach (var handlerType in handlerTypes)
            {
                var handler = (ISerialEventHandler<TEvent>)serviceProvider.GetRequiredService(handlerType);
                await handler.Handle(@event, ct);
            }
        }

        private static async Task ProcessHandlerAsync(IServiceProvider serviceProvider,
            Type handlerType, TEvent @event, CancellationToken ct)
        {
            var handler = (IParallelInMainScopeEventHandler<TEvent>)serviceProvider.GetRequiredService(handlerType);
            await handler.Handle(@event, ct);
        }

        private static async Task ProcessHandlerInDedicatedScopeAsync(IServiceProvider serviceFactory,
            Type handlerType, TEvent @event, CancellationToken ct)
        {
            await using var scope = serviceFactory.CreateAsyncScope();
            var handler = (IParallelInDedicatedScopeEventHandler<TEvent>)scope.ServiceProvider.GetRequiredService(handlerType);
            await handler.Handle(@event, ct);
        }
    }
}

