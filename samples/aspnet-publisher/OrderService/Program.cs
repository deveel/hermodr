using Deveel;
using Deveel.Events;

using OrderService.Endpoints;
using OrderService.Events;
using OrderService.Services;

// ── Builder ────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Order business logic
builder.Services.AddSingleton<IOrderService, OrderManagementService>();

// ── Deveel.Events ──────────────────────────────────────────────────────────
//
// Registers the event publisher and wires up a per-event-type RabbitMQ channel
// for each Order domain event.  Each event class carries [AmqpExchange] and
// [AmqpRoutingKey] annotations so the framework routes each event to the
// correct exchange / routing key automatically — no manual routing code needed.
//
// Shared connection options come from appsettings.json "Events:RabbitMq".
// Per-event overrides (exchange, routing key) are declared via annotations.

builder.Services
    .AddEventPublisher(options =>
    {
        // The source identifies this microservice in every CloudEvent envelope.
        options.Source = new Uri("https://example.com/services/order-service");
    })
    // Register a shared RabbitMQ channel that connects using appsettings options.
    .AddRabbitMq("Events:RabbitMq")
    // Typed channels: each event type is routed independently by its annotations.
    .AddRabbitMq<OrderCreated>("Events:RabbitMq")
    .AddRabbitMq<OrderConfirmed>("Events:RabbitMq")
    .AddRabbitMq<OrderShipped>("Events:RabbitMq")
    .AddRabbitMq<OrderDelivered>("Events:RabbitMq")
    .AddRabbitMq<OrderCancelled>("Events:RabbitMq");

// ── Application ────────────────────────────────────────────────────────────

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapOrderEndpoints();

app.Run();


