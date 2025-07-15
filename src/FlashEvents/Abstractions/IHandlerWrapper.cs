
namespace FlashEvents.Abstractions
{
    internal interface IHandlerWrapper
    {
        Task Handle(IEvent @event, IServiceProvider serviceFactory, IEventHandlerRegistry registry, CancellationToken ct);
    }
}
