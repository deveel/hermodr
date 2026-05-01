using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

using Deveel.Data;

namespace Deveel.Events;

/// <summary>
/// A default entity validator for <typeparamref name="TMessage"/> outbox entities.
/// </summary>
/// <remarks>
/// The following rules are enforced:
/// <list type="bullet">
///   <item><description>The <see cref="IOutboxMessage.Event"/> must not be <c>null</c>.</description></item>
///   <item><description>When the event is present, the CloudEvents <c>source</c> attribute
///     must not be <c>null</c>.  Note: the CloudNative SDK already enforces that <c>id</c>,
///     <c>type</c>, and <c>specversion</c> are non-empty at assignment time, so those attributes
///     are not re-checked here.</description></item>
///   <item><description><see cref="IOutboxMessage.Status"/> must be a recognised
///     <see cref="OutboxMessageStatus"/> value.</description></item>
///   <item><description><see cref="IOutboxMessage.RetryCount"/> must be non-negative.</description></item>
///   <item><description>When <see cref="IOutboxMessage.Status"/> is
///     <see cref="OutboxMessageStatus.Failed"/>, an <see cref="IOutboxMessage.ErrorMessage"/>
///     must be provided.</description></item>
///   <item><description>When <see cref="IOutboxMessage.RetryCount"/> is greater than zero,
///     an <see cref="IOutboxMessage.ErrorMessage"/> must be provided.</description></item>
///   <item><description><see cref="IOutboxMessage.NextRetryAt"/> must be <c>null</c> when
///     <see cref="IOutboxMessage.Status"/> is <see cref="OutboxMessageStatus.Delivered"/> or
///     <see cref="OutboxMessageStatus.Failed"/>.</description></item>
/// </list>
/// </remarks>
/// <typeparam name="TMessage">
/// The outbox message entity type.  Must be a reference type and implement
/// <see cref="IOutboxMessage"/>.
/// </typeparam>
public class OutboxMessageValidator<TMessage> : IEntityValidator<TMessage, string>
    where TMessage : class, IOutboxMessage
{
    IAsyncEnumerable<ValidationResult> IEntityValidator<TMessage, string>.ValidateAsync(EntityManager<TMessage, string> manager, TMessage entity,
        CancellationToken cancellationToken)
    {
        return ValidateAsync((OutboxMessageManager<TMessage>)manager, entity, cancellationToken);
    }

    /// <summary>
    /// Validates <paramref name="message"/> against the outbox-specific rules.
    /// </summary>
    /// <param name="manager">The owning <see cref="OutboxMessageManager{TMessage}"/>.</param>
    /// <param name="message">The message entity to validate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// An async sequence of <see cref="ValidationResult"/> entries, one per violation.
    /// An empty sequence indicates the entity is valid.
    /// </returns>
    public virtual async IAsyncEnumerable<ValidationResult> ValidateAsync(
        OutboxMessageManager<TMessage> manager,
        TMessage message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ── 1. Event must not be null ────────────────────────────────────────
        // Defensive check: implementations may not honour the non-nullable contract.
#pragma warning disable CS8073 // Expression is always false according to nullable reference types' annotations
        var @event = message.Event;
        if (@event == null)
#pragma warning restore CS8073
        {
            yield return new ValidationResult(
                "The CloudEvent payload cannot be null.",
                new[] { nameof(IOutboxMessage.Event) });
        }
        else
        {
            // ── 2. CloudEvent.Source must not be null ─────────────────────────
            // The CloudNative SDK enforces non-empty id/type/specversion at set time,
            // so only Source (which the SDK permits to be null) needs a runtime check.
            if (@event.Source == null)
                yield return new ValidationResult(
                    "The CloudEvent 'source' attribute is required.",
                    new[] { nameof(IOutboxMessage.Event) });
        }

        // ── 3. Status must be a recognised enum value ────────────────────────
        if (!Enum.IsDefined(typeof(OutboxMessageStatus), message.Status))
            yield return new ValidationResult(
                $"The status value '{(int)message.Status}' is not a valid {nameof(OutboxMessageStatus)}.",
                new[] { nameof(IOutboxMessage.Status) });

        // ── 4. RetryCount must be non-negative ───────────────────────────────
        if (message.RetryCount < 0)
            yield return new ValidationResult(
                "The retry count must be a non-negative integer.",
                new[] { nameof(IOutboxMessage.RetryCount) });

        // ── 5. ErrorMessage required when Status is Failed ───────────────────
        if (message.Status == OutboxMessageStatus.Failed &&
            string.IsNullOrWhiteSpace(message.ErrorMessage))
            yield return new ValidationResult(
                "An error message must be provided when the outbox message status is 'Failed'.",
                new[] { nameof(IOutboxMessage.ErrorMessage) });

        // ── 6. ErrorMessage required when retries have occurred ──────────────
        if (message.RetryCount > 0 && string.IsNullOrWhiteSpace(message.ErrorMessage))
            yield return new ValidationResult(
                "An error message must be provided when the retry count is greater than zero.",
                new[] { nameof(IOutboxMessage.ErrorMessage), nameof(IOutboxMessage.RetryCount) });

        // ── 7. NextRetryAt must be null for terminal statuses ───────────────
        if (message.NextRetryAt.HasValue &&
            (message.Status == OutboxMessageStatus.Delivered || message.Status == OutboxMessageStatus.Failed))
            yield return new ValidationResult(
                $"The next-retry timestamp must be null when the outbox message status is '{message.Status}'.",
                new[] { nameof(IOutboxMessage.NextRetryAt), nameof(IOutboxMessage.Status) });
    }
}