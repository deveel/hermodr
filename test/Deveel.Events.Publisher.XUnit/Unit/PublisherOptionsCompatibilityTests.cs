//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    /// <summary>
    /// Verifies that <see cref="EventPublisher"/> correctly resolves per-call
    /// <see cref="EventPublishOptions"/> compatibility for each channel
    /// in the pipeline before forwarding the options.
    /// </summary>
    [Trait("Function", "Publisher")]
    [Trait("Concern", "OptionsCompatibility")]
    public class PublisherOptionsCompatibilityTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        /// <summary>A simple concrete options type used by <see cref="AlphaChannel"/>.</summary>
        private class AlphaOptions : EventPublishOptions
        {
            public string? Tag { get; init; }
        }

        /// <summary>A different concrete options type used by <see cref="BetaChannel"/>.</summary>
        private sealed class BetaOptions : EventPublishOptions
        {
            public int Priority { get; init; }
        }

        /// <summary>
        /// A channel built on <see cref="EventPublishChannel{TOptions}"/> that records
        /// the effective options it received for the last publish call.
        /// </summary>
        private sealed class AlphaChannel : EventPublishChannel<AlphaOptions>
        {
            public AlphaChannel(AlphaOptions defaults) : base(defaults) { }

            /// <summary>Options received by the last <see cref="PublishCoreAsync"/> invocation.</summary>
            public AlphaOptions? LastEffectiveOptions { get; private set; }

            protected override Task PublishCoreAsync(
                CloudEvent @event,
                AlphaOptions options,
                CancellationToken cancellationToken)
            {
                LastEffectiveOptions = options;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// A second channel with a different options type. Records the effective options
        /// it received so tests can assert them.
        /// </summary>
        private sealed class BetaChannel : EventPublishChannel<BetaOptions>
        {
            public BetaChannel(BetaOptions defaults) : base(defaults) { }

            public BetaOptions? LastEffectiveOptions { get; private set; }

            protected override Task PublishCoreAsync(
                CloudEvent @event,
                BetaOptions options,
                CancellationToken cancellationToken)
            {
                LastEffectiveOptions = options;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// A raw <see cref="IEventPublishChannel"/> implementation (no
        /// <see cref="EventPublishChannel{TOptions}"/>) that records what it receives.
        /// </summary>
        private sealed class RawChannel : IEventPublishChannel
        {
            public EventPublishOptions? LastOptions { get; private set; }

            public Task PublishAsync(
                CloudEvent @event,
                EventPublishOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                LastOptions = options;
                return Task.CompletedTask;
            }
        }

        private static CloudEvent MakeEvent() => new CloudEvent
        {
            Type = "test.event",
            Source = new Uri("https://api.example.com"),
            Id = Guid.NewGuid().ToString("N"),
        };

        private static EventPublisher BuildPublisher(params IEventPublishChannel[] channels)
        {
            var services = new ServiceCollection();
            var builder = services.AddEventPublisher(o =>
            {
                o.Source = new Uri("https://api.example.com");
                o.ThrowOnErrors = true;
            });

            foreach (var ch in channels)
                builder.AddChannel(ch);

            return services.BuildServiceProvider().GetRequiredService<EventPublisher>();
        }

        // ── tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task PublishEvent_Compatible_Options_ArePassed_ToChannel()
        {
            // Arrange
            var defaultOpts = new AlphaOptions { Tag = "default" };
            var channel = new AlphaChannel(defaultOpts);
            var publisher = BuildPublisher(channel);

            var perCallOpts = new AlphaOptions { Tag = "per-call" };

            // Act
            await publisher.PublishEventAsync(MakeEvent(), perCallOpts, TestContext.Current.CancellationToken);

            // Assert – channel should have received the per-call options merged with defaults;
            // because MergeOptions returns perCallOptions when non-null, Tag should be "per-call".
            Assert.NotNull(channel.LastEffectiveOptions);
            Assert.Equal("per-call", channel.LastEffectiveOptions.Tag);
        }

        [Fact]
        public async Task PublishEvent_Incompatible_Options_ChannelReceivesNull_FallsBackToDefaults()
        {
            // Arrange
            var defaultOpts = new AlphaOptions { Tag = "default-tag" };
            var channel = new AlphaChannel(defaultOpts);
            var publisher = BuildPublisher(channel);

            // BetaOptions is incompatible with AlphaChannel (which expects AlphaOptions)
            var incompatibleOpts = new BetaOptions { Priority = 99 };

            // Act
            await publisher.PublishEventAsync(MakeEvent(), incompatibleOpts, TestContext.Current.CancellationToken);

            // Assert – publisher passed null, so channel fell back to its defaults
            Assert.NotNull(channel.LastEffectiveOptions);
            Assert.Equal("default-tag", channel.LastEffectiveOptions.Tag);
        }

        [Fact]
        public async Task PublishEvent_TwoChannels_OnlyCompatibleChannelReceivesOptions()
        {
            // Arrange
            var alphaChannel = new AlphaChannel(new AlphaOptions { Tag = "alpha-default" });
            var betaChannel  = new BetaChannel(new BetaOptions { Priority = 0 });
            var publisher    = BuildPublisher(alphaChannel, betaChannel);

            var alphaOpts = new AlphaOptions { Tag = "per-call-alpha" };

            // Act
            await publisher.PublishEventAsync(MakeEvent(), alphaOpts, TestContext.Current.CancellationToken);

            // Assert – AlphaChannel gets the per-call options
            Assert.NotNull(alphaChannel.LastEffectiveOptions);
            Assert.Equal("per-call-alpha", alphaChannel.LastEffectiveOptions.Tag);

            // Assert – BetaChannel received null → used its defaults (Priority = 0)
            Assert.NotNull(betaChannel.LastEffectiveOptions);
            Assert.Equal(0, betaChannel.LastEffectiveOptions.Priority);
        }

        [Fact]
        public async Task PublishEvent_NullOptions_ChannelReceivesNull_FallsBackToDefaults()
        {
            // Arrange
            var defaultOpts = new AlphaOptions { Tag = "default-tag" };
            var channel = new AlphaChannel(defaultOpts);
            var publisher = BuildPublisher(channel);

            // Act – explicitly pass null
            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            // Assert – null forwarded; channel used its registered defaults
            Assert.NotNull(channel.LastEffectiveOptions);
            Assert.Equal("default-tag", channel.LastEffectiveOptions.Tag);
        }

        [Fact]
        public async Task PublishEvent_RawChannel_IncompatibleOptions_ChannelReceivesNull()
        {
            // Arrange – a channel that does NOT extend EventPublishChannel<TOptions>;
            // ResolveChannelOptions should return null for it regardless of what options are passed.
            var rawChannel = new RawChannel();
            var publisher  = BuildPublisher(rawChannel);

            // Act
            await publisher.PublishEventAsync(MakeEvent(), new AlphaOptions { Tag = "x" }, TestContext.Current.CancellationToken);

            // Assert – raw channel received null (publisher found no compatible base type)
            Assert.Null(rawChannel.LastOptions);
        }

        [Fact]
        public async Task PublishAsync_Data_IncompatibleOptions_ChannelFallsBackToDefaults()
        {
            // Arrange
            var defaultOpts = new AlphaOptions { Tag = "default" };
            var channel = new AlphaChannel(defaultOpts);
            var publisher = BuildPublisher(channel);

            var incompatibleOpts = new BetaOptions { Priority = 5 };

            // Act – publish via the data overload
            await publisher.PublishAsync(typeof(TestEventData), new TestEventData(), incompatibleOpts, TestContext.Current.CancellationToken);

            // Assert – channel used defaults because options were incompatible
            Assert.NotNull(channel.LastEffectiveOptions);
            Assert.Equal("default", channel.LastEffectiveOptions.Tag);
        }

        [Fact]
        public async Task PublishAsync_Generic_Data_CompatibleOptions_ArePassed()
        {
            // Arrange
            var channel = new AlphaChannel(new AlphaOptions { Tag = "default" });
            var publisher = BuildPublisher(channel);

            var perCallOpts = new AlphaOptions { Tag = "generic-call" };

            // Act
            await publisher.PublishAsync<TestEventData>(new TestEventData(), perCallOpts, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(channel.LastEffectiveOptions);
            Assert.Equal("generic-call", channel.LastEffectiveOptions.Tag);
        }

        [Fact]
        public async Task PublishEventAsync_Factory_IncompatibleOptions_ChannelFallsBackToDefaults()
        {
            // Arrange
            var defaultOpts = new AlphaOptions { Tag = "default" };
            var channel = new AlphaChannel(defaultOpts);
            var publisher = BuildPublisher(channel);

            var incompatibleOpts = new BetaOptions { Priority = 7 };

            // Act
            await publisher.PublishAsync(new TestConvertibleEvent(), incompatibleOpts, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(channel.LastEffectiveOptions);
            Assert.Equal("default", channel.LastEffectiveOptions.Tag);
        }

        // ── Typed-channel options routing tests ───────────────────────────────

        /// <summary>A typed options subclass keyed to a specific event type.</summary>
        private sealed class AlphaOptions<TEvent> : AlphaOptions where TEvent : class
        {
            // No extra properties — the generic type argument is the discriminator.
        }

        /// <summary>
        /// A channel that accepts only <typeparamref name="TEvent"/> events
        /// and is wired to <see cref="AlphaOptions"/>.
        /// </summary>
        private sealed class TypedAlphaChannel<TEvent> :
            EventPublishChannel<AlphaOptions>,
            IEventPublishChannel<TEvent>
            where TEvent : class
        {
            public TypedAlphaChannel(AlphaOptions defaults) : base(defaults) { }

            public AlphaOptions? LastEffectiveOptions { get; private set; }

            protected override Task PublishCoreAsync(
                CloudEvent @event,
                AlphaOptions options,
                CancellationToken cancellationToken)
            {
                LastEffectiveOptions = options;
                return Task.CompletedTask;
            }
        }

        private class TypedEventA { }
        private class TypedEventB { }

        [Fact]
        public async Task PublishEvent_GeneralOptions_NotForwardedToTypedChannel()
        {
            // Arrange — general channel + typed channel, both backed by AlphaOptions
            var generalChannel = new AlphaChannel(new AlphaOptions { Tag = "general-default" });
            var typedChannel   = new TypedAlphaChannel<TypedEventA>(new AlphaOptions { Tag = "typed-default" });
            var publisher      = BuildPublisher(generalChannel, typedChannel);

            // A non-generic (general) options override
            var generalOpts = new AlphaOptions { Tag = "general-override" };

            // Act
            await publisher.PublishEventAsync(MakeEvent(), generalOpts, TestContext.Current.CancellationToken);

            // Assert — general channel received the override
            Assert.NotNull(generalChannel.LastEffectiveOptions);
            Assert.Equal("general-override", generalChannel.LastEffectiveOptions.Tag);

            // Assert — typed channel must NOT have received the general override;
            // it should have used its own registered defaults instead.
            Assert.NotNull(typedChannel.LastEffectiveOptions);
            Assert.Equal("typed-default", typedChannel.LastEffectiveOptions.Tag);
        }

        [Fact]
        public async Task PublishEvent_TypedOptions_OnlyForwardedToMatchingTypedChannel()
        {
            // Arrange — one general channel + two typed channels for different event types
            var generalChannel  = new AlphaChannel(new AlphaOptions { Tag = "general-default" });
            var typedChannelA   = new TypedAlphaChannel<TypedEventA>(new AlphaOptions { Tag = "typed-a-default" });
            var typedChannelB   = new TypedAlphaChannel<TypedEventB>(new AlphaOptions { Tag = "typed-b-default" });
            var publisher       = BuildPublisher(generalChannel, typedChannelA, typedChannelB);

            // A typed override aimed at TypedEventA only
            var typedOptsA = new AlphaOptions<TypedEventA> { Tag = "typed-a-override" };

            // Act
            await publisher.PublishEventAsync(MakeEvent(), typedOptsA, TestContext.Current.CancellationToken);

            // Assert — TypedEventA channel received the typed override
            Assert.NotNull(typedChannelA.LastEffectiveOptions);
            Assert.Equal("typed-a-override", typedChannelA.LastEffectiveOptions.Tag);

            // Assert — TypedEventB channel must NOT have received the override
            Assert.NotNull(typedChannelB.LastEffectiveOptions);
            Assert.Equal("typed-b-default", typedChannelB.LastEffectiveOptions.Tag);

            // Assert — general channel must NOT have received a typed override
            Assert.NotNull(generalChannel.LastEffectiveOptions);
            Assert.Equal("general-default", generalChannel.LastEffectiveOptions.Tag);
        }

        [Fact]
        public async Task PublishEvent_CombinedOptions_GeneralAndTyped_EachChannelReceivesCorrectOverride()
        {
            // Arrange — general + two typed channels
            var generalChannel = new AlphaChannel(new AlphaOptions { Tag = "general-default" });
            var typedChannelA  = new TypedAlphaChannel<TypedEventA>(new AlphaOptions { Tag = "typed-a-default" });
            var typedChannelB  = new TypedAlphaChannel<TypedEventB>(new AlphaOptions { Tag = "typed-b-default" });
            var publisher      = BuildPublisher(generalChannel, typedChannelA, typedChannelB);

            var combined = new CombinedPublishOptions(
                new AlphaOptions           { Tag = "general-override" },
                new AlphaOptions<TypedEventA> { Tag = "typed-a-override" }
                // no entry for TypedEventB → it should fall back to its defaults
            );

            // Act
            await publisher.PublishEventAsync(MakeEvent(), combined, TestContext.Current.CancellationToken);

            // Assert — each channel received only what it should
            Assert.Equal("general-override",  generalChannel.LastEffectiveOptions!.Tag);
            Assert.Equal("typed-a-override",  typedChannelA.LastEffectiveOptions!.Tag);
            Assert.Equal("typed-b-default",   typedChannelB.LastEffectiveOptions!.Tag);
        }

        // ── CombinedPublishOptions tests ─────────────────────────────────

        [Fact]
        public async Task PublishEvent_CombinedOptions_EachChannelReceivesItsOwnOptions()
        {
            // Arrange – two channels with different option types
            var alphaChannel = new AlphaChannel(new AlphaOptions { Tag = "alpha-default" });
            var betaChannel  = new BetaChannel(new BetaOptions  { Priority = 0 });
            var publisher    = BuildPublisher(alphaChannel, betaChannel);

            var combined = new CombinedPublishOptions(
                new AlphaOptions { Tag = "alpha-per-call" },
                new BetaOptions  { Priority = 42 });

            // Act
            await publisher.PublishEventAsync(MakeEvent(), combined, TestContext.Current.CancellationToken);

            // Assert – AlphaChannel received its specific options
            Assert.NotNull(alphaChannel.LastEffectiveOptions);
            Assert.Equal("alpha-per-call", alphaChannel.LastEffectiveOptions.Tag);

            // Assert – BetaChannel received its specific options
            Assert.NotNull(betaChannel.LastEffectiveOptions);
            Assert.Equal(42, betaChannel.LastEffectiveOptions.Priority);
        }

        [Fact]
        public async Task PublishEvent_CombinedOptions_MissingEntry_ChannelFallsBackToDefaults()
        {
            // Arrange – only AlphaOptions in the combined bag; BetaChannel gets nothing
            var alphaChannel = new AlphaChannel(new AlphaOptions { Tag = "alpha-default" });
            var betaChannel  = new BetaChannel(new BetaOptions  { Priority = 99 });
            var publisher    = BuildPublisher(alphaChannel, betaChannel);

            var combined = new CombinedPublishOptions(
                new AlphaOptions { Tag = "alpha-per-call" });

            // Act
            await publisher.PublishEventAsync(MakeEvent(), combined, TestContext.Current.CancellationToken);

            // Assert – AlphaChannel received the per-call override
            Assert.NotNull(alphaChannel.LastEffectiveOptions);
            Assert.Equal("alpha-per-call", alphaChannel.LastEffectiveOptions.Tag);

            // Assert – BetaChannel got null → used defaults (Priority = 99)
            Assert.NotNull(betaChannel.LastEffectiveOptions);
            Assert.Equal(99, betaChannel.LastEffectiveOptions.Priority);
        }

        [Fact]
        public async Task PublishEvent_CombinedOptions_RawChannel_ReceivesNull()
        {
            // Arrange – raw channel not derived from EventPublishChannel<TOptions>
            var rawChannel = new RawChannel();
            var publisher  = BuildPublisher(rawChannel);

            var combined = new CombinedPublishOptions(
                new AlphaOptions { Tag = "alpha" },
                new BetaOptions  { Priority = 1 });

            // Act
            await publisher.PublishEventAsync(MakeEvent(), combined, TestContext.Current.CancellationToken);

            // Assert – raw channel always receives null regardless of combined options
            Assert.Null(rawChannel.LastOptions);
        }

        [Fact]
        public void CombinedPublishOptions_GetOptions_Generic_ReturnsCorrectEntry()
        {
            var alphaOpts = new AlphaOptions { Tag = "alpha" };
            var betaOpts  = new BetaOptions  { Priority = 7 };
            var combined  = new CombinedPublishOptions(alphaOpts, betaOpts);

            Assert.Same(alphaOpts, combined.GetOptions<AlphaOptions>());
            Assert.Same(betaOpts,  combined.GetOptions<BetaOptions>());
        }

        [Fact]
        public void CombinedPublishOptions_GetOptions_ByType_ReturnsCorrectEntry()
        {
            var alphaOpts = new AlphaOptions { Tag = "alpha" };
            var betaOpts  = new BetaOptions  { Priority = 3 };
            var combined  = new CombinedPublishOptions(alphaOpts, betaOpts);

            Assert.Same(alphaOpts, combined.GetOptions(typeof(AlphaOptions)));
            Assert.Same(betaOpts,  combined.GetOptions(typeof(BetaOptions)));
        }

        [Fact]
        public void CombinedPublishOptions_GetOptions_MissingType_ReturnsNull()
        {
            var combined = new CombinedPublishOptions(new AlphaOptions { Tag = "x" });

            Assert.Null(combined.GetOptions<BetaOptions>());
            Assert.Null(combined.GetOptions(typeof(BetaOptions)));
        }

        [Fact]
        public void CombinedPublishOptions_Options_ReflectsAllEntries()
        {
            var alphaOpts = new AlphaOptions { Tag = "a" };
            var betaOpts  = new BetaOptions  { Priority = 1 };
            var combined  = new CombinedPublishOptions(alphaOpts, betaOpts);

            Assert.Equal(2, combined.Options.Count);
            Assert.Contains(alphaOpts, combined.Options);
            Assert.Contains(betaOpts,  combined.Options);
        }

        // ── test event data / factory ──────────────────────────────────────────

        [Event("test.compatibility.event", "https://example.com/events/test/1.0")]
        private class TestEventData { }

        private class TestConvertibleEvent : IEventConvertible
        {
            public CloudEvent ToCloudEvent() => new CloudEvent
            {
                Type = "test.factory.event",
                Source = new Uri("https://api.example.com"),
                Id = Guid.NewGuid().ToString("N"),
            };
        }
    }
}

