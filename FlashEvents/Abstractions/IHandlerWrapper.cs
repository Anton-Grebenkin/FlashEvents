namespace FlashEvents.Abstractions
{
    internal interface IHandlerWrapper
    {
        Task Handle(IEvent @event, IServiceProvider serviceFactory, CancellationToken ct);
    }
}
