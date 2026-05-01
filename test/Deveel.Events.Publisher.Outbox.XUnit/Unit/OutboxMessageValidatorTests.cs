//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel.Events.Fakes;

namespace Deveel.Events
{
    /// <summary>
    /// Unit tests for <see cref="OutboxMessageValidator{TMessage}"/>.
    /// Each test targets one of the documented validation rules.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Layer", "Domain")]
    [Trait("Feature", "Outbox")]
    public class OutboxMessageValidatorTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static OutboxMessageValidator<FakeOutboxMessage> CreateValidator()
            => new();

        private static OutboxMessageManager<FakeOutboxMessage> CreateManager()
            => new(new FakeOutboxMessageRepository());

        /// <summary>Builds a fully valid <see cref="CloudEvent"/>.</summary>
        private static CloudEvent ValidCloudEvent() => new()
        {
            Id     = Guid.NewGuid().ToString("N"),
            Source = new Uri("https://example.com"),
            Type   = "test.event",
        };

        /// <summary>Collects all validation results from the validator.</summary>
        private static async Task<List<System.ComponentModel.DataAnnotations.ValidationResult>> ValidateAsync(
            OutboxMessageValidator<FakeOutboxMessage> validator,
            FakeOutboxMessage message)
        {
            var manager = CreateManager();
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            await foreach (var r in validator.ValidateAsync(manager, message))
                results.Add(r);
            return results;
        }

        // ── Rule 1: Event must not be null ────────────────────────────────────

        [Fact]
        public async Task Validate_NullEvent_YieldsValidationError()
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(null!);   // deliberately violate non-nullable contract

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert
            Assert.Contains(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.Event)) &&
                r.ErrorMessage!.Contains("null", StringComparison.OrdinalIgnoreCase));
        }

        // ── Rule 2: CloudEvent.Source must not be null ───────────────────────
        // id, type, and specversion are enforced by the CloudNative SDK at assignment
        // time (ArgumentException thrown on empty string), so only Source needs a
        // runtime null-check in the validator.

        [Fact]
        public async Task Validate_NullEventSource_YieldsValidationError()
        {
            // Arrange
            var validator  = CreateValidator();
            var @event     = ValidCloudEvent();
            @event.Source  = null;
            var message    = new FakeOutboxMessage(@event);

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert
            Assert.Contains(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.Event)) &&
                r.ErrorMessage!.Contains("source", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Validate_ValidEventSource_NoSourceError()
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(ValidCloudEvent());

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert - no source validation error
            Assert.DoesNotContain(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.Event)) &&
                r.ErrorMessage!.Contains("source", StringComparison.OrdinalIgnoreCase));
        }

        // ── Rule 3: Status must be a defined enum value ───────────────────────

        [Fact]
        public async Task Validate_InvalidStatus_YieldsValidationError()
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(ValidCloudEvent())
            {
                Status = (OutboxMessageStatus)999
            };

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert
            Assert.Contains(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.Status)));
        }

        // ── Rule 4: RetryCount must be non-negative ───────────────────────────

        [Fact]
        public async Task Validate_NegativeRetryCount_YieldsValidationError()
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(ValidCloudEvent())
            {
                RetryCount = -1
            };

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert
            Assert.Contains(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.RetryCount)));
        }

        // ── Rule 5: ErrorMessage required when Status is Failed ───────────────

        [Fact]
        public async Task Validate_FailedStatusWithoutErrorMessage_YieldsValidationError()
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(ValidCloudEvent())
            {
                Status       = OutboxMessageStatus.Failed,
                ErrorMessage = null
            };

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert
            Assert.Contains(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.ErrorMessage)) &&
                r.ErrorMessage!.Contains("Failed", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Validate_FailedStatusWithErrorMessage_NoRelatedError()
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(ValidCloudEvent())
            {
                Status       = OutboxMessageStatus.Failed,
                ErrorMessage = "Transient network error."
            };

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert – no 'Failed'/'ErrorMessage' rule should fire
            Assert.DoesNotContain(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.ErrorMessage)) &&
                r.ErrorMessage!.Contains("Failed", StringComparison.OrdinalIgnoreCase));
        }

        // ── Rule 6: ErrorMessage required when RetryCount > 0 ────────────────

        [Fact]
        public async Task Validate_RetryCountPositiveWithoutErrorMessage_YieldsValidationError()
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(ValidCloudEvent())
            {
                RetryCount   = 2,
                ErrorMessage = null
            };

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert
            Assert.Contains(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.RetryCount)) &&
                r.MemberNames.Contains(nameof(IOutboxMessage.ErrorMessage)));
        }

        [Fact]
        public async Task Validate_RetryCountPositiveWithErrorMessage_NoRelatedError()
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(ValidCloudEvent())
            {
                RetryCount   = 1,
                ErrorMessage = "First attempt failed."
            };

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert – the retry-count/error-message rule must not fire
            Assert.DoesNotContain(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.RetryCount)) &&
                r.MemberNames.Contains(nameof(IOutboxMessage.ErrorMessage)));
        }

        // ── Rule 7: NextRetryAt must be null for terminal statuses ────────────

        [Theory]
        [InlineData(OutboxMessageStatus.Delivered)]
        [InlineData(OutboxMessageStatus.Failed)]
        public async Task Validate_NextRetryAtSetOnTerminalStatus_YieldsValidationError(OutboxMessageStatus terminalStatus)
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(ValidCloudEvent())
            {
                Status       = terminalStatus,
                ErrorMessage = terminalStatus == OutboxMessageStatus.Failed ? "error" : null,
                NextRetryAt  = DateTimeOffset.UtcNow.AddHours(1)
            };

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert
            Assert.Contains(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.NextRetryAt)) &&
                r.MemberNames.Contains(nameof(IOutboxMessage.Status)));
        }

        [Theory]
        [InlineData(OutboxMessageStatus.Pending)]
        [InlineData(OutboxMessageStatus.Sending)]
        public async Task Validate_NextRetryAtSetOnNonTerminalStatus_NoRelatedError(OutboxMessageStatus nonTerminal)
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(ValidCloudEvent())
            {
                Status      = nonTerminal,
                NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(30)
            };

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert – the NextRetryAt/Status rule must not fire
            Assert.DoesNotContain(results, r =>
                r.MemberNames.Contains(nameof(IOutboxMessage.NextRetryAt)) &&
                r.MemberNames.Contains(nameof(IOutboxMessage.Status)));
        }

        // ── Happy path: fully valid message ──────────────────────────────────

        [Fact]
        public async Task Validate_FullyValidMessage_YieldsNoErrors()
        {
            // Arrange
            var validator = CreateValidator();
            var message   = new FakeOutboxMessage(ValidCloudEvent());

            // Act
            var results = await ValidateAsync(validator, message);

            // Assert
            Assert.Empty(results);
        }
    }
}


