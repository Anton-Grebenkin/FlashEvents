namespace FlashEvents.Abstractions
{
    public interface IEventHandlerRegistry
    {
        ICollection<Type> GetHandlerTypesFor<TEvent>() where TEvent : IEvent;

        ICollection<Type> GetHandlerTypesFor<TEvent, THandlerInterface>()
            where TEvent : IEvent
            where THandlerInterface : IEventHandler;
    }
}
