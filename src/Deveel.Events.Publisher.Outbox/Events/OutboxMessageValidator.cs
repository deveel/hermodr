using System.ComponentModel.DataAnnotations;

using Deveel.Data;

namespace Deveel.Events;

public class OutboxMessageValidator<TMessage> : IEntityValidator<TMessage, string>
    where TMessage : class, IOutboxMessage
{
    IAsyncEnumerable<ValidationResult> IEntityValidator<TMessage, string>.ValidateAsync(EntityManager<TMessage, string> manager, TMessage entity,
        CancellationToken cancellationToken)
    {
        return ValidateAsync((OutboxMessageManager<TMessage>)manager, entity, cancellationToken);
    }
    
    public async virtual IAsyncEnumerable<ValidationResult> ValidateAsync(OutboxMessageManager<TMessage> manager, TMessage message, CancellationToken cancellationToken = new CancellationToken())
    {
        var @event = message.CloudEvent;
        if (@event == null)
            yield return new ValidationResult("CloudEvent cannot be null.", new[] { nameof(IOutboxMessage.CloudEvent) });
        
        // Additional validation rules can be added here as needed.
    }
}