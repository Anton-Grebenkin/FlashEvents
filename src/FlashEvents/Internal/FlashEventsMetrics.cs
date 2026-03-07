using System.Diagnostics.Metrics;

namespace FlashEvents.Internal;

internal sealed class FlashEventsMetrics
{
    public const string MeterName = "FlashEvents";

    private readonly Counter<long> _eventsPublished;
    private readonly Histogram<double> _publishDuration;
    private readonly Histogram<double> _handlerDuration;
    private readonly Counter<long> _handlerErrors;
    private readonly UpDownCounter<long> _channelQueueSize;

    public FlashEventsMetrics(IMeterFactory? meterFactory = null)
    {
        var meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);

        _eventsPublished = meter.CreateCounter<long>(
            "flashevents.publish.events",
            unit: "{event}",
            description: "The number of events published.");

        _publishDuration = meter.CreateHistogram<double>(
            "flashevents.publish.duration",
            unit: "ms",
            description: "The duration of event publishing in milliseconds.");

        _handlerDuration = meter.CreateHistogram<double>(
            "flashevents.handler.duration",
            unit: "ms",
            description: "The duration of individual handler execution in milliseconds.");

        _handlerErrors = meter.CreateCounter<long>(
            "flashevents.handler.errors",
            unit: "{error}",
            description: "The number of handler execution errors.");

        _channelQueueSize = meter.CreateUpDownCounter<long>(
            "flashevents.channel.queue_size",
            unit: "{event}",
            description: "The number of events currently waiting in channel queues.");
    }

    public void EventPublished(string eventType)
    {
        _eventsPublished.Add(1, new KeyValuePair<string, object?>("event.type", eventType));
    }

    public void RecordPublishDuration(double durationMs, string eventType)
    {
        _publishDuration.Record(durationMs, new KeyValuePair<string, object?>("event.type", eventType));
    }

    public void RecordHandlerDuration(double durationMs, string eventType, string handlerType)
    {
        _handlerDuration.Record(durationMs,
            new KeyValuePair<string, object?>("event.type", eventType),
            new KeyValuePair<string, object?>("handler.type", handlerType));
    }

    public void HandlerError(string eventType, string handlerType)
    {
        _handlerErrors.Add(1,
            new KeyValuePair<string, object?>("event.type", eventType),
            new KeyValuePair<string, object?>("handler.type", handlerType));
    }

    public void ChannelEventEnqueued(string eventType)
    {
        _channelQueueSize.Add(1, new KeyValuePair<string, object?>("event.type", eventType));
    }

    public void ChannelEventDequeued(string eventType)
    {
        _channelQueueSize.Add(-1, new KeyValuePair<string, object?>("event.type", eventType));
    }
}
