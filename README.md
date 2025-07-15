# FlashEvents

[FlashEvents on NuGet](https://www.nuget.org/packages/FlashEvents/)

**FlashEvents** is a high-performance, in-memory event publishing library for .NET designed with simplicity and speed in mind. It enables a robust publish-subscribe pattern where event handlers are executed **in parallel**, and each handler runs within its own isolated **Dependency Injection scope**.

This approach ensures that handlers do not interfere with each other, making it ideal for applications where handlers have their own unit of work, such as interacting with a database context (`DbContext`) or managing other scoped services.

## Key Features

*   🚀 **Blazing Fast Performance**: Optimized for low-latency and minimal memory allocations. See benchmarks below.
*   ⚡ **Parallel Execution**: Automatically runs all handlers for a given event concurrently using `Task.WhenAll`, maximizing throughput.
*   🛡️ **Scoped Handler Isolation**: Each event handler is resolved and executed in its own `IServiceScope`. This prevents issues with shared state and transient service lifetimes (e.g., `DbContext` instances).
*   🔧 **Simple & Fluent API**: Easy to set up and use with clean dependency injection extensions.
*   🔍 **Automatic Handler Discovery**: Register all your event handlers from an assembly with a single line of code.

## Getting Started

Using FlashEvents is straightforward. Follow these steps to integrate it into your application.

### 1\. Define an Event

An event is a simple class or record that implements the `IEvent` marker interface.

```csharp
// Define your event contracts in a shared library
using FlashEvents.Abstractions;

public record OrderCreatedEvent(int OrderId, string CustomerEmail) : IEvent;
```

### 2\. Create Event Handlers

Create one or more handlers for your event. Each handler must implement `IEventHandler<TEvent>`.

```csharp
using FlashEvents.Abstractions;
using Microsoft.Extensions.Logging;

// Handler 1: Sends a welcome email
public class SendWelcomeEmailHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<SendWelcomeEmailHandler> _logger;

    public SendWelcomeEmailHandler(IEmailService emailService, ILogger<SendWelcomeEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(OrderCreatedEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("Sending welcome email for order {OrderId}...", @event.OrderId);
        await _emailService.SendEmailAsync(@event.CustomerEmail, "Your order is confirmed!");
        _logger.LogInformation("Email sent successfully for order {OrderId}.", @event.OrderId);
    }
}

// Handler 2: Updates analytics
public class UpdateAnalyticsHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<UpdateAnalyticsHandler> _logger;

    public UpdateAnalyticsHandler(IAnalyticsService analyticsService, ILogger<UpdateAnalyticsHandler> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task Handle(OrderCreatedEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("Updating analytics for order {OrderId}...", @event.OrderId);
        await _analyticsService.TrackOrderAsync(@event.OrderId);
        _logger.LogInformation("Analytics updated for order {OrderId}.", @event.OrderId);
    }
}
```

### 3\. Configure Dependency Injection

In your `Program.cs` or `Startup.cs`, register the event publisher and your handlers.

```csharp
using FlashEvents;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// 1. Add FlashEvents services
builder.Services.AddEventPublisher();

// 2. Automatically discover and register all handlers from an assembly
builder.Services.AddEventHandlersFromAssembly(Assembly.GetExecutingAssembly());

// You can also register handlers manually
// builder.Services.AddEventHandler<IEventHandler<OrderCreatedEvent>, SendWelcomeEmailHandler>();

// Register other application services
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>(); // Example of a scoped service

var app = builder.Build();

// ...
```

### 4\. Publish an Event

Inject `IEventPublisher` into your services and call `PublishAsync` to trigger all registered handlers.

```csharp
using FlashEvents.Abstractions;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IEventPublisher _eventPublisher;

    public OrdersController(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder()
    {
        // ... logic to create an order ...
        int newOrderId = 123;
        string customerEmail = "test@example.com";

        // Create the event
        var orderEvent = new OrderCreatedEvent(newOrderId, customerEmail);

        // Publish it. FlashEvents will find and run both handlers in parallel.
        await _eventPublisher.PublishAsync(orderEvent);

        return Ok("Order created and events are being handled.");
    }
}
```

## ⚠️ Important Note: Handler Caching

For maximum performance, **FlashEvents caches the list of handler types (`Type[]`) for each event type (`IEvent`) upon its first publication**.

This means that any changes to handler registrations in the DI container _after an event of that type has already been published_ will not be recognized. The handler cache **cannot be reset** during the application's runtime.

**All event handler registrations must be configured at application startup.**

## How It Works

FlashEvents achieves its performance and isolation through a simple but effective mechanism:

1.  When `PublishAsync` is called for an event type for the first time, it resolves all registered `IEventHandler<TEvent>` services from the DI container.
2.  The concrete types of these handlers are stored in a static, thread-safe cache (`LazyInitializer`) associated with the event type.
3.  On every publish call (including the first), it iterates through the cached handler types.
4.  For each handler type, it creates a new `AsyncServiceScope` from the root `IServiceProvider`.
5.  It resolves the handler service within this new scope and executes its `Handle` method.
6.  `Task.WhenAll` is used to await all handler tasks, ensuring they run concurrently.
7.  If any handler throws an exception, all exceptions are collected into a single `AggregateException`.

This design guarantees that each handler gets a fresh set of scoped dependencies, providing perfect isolation with minimal overhead.

## Benchmarks

The following benchmarks compare `FlashEvents` with `MediatR v12.5.0` under different scenarios.

**System Configuration:**

*   BenchmarkDotNet v0.15.2, Windows 11 (10.0.22631.5335)
*   13th Gen Intel Core i5-13400F 2.50GHz, 1 CPU, 16 logical and 10 physical cores
*   .NET SDK 9.0.302, .NET 8.0.18

- - -

### Scenario 1: Single Handler (`TaskWhenAllPublisher in MediatR`)

This test measures the performance of publishing an event that is handled by a single handler.

| Method | Mean | Error | StdDev | Ratio | Gen0 | Allocated | Alloc Ratio |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **FlashEvents\_Publish** | **91.28 ns** | **1.149 ns** | **1.075 ns** | **1.00** | **0.0168** | **176 B** | **1.00** |
| MediatR\_Publish | 141.19 ns | 1.114 ns | 0.988 ns | 1.55 | 0.0634 | 664 B | 3.77 |

**Result:** In a single-handler scenario, FlashEvents is approximately **1.55× faster** and allocates **3.77× less memory** than MediatR using its parallel publisher.

- - -

### Scenario 2: Single Handler (`ForeachAwaitPublisher in MediatR`)

This test compares against MediatR’s default sequential publisher.

| Method | Mean | Error | StdDev | Ratio | Gen0 | Allocated | Alloc Ratio |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **FlashEvents\_Publish** | **89.94 ns** | **1.673 ns** | **1.644 ns** | **1.00** | **0.0168** | **176 B** | **1.00** |
| MediatR\_Publish | 84.09 ns | 0.739 ns | 0.691 ns | 0.94 | 0.0298 | 312 B | 1.77 |

**Result:** MediatR’s sequential publisher is slightly faster for a single handler, but FlashEvents remains significantly more memory-efficient, allocating **1.77× less memory**.

- - -

### Scenario 3: Two Handlers (`TaskWhenAllPublisher in MediatR`)

This test measures parallel execution with two handlers for the same event.

| Method | Mean | Error | StdDev | Ratio | Gen0 | Allocated | Alloc Ratio |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **FlashEvents\_Publish** | **269.3 ns** | **3.34 ns** | **2.79 ns** | **1.00** | **0.0677** | **712 B** | **1.00** |
| MediatR\_Publish | 171.4 ns | 0.95 ns | 0.79 ns | 0.64 | 0.0787 | 824 B | 1.16 |

**Result:** In the multi-handler scenario, MediatR’s publisher shows faster execution time. However, FlashEvents provides built-in scoped parallelism by design without extra configuration and maintains a slight edge in memory efficiency, allocating **1.16× less memory**. The primary benefit of FlashEvents is its architectural simplicity for achieving isolated, parallel execution.
