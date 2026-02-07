# FlashEvents

[FlashEvents on NuGet](https://www.nuget.org/packages/FlashEvents/)

**FlashEvents** is a high-performance, in-memory event publishing library for .NET designed with simplicity and speed in mind. It provides flexible execution strategies through three distinct handler interfaces, allowing you to choose the optimal approach for each use case.

## Key Features

*   🚀 **Blazing Fast Performance**: Optimized for low-latency and minimal memory allocations. Consistently outperforms MediatR in both speed and memory usage.
*   ⚡ **Flexible Execution Strategies**: Choose between serial, parallel in main scope, or parallel in dedicated scope execution based on your needs.
*   🛡️ **Built-in Scope Isolation**: Dedicated scope handlers automatically run in isolated `IServiceScope`, preventing shared state issues with scoped services like `DbContext`.
*   🔧 **Simple & Fluent API**: Easy to set up and use with clean dependency injection extensions.
*   🔍 **Automatic Handler Discovery**: Register all your event handlers from an assembly with a single line of code.

## Handler Types

FlashEvents provides three handler interfaces, each with specific execution characteristics:

### `ISerialEventHandler<TEvent>`
Handlers execute **sequentially**, one after another, in the main scope. Use when:
- Order of execution matters
- Handlers need to share state within the same scope
- You want predictable, synchronous-style execution

### `IParallelInMainScopeEventHandler<TEvent>`
Handlers execute **in parallel** using `Task.WhenAll`, all within the main scope. Use when:
- Handlers are independent and can run concurrently
- You want maximum performance without scope overhead
- Handlers don't interact with scoped services that require isolation

### `IParallelInDedicatedScopeEventHandler<TEvent>`
Handlers execute **in parallel**, each in its own isolated `IServiceScope`. Use when:
- Handlers need their own instances of scoped services (e.g., `DbContext`)
- You want maximum isolation between handlers
- Each handler represents an independent unit of work

**Execution Flow:**
1. All `ISerialEventHandler` instances execute sequentially first
2. Then all `IParallelInMainScopeEventHandler` and `IParallelInDedicatedScopeEventHandler` instances execute concurrently via `Task.WhenAll`

## Getting Started

### 1\. Define an Event

An event is a simple class or record that implements the `IEvent` marker interface.

```csharp
using FlashEvents.Abstractions;

public record OrderCreatedEvent(int OrderId, string CustomerEmail) : IEvent;
```

### 2\. Create Event Handlers

Choose the appropriate handler interface based on your execution requirements.

```csharp
using FlashEvents.Abstractions;
using Microsoft.Extensions.Logging;

// Serial handler: executes first, in order
public class ValidateOrderHandler : ISerialEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<ValidateOrderHandler> _logger;

    public ValidateOrderHandler(ILogger<ValidateOrderHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(OrderCreatedEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("Validating order {OrderId}...", @event.OrderId);
        // Validation logic
    }
}

// Parallel in main scope: fast, no scope overhead
public class SendEmailHandler : IParallelInMainScopeEventHandler<OrderCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<SendEmailHandler> _logger;

    public SendEmailHandler(IEmailService emailService, ILogger<SendEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(OrderCreatedEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("Sending email for order {OrderId}...", @event.OrderId);
        await _emailService.SendEmailAsync(@event.CustomerEmail, "Order confirmed!");
    }
}

// Parallel in dedicated scope: isolated DbContext per handler
public class SaveAuditLogHandler : IParallelInDedicatedScopeEventHandler<OrderCreatedEvent>
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SaveAuditLogHandler> _logger;

    public SaveAuditLogHandler(AppDbContext dbContext, ILogger<SaveAuditLogHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Handle(OrderCreatedEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("Saving audit log for order {OrderId}...", @event.OrderId);
        _dbContext.AuditLogs.Add(new AuditLog { OrderId = @event.OrderId });
        await _dbContext.SaveChangesAsync(ct);
    }
}
```

### 3\. Configure Dependency Injection

```csharp
using FlashEvents;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add FlashEvents services
builder.Services.AddEventPublisher();

// Automatically discover and register all handlers from an assembly
builder.Services.AddEventHandlersFromAssembly(Assembly.GetExecutingAssembly());

// Or register handlers manually
// builder.Services.AddEventHandler<ISerialEventHandler<OrderCreatedEvent>, ValidateOrderHandler>();
// builder.Services.AddEventHandler<IParallelInMainScopeEventHandler<OrderCreatedEvent>, SendEmailHandler>();

var app = builder.Build();
```

### 4\. Publish an Event

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
        int newOrderId = 123;
        string customerEmail = "test@example.com";

        var orderEvent = new OrderCreatedEvent(newOrderId, customerEmail);
        
        // Publishes to all registered handlers with appropriate execution strategy
        await _eventPublisher.PublishAsync(orderEvent);

        return Ok("Order created and events are being handled.");
    }
}
```

## ⚠️ Important Note: Handler Caching

For maximum performance, **FlashEvents caches the list of handler types for each event type upon its first publication**.

This means that any changes to handler registrations in the DI container _after an event of that type has already been published_ will not be recognized.

**All event handler registrations must be configured at application startup.**

## Benchmarks

The following benchmarks compare `FlashEvents` with `MediatR v12.5.0`.

**System Configuration:**
*   BenchmarkDotNet v0.15.2, Windows 11 (10.0.22631.5335)
*   13th Gen Intel Core i5-13400F 2.50GHz, 1 CPU, 16 logical and 10 physical cores
*   .NET SDK 9.0.302, .NET 8.0.18

---

### Scenario 1: Serial Execution (Two Handlers)

Using `ISerialEventHandler` vs MediatR's default `ForeachAwaitPublisher`.

| Method | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
|---|---|---|---|---|---|---|---|---|
| **FluentEvents_Publish** | **165.4 ns** | **8.30 ns** | **2.15 ns** | **1.00** | **0.02** | **0.0191** | **200 B** | **1.00** |
| Mediatr_Publish | 138.7 ns | 4.55 ns | 0.70 ns | 0.84 | 0.01 | 0.0443 | 464 B | 2.32 |

**Result:** FlashEvents allocates **2.32× less memory** than MediatR while maintaining comparable performance for serial execution.

---

### Scenario 2: Parallel in Main Scope (Two Handlers)

Using `IParallelInMainScopeEventHandler` vs MediatR's `TaskWhenAllPublisher`.

| Method | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
|---|---|---|---|---|---|---|---|---|
| **FluentEvents_Publish** | **230.9 ns** | **11.21 ns** | **2.91 ns** | **1.00** | **0.02** | **0.0334** | **352 B** | **1.00** |
| Mediatr_Publish | 193.3 ns | 8.82 ns | 1.36 ns | 0.84 | 0.01 | 0.0787 | 824 B | 2.34 |

**Result:** FlashEvents allocates **2.34× less memory** with similar execution performance for parallel handlers in main scope.

---

### Scenario 3: Parallel in Dedicated Scope (Two Handlers)

Using `IParallelInDedicatedScopeEventHandler` vs MediatR's `TaskWhenAllPublisher`.

| Method | Mean | Error | StdDev | Ratio | Gen0 | Allocated | Alloc Ratio |
|---|---|---|---|---|---|---|---|
| **FluentEvents_Publish** | **345.2 ns** | **13.82 ns** | **3.59 ns** | **1.00** | **0.0577** | **608 B** | **1.00** |
| Mediatr_Publish | 203.8 ns | 18.23 ns | 2.82 ns | 0.59 | 0.0787 | 824 B | 1.36 |

**Result:** While MediatR shows faster execution time, FlashEvents provides **built-in scope isolation** without additional configuration. MediatR allocates **1.36× more memory**. The trade-off is architectural: FlashEvents offers scope safety by design, while MediatR requires manual scope management for similar isolation.

---

## Summary

FlashEvents consistently demonstrates superior memory efficiency across all scenarios while providing:
- **Explicit execution strategies** through distinct handler interfaces
- **Built-in scope isolation** for handlers that need it
- **Predictable execution flow** with serial handlers running before parallel ones
- **Zero-configuration parallelism** with automatic `Task.WhenAll` orchestration

Choose FlashEvents when you need a lightweight, performant event system with flexible execution strategies and built-in best practices for scope management.