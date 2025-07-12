using FlashEvents.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FlashEvents.Tests
{
    public class EventPublisherTests
    {
        [SetUp]
        public void Setup()
        {
            TestEventHandler.Reset();
            SecondTestEventHandler.Reset();
            AnotherTestEventHandler.Reset();
        }

        [Test]
        public async Task PublishAsync_WithSingleRegisteredHandler_ShouldExecuteHandler()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddEventPublisher();
            services.AddEventHandler<IEventHandler<AnotherTestEvent>, AnotherTestEventHandler>();

            using var serviceProvider = services.BuildServiceProvider();
            var publisher = serviceProvider.GetRequiredService<IEventPublisher>();

            var testEvent = new AnotherTestEvent();

            // Act
            await publisher.PublishAsync(testEvent);

            // Assert
            Assert.That(AnotherTestEventHandler.WasCalled, Is.True, "Обработчик для AnotherTestEvent должен был быть вызван.");
        }

        [Test]
        public async Task PublishAsync_WithMultipleRegisteredHandlers_ShouldExecuteAllHandlers()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddEventPublisher();
            services.AddEventHandler<IEventHandler<TestEvent>, TestEventHandler>();
            services.AddEventHandler<IEventHandler<TestEvent>, SecondTestEventHandler>();

            using var serviceProvider = services.BuildServiceProvider();
            var publisher = serviceProvider.GetRequiredService<IEventPublisher>();

            var testEvent = new TestEvent();

            // Act
            await publisher.PublishAsync(testEvent);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(TestEventHandler.WasCalled, Is.True, "Первый обработчик должен был быть вызван.");
                Assert.That(SecondTestEventHandler.WasCalled, Is.True, "Второй обработчик должен был быть вызван.");
                Assert.That(TestEventHandler.CallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void PublishAsync_WhenHandlerThrowsException_ShouldThrowAggregateException()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddEventPublisher();
            services.AddEventHandler<IEventHandler<TestEvent>, FailingTestEventHandler>();

            using var serviceProvider = services.BuildServiceProvider();
            var publisher = serviceProvider.GetRequiredService<IEventPublisher>();

            var testEvent = new TestEvent();

            // Act & Assert
            var aggregateException = Assert.ThrowsAsync<AggregateException>(async () => await publisher.PublishAsync(testEvent));

            Assert.That(aggregateException, Is.Not.Null);
            Assert.That(aggregateException.InnerExceptions, Has.Count.EqualTo(1));
            Assert.That(aggregateException.InnerExceptions[0], Is.TypeOf<InvalidOperationException>());
            Assert.That(aggregateException.InnerExceptions[0].Message, Is.EqualTo("Handler failed intentionally."));
        }

        [Test]
        public void PublishAsync_WithMixedSuccessAndFailureHandlers_ShouldExecuteAllAndThrowAggregateException()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddEventPublisher();

            services.AddEventHandler<IEventHandler<TestEvent>, FailingTestEventHandler>();
            services.AddEventHandler<IEventHandler<TestEvent>, TestEventHandler>();

            using var serviceProvider = services.BuildServiceProvider();
            var publisher = serviceProvider.GetRequiredService<IEventPublisher>();

            var testEvent = new TestEvent();

            // Act & Assert
            var ex = Assert.ThrowsAsync<AggregateException>(async () => await publisher.PublishAsync(testEvent));

            Assert.That(TestEventHandler.WasCalled, Is.True, "Успешный обработчик должен был выполниться до выброса исключения.");

            Assert.That(ex.InnerExceptions, Has.Count.EqualTo(1));
            Assert.That(ex.InnerExceptions[0], Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public async Task PublishAsync_WithNoRegisteredHandlers_ShouldCompleteGracefully()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddEventPublisher();

            using var serviceProvider = services.BuildServiceProvider();
            var publisher = serviceProvider.GetRequiredService<IEventPublisher>();

            var unhandledEvent = new UnhandledEvent();

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await publisher.PublishAsync(unhandledEvent),
                "Публикация события без обработчиков не должна вызывать исключений.");
        }

        [Test]
        public void PublishAsync_WithCancelledToken_ShouldThrowTaskCanceledExceptionWrappedInAggregate()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddEventPublisher();

            services.AddEventHandler<IEventHandler<TestEvent>, CancellableEventHandler>();

            using var serviceProvider = services.BuildServiceProvider();
            var publisher = serviceProvider.GetRequiredService<IEventPublisher>();

            var testEvent = new TestEvent();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            var ex = Assert.ThrowsAsync<AggregateException>(async () => await publisher.PublishAsync(testEvent, cts.Token));

            Assert.That(ex.InnerExceptions, Has.Count.EqualTo(1));
            Assert.That(ex.InnerExceptions[0], Is.InstanceOf<OperationCanceledException>());
        }
    }

    public class TestEvent : IEvent { }
    public class AnotherTestEvent : IEvent { }
    public class UnhandledEvent : IEvent { }

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        public static bool WasCalled { get; private set; }
        public static int CallCount { get; private set; }

        public static void Reset()
        {
            WasCalled = false;
            CallCount = 0;
        }

        public Task Handle(TestEvent @event, CancellationToken ct)
        {
            WasCalled = true;
            CallCount++;
            return Task.CompletedTask;
        }
    }

    public class SecondTestEventHandler : IEventHandler<TestEvent>
    {
        public static bool WasCalled { get; private set; }

        public static void Reset() => WasCalled = false;

        public Task Handle(TestEvent @event, CancellationToken ct)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    public class FailingTestEventHandler : IEventHandler<TestEvent>
    {
        public Task Handle(TestEvent @event, CancellationToken ct)
        {
            throw new InvalidOperationException("Handler failed intentionally.");
        }
    }

    public class AnotherTestEventHandler : IEventHandler<AnotherTestEvent>
    {
        public static bool WasCalled { get; private set; }

        public static void Reset() => WasCalled = false;

        public Task Handle(AnotherTestEvent @event, CancellationToken ct)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    public class CancellableEventHandler : IEventHandler<TestEvent>
    {
        public async Task Handle(TestEvent @event, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}
