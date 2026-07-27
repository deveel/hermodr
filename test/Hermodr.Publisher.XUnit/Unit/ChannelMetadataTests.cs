//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr {
    /// <summary>
    /// Tests for <see cref="IEventPublishChannelMetadata"/>,
    /// <see cref="EventPublishChannelMetadata"/>, and
    /// <see cref="EventPublisher.GetChannelMetadata"/>.
    /// </summary>
    public class ChannelMetadataTests {
        [Fact]
        public void EventTransports_ConstantsAreStableStrings() {
            Assert.Equal("rabbitmq", EventTransports.RabbitMq);
            Assert.Equal("azureservicebus", EventTransports.AzureServiceBus);
            Assert.Equal("webhook", EventTransports.Webhook);
            Assert.Equal("http", EventTransports.Http);
            Assert.Equal("other", EventTransports.Other);
        }

        [Fact]
        public void Metadata_DefaultPropertiesEmpty() {
            var meta = new EventPublishChannelMetadata("ch", EventTransports.RabbitMq, null);
            Assert.Equal("ch", meta.ChannelName);
            Assert.Equal(EventTransports.RabbitMq, meta.Transport);
            Assert.NotNull(meta.Properties);
            Assert.Empty(meta.Properties);
        }

        [Fact]
        public void For_NullChannel_Throws() {
            Assert.Throws<ArgumentNullException>(() => EventPublishChannelMetadata.For(null!));
        }

        [Fact]
        public void For_AnonymousNonMetadataChannel_ReturnsOtherTransport() {
            var channel = new PlainChannel();
            var meta = EventPublishChannelMetadata.For(channel);
            Assert.Equal(EventTransports.Other, meta.Transport);
            Assert.Empty(meta.Properties);
        }

        [Fact]
        public void For_ChannelImplementingMetadata_ReturnsChannelMetadata() {
            var channel = new SelfDescribingChannel("orders", EventTransports.RabbitMq,
                new Dictionary<string, string?> { ["exchange"] = "orders.x" });
            var meta = EventPublishChannelMetadata.For(channel);
            Assert.Equal("orders", meta.ChannelName);
            Assert.Equal(EventTransports.RabbitMq, meta.Transport);
            Assert.Equal("orders.x", meta.Properties["exchange"]);
        }

        [Fact]
        public void For_OptionsChannel_ReadsMetadataFromOptions() {
            var channel = new OptionsBackedChannel(new OptionsBackedOptions {
                ChannelName = "orders",
                ExchangeName = "orders.x",
                RoutingKey = "#"
            });
            var meta = EventPublishChannelMetadata.For(channel);
            Assert.Equal("orders", meta.ChannelName);
            Assert.Equal(EventTransports.RabbitMq, meta.Transport);
            Assert.Equal("orders.x", meta.Properties["exchange"]);
            Assert.Equal("#", meta.Properties["routingKey"]);
        }

        // ── Test doubles ────────────────────────────────────────────────────

        private sealed class PlainChannel : IEventPublishChannel {
            public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null,
                CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class SelfDescribingChannel : IEventPublishChannel, IEventPublishChannelMetadata {
            private readonly EventPublishChannelMetadata _metadata;
            public SelfDescribingChannel(string? name, string transport,
                IReadOnlyDictionary<string, string?>? properties) {
                _metadata = new EventPublishChannelMetadata(name, transport, properties);
            }
            public string? ChannelName => _metadata.ChannelName;
            public string Transport => _metadata.Transport;
            public IReadOnlyDictionary<string, string?> Properties => _metadata.Properties;
            public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null,
                CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class OptionsBackedOptions : NamedChannelPublishOptions, IChannelMetadataSource {
            public string? ExchangeName { get; set; }
            public string? RoutingKey { get; set; }

            public IEventPublishChannelMetadata GetChannelMetadata() {
                var props = new Dictionary<string, string?> {
                    ["exchange"] = ExchangeName,
                    ["routingKey"] = RoutingKey
                };
                return new EventPublishChannelMetadata(ChannelName, EventTransports.RabbitMq, props);
            }
        }

        private sealed class OptionsBackedChannel : EventPublishChannel<OptionsBackedOptions> {
            public OptionsBackedChannel(OptionsBackedOptions options)
                : base(options, serviceProvider: null!) { }
            protected override Task PublishCoreAsync(CloudEvent @event, OptionsBackedOptions options,
                CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}