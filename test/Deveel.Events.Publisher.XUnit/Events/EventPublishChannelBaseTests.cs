//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.Options;

using System.ComponentModel.DataAnnotations;

namespace Deveel.Events
{
    /// <summary>
    /// Unit tests for <see cref="EventPublishChannelBase{TOptions}"/>.
    /// </summary>
    public class EventPublishChannelBaseTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static CloudEvent ValidEvent() => new()
        {
            Type   = "test.event",
            Source = new Uri("https://example.com"),
            Id     = "evt-base-001",
        };

        // Simple concrete options class with one [Required] property
        private class SimpleOptions : EventPublishChannelOptions
        {
            [Required]
            public string? Endpoint { get; set; }

            public string? Format { get; set; }
        }

        // Minimal concrete channel that records delivered events + effective options
        private class SimpleChannel : EventPublishChannelBase<SimpleOptions>
        {
            public SimpleChannel(
                SimpleOptions defaults,
                IEnumerable<IValidateOptions<SimpleOptions>>? validators = null)
                : base(defaults, validators) { }

            public List<(CloudEvent Event, SimpleOptions Options)> Published { get; } = new();

            protected override Task PublishCoreAsync(
                CloudEvent @event,
                SimpleOptions options,
                CancellationToken cancellationToken)
            {
                Published.Add((@event, options));
                return Task.CompletedTask;
            }
        }

        // ── Null event guard ─────────────────────────────────────────────────

        [Fact]
        public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
        {
            var channel = new SimpleChannel(new SimpleOptions { Endpoint = "https://example.com" });

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => channel.PublishAsync(null!, null, CancellationToken.None));
        }

        // ── MergeOptions: base implementation ───────────────────────────────

        [Fact]
        public async Task PublishAsync_NullPerCallOptions_UsesDefaults()
        {
            var defaults = new SimpleOptions { Endpoint = "https://default.example.com", Format = "json" };
            var channel  = new SimpleChannel(defaults);

            await channel.PublishAsync(ValidEvent(), null, CancellationToken.None);

            var (_, opts) = Assert.Single(channel.Published);
            Assert.Equal("https://default.example.com", opts.Endpoint);
            Assert.Equal("json",                         opts.Format);
        }

        [Fact]
        public async Task PublishAsync_NonNullPerCallOptions_OverridesDefaults()
        {
            var defaults   = new SimpleOptions { Endpoint = "https://default.example.com" };
            var perCall    = new SimpleOptions { Endpoint = "https://override.example.com" };
            var channel    = new SimpleChannel(defaults);

            await channel.PublishAsync(ValidEvent(), perCall, CancellationToken.None);

            var (_, opts) = Assert.Single(channel.Published);
            Assert.Equal("https://override.example.com", opts.Endpoint);
        }

        // ── ValidateOptions: DataAnnotations path ────────────────────────────

        [Fact]
        public async Task PublishAsync_InvalidOptions_MissingRequiredProperty_ThrowsValidationException()
        {
            // Endpoint is [Required] – supplying null should fail DataAnnotations
            var channel = new SimpleChannel(new SimpleOptions { Endpoint = null });

            var ex = await Assert.ThrowsAsync<ValidationException>(
                () => channel.PublishAsync(ValidEvent(), null, CancellationToken.None));

            Assert.NotNull(ex);
        }

        [Fact]
        public async Task PublishAsync_ValidOptions_DeliversEvent()
        {
            var channel = new SimpleChannel(new SimpleOptions { Endpoint = "https://ok.example.com" });

            await channel.PublishAsync(ValidEvent(), null, CancellationToken.None);

            Assert.Single(channel.Published);
        }

        // ── ValidateOptions: IValidateOptions<T> path ────────────────────────

        [Fact]
        public async Task PublishAsync_WithIValidateOptionsFailure_ThrowsOptionsValidationException()
        {
            var validator = new AlwaysFailValidator();
            var channel   = new SimpleChannel(
                new SimpleOptions { Endpoint = "https://example.com" },
                new[] { validator });

            await Assert.ThrowsAsync<OptionsValidationException>(
                () => channel.PublishAsync(ValidEvent(), null, CancellationToken.None));
        }

        [Fact]
        public async Task PublishAsync_WithIValidateOptionsSuccess_DeliversEvent()
        {
            var validator = new AlwaysPassValidator();
            var channel   = new SimpleChannel(
                new SimpleOptions { Endpoint = "https://example.com" },
                new[] { validator });

            await channel.PublishAsync(ValidEvent(), null, CancellationToken.None);

            Assert.Single(channel.Published);
        }

        [Fact]
        public async Task PublishAsync_MultipleValidators_OneFailure_ThrowsAndAccumulatesMessages()
        {
            var validators = new IValidateOptions<SimpleOptions>[]
            {
                new AlwaysPassValidator(),
                new AlwaysFailValidator("validation-error-1"),
                new AlwaysFailValidator("validation-error-2"),
            };
            var channel = new SimpleChannel(
                new SimpleOptions { Endpoint = "https://example.com" },
                validators);

            var ex = await Assert.ThrowsAsync<OptionsValidationException>(
                () => channel.PublishAsync(ValidEvent(), null, CancellationToken.None));

            Assert.Contains("validation-error-1", ex.Failures);
            Assert.Contains("validation-error-2", ex.Failures);
        }

        // ── DefaultOptions property ──────────────────────────────────────────

        [Fact]
        public void DefaultOptions_ExposedViaProtectedProperty()
        {
            var defaults = new SimpleOptions { Endpoint = "https://example.com" };
            var channel  = new InspectableChannel(defaults);

            Assert.Same(defaults, channel.ExposedDefaults);
        }

        // ── Cancellation ────────────────────────────────────────────────────

        [Fact]
        public async Task PublishAsync_CancelledToken_PropagatesCancellation()
        {
            using var cts     = new CancellationTokenSource();
            var published     = false;
            var slowChannel   = new SlowChannel(new SimpleOptions { Endpoint = "https://example.com" });

            cts.CancelAfter(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => slowChannel.PublishAsync(ValidEvent(), null, cts.Token));

            Assert.False(published);
        }

        // ── Helper implementations ───────────────────────────────────────────

        private sealed class AlwaysFailValidator : IValidateOptions<SimpleOptions>
        {
            private readonly string _failureMessage;
            public AlwaysFailValidator(string message = "always-fail") => _failureMessage = message;

            public ValidateOptionsResult Validate(string? name, SimpleOptions options)
                => ValidateOptionsResult.Fail(_failureMessage);
        }

        private sealed class AlwaysPassValidator : IValidateOptions<SimpleOptions>
        {
            public ValidateOptionsResult Validate(string? name, SimpleOptions options)
                => ValidateOptionsResult.Success;
        }

        private sealed class InspectableChannel : EventPublishChannelBase<SimpleOptions>
        {
            public InspectableChannel(SimpleOptions defaults) : base(defaults) { }
            public SimpleOptions ExposedDefaults => DefaultOptions;
            protected override Task PublishCoreAsync(CloudEvent @event, SimpleOptions options, CancellationToken ct)
                => Task.CompletedTask;
        }

        private sealed class SlowChannel : EventPublishChannelBase<SimpleOptions>
        {
            public SlowChannel(SimpleOptions defaults) : base(defaults) { }
            protected override async Task PublishCoreAsync(CloudEvent @event, SimpleOptions options, CancellationToken ct)
                => await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
    }
}

