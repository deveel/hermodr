using Deveel;
using Deveel.Events;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using OrderService.RelayWorker.Infrastructure;

// ── Builder ────────────────────────────────────────────────────────────────

var builder = Host.CreateApplicationBuilder(args);

// ── MassTransit — RabbitMQ transport ──────────────────────────────────────
//
// MassTransit is registered first so that IPublishEndpoint / ISendEndpointProvider
// are available in the DI container when the Deveel.Events MassTransit channel resolves them.

builder.Services.AddMassTransit(mt =>
{
    mt.UsingRabbitMq((ctx, cfg) =>
    {
        var host     = builder.Configuration["Events:MassTransit:RabbitMq:Host"]     ?? "localhost";
        var username = builder.Configuration["Events:MassTransit:RabbitMq:Username"] ?? "guest";
        var password = builder.Configuration["Events:MassTransit:RabbitMq:Password"] ?? "guest";

        cfg.Host(host, h =>
        {
            h.Username(username);
            h.Password(password);
        });

        // Let MassTransit auto-configure consumer endpoints (none here — publish only).
        cfg.ConfigureEndpoints(ctx);
    });
});

// ── Deveel.Events — Outbox relay → MassTransit ────────────────────────────
//
// Architecture:
//
//   ┌─────────── shared outbox.db ──────────────────────────────────────────┐
//   │  (written by OrderService.Api via OutboxPublishChannel)               │
//   └────────────────────────┬──────────────────────────────────────────────┘
//                            │  polled every 5 s by OutboxRelayService
//                            ▼
//   OutboxRelayService  (BackgroundService — this process)
//       │   reads pending rows from DbOutboxMessages, marks them Sending
//       │
//       ▼
//   MassTransitPublishChannel  ──── publishes CloudEvent to RabbitMQ
//
// The relay re-enters the event publisher pipeline with an OutboxRelayPublishOptions
// token so the OutboxPublishChannel short-circuits (skips re-persistence) and the
// MassTransit channel takes over as the transport.

var events = builder.Services
    .AddEventPublisher(options =>
    {
        // Must match the source registered in OrderService.Api so CloudEvents are consistent.
        options.Source = new Uri("https://example.com/services/order-service");
    });

// ── Channel 1: Outbox (read side only) ────────────────────────────────────
//
// Points to the SAME database file that the API writes to.
// WithRelay registers OutboxRelayService<DbOutboxMessage> as the hosted BackgroundService
// that polls and dispatches pending messages.

events
    .AddEntityFrameworkOutbox(opts =>
        opts.UseSqlite(
            builder.Configuration.GetConnectionString("Outbox") ?? "Data Source=outbox.db"))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(opts =>
    {
        opts.Interval     = TimeSpan.FromSeconds(5);   // poll cadence — tune for production
        opts.MaxBatchSize = 50;                         // max messages per relay cycle
    });

// ── Channel 2: MassTransit (send side) ────────────────────────────────────
//
// A single default MassTransit channel handles all event types forwarded by the relay.

events
    .AddMassTransit();

// ── Run ────────────────────────────────────────────────────────────────────

var host = builder.Build();
host.Run();

