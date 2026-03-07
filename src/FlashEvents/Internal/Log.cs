using Microsoft.Extensions.Logging;

namespace FlashEvents.Internal;

internal static partial class Log
{
    // EventPublisher (1-9)

    [LoggerMessage(EventId = 1, Level = LogLevel.Trace, Message = "Publishing event {EventType}.")]
    public static partial void PublishingEvent(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Trace, Message = "Published event {EventType}.")]
    public static partial void PublishedEvent(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Publishing event {EventType} was cancelled.")]
    public static partial void PublishingCancelled(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Unhandled exception while publishing event {EventType}.")]
    public static partial void PublishingFailed(this ILogger logger, Exception exception, Type eventType);

    // HandlerWrapper dispatch (10-19)

    [LoggerMessage(EventId = 10, Level = LogLevel.Trace, Message = "No handlers registered for event {EventType}.")]
    public static partial void NoHandlersRegistered(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Dispatching event {EventType}: Serial={SerialCount}, ParallelMain={ParallelMainCount}, ParallelDedicated={ParallelDedicatedCount}, Channel={ChannelCount}.")]
    public static partial void DispatchingEvent(this ILogger logger, Type eventType, int serialCount, int parallelMainCount, int parallelDedicatedCount, int channelCount);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "Exception in handler {HandlerType} for event {EventType}.")]
    public static partial void HandlerFailed(this ILogger logger, Exception exception, Type handlerType, Type eventType);

    // ChannelDispatcher (20-39)

    [LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "Creating channel processor for event type {EventType}.")]
    public static partial void CreatingChannelProcessor(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 21, Level = LogLevel.Trace, Message = "Enqueuing channel event {EventType}.")]
    public static partial void EnqueuingChannelEvent(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 22, Level = LogLevel.Debug, Message = "Starting channel reader for event type {EventType}.")]
    public static partial void ChannelReaderStarted(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 23, Level = LogLevel.Debug, Message = "Channel reader stopped for event type {EventType}.")]
    public static partial void ChannelReaderStopped(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 24, Level = LogLevel.Error, Message = "Unhandled exception while processing channel event {EventType}. Processing will continue.")]
    public static partial void ChannelEventProcessingFailed(this ILogger logger, Exception exception, Type eventType);

    [LoggerMessage(EventId = 25, Level = LogLevel.Error, Message = "Channel read loop crashed for event type {EventType}.")]
    public static partial void ChannelReadLoopCrashed(this ILogger logger, Exception exception, Type eventType);

    [LoggerMessage(EventId = 26, Level = LogLevel.Error, Message = "Exception in channel handler {HandlerType} for event {EventType}.")]
    public static partial void ChannelHandlerFailed(this ILogger logger, Exception exception, Type handlerType, Type eventType);

    [LoggerMessage(EventId = 27, Level = LogLevel.Debug, Message = "Disposing ChannelDispatcher with {ProcessorCount} processors.")]
    public static partial void DisposingChannelDispatcher(this ILogger logger, int processorCount);

    [LoggerMessage(EventId = 28, Level = LogLevel.Debug, Message = "Disposing channel processor for event type {EventType}.")]
    public static partial void DisposingChannelProcessor(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 29, Level = LogLevel.Warning, Message = "Error while disposing channel processor for event type {EventType}.")]
    public static partial void ChannelProcessorDisposeFailed(this ILogger logger, Exception exception, Type eventType);

    [LoggerMessage(EventId = 30, Level = LogLevel.Trace, Message = "No channel handlers registered for event type {EventType}.")]
    public static partial void NoChannelHandlersRegistered(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 31, Level = LogLevel.Trace, Message = "Processing channel event {EventType} with {HandlerCount} handlers.")]
    public static partial void ProcessingChannelEvent(this ILogger logger, Type eventType, int handlerCount);

    // HandlerWrapperCache (40-49)

    [LoggerMessage(EventId = 40, Level = LogLevel.Debug, Message = "Created handler wrapper for event type {EventType}.")]
    public static partial void HandlerWrapperCreated(this ILogger logger, Type eventType);

    [LoggerMessage(EventId = 41, Level = LogLevel.Error, Message = "Failed to create handler wrapper for event type {EventType}.")]
    public static partial void HandlerWrapperCreationFailed(this ILogger logger, Exception exception, Type eventType);

    // EventHandlerRegistry (50-59)

    [LoggerMessage(EventId = 50, Level = LogLevel.Debug, Message = "Registered handler {HandlerType} for event {EventType}.")]
    public static partial void HandlerRegistered(this ILogger logger, Type handlerType, Type eventType);
}
