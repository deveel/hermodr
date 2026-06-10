using System.Collections;

namespace Hermodr.Fakes;

/// <summary>
/// An in-memory <see cref="IOutboxMessageStore{TMessage}"/> that records
/// every outbox operation, suitable for unit testing.
/// </summary>
internal sealed class FakeOutboxMessageRepository : IOutboxMessageStore<FakeOutboxMessage>
{
    private readonly List<FakeOutboxMessage> _store = new();

    /// <summary>Snapshot of every message that has been added.</summary>
    public IReadOnlyList<FakeOutboxMessage> Store => _store;

    /// <summary>
    /// Synchronously seeds the in-memory store with the given messages.
    /// Intended for test setup only.
    /// </summary>
    public void SeedAsync(params FakeOutboxMessage[] messages)
        => _store.AddRange(messages);

    // ── IOutboxMessageStore<FakeOutboxMessage> ─────────────────────

    public Task AddAsync(FakeOutboxMessage message, CancellationToken cancellationToken = default)
    {
        _store.Add(message);
        return Task.CompletedTask;
    }

    public Task<OutboxMessageStatus> GetStatusAsync(FakeOutboxMessage message, CancellationToken cancellationToken = default)
        => Task.FromResult(message.Status);

    public Task SetSendingAsync(FakeOutboxMessage message, CancellationToken cancellationToken = default)
    {
        message.Status = OutboxMessageStatus.Sending;
        return Task.CompletedTask;
    }

    public Task SetDeliveredAsync(FakeOutboxMessage message, CancellationToken cancellationToken = default)
    {
        message.Status = OutboxMessageStatus.Delivered;
        return Task.CompletedTask;
    }

    public Task SetDeferredAsync(
        FakeOutboxMessage message,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken = default)
    {
        message.Status = OutboxMessageStatus.Pending;
        message.ErrorMessage = null;
        message.NextRetryAt = scheduledAt;
        return Task.CompletedTask;
    }

    public Task SetRetryAsync(
        FakeOutboxMessage message,
        string errorMessage,
        DateTimeOffset nextRetryAt,
        CancellationToken cancellationToken = default)
    {
        message.Status       = OutboxMessageStatus.Pending;
        message.ErrorMessage = errorMessage;
        message.RetryCount  += 1;
        message.NextRetryAt  = nextRetryAt;
        return Task.CompletedTask;
    }

    public Task SetFailedAsync(
        FakeOutboxMessage message,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        message.Status       = OutboxMessageStatus.Failed;
        message.ErrorMessage = errorMessage;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FakeOutboxMessage>> GetPendingMessagesAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        IEnumerable<FakeOutboxMessage> query = _store
            .Where(m => m.Status == OutboxMessageStatus.Pending &&
                        (m.NextRetryAt is null || m.NextRetryAt <= now));

        if (limit.HasValue)
            query = query.Take(limit.Value);

        IReadOnlyList<FakeOutboxMessage> result = query.ToList();
        return Task.FromResult(result);
    }
}
