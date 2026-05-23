using Hermodr;

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

// ── Hermodr — Transactional Outbox + Scheduled Delivery Relay ───────
//
// Architecture (all within the same ASP.NET process):
//
//   HTTP request
//       │
//       ▼
//   OrderManagementService.PublishAsync(event, new OutboxPublishOptions
//   {
//       ScheduleDeliveryAt = <future UTC timestamp>
//   })
//       │
//       ▼
//   OutboxPublishChannel  ──── saves CloudEvent to SQLite (outbox-scheduling.db)
//                                     │
//                   ┌─────────────────┘  (polled every 1 s)
//                   │
//                   ▼
//   OutboxRelayService  (BackgroundService — same process)
//       │   waits until NextRetryAt <= now, then marks rows Sending
//       │
//       ▼
//   RabbitMQ channels  ──── forwards CloudEvent to RabbitMQ broker
//
// The OutboxPublishChannel detects the OutboxRelayPublishOptions signal emitted
// by the relay and short-circuits (no re-persistence), preventing an infinite loop.
//
// The event itself already happened when it is persisted; only transport delivery is
// delayed. Going through the outbox preserves the event record even if broker delivery
// later fails.

var events = builder.Services
    .AddEventPublisher(options =>
    {
        // The source identifies this microservice in every CloudEvent envelope.
        options.Source = new Uri("https://example.com/services/order-service-outbox-scheduling");
    });

// ── Channel 1: Outbox (SQLite via EF Core) ─────────────────────────────────
//
// AddEntityFrameworkOutbox registers OutboxPublishChannel<DbOutboxMessage> and
// creates an OutboxDbContext backed by SQLite.
// WithFactory wires up the thin OrderOutboxMessageFactory.
// WithRelay registers OutboxRelayService<DbOutboxMessage> as a BackgroundService
// in the same host — the relay polls every second to forward due messages.

events
    .AddEntityFrameworkOutbox(opts =>
        opts.UseSqlite(
            builder.Configuration.GetConnectionString("Outbox") ?? "Data Source=outbox-scheduling.db"))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(opts =>
    {
        opts.Interval     = TimeSpan.FromSeconds(1); // short so the demo shows scheduled release quickly
        opts.MaxBatchSize = 50;
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

