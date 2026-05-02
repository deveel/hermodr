using CloudNative.CloudEvents;

using Deveel.Events;

namespace OrderService.RelayWorker.Infrastructure;

/// <summary>
/// Creates <see cref="DbOutboxMessage"/> instances from inbound <see cref="CloudEvent"/>
/// objects — used by the outbox relay to persist re-routed messages (no-op path in this
/// worker since the relay reads but does not re-write to the outbox).
/// </summary>
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

