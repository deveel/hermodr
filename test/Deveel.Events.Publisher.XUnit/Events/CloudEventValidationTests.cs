//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Deveel.Events {
    /// <summary>
    /// Tests for the CloudEvents required-attribute guard that runs
    /// inside <see cref="EventPublisher.PublishEventAsync(CloudEvent,CancellationToken)"/>
    /// after enrichment and before channel dispatch.
    /// </summary>
    public class CloudEventValidationTests {
        private readonly EventPublisher _publisher;
        private readonly IList<CloudEvent> _published = new List<CloudEvent>();

        public CloudEventValidationTests(ITestOutputHelper outputHelper) {
            var services = new ServiceCollection()
                .AddLogging(l => l.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Debug));

            // Publisher with NO default source so that "source" cannot be auto-filled.
            services
                .AddEventPublisher(options => { /* no source */ })
                .AddTestChannel(e => _published.Add(e));

            _publisher = services.BuildServiceProvider()
                .GetRequiredService<EventPublisher>();
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static CloudEvent ValidEvent() => new CloudEvent {
            Type   = "test.event",
            Source = new Uri("https://example.com"),
            Id     = "evt-001",
        };

        // ── missing type ─────────────────────────────────────────────────────

        [Fact]
        public async Task PublishEvent_MissingType_ThrowsInvalidCloudEventException() {
            var @event = new CloudEvent {
                Source = new Uri("https://example.com"),
                Id     = "evt-001",
                // Type is intentionally omitted
            };

            var ex = await Assert.ThrowsAsync<InvalidCloudEventException>(
                () => _publisher.PublishEventAsync(@event, TestContext.Current.CancellationToken));

            Assert.Contains("type", ex.MissingAttributes);
            Assert.Empty(_published);
        }

        // ── missing source (and no source configured in options) ──────────────

        [Fact]
        public async Task PublishEvent_MissingSource_ThrowsInvalidCloudEventException() {
            var @event = new CloudEvent {
                Type = "test.event",
                Id   = "evt-001",
                // Source is intentionally omitted and not configured in options
            };

            var ex = await Assert.ThrowsAsync<InvalidCloudEventException>(
                () => _publisher.PublishEventAsync(@event, TestContext.Current.CancellationToken));

            Assert.Contains("source", ex.MissingAttributes);
            Assert.Empty(_published);
        }

        // ── missing id (auto-generation is disabled artificially via test publisher) ──

        /// <summary>
        /// We use a custom publisher sub-class that overrides <c>SetEventId</c>
        /// to return the event unchanged, simulating a scenario where the SDK or
        /// a custom generator fails to produce an id.
        /// </summary>
        [Fact]
        public async Task PublishEvent_MissingId_ThrowsInvalidCloudEventException() {
            var published = new List<CloudEvent>();
            var services  = new ServiceCollection()
                .AddLogging(_ => { });

            services
                .AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddTestChannel(e => published.Add(e));

            // We cannot easily disable id generation via options, so we exercise
            // ValidateCloudEvent directly through a subclass override.
            var provider  = services.BuildServiceProvider();
            var publisher = new NoIdPublisher(provider);

            var @event = new CloudEvent {
                Type   = "test.event",
                Source = new Uri("https://example.com"),
                // Id deliberately omitted — NoIdPublisher will not fill it in
            };

            var ex = await Assert.ThrowsAsync<InvalidCloudEventException>(
                () => publisher.PublishEventAsync(@event, TestContext.Current.CancellationToken));

            Assert.Contains("id", ex.MissingAttributes);
            Assert.Empty(published);
        }

        // ── multiple attributes missing ───────────────────────────────────────

        [Fact]
        public async Task PublishEvent_MultipleAttributesMissing_ReportsAll() {
            // Both type and source missing; an empty CloudEvent.
            var @event = new CloudEvent();

            var ex = await Assert.ThrowsAsync<InvalidCloudEventException>(
                () => _publisher.PublishEventAsync(@event, TestContext.Current.CancellationToken));

            Assert.Contains("type",   ex.MissingAttributes);
            Assert.Contains("source", ex.MissingAttributes);
            Assert.Empty(_published);
        }

        // ── valid event passes validation ─────────────────────────────────────

        [Fact]
        public async Task PublishEvent_AllRequiredAttributesPresent_Succeeds() {
            var @event = ValidEvent();

            await _publisher.PublishEventAsync(@event, TestContext.Current.CancellationToken);

            Assert.Single(_published);
            Assert.Equal("test.event", _published[0].Type);
        }

        // ── source can come from options instead of the event itself ──────────

        [Fact]
        public async Task PublishEvent_SourceFromOptions_PassesValidation() {
            // Build a publisher that HAS a default source configured.
            var published = new List<CloudEvent>();
            var services  = new ServiceCollection().AddLogging(_ => { });
            services
                .AddEventPublisher(o => o.Source = new Uri("https://options.example.com"))
                .AddTestChannel(e => published.Add(e));

            var optionalPublisher = services.BuildServiceProvider()
                .GetRequiredService<EventPublisher>();

            var @event = new CloudEvent {
                Type = "test.event",
                // Source intentionally omitted – should be filled from options
            };

            await optionalPublisher.PublishEventAsync(@event, TestContext.Current.CancellationToken);

            Assert.Single(published);
            Assert.Equal(new Uri("https://options.example.com"), published[0].Source);
        }

        // ── InvalidCloudEventException shape ─────────────────────────────────

        [Fact]
        public void InvalidCloudEventException_MessageContainsMissingAttributes() {
            var ex = new InvalidCloudEventException(new[] { "type", "source" });

            Assert.Contains("type",   ex.Message);
            Assert.Contains("source", ex.Message);
            Assert.Equal(2, ex.MissingAttributes.Count);
        }

        [Fact]
        public void InvalidCloudEventException_CustomMessage_IsUsed() {
            var ex = new InvalidCloudEventException("custom message", new[] { "id" });

            Assert.Equal("custom message", ex.Message);
            Assert.Contains("id", ex.MissingAttributes);
        }

        // ── helper sub-class that skips id generation ─────────────────────────

        private sealed class NoIdPublisher : EventPublisher {
            public NoIdPublisher(IServiceProvider provider)
                : base(
                    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EventPublisherOptions>>(),
                    provider.GetRequiredService<IEnumerable<IEventPublishChannel>>()) { }

            // Override to skip setting the id so the validator sees a null id.
            protected override CloudEvent SetEventId(CloudEvent @event) => @event;
        }
    }
}

