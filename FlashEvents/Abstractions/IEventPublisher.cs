namespace FlashEvents.Abstractions
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(T notification, CancellationToken ct = default) where T : class, IEvent;
    }
}
