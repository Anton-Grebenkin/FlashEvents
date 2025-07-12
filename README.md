<h1>FlashEvents</h1>
<p><strong>FlashEvents</strong> is a high-performance, in-memory event publishing library for .NET designed with simplicity and speed in mind. It enables a robust publish-subscribe pattern where event handlers are executed <strong>in parallel</strong>, and each handler runs within its own isolated <strong>Dependency Injection scope</strong>.</p>
<p>This approach ensures that handlers do not interfere with each other, making it ideal for applications where handlers have their own unit of work, such as interacting with a database context (<code>DbContext</code>) or managing other scoped services.</p>
<h2>Key Features</h2>
<ul>
<li>🚀 <strong>Blazing Fast Performance</strong>: Optimized for low-latency and minimal memory allocations. See benchmarks below.</li>
<li>⚡ <strong>Parallel Execution</strong>: Automatically runs all handlers for a given event concurrently using <code>Task.WhenAll</code>, maximizing throughput.</li>
<li>🛡️ <strong>Scoped Handler Isolation</strong>: Each event handler is resolved and executed in its own <code>IServiceScope</code>. This prevents issues with shared state and transient service lifetimes (e.g., <code>DbContext</code> instances).</li>
<li>🔧 <strong>Simple &amp; Fluent API</strong>: Easy to set up and use with clean dependency injection extensions.</li>
<li>🔍 <strong>Automatic Handler Discovery</strong>: Register all your event handlers from an assembly with a single line of code.</li>
</ul>
<h2>Getting Started</h2>
<p>Using FlashEvents is straightforward. Follow these steps to integrate it into your application.</p>
<h3>1. Define an Event</h3>
<p>An event is a simple class or record that implements the <code>IEvent</code> marker interface.</p>
<pre><code class="language-csharp">// Define your event contracts in a shared library
using FlashEvents.Abstractions;

public record OrderCreatedEvent(int OrderId, string CustomerEmail) : IEvent;

// Abstractions (put these in a separate assembly if needed)
namespace FlashEvents.Abstractions
{
    public interface IEvent { }

    public interface IEventHandler&lt;in TEvent&gt; where TEvent : IEvent
    {
        Task Handle(TEvent @event, CancellationToken ct);
    }
}
</code></pre>
<h3>2. Create Event Handlers</h3>
<p>Create one or more handlers for your event. Each handler must implement <code>IEventHandler&lt;TEvent&gt;</code>.</p>
<pre><code class="language-csharp">using FlashEvents.Abstractions;
using Microsoft.Extensions.Logging;

// Handler 1: Sends a welcome email
public class SendWelcomeEmailHandler : IEventHandler&lt;OrderCreatedEvent&gt;
{
    private readonly IEmailService _emailService;
    private readonly ILogger&lt;SendWelcomeEmailHandler&gt; _logger;

    public SendWelcomeEmailHandler(IEmailService emailService, ILogger&lt;SendWelcomeEmailHandler&gt; logger)
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
public class UpdateAnalyticsHandler : IEventHandler&lt;OrderCreatedEvent&gt;
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger&lt;UpdateAnalyticsHandler&gt; _logger;

    public UpdateAnalyticsHandler(IAnalyticsService analyticsService, ILogger&lt;UpdateAnalyticsHandler&gt; logger)
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
</code></pre>
<h3>3. Configure Dependency Injection</h3>
<p>In your <code>Program.cs</code> or <code>Startup.cs</code>, register the event publisher and your handlers.</p>
<pre><code class="language-csharp">using FlashEvents;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// 1. Add FlashEvents services
builder.Services.AddEventPublisher();

// 2. Automatically discover and register all handlers from an assembly
builder.Services.AddEventHandlersFromAssembly(Assembly.GetExecutingAssembly());

// You can also register handlers manually
// builder.Services.AddEventHandler&lt;IEventHandler&lt;OrderCreatedEvent&gt;, SendWelcomeEmailHandler&gt;();

// Register other application services
builder.Services.AddTransient&lt;IEmailService, EmailService&gt;();
builder.Services.AddScoped&lt;IAnalyticsService, AnalyticsService&gt;(); // Example of a scoped service

var app = builder.Build();

// ...
</code></pre>
<h3>4. Publish an Event</h3>
<p>Inject <code>IEventPublisher</code> into your services and call <code>PublishAsync</code> to trigger all registered handlers.</p>
<pre><code class="language-csharp">using FlashEvents.Abstractions;
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
    public async Task&lt;IActionResult&gt; CreateOrder()
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
</code></pre>
<h2>⚠️ Important Note: Handler Caching</h2>
<p>For maximum performance, <strong>FlashEvents caches the list of handler types (<code>Type[]</code>) for each event type (<code>IEvent</code>) upon its first publication</strong>.</p>
<p>This means that any changes to handler registrations in the DI container <em>after an event of that type has already been published</em> will not be recognized. The handler cache <strong>cannot be reset</strong> during the application's runtime.</p>
<p><strong>All event handler registrations must be configured at application startup.</strong></p>
<h2>How It Works</h2>
<p>FlashEvents achieves its performance and isolation through a simple but effective mechanism:</p>
<ol>
<li>When <code>PublishAsync</code> is called for an event type for the first time, it resolves all registered <code>IEventHandler&lt;TEvent&gt;</code> services from the DI container.</li>
<li>The concrete types of these handlers are stored in a static, thread-safe cache (<code>LazyInitializer</code>) associated with the event type.</li>
<li>On every publish call (including the first), it iterates through the cached handler types.</li>
<li>For each handler type, it creates a new <code>AsyncServiceScope</code> from the root <code>IServiceProvider</code>.</li>
<li>It resolves the handler service within this new scope and executes its <code>Handle</code> method.</li>
<li><code>Task.WhenAll</code> is used to await all handler tasks, ensuring they run concurrently.</li>
<li>If any handler throws an exception, all exceptions are collected into a single <code>AggregateException</code>.</li>
</ol>
<p>This design guarantees that each handler gets a fresh set of scoped dependencies, providing perfect isolation with minimal overhead.</p>
<h2>Benchmarks</h2>
<p>The following benchmarks compare <code>FlashEvents</code> with <code>MediatR v12.5.0</code> under different scenarios.</p>
<p><strong>System Configuration:</strong></p>
<ul>
<li>BenchmarkDotNet v0.15.2, Windows 11 (10.0.22631.5335)</li>
<li>13th Gen Intel Core i5-13400F 2.50GHz, 1 CPU, 16 logical and 10 physical cores</li>
<li>.NET SDK 9.0.302, .NET 8.0.18</li>
</ul>
<hr>
<h3>Scenario 1: Single Handler (<code>TaskWhenAllPublisher in MediatR</code>)</h3>
<p>This test measures the performance of publishing an event that is handled by a single handler.</p>
<table>
<thead>
<tr>
<th align="left">Method</th>
<th align="right">Mean</th>
<th align="right">Error</th>
<th align="right">StdDev</th>
<th align="right">Ratio</th>
<th align="right">Gen0</th>
<th align="right">Allocated</th>
<th align="right">Alloc Ratio</th>
</tr>
</thead>
<tbody><tr>
<td align="left"><strong>FlashEvents_Publish</strong></td>
<td align="right"><strong>91.28 ns</strong></td>
<td align="right"><strong>1.149 ns</strong></td>
<td align="right"><strong>1.075 ns</strong></td>
<td align="right"><strong>1.00</strong></td>
<td align="right"><strong>0.0168</strong></td>
<td align="right"><strong>176 B</strong></td>
<td align="right"><strong>1.00</strong></td>
</tr>
<tr>
<td align="left">Mediatr_Publish</td>
<td align="right">141.19 ns</td>
<td align="right">1.114 ns</td>
<td align="right">0.988 ns</td>
<td align="right">1.55</td>
<td align="right">0.0634</td>
<td align="right">664 B</td>
<td align="right">3.77</td>
</tr>
</tbody></table>
<p><strong>Result:</strong> In a single-handler scenario, FlashEvents is approximately <strong>1.55x faster</strong> and allocates <strong>3.77x less memory</strong> than MediatR using its parallel publisher.</p>
<hr>
<h3>Scenario 2: Single Handler(<code>ForeachAwaitPublisher in MediatR</code>)</h3>
<p>This test compares against MediatR's default sequential publisher.</p>
<table>
<thead>
<tr>
<th align="left">Method</th>
<th align="right">Mean</th>
<th align="right">Error</th>
<th align="right">StdDev</th>
<th align="right">Ratio</th>
<th align="right">Gen0</th>
<th align="right">Allocated</th>
<th align="right">Alloc Ratio</th>
</tr>
</thead>
<tbody><tr>
<td align="left"><strong>FlashEvents_Publish</strong></td>
<td align="right"><strong>89.94 ns</strong></td>
<td align="right"><strong>1.673 ns</strong></td>
<td align="right"><strong>1.644 ns</strong></td>
<td align="right"><strong>1.00</strong></td>
<td align="right"><strong>0.0168</strong></td>
<td align="right"><strong>176 B</strong></td>
<td align="right"><strong>1.00</strong></td>
</tr>
<tr>
<td align="left">Mediatr_Publish</td>
<td align="right">84.09 ns</td>
<td align="right">0.739 ns</td>
<td align="right">0.691 ns</td>
<td align="right">0.94</td>
<td align="right">0.0298</td>
<td align="right">312 B</td>
<td align="right">1.77</td>
</tr>
</tbody></table>
<p><strong>Result:</strong> MediatR's sequential publisher is slightly faster for a single handler, but FlashEvents remains significantly more memory-efficient, allocating <strong>1.77x less memory</strong>.</p>
<hr>
<h3>Scenario 3: Two Handlers (<code>TaskWhenAllPublisher in MediatR</code>)</h3>
<p>This test measures parallel execution with two handlers for the same event.</p>
<table>
<thead>
<tr>
<th align="left">Method</th>
<th align="right">Mean</th>
<th align="right">Error</th>
<th align="right">StdDev</th>
<th align="right">Ratio</th>
<th align="right">Gen0</th>
<th align="right">Allocated</th>
<th align="right">Alloc Ratio</th>
</tr>
</thead>
<tbody><tr>
<td align="left"><strong>FlashEvents_Publish</strong></td>
<td align="right"><strong>269.3 ns</strong></td>
<td align="right"><strong>3.34 ns</strong></td>
<td align="right"><strong>2.79 ns</strong></td>
<td align="right"><strong>1.00</strong></td>
<td align="right"><strong>0.0677</strong></td>
<td align="right"><strong>712 B</strong></td>
<td align="right"><strong>1.00</strong></td>
</tr>
<tr>
<td align="left">Mediatr_Publish</td>
<td align="right">171.4 ns</td>
<td align="right">0.95 ns</td>
<td align="right">0.79 ns</td>
<td align="right">0.64</td>
<td align="right">0.0787</td>
<td align="right">824 B</td>
<td align="right">1.16</td>
</tr>
</tbody></table>
<p><strong>Result:</strong> In the multi-handler scenario, MediatR's publisher shows a faster execution time. However, FlashEvents provides built-in scoped parallelism by design without any extra configuration and maintains a slight edge in memory efficiency, allocating <strong>1.16x less memory</strong>. The primary benefit of FlashEvents is its architectural simplicity for achieving isolated, parallel execution.</p>
<h2>License</h2>
<p>This project is licensed under the MIT License. See the <code>LICENSE</code> file for details.</p>
