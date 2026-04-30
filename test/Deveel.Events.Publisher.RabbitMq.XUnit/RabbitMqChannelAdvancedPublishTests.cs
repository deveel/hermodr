// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using System.Text.Json;

namespace Deveel.Events
{
    /// <summary>
    /// Integration tests for <see cref="RabbitMqPublishChannel"/> that cover additional
    /// scenarios not included in <see cref="RabbitMqChannelPublishTests"/>, such as:
    /// publisher confirms, options-only routing, per-call overrides, typed channels,
    /// and already-cancelled-token handling.
    /// All tests require a live RabbitMQ broker from the shared
    /// <see cref="RabbitMqTestServer"/> Testcontainers fixture.
    /// </summary>
    [Trait("Channel", "RabbitMQ")]
    [Trait("Function", "Publish")]
    [Trait("Kind", "Integration")]
    [Trait("CICD", "WindowsExclude")]
    public class RabbitMqChannelAdvancedPublishTests : IClassFixture<RabbitMqTestServer>, IAsyncLifetime
    {
        private const string ExchangeName = "advanced-test";
        private const string QueueName    = "advanced-test.queue";
        private const string RoutingKey   = "advanced.event";

        private readonly string _connectionString;
        private IChannel? _consumerChannel;
        private CloudEvent? ReceivedEvent { get; set; }

        public RabbitMqChannelAdvancedPublishTests(
            RabbitMqTestServer testServer,
            ITestOutputHelper outputHelper)
        {
            _connectionString = testServer.ConnectionString;

            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.DataSchemaBaseUri = new Uri("http://example.com/events/schema");
                options.Source            = new Uri("https://api.svc.deveel.com");
            })
            .AddRabbitMq(options =>
            {
                options.ConnectionString  = _connectionString;
                options.ExchangeName      = ExchangeName;
                options.RoutingKey        = RoutingKey;
                // Publisher confirms enabled – exercises a different code path than
                // RabbitMqChannelPublishTests which always sets PublisherConfirms = false.
                options.PublisherConfirms = true;
            });

            services.AddLogging(logging =>
                logging.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Trace));

            Services = services.BuildServiceProvider();
        }

        private IServiceProvider Services { get; }

        private EventPublisher Publisher => Services.GetRequiredService<EventPublisher>();

        // Channels are keyed by pipeline name (string.Empty for the default pipeline).
        private IEventPublishChannel Channel
            => Services.GetRequiredKeyedService<IEventPublishChannel>(string.Empty);

        public async ValueTask InitializeAsync()
        {
            var connection = Services.GetRequiredService<IConnection>();
            _consumerChannel = await connection.CreateChannelAsync();

            await _consumerChannel.ExchangeDeclareAsync(
                ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
            await _consumerChannel.QueueDeclareAsync(
                QueueName, durable: true, exclusive: false, autoDelete: false);
            await _consumerChannel.QueueBindAsync(QueueName, ExchangeName, RoutingKey);

            var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
            consumer.ReceivedAsync += (_, args) =>
            {
                var json = JsonSerializer.Deserialize<JsonElement>(args.Body.ToArray());
                var formatter = new JsonEventFormatter();
                ReceivedEvent = formatter.ConvertFromJsonElement(json, null);
                return Task.CompletedTask;
            };

            await _consumerChannel.BasicConsumeAsync(QueueName, autoAck: true, consumer);
        }

        public async ValueTask DisposeAsync()
        {
            if (_consumerChannel != null)
                await _consumerChannel.DisposeAsync();

            (Services as IDisposable)?.Dispose();
        }

        // ── Publisher confirms ────────────────────────────────────────────────

        [Fact]
        public async Task PublishCloudEvent_WithPublisherConfirmsEnabled_IsBrokerAcknowledged()
        {
            var cloudEvent = new CloudEvent
            {
                Subject         = "confirms-test",
                DataContentType = "application/json",
                Data            = JsonSerializer.Serialize(new { OrderId = "O-001" }),
                Source          = new Uri("https://api.svc.deveel.com/orders"),
                Type            = "order.placed",
                Time            = DateTimeOffset.UtcNow,
                Id              = Guid.NewGuid().ToString("N"),
            };

            // Exchange and routing key come entirely from channel options.
            await Publisher.PublishEventAsync(cloudEvent);
            await Task.Delay(500);

            Assert.NotNull(ReceivedEvent);
            Assert.Equal(cloudEvent.Id,   ReceivedEvent.Id);
            Assert.Equal(cloudEvent.Type, ReceivedEvent.Type);
        }

        // ── Options-based routing (no AMQP attributes on the event) ──────────

        [Fact]
        public async Task PublishCloudEvent_UsingOptionsRouting_DeliveredToCorrectQueue()
        {
            // The CloudEvent carries NO amqpexchange / amqproutingkey extension attributes.
            // The channel must fall back to ExchangeName and RoutingKey from options.
            var cloudEvent = new CloudEvent
            {
                Subject         = "options-routing",
                DataContentType = "application/json",
                Data            = JsonSerializer.Serialize(new { Value = "hello" }),
                Source          = new Uri("https://api.svc.deveel.com"),
                Type            = "test.options-routing",
                Time            = DateTimeOffset.UtcNow,
                Id              = Guid.NewGuid().ToString("N"),
            };

            await Publisher.PublishEventAsync(cloudEvent);
            await Task.Delay(500);

            Assert.NotNull(ReceivedEvent);
            Assert.Equal(cloudEvent.Id, ReceivedEvent.Id);
        }

        // ── Non-persistent delivery mode (per-call override) ─────────────────

        [Fact]
        public async Task PublishCloudEvent_WithPerCallNonPersistentOverride_IsDelivered()
        {
            // Exchange and routing key are null in the per-call override so they fall
            // through to the channel defaults via MergeOptions.
            var channel = Channel;

            var cloudEvent = new CloudEvent
            {
                Subject         = "non-persistent",
                DataContentType = "application/json",
                Data            = JsonSerializer.Serialize(new { Flag = "transient" }),
                Source          = new Uri("https://api.svc.deveel.com"),
                Type            = "test.non-persistent",
                Time            = DateTimeOffset.UtcNow,
                Id              = Guid.NewGuid().ToString("N"),
            };

            var perCallOptions = new RabbitMqPublishOptions
            {
                PersistentMessages = false,
            };

            await channel.PublishAsync(cloudEvent, perCallOptions, CancellationToken.None);
            await Task.Delay(500);

            Assert.NotNull(ReceivedEvent);
            Assert.Equal(cloudEvent.Id, ReceivedEvent.Id);
        }

        // ── Per-call ClientName override ──────────────────────────────────────

        [Fact]
        public async Task PublishCloudEvent_WithPerCallClientNameOverride_IsDelivered()
        {
            var channel = Channel;

            var cloudEvent = new CloudEvent
            {
                Subject         = "client-name-test",
                DataContentType = "application/json",
                Data            = JsonSerializer.Serialize(new { X = 1 }),
                Source          = new Uri("https://api.svc.deveel.com"),
                Type            = "test.client-name",
                Time            = DateTimeOffset.UtcNow,
                Id              = Guid.NewGuid().ToString("N"),
            };

            var perCallOptions = new RabbitMqPublishOptions
            {
                ClientName = "OverriddenClient",
            };

            await channel.PublishAsync(cloudEvent, perCallOptions, CancellationToken.None);
            await Task.Delay(500);

            Assert.NotNull(ReceivedEvent);
            Assert.Equal(cloudEvent.Id, ReceivedEvent.Id);
        }

        // ── Typed channel ─────────────────────────────────────────────────────

        [Fact]
        public async Task PublishTypedEvent_ViaTypedChannel_IsDeliveredToCorrectQueue()
        {
            // Build a provider that registers both the generic channel AND a typed channel
            // for OrderCreated bound to the same exchange/routing key.
            var services = new ServiceCollection();
            services.AddEventPublisher(o =>
            {
                o.DataSchemaBaseUri = new Uri("http://example.com/events/schema");
                o.Source            = new Uri("https://api.svc.deveel.com");
            })
            .AddRabbitMq(opts =>
            {
                opts.ConnectionString  = _connectionString;
                opts.PublisherConfirms = true;
            })
            .AddRabbitMq<OrderCreated>(opts =>
            {
                opts.ExchangeName = ExchangeName;
                opts.RoutingKey   = RoutingKey;
            });

            services.AddLogging();

            await using var provider = services.BuildServiceProvider();

            var publisher = provider.GetRequiredService<EventPublisher>();
            var order = new OrderCreated { OrderId = "X-999", Total = 49.99m };

            await publisher.PublishAsync(order);
            await Task.Delay(500);

            Assert.NotNull(ReceivedEvent);
            Assert.Equal("order.created", ReceivedEvent.Type);
        }

        // ── Already-cancelled token ───────────────────────────────────────────

        [Fact]
        public async Task PublishAsync_WithAlreadyCancelledToken_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var cloudEvent = new CloudEvent
            {
                Subject         = "cancel-test",
                DataContentType = "application/json",
                Data            = JsonSerializer.Serialize(new { X = 1 }),
                Source          = new Uri("https://api.svc.deveel.com"),
                Type            = "test.cancelled",
                Time            = DateTimeOffset.UtcNow,
                Id              = Guid.NewGuid().ToString("N"),
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Publisher.PublishEventAsync(cloudEvent, cancellationToken: cts.Token));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        [Event("order.created", "1.0")]
        private class OrderCreated
        {
            public string  OrderId { get; set; } = string.Empty;
            public decimal Total   { get; set; }
        }
    }
}




