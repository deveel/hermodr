using Deveel;
using Deveel.Events;

using Microsoft.EntityFrameworkCore;

using OrderService.Endpoints;
using OrderService.Infrastructure;
using OrderService.Services;

// ── Builder ────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Order business logic
builder.Services.AddSingleton<IOrderService, OrderManagementService>();

// ── Deveel.Events — Transactional Outbox + In-Process Relay ───────────────
//
// Architecture (all within the same ASP.NET process):
//
//   HTTP request
//       │
//       ▼
//   OrderManagementService.PublishAsync(event)
//       │
//       ▼
//   OutboxPublishChannel  ──── saves CloudEvent to SQLite (outbox.db)
//                                     │
//                   ┌─────────────────┘  (polled every 5 s)
//                   │
//                   ▼
//   OutboxRelayService  (BackgroundService — same process)
//       │   reads pending rows, marks them Sending
//       │
//       ▼
//   RabbitMQ channels  ──── forwards CloudEvent to RabbitMQ broker
//
// The OutboxPublishChannel detects the OutboxRelayPublishOptions signal emitted
// by the relay and short-circuits (no re-persistence), preventing an infinite loop.
//
// NOTE: the relay uses the same default IEventPublisher pipeline, so you just need
// to register all channels (outbox + RabbitMQ) on the same builder.

var events = builder.Services
    .AddEventPublisher(options =>
    {
        // The source identifies this microservice in every CloudEvent envelope.
        options.Source = new Uri("https://example.com/services/order-service-outbox");
    });

// ── Channel 1: Outbox (SQLite via EF Core) ─────────────────────────────────
//
// AddEntityFrameworkOutbox registers OutboxPublishChannel<DbOutboxMessage> and
// creates an OutboxDbContext backed by SQLite.
// WithFactory wires up the thin OrderOutboxMessageFactory.
// WithRelay registers OutboxRelayService<DbOutboxMessage> as a BackgroundService
// in the same host — the relay polls every 5 seconds to forward pending messages.

events
    .AddEntityFrameworkOutbox(opts =>
        opts.UseSqlite(
            builder.Configuration.GetConnectionString("Outbox") ?? "Data Source=outbox.db"))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(opts =>
    {
        opts.Interval     = TimeSpan.FromSeconds(5);  // fast for the demo; tune for production
        opts.MaxBatchSize = 50;                        // cap per relay cycle
    });

// ── Channel 2: RabbitMQ (transport — used by the relay) ──────────────────
//
// A single default RabbitMQ channel handles all event types.
// Per-event routing (exchange, routing key) is driven by [AmqpExchange] and
// [AmqpRoutingKey] annotations — no per-type registration needed.

events
    .AddRabbitMq("Events:RabbitMq");

// ── Application ────────────────────────────────────────────────────────────

var app = builder.Build();

// Ensure the SQLite outbox schema exists before handling any requests.
// In production use proper EF Core migrations instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapOrderEndpoints();

app.Run();

