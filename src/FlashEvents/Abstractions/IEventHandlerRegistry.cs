
namespace FlashEvents.Abstractions
{
    internal interface IEventHandlerRegistry
    {
        IEnumerable<Type> GetHandlerTypesFor<TEvent>() where TEvent : IEvent;
    }
}
