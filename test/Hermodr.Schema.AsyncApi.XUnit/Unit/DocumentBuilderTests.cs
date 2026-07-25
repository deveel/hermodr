//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Saunter.AsyncApiSchema.v2;
using Saunter.AsyncApiSchema.v2.Bindings;
using Saunter.AsyncApiSchema.v2.Bindings.Amqp;

namespace Hermodr {
    /// <summary>
    /// Tests for <see cref="AsyncApiDocumentBuilder"/>.
    /// </summary>
    public class AsyncApiDocumentBuilderTests {
        [Fact]
        public void Build_WithoutTitle_Throws() {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new AsyncApiDocumentBuilder().WithVersion("1.0").Build());
            Assert.Contains("title", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_WithoutVersion_Throws() {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new AsyncApiDocumentBuilder().WithTitle("T").Build());
            Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_WithSchemas_AddsComponentsAndChannels() {
            var doc = new AsyncApiDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithSchemas(new[] { TestSchemas.SimpleSchema() })
                .Build();

            Assert.True(doc.Components!.Schemas.ContainsKey("user-registered"));
            Assert.True(doc.Components.Messages.ContainsKey("user-registered"));
            Assert.True(doc.Channels.ContainsKey("user-registered"));
        }

        [Fact]
        public void Build_OtherTransportChannelIsExcluded() {
            var otherChannel = new EventPublishChannelMetadata(null, EventTransports.Other);

            var doc = new AsyncApiDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithSchemas(new[] { TestSchemas.SimpleSchema() })
                .WithChannels(new[] { otherChannel })
                .Build();

            // The schema-only channel from AddSchema is still present, but no
            // transport channel should be added.
            Assert.True(doc.Channels.ContainsKey("user-registered"));
            Assert.Equal(1, doc.Channels.Count);
        }

        [Fact]
        public void Build_RabbitMqChannelProducesAmqpBinding() {
            var rabbit = new EventPublishChannelMetadata(
                "orders",
                EventTransports.RabbitMq,
                new Dictionary<string, string?> {
                    ["exchange"] = "orders.x",
                    ["routingKey"] = "#"
                });

            var doc = new AsyncApiDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithChannels(new[] { rabbit })
                .Build();

            Assert.True(doc.Channels.ContainsKey("orders"));
            var channel = doc.Channels["orders"];
            Assert.NotNull(channel.Bindings);
            var bindings = Assert.IsType<ChannelBindings>(channel.Bindings);
            Assert.NotNull(bindings.Amqp);
            Assert.Equal("orders.x", bindings.Amqp!.Exchange?.Name);
            Assert.NotNull(channel.Publish);
        }

        [Fact]
        public void Build_UnknownTransportProducesNoBindingButChannelPresent() {
            var kafka = new EventPublishChannelMetadata(
                "events",
                "kafka",
                new Dictionary<string, string?> { ["topic"] = "events.v1" });

            var doc = new AsyncApiDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithChannels(new[] { kafka })
                .Build();

            Assert.True(doc.Channels.ContainsKey("events"));
            var channel = doc.Channels["events"];
            // No native binding for kafka in Saunter.
            Assert.Null(channel.Bindings);
            // Transport is preserved in the channel description.
            Assert.Contains("kafka", channel.Description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_AggregatesServersFromWebhookChannels() {
            var webhook = new EventPublishChannelMetadata(
                "hooks",
                EventTransports.Webhook,
                new Dictionary<string, string?> { ["url"] = "https://example.com/hook" });

            var doc = new AsyncApiDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithChannels(new[] { webhook })
                .Build();

            Assert.NotEmpty(doc.Servers);
            var server = doc.Servers.Values.First();
            Assert.Equal("https://example.com/hook", server.Url);
        }

        [Fact]
        public void Build_MultipleSchemasAndChannels() {
            var schemas = new[] { TestSchemas.SimpleSchema(), TestSchemas.NestedSchema() };
            var channels = new[] {
                new EventPublishChannelMetadata("user-registered", EventTransports.RabbitMq,
                    new Dictionary<string, string?> { ["exchange"] = "users" }),
                new EventPublishChannelMetadata("order-shipped", EventTransports.Webhook,
                    new Dictionary<string, string?> { ["url"] = "https://example.com" })
            };

            var doc = new AsyncApiDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("2.0")
                .WithSchemas(schemas)
                .WithChannels(channels)
                .Build();

            Assert.Equal(2, doc.Components!.Schemas.Count);
            Assert.True(doc.Channels.ContainsKey("user-registered"));
            Assert.True(doc.Channels.ContainsKey("order-shipped"));
        }
    }

    /// <summary>
    /// Tests for <see cref="EventSchemaDiscovery"/>.
    /// </summary>
    public class EventSchemaDiscoveryTests {
        [Fact]
        public void DiscoverSchemas_FindsAnnotatedTypesInCallingAssembly() {
            var discovery = new EventSchemaDiscovery();
            var schemas = discovery.DiscoverSchemas(typeof(EventSchemaDiscoveryTests).Assembly);

            var eventTypes = schemas.Select(s => s.EventType).ToList();
            Assert.Contains("person.created", eventTypes);
            Assert.Contains("order.placed", eventTypes);
        }

        [Fact]
        public void DiscoverSchemas_SkipsDataSchemaOnlyTypes() {
            var discovery = new EventSchemaDiscovery();
            var schemas = discovery.DiscoverSchemas(typeof(EventSchemaDiscoveryTests).Assembly);

            // SchemaUriOnlyEvent has DataSchema (URI) set, not DataVersion;
            // it must be skipped by discovery.
            Assert.DoesNotContain("schema.uri.only", schemas.Select(s => s.EventType));
        }

        [Fact]
        public void DiscoverSchemas_UnknownAssemblyReturnsEmpty() {
            var discovery = new EventSchemaDiscovery();
            var schemas = discovery.DiscoverSchemas(typeof(object).Assembly);
            Assert.Empty(schemas);
        }
    }

    /// <summary>
    /// Tests for the <see cref="EventSchemaAsyncApiExtensions.AddEvent"/> overload.
    /// </summary>
    public class AddEventTests {
        [Fact]
        public void AddEvent_NullChannel_FallsBackToSubscribeChannel() {
            var doc = new AsyncApiDocument { Info = new Info("T", "1") };
            doc.AddEvent(TestSchemas.SimpleSchema(), channel: null);

            Assert.NotNull(doc.Channels["user-registered"].Subscribe);
            Assert.Null(doc.Channels["user-registered"].Publish);
        }

        [Fact]
        public void AddEvent_OtherTransport_FallsBackToSubscribeChannel() {
            var doc = new AsyncApiDocument { Info = new Info("T", "1") };
            var other = new EventPublishChannelMetadata(null, EventTransports.Other);

            doc.AddEvent(TestSchemas.SimpleSchema(), other);

            Assert.NotNull(doc.Channels["user-registered"].Subscribe);
            Assert.Null(doc.Channels["user-registered"].Publish);
        }

        [Fact]
        public void AddEvent_RabbitMqChannel_AddsPublishAndAmqpBinding() {
            var doc = new AsyncApiDocument { Info = new Info("T", "1") };
            var rabbit = new EventPublishChannelMetadata(
                "user-registered",
                EventTransports.RabbitMq,
                new Dictionary<string, string?> { ["exchange"] = "users.x" });

            doc.AddEvent(TestSchemas.SimpleSchema(), rabbit);

            var channel = doc.Channels["user-registered"];
            Assert.NotNull(channel.Publish);
            Assert.NotNull(channel.Bindings);
            var bindings = Assert.IsType<ChannelBindings>(channel.Bindings);
            Assert.NotNull(bindings.Amqp);
        }
    }

    /// <summary>
    /// A type annotated with [Event] using a DataSchema URI rather than a
    /// DataVersion; used to verify discovery skips it.
    /// </summary>
    [Event("schema.uri.only", "https://example.com/schemas/schema-uri-only")]
    public class SchemaUriOnlyEvent {
        [EventProperty("value")]
        public string? Value { get; set; }
    }
}