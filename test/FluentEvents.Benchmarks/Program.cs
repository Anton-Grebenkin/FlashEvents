using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FlashEvents;
using FlashEvents.Abstractions;
using MediatR;
using MediatR.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;

namespace PublisherBenchmark
{
    public class TestEvent : IEvent, INotification { }

    public class CustomHandler : IEventHandler<TestEvent>
    {
        public async Task Handle(TestEvent @event, CancellationToken ct)
            => await Task.CompletedTask;
    }

    public class CustomHandler2 : IEventHandler<TestEvent>
    {
        public async Task Handle(TestEvent @event, CancellationToken ct)
           => await Task.CompletedTask;
    }

    public class MediatrHandler : INotificationHandler<TestEvent>
    {
        public async Task Handle(TestEvent notification, CancellationToken cancellationToken)
            => await Task.CompletedTask;
    }

    public class MediatrHandler2 : INotificationHandler<TestEvent>
    {
        public async Task Handle(TestEvent notification, CancellationToken cancellationToken)
            => await Task.CompletedTask;
    }

    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 1)]
    public class Benchmarks
    {
        private IServiceProvider _customServices;
        private IEventPublisher _customPublisher;

        private IServiceProvider _mediatrServices;
        private IMediator _mediator;

        [GlobalSetup]
        public void Setup()
        {
            var customServices = new ServiceCollection();
            customServices.AddEventHandler<IEventHandler<TestEvent>, CustomHandler>();
            customServices.AddEventHandler<IEventHandler<TestEvent>, CustomHandler2>();
            customServices.AddEventPublisher();

            _customServices = customServices.BuildServiceProvider();
            _customPublisher = _customServices.GetRequiredService<IEventPublisher>();

            var mediatrServices = new ServiceCollection();
            mediatrServices.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(Benchmarks).Assembly);
                cfg.NotificationPublisher = new TaskWhenAllPublisher();
            });
            _mediatrServices = mediatrServices.BuildServiceProvider();
            _mediator = _mediatrServices.GetRequiredService<IMediator>();
        }

        [Benchmark(Baseline = true)]
        public async Task FluentEvents_Publish()
        {
            await _customPublisher.PublishAsync(new TestEvent());
        }

        [Benchmark]
        public async Task Mediatr_Publish()
        {
            await _mediator.Publish(new TestEvent());
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {

            BenchmarkRunner.Run<Benchmarks>();
           
        }
    }
}
