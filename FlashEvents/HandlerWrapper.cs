using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FlashEvents
{
    internal class HandlerWrapper<TEvent> : IHandlerWrapper where TEvent : IEvent
    {
        private Type[]? _cachedHandlerTypes;
        public async Task Handle(IEvent @event, IServiceProvider serviceProvider, CancellationToken ct)
        {
            var handlerTypes = LazyInitializer.EnsureInitialized(
                ref _cachedHandlerTypes,
                () => serviceProvider.GetServices<IEventHandler<TEvent>>()
                                    .Select(h => h.GetType())
                                    .ToArray()
            );

            if (handlerTypes.Length == 0)
                return;

            if (handlerTypes.Length == 1)
            {
                try
                {
                    await serviceProvider.GetRequiredService<IEventHandler<TEvent>>().Handle((TEvent)@event, ct);
                    return;
                }
                catch (Exception ex)
                {
                    throw new AggregateException(ex);
                }
            }

            var all = Task.WhenAll(handlerTypes.Select(
                handlerType => ProcessHandlerAsync(serviceProvider, handlerType, @event, ct))
            );

            try
            {
                await all; 
            }
            catch (Exception ex)
            {
                if (all.Exception != null)
                    throw new AggregateException(all.Exception!.Flatten().InnerExceptions);

                throw new AggregateException(ex);
            }
        }

        private static async Task ProcessHandlerAsync(IServiceProvider serviceFactory, Type handlerType, IEvent @event, CancellationToken ct)
        {
            await using var scope = serviceFactory.CreateAsyncScope();
            var handler = (IEventHandler<TEvent>)scope.ServiceProvider.GetRequiredService(handlerType);
            await handler.Handle((TEvent)@event, ct);
        }
    }
}

