using System.ComponentModel.DataAnnotations;

using Deveel.Data;

namespace Deveel.Events;

/// <summary>
/// A default entity validator for <typeparamref name="TMessage"/> outbox entities that
/// ensures the associated <see cref="IOutboxMessage.CloudEvent"/> is not <c>null</c>.
/// </summary>
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
    public async virtual IAsyncEnumerable<ValidationResult> ValidateAsync(OutboxMessageManager<TMessage> manager, TMessage message, CancellationToken cancellationToken = new CancellationToken())
    {
        var @event = message.CloudEvent;
        if (@event == null)
            yield return new ValidationResult("CloudEvent cannot be null.", new[] { nameof(IOutboxMessage.CloudEvent) });
        
        // Additional validation rules can be added here as needed.
    }
}