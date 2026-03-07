using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
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
            _logger?.CreatingChannelProcessor(eventType);

            var p = (IEventChannelProcessor)Activator.CreateInstance(
                typeof(EventChannelProcessor<>).MakeGenericType(eventType), serviceProvider, registry)!;
            p.Start();
            return p;
        });

        _logger?.EnqueuingChannelEvent(eventType);

        return processor.EnqueueAsync(@event, ct);
    }

    public async ValueTask DisposeAsync()
    {
        _logger?.DisposingChannelDispatcher(_processors.Count);

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
        private readonly FlashEventsMetrics? _metrics;

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
            _metrics = rootServiceProvider.GetService<FlashEventsMetrics>();
        }

        public void Start()
        {
            if (_reader is not null)
                return;

            _logger?.ChannelReaderStarted(typeof(TEvent));

            _cts = new CancellationTokenSource();
            _reader = Task.Run(() => ReadLoopAsync(_cts.Token));
        }

        public async ValueTask EnqueueAsync(IEvent @event, CancellationToken ct)
        {
            if (@event is not TEvent typed)
                throw new ArgumentException($"Invalid event type. Expected {typeof(TEvent)}, got {@event.GetType()}.");

            await _channel.Writer.WriteAsync(typed, ct).ConfigureAwait(false);
            _metrics?.ChannelEventEnqueued(typeof(TEvent).Name);
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out var ev))
                    {
                        _metrics?.ChannelEventDequeued(typeof(TEvent).Name);
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
                            _logger?.ChannelEventProcessingFailed(ex, typeof(TEvent));
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
                _logger?.ChannelReadLoopCrashed(ex, typeof(TEvent));
            }
            finally
            {
                _logger?.ChannelReaderStopped(typeof(TEvent));
            }
        }

        private async Task ProcessEventAsync(TEvent ev, CancellationToken ct)
        {
            var channelHandlerTypes = _registry.GetHandlerTypesFor<TEvent, IChannelEventHandler<TEvent>>();
            var count = channelHandlerTypes.Count;
            if (count == 0)
            {
                _logger?.NoChannelHandlersRegistered(typeof(TEvent));
                return;
            }

            _logger?.ProcessingChannelEvent(typeof(TEvent), count);

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
            var startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                await ProcessHandlerInDedicatedScopeAsync(handlerType, ev, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _metrics?.HandlerError(typeof(TEvent).Name, handlerType.Name);
                _logger?.ChannelHandlerFailed(ex, handlerType, typeof(TEvent));
            }
            finally
            {
                _metrics?.RecordHandlerDuration(
                    Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                    typeof(TEvent).Name, handlerType.Name);
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
                _logger?.DisposingChannelProcessor(typeof(TEvent));

                _channel.Writer.TryComplete();
                _cts.Cancel();

                if (_reader is not null)
                    await _reader.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.ChannelProcessorDisposeFailed(ex, typeof(TEvent));
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
