using CloudNative.CloudEvents;

using Deveel.Events;

namespace OrderService.Infrastructure;

/// <summary>
/// Creates <see cref="DbOutboxMessage"/> instances from inbound <see cref="CloudEvent"/>
/// objects so they can be persisted to the SQLite outbox table.
/// </summary>
/// <remarks>
/// <see cref="DbOutboxMessage.PopulateFromCloudEvent"/> handles all standard
/// CloudEvents context attributes and the data payload; this factory is intentionally
/// thin — extend it if you need to encrypt the payload, add custom columns, etc.
/// </remarks>
public sealed class OrderOutboxMessageFactory : IOutboxMessageFactory<DbOutboxMessage>
{
    /// <inheritdoc/>
    public DbOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
    {
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(cloudEvent);
        return message;
    }
}

