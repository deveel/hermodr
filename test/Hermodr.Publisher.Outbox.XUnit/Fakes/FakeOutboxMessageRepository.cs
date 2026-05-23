using Deveel.Data;

namespace Hermodr.Fakes;

/// <summary>
/// An in-memory <see cref="IOutboxMessageRepository{TMessage}"/> that records
/// every outbox operation, suitable for unit testing.
/// </summary>
internal sealed class FakeOutboxMessageRepository : IOutboxMessageRepository<FakeOutboxMessage>
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

    // ── IRepository<FakeOutboxMessage, string> ───────────────────────

    public string GetEntityKey(FakeOutboxMessage entity) => entity.Id;

    public Task AddAsync(FakeOutboxMessage entity, CancellationToken cancellationToken = default)
    {
        _store.Add(entity);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(
        IEnumerable<FakeOutboxMessage> entities,
        CancellationToken cancellationToken = default)
    {
        _store.AddRange(entities);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(FakeOutboxMessage entity, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> RemoveAsync(FakeOutboxMessage entity, CancellationToken cancellationToken = default)
    {
        var removed = _store.Remove(entity);
        return Task.FromResult(removed);
    }

    public Task RemoveRangeAsync(
        IEnumerable<FakeOutboxMessage> entities,
        CancellationToken cancellationToken = default)
    {
        foreach (var e in entities)
            _store.Remove(e);
        return Task.CompletedTask;
    }

    public Task<FakeOutboxMessage?> FindAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.FirstOrDefault(m => m.Id == key));

    // ── IOutboxMessageRepository<FakeOutboxMessage> ──────────────────

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
