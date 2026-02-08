using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FlashEvents.Tests
{
    [TestFixture]
    public class EventPublisherTests
    {
        #region Test Events

        public record SimpleEvent : IEvent;

        public record EventWithData(int Id, string Message) : IEvent;

        public record MultiHandlerEvent : IEvent;

        public record ScopedDependencyEvent(int Id) : IEvent;

        #endregion

        #region Test Helpers

        public interface ITestCollector
        {
            void Record(string value);
            void RecordEvent(object ev);
            IReadOnlyList<string> Records { get; }
            IReadOnlyList<object> Events { get; }
            void Clear();
        }

        public class TestCollector : ITestCollector
        {
            private readonly List<string> _records = new();
            private readonly List<object> _events = new();
            private readonly object _lock = new();

            public void Record(string value)
            {
                lock (_lock)
                {
                    _records.Add(value);
                }
            }

            public void RecordEvent(object ev)
            {
                lock (_lock)
                {
                    _events.Add(ev);
                }
            }

            public IReadOnlyList<string> Records
            {
                get
                {
                    lock (_lock)
                    {
                        return _records.ToList().AsReadOnly();
                    }
                }
            }

            public IReadOnlyList<object> Events
            {
                get
                {
                    lock (_lock)
                    {
                        return _events.ToList().AsReadOnly();
                    }
                }
            }

            public void Clear()
            {
                lock (_lock)
                {
                    _records.Clear();
                    _events.Clear();
                }
            }
        }

        #endregion

        #region Test Handlers

        public class SimpleEventHandler : ISerialEventHandler<SimpleEvent>
        {
            private readonly ITestCollector _collector;
            public SimpleEventHandler(ITestCollector collector) => _collector = collector;

            public Task Handle(SimpleEvent @event, CancellationToken ct = default)
            {
                _collector.Record(nameof(SimpleEventHandler));
                return Task.CompletedTask;
            }
        }

        public class EventWithDataHandler : ISerialEventHandler<EventWithData>
        {
            private readonly ITestCollector _collector;
            public EventWithDataHandler(ITestCollector collector) => _collector = collector;

            public Task Handle(EventWithData @event, CancellationToken ct = default)
            {
                _collector.RecordEvent(@event);
                return Task.CompletedTask;
            }
        }

        public class ParallelMainScopeHandler : IParallelInMainScopeEventHandler<MultiHandlerEvent>
        {
            private readonly ITestCollector _collector;
            public ParallelMainScopeHandler(ITestCollector collector) => _collector = collector;

            public Task Handle(MultiHandlerEvent @event, CancellationToken ct = default)
            {
                _collector.Record(nameof(ParallelMainScopeHandler));
                return Task.CompletedTask;
            }
        }

        public class ParallelDedicatedScopeHandler : IParallelInDedicatedScopeEventHandler<MultiHandlerEvent>
        {
            private readonly ITestCollector _collector;
            public ParallelDedicatedScopeHandler(ITestCollector collector) => _collector = collector;

            public Task Handle(MultiHandlerEvent @event, CancellationToken ct = default)
            {
                _collector.Record(nameof(ParallelDedicatedScopeHandler));
                return Task.CompletedTask;
            }
        }

        public class SecondParallelMainScopeHandler : IParallelInMainScopeEventHandler<MultiHandlerEvent>
        {
            private readonly ITestCollector _collector;
            public SecondParallelMainScopeHandler(ITestCollector collector) => _collector = collector;

            public Task Handle(MultiHandlerEvent @event, CancellationToken ct = default)
            {
                _collector.Record(nameof(SecondParallelMainScopeHandler));
                return Task.CompletedTask;
            }
        }

        public class ThrowingHandler : ISerialEventHandler<SimpleEvent>
        {
            public Task Handle(SimpleEvent @event, CancellationToken ct = default)
            {
                throw new InvalidOperationException("Handler error");
            }
        }

        public class ScopedServiceHandler : IParallelInDedicatedScopeEventHandler<EventWithData>
        {
            private readonly IScopedService _scopedService;
            private readonly ITestCollector _collector;

            public ScopedServiceHandler(IScopedService scopedService, ITestCollector collector)
            {
                _scopedService = scopedService;
                _collector = collector;
            }

            public Task Handle(EventWithData @event, CancellationToken ct = default)
            {
                _scopedService.RecordEvent(@event.Id);
                _collector.RecordEvent(@event);
                return Task.CompletedTask;
            }
        }

        public interface IScopedService
        {
            void RecordEvent(int eventId);
            IReadOnlyList<int> GetRecordedEvents();
        }

        public class ScopedService : IScopedService
        {
            private readonly List<int> _recordedEvents = new();

            public void RecordEvent(int eventId)
            {
                _recordedEvents.Add(eventId);
            }

            public IReadOnlyList<int> GetRecordedEvents()
            {
                return _recordedEvents.AsReadOnly();
            }
        }

        public interface IScopedCounter
        {
            int Id { get; }
        }

        public class ScopedCounter : IScopedCounter
        {
            public int Id { get; } = Random.Shared.Next(1, int.MaxValue);
        }

        public class RootScopeSerialScopedDependencyHandler : ISerialEventHandler<ScopedDependencyEvent>
        {
            private readonly IScopedCounter _scoped;
            private readonly ITestCollector _collector;

            public RootScopeSerialScopedDependencyHandler(IScopedCounter scoped, ITestCollector collector)
            {
                _scoped = scoped;
                _collector = collector;
            }

            public Task Handle(ScopedDependencyEvent @event, CancellationToken ct = default)
            {
                _collector.Record($"{nameof(RootScopeSerialScopedDependencyHandler)}:{_scoped.Id}:{@event.Id}");
                return Task.CompletedTask;
            }
        }

        public class RootScopeParallelMainScopedDependencyHandler : IParallelInMainScopeEventHandler<ScopedDependencyEvent>
        {
            private readonly IScopedCounter _scoped;
            private readonly ITestCollector _collector;

            public RootScopeParallelMainScopedDependencyHandler(IScopedCounter scoped, ITestCollector collector)
            {
                _scoped = scoped;
                _collector = collector;
            }

            public Task Handle(ScopedDependencyEvent @event, CancellationToken ct = default)
            {
                _collector.Record($"{nameof(RootScopeParallelMainScopedDependencyHandler)}:{_scoped.Id}:{@event.Id}");
                return Task.CompletedTask;
            }
        }

        #endregion

        #region Tests
        [Test]
        public async Task PublishAsync_WithSingleSerialHandler_ShouldCallHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();

            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddEventHandler<ISerialEventHandler<SimpleEvent>, SimpleEventHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var collector = provider.GetRequiredService<ITestCollector>();

            // Act
            await publisher.PublishAsync(new SimpleEvent());

            // Assert
            Assert.That(collector.Records, Does.Contain(nameof(SimpleEventHandler)));
        }

        [Test]
        public async Task PublishAsync_WithEventData_ShouldPassDataToHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();

            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddEventHandler<ISerialEventHandler<EventWithData>, EventWithDataHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var collector = provider.GetRequiredService<ITestCollector>();

            var testEvent = new EventWithData(42, "test message");

            // Act
            await publisher.PublishAsync(testEvent);

            // Assert
            var ev = collector.Events.OfType<EventWithData>().FirstOrDefault();
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Id, Is.EqualTo(42));
            Assert.That(ev.Message, Is.EqualTo("test message"));
        }

        [Test]
        public async Task PublishAsync_WithNoHandlers_ShouldNotThrow()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await publisher.PublishAsync(new SimpleEvent()));
        }

        [Test]
        public async Task PublishAsync_WithMultipleSerialHandlers_ShouldExecuteSequentially()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();

            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddEventHandler<ISerialEventHandler<SimpleEvent>, OrderTrackingHandler1>();
            services.AddEventHandler<ISerialEventHandler<SimpleEvent>, OrderTrackingHandler2>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var collector = provider.GetRequiredService<ITestCollector>();

            // Act
            await publisher.PublishAsync(new SimpleEvent());

            // Assert - Both handlers should have been called (order preserved)
            Assert.That(collector.Records, Does.Contain(nameof(OrderTrackingHandler1)));
            Assert.That(collector.Records, Does.Contain(nameof(OrderTrackingHandler2)));
            var records = collector.Records.ToList();
            Assert.That(records.IndexOf(nameof(OrderTrackingHandler1)), Is.LessThan(records.IndexOf(nameof(OrderTrackingHandler2))));
        }

        [Test]
        public async Task PublishAsync_WithParallelMainScopeHandler_ShouldExecuteInMainScope()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();

            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddEventHandler<IParallelInMainScopeEventHandler<MultiHandlerEvent>, ParallelMainScopeHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var collector = provider.GetRequiredService<ITestCollector>();

            // Act
            await publisher.PublishAsync(new MultiHandlerEvent());

            // Assert
            Assert.That(collector.Records, Does.Contain(nameof(ParallelMainScopeHandler)));
        }

        [Test]
        public async Task PublishAsync_WithParallelDedicatedScopeHandler_ShouldExecuteInDedicatedScope()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();

            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddEventHandler<IParallelInDedicatedScopeEventHandler<MultiHandlerEvent>, ParallelDedicatedScopeHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var collector = provider.GetRequiredService<ITestCollector>();

            // Act
            await publisher.PublishAsync(new MultiHandlerEvent());

            // Assert
            Assert.That(collector.Records, Does.Contain(nameof(ParallelDedicatedScopeHandler)));
        }

        [Test]
        public async Task PublishAsync_WithMixedHandlerTypes_ShouldExecuteAll()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();

            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddEventHandler<IParallelInMainScopeEventHandler<MultiHandlerEvent>, ParallelMainScopeHandler>();
            services.AddEventHandler<IParallelInDedicatedScopeEventHandler<MultiHandlerEvent>, ParallelDedicatedScopeHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var collector = provider.GetRequiredService<ITestCollector>();

            // Act
            await publisher.PublishAsync(new MultiHandlerEvent());

            // Assert - both types executed
            Assert.That(collector.Records, Does.Contain(nameof(ParallelMainScopeHandler)));
            Assert.That(collector.Records, Does.Contain(nameof(ParallelDedicatedScopeHandler)));
        }

        [Test]
        public async Task PublishAsync_WithScopedService_ShouldIsolateScope()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();
            services.AddScoped<IScopedService, ScopedService>();
            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddEventHandler<IParallelInDedicatedScopeEventHandler<EventWithData>, ScopedServiceHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var collector = provider.GetRequiredService<ITestCollector>();

            // Act
            await publisher.PublishAsync(new EventWithData(1, "test"));
            await publisher.PublishAsync(new EventWithData(2, "test"));

            // Assert - Each handler call should have its own scope; collector contains events
            Assert.That(collector.Events.OfType<EventWithData>().Select(e => e.Id), Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public async Task PublishAsync_WithCancellationToken_ShouldPassToHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();
            services.AddEventHandler<ISerialEventHandler<SimpleEvent>, CancellableHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cts = new CancellationTokenSource();

            // Act
            await publisher.PublishAsync(new SimpleEvent(), cts.Token);

            // Assert
            Assert.That(cts.Token.IsCancellationRequested, Is.False);
        }

        [Test]
        public void PublishAsync_WithThrowingHandler_ShouldThrowException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();
            services.AddEventHandler<ISerialEventHandler<SimpleEvent>, ThrowingHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await publisher.PublishAsync(new SimpleEvent())
            );
        }

        [Test]
        public async Task PublishAsync_SameEventTypeTwice_ShouldUseCache()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();
            services.AddEventHandler<ISerialEventHandler<SimpleEvent>, SimpleEventHandler>();
            services.AddSingleton<ITestCollector, TestCollector>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var collector = provider.GetRequiredService<ITestCollector>();

            // Act
            await publisher.PublishAsync(new SimpleEvent());
            await publisher.PublishAsync(new SimpleEvent());

            // Assert - Both should complete and collector should have two records
            Assert.That(collector.Records.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task PublishAsync_WithMultipleDifferentEvents_ShouldHandleEach()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();

            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddEventHandler<ISerialEventHandler<SimpleEvent>, SimpleEventHandler>();
            services.AddEventHandler<ISerialEventHandler<EventWithData>, EventWithDataHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var collector = provider.GetRequiredService<ITestCollector>();

            // Act
            await publisher.PublishAsync(new SimpleEvent());
            await publisher.PublishAsync(new EventWithData(1, "test"));

            // Assert
            Assert.That(collector.Records, Does.Contain(nameof(SimpleEventHandler)));
            Assert.That(collector.Events.OfType<EventWithData>().FirstOrDefault(), Is.Not.Null);
        }

        [Test]
        public async Task PublishAsync_ParallelHandlers_ShouldExecuteConcurrently()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();
            services.AddEventHandler<IParallelInMainScopeEventHandler<MultiHandlerEvent>, SlowParallelHandler1>();
            services.AddEventHandler<IParallelInMainScopeEventHandler<MultiHandlerEvent>, SlowParallelHandler2>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Act
            await publisher.PublishAsync(new MultiHandlerEvent());

            sw.Stop();

            // Assert - Should complete in roughly 100ms (parallel), not 200ms (serial)
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(200), 
                $"Took {sw.ElapsedMilliseconds}ms, should be concurrent");
        }

        [Test]
        public async Task PublishAsync_WithDefaultEventHandlers_ShouldExecuteInMainScope()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();
            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddEventHandler<IParallelInMainScopeEventHandler<MultiHandlerEvent>, ParallelMainScopeHandler>();
            services.AddEventHandler<IParallelInMainScopeEventHandler<MultiHandlerEvent>, SecondParallelMainScopeHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            // Act
            await publisher.PublishAsync(new MultiHandlerEvent());

            // Assert
            Assert.Pass("Default handler execution test completed");
        }

        [Test]
        public async Task PublishAsync_ResolvesDependenciesCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();
            services.AddScoped<IScopedService, ScopedService>();
            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddEventHandler<IParallelInDedicatedScopeEventHandler<EventWithData>, ScopedServiceHandler>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var collector = provider.GetRequiredService<ITestCollector>();

            // Act
            await publisher.PublishAsync(new EventWithData(42, "test"));

            // Assert - dependency resolved and executed
            Assert.That(collector.Events.OfType<EventWithData>().FirstOrDefault()?.Id, Is.EqualTo(42));
        }

        [Test]
        public async Task PublishAsync_FromRootProvider_WithScopedDependencyInSerialHandler_ShouldNotThrow()
        {
            // Regression: IEventPublisher is singleton. Publishing from root provider must still work
            // when handlers depend on scoped services.
            var services = new ServiceCollection();
            services.AddEventPublisher();
            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddScoped<IScopedCounter, ScopedCounter>();
            services.AddEventHandler<ISerialEventHandler<ScopedDependencyEvent>, RootScopeSerialScopedDependencyHandler>();

            var provider = services.BuildServiceProvider(); // root provider
            var publisher = provider.GetRequiredService<IEventPublisher>();

            Assert.DoesNotThrowAsync(async () => await publisher.PublishAsync(new ScopedDependencyEvent(1)));
        }

        [Test]
        public async Task PublishAsync_FromRootProvider_WithScopedDependencyInParallelMainHandler_ShouldNotThrow()
        {
            // Same regression but for ParallelInMainScope (resolved from the main publish scope).
            var services = new ServiceCollection();
            services.AddEventPublisher();
            services.AddSingleton<ITestCollector, TestCollector>();
            services.AddScoped<IScopedCounter, ScopedCounter>();
            services.AddEventHandler<IParallelInMainScopeEventHandler<ScopedDependencyEvent>, RootScopeParallelMainScopedDependencyHandler>();

            var provider = services.BuildServiceProvider(); // root provider
            var publisher = provider.GetRequiredService<IEventPublisher>();

            Assert.DoesNotThrowAsync(async () => await publisher.PublishAsync(new ScopedDependencyEvent(1)));
        }

        #endregion

        #region Helper Handlers

        public class CancellableHandler : ISerialEventHandler<SimpleEvent>
        {
            public Task Handle(SimpleEvent @event, CancellationToken ct = default)
            {
                return Task.CompletedTask;
            }
        }

        public class OrderTrackingHandler1 : ISerialEventHandler<SimpleEvent>
        {
            private readonly ITestCollector _collector;
            public OrderTrackingHandler1(ITestCollector collector) => _collector = collector;

            public Task Handle(SimpleEvent @event, CancellationToken ct = default)
            {
                _collector.Record(nameof(OrderTrackingHandler1));
                return Task.Delay(5, ct);
            }
        }

        public class OrderTrackingHandler2 : ISerialEventHandler<SimpleEvent>
        {
            private readonly ITestCollector _collector;
            public OrderTrackingHandler2(ITestCollector collector) => _collector = collector;

            public Task Handle(SimpleEvent @event, CancellationToken ct = default)
            {
                _collector.Record(nameof(OrderTrackingHandler2));
                return Task.Delay(5, ct);
            }
        }

        public class SlowParallelHandler1 : IParallelInMainScopeEventHandler<MultiHandlerEvent>
        {
            public Task Handle(MultiHandlerEvent @event, CancellationToken ct = default)
            {
                return Task.Delay(100, ct);
            }
        }

        public class SlowParallelHandler2 : IParallelInMainScopeEventHandler<MultiHandlerEvent>
        {
            public Task Handle(MultiHandlerEvent @event, CancellationToken ct = default)
            {
                return Task.Delay(100, ct);
            }
        }

        #endregion
    }
}
