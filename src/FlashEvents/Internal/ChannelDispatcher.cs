using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FlashEvents.Internal;

internal sealed class ChannelDispatcher : IChannelDispatcher, IAsyncDisposable
{
    private readonly ConcurrentDictionary<Type, IEventChannelProcessor> _processors = new();
    private readonly ILogger<ChannelDispatcher>? _logger;

    public ChannelDispatcher(ILogger<ChannelDispatcher>? logger = null)
    {
        _logger = logger;
    }

    public ValueTask EnqueueAsync(IEvent @event, IServiceProvider serviceProvider, IEventHandlerRegistry registry, CancellationToken ct)
    {
        var eventType = @event.GetType();

        var processor = _processors.GetOrAdd(eventType, _ =>
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
                _logger.LogDebug("Creating channel processor for event type {EventType}.", eventType);

            var p = (IEventChannelProcessor)Activator.CreateInstance(
                typeof(EventChannelProcessor<>).MakeGenericType(eventType), serviceProvider, registry)!;
            p.Start();
            return p;
        });

        if (_logger?.IsEnabled(LogLevel.Trace) == true)
            _logger.LogTrace("Enqueuing channel event {EventType}.", eventType);

        return processor.EnqueueAsync(@event, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_logger?.IsEnabled(LogLevel.Debug) == true)
            _logger.LogDebug("Disposing ChannelDispatcher with {ProcessorCount} processors.", _processors.Count);

        foreach (var p in _processors.Values)
        {
            await p.DisposeAsync().ConfigureAwait(false);
        }

        _processors.Clear();
    }

    private interface IEventChannelProcessor : IAsyncDisposable
    {
        void Start();
        ValueTask EnqueueAsync(IEvent @event, CancellationToken ct);
    }

    private sealed class EventChannelProcessor<TEvent> : IEventChannelProcessor where TEvent : class, IEvent
    {
        private readonly IServiceProvider _rootServiceProvider;
        private readonly IEventHandlerRegistry _registry;
        private readonly ILogger? _logger;

        private readonly Channel<TEvent> _channel = Channel.CreateUnbounded<TEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = true
        });

        private CancellationTokenSource? _cts;
        private Task? _reader;

        public EventChannelProcessor(IServiceProvider rootServiceProvider, IEventHandlerRegistry registry)
        {
            _rootServiceProvider = rootServiceProvider;
            _registry = registry;

            _logger = rootServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("FlashEvents.Channel");
        }

        public void Start()
        {
            if (_reader is not null)
                return;

            _logger?.LogDebug("Starting channel reader for event type {EventType}.", typeof(TEvent));

            _cts = new CancellationTokenSource();
            _reader = Task.Run(() => ReadLoopAsync(_cts.Token));
        }

        public ValueTask EnqueueAsync(IEvent @event, CancellationToken ct)
        {
            if (@event is not TEvent typed)
                throw new ArgumentException($"Invalid event type. Expected {typeof(TEvent)}, got {@event.GetType()}.");

            return _channel.Writer.WriteAsync(typed, ct);
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out var ev))
                    {
                        try
                        {
                            await ProcessEventAsync(ev, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Unhandled exception while processing channel event {EventType}. Processing will continue.", typeof(TEvent));
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // expected on shutdown
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Channel read loop crashed for event type {EventType}.", typeof(TEvent));
            }
            finally
            {
                _logger?.LogDebug("Channel reader stopped for event type {EventType}.", typeof(TEvent));
            }
        }

        private async Task ProcessEventAsync(TEvent ev, CancellationToken ct)
        {
            var channelHandlerTypes = _registry.GetHandlerTypesFor<TEvent, IChannelEventHandler<TEvent>>();
            var count = channelHandlerTypes.Count;
            if (count == 0)
            {
                if (_logger?.IsEnabled(LogLevel.Trace) == true)
                    _logger.LogTrace("No channel handlers registered for event type {EventType}.", typeof(TEvent));
                return;
            }

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
                _logger.LogTrace("Processing channel event {EventType} with {HandlerCount} handlers.", typeof(TEvent), count);

            var tasks = new Task[count];
            var i = 0;
            foreach (var handlerType in channelHandlerTypes)
            {
                tasks[i++] = ProcessHandlerAndLogAsync(handlerType, ev, ct);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task ProcessHandlerAndLogAsync(Type handlerType, TEvent ev, CancellationToken ct)
        {
            try
            {
                await ProcessHandlerInDedicatedScopeAsync(handlerType, ev, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Exception in channel handler {HandlerType} for event {EventType}.",
                    handlerType,
                    typeof(TEvent));
            }
        }

        private async Task ProcessHandlerInDedicatedScopeAsync(Type handlerType, TEvent ev, CancellationToken ct)
        {
            await using var scope = _rootServiceProvider.CreateAsyncScope();
            var handler = (IChannelEventHandler<TEvent>)scope.ServiceProvider.GetRequiredService(handlerType);
            await handler.Handle(ev, ct).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (_cts is null)
                return;

            try
            {
                _logger?.LogDebug("Disposing channel processor for event type {EventType}.", typeof(TEvent));

                _channel.Writer.TryComplete();
                _cts.Cancel();

                if (_reader is not null)
                    await _reader.ConfigureAwait(false);
            }
            catch
            {
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _reader = null;
            }
        }
    }
}
