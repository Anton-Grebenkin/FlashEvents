namespace FlashEvents.Abstractions
{
    internal interface IEventHandlerRegistry
    {
        ICollection<Type> GetHandlerTypesFor<TEvent>() where TEvent : IEvent;
    }
}
