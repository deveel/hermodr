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

// ── Hermodr — Outbox only (no relay in this process) ────────────────
//
// Architecture:
//
//   HTTP request
//       │
//       ▼
//   OrderManagementService.PublishAsync(event)
//       │
//       ▼
//   OutboxPublishChannel  ──── saves CloudEvent to SQLite (shared outbox.db)
//
//   ┌─ different process ──────────────────────────────────────────────────┐
//   │  OrderService.RelayWorker                                            │
//   │      reads pending rows from the same outbox.db                     │
//   │      forwards them to RabbitMQ via MassTransit                      │
//   └──────────────────────────────────────────────────────────────────────┘
//
// This process has NO knowledge of RabbitMQ or MassTransit — it only writes
// to the outbox. The relay worker is responsible for transport delivery.

var events = builder.Services
    .AddEventPublisher(options =>
    {
        options.Source = new Uri("https://example.com/services/order-service");
    });

// ── Outbox channel (SQLite via EF Core) — NO relay here ───────────────────
//
// WithFactory wires up the thin OrderOutboxMessageFactory.
// WithRelay is intentionally absent — the relay runs in OrderService.RelayWorker.

events
    .AddEntityFrameworkOutbox(opts =>
        opts.UseSqlite(
            builder.Configuration.GetConnectionString("Outbox") ?? "Data Source=outbox.db"))
    .WithFactory<OrderOutboxMessageFactory>();
    // ↑ No .WithRelay() — forwarding is handled by the external relay worker.

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

