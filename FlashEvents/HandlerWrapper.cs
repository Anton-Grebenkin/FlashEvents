using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FlashEvents
{
    internal class HandlerWrapper<TEvent> : IHandlerWrapper where TEvent : IEvent
    {
        public async Task Handle(IEvent @event, IServiceProvider serviceProvider, IEventHandlerRegistry registry, CancellationToken ct)
        {
            var handlerTypes = registry.GetHandlerTypesFor<TEvent>();

            if (!handlerTypes.Any())
                return;

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

