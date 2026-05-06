//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Data;

using System.Collections.Concurrent;

namespace Deveel.Events;

/// <summary>
/// A singleton in-memory dead-letter message store used as the default replay backing store.
/// </summary>
public sealed class InMemoryDeadLetterMessageStore : IDeadLetterMessageStore
{
    private readonly ConcurrentDictionary<string, DeadLetterMessage> _messages = new(StringComparer.Ordinal);

    public Task AddAsync(DeadLetterMessage entity, CancellationToken cancellationToken = default)
    {
        _messages[entity.Id] = entity;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetReplayingAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Replaying;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetReplayedAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Replayed;
        message.NextReplayAt = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetRetryAsync(
        DeadLetterMessage message,
        string errorMessage,
        DateTimeOffset nextReplayAt,
        CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Pending;
        message.ErrorMessage = errorMessage;
        message.ReplayCount += 1;
        message.NextReplayAt = nextReplayAt;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetFailedAsync(DeadLetterMessage message, string errorMessage, CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Failed;
        message.ErrorMessage = errorMessage;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeadLetterMessage>> GetPendingMessagesAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        IEnumerable<DeadLetterMessage> query = _messages.Values
            .Where(message => message.Status == DeadLetterMessageStatus.Pending)
            .Where(message => message.NextReplayAt is null || message.NextReplayAt <= now)
            .OrderBy(message => message.Event.Time ?? DateTimeOffset.MinValue);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return Task.FromResult((IReadOnlyList<DeadLetterMessage>)query.ToList());
    }

    Task IDeadLetterMessageStore.AddAsync(IDeadLetterMessage entity, CancellationToken cancellationToken)
        => AddAsync(GetTypedMessage(entity), cancellationToken);

    Task IDeadLetterMessageStore.SetReplayingAsync(IDeadLetterMessage message, CancellationToken cancellationToken)
        => SetReplayingAsync(GetTypedMessage(message), cancellationToken);

    Task IDeadLetterMessageStore.SetReplayedAsync(IDeadLetterMessage message, CancellationToken cancellationToken)
        => SetReplayedAsync(GetTypedMessage(message), cancellationToken);

    Task IDeadLetterMessageStore.SetRetryAsync(
        IDeadLetterMessage message,
        string errorMessage,
        DateTimeOffset nextReplayAt,
        CancellationToken cancellationToken)
        => SetRetryAsync(GetTypedMessage(message), errorMessage, nextReplayAt, cancellationToken);

    Task IDeadLetterMessageStore.SetFailedAsync(IDeadLetterMessage message, string errorMessage, CancellationToken cancellationToken)
        => SetFailedAsync(GetTypedMessage(message), errorMessage, cancellationToken);

    async Task<IReadOnlyList<IDeadLetterMessage>> IDeadLetterMessageStore.GetPendingMessagesAsync(int? limit, CancellationToken cancellationToken)
        => (await GetPendingMessagesAsync(limit, cancellationToken)).Cast<IDeadLetterMessage>().ToList();

    private static DeadLetterMessage GetTypedMessage(IDeadLetterMessage message)
    {
        if (message is DeadLetterMessage typed)
            return typed;

        throw new InvalidOperationException(
            $"The dead-letter message must be assignable to '{typeof(DeadLetterMessage).FullName}'.");
    }
}
