//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel;
using Kista;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hermodr;

/// <summary>
/// An Entity Framework Core-backed dead-letter message store.
/// </summary>
/// <typeparam name="TMessage">The EF dead-letter entity type.</typeparam>
public class EntityDeadLetterMessageStore<TMessage>
    : EntityRepository<TMessage, string>, IDeadLetterMessageStore
    where TMessage : DbDeadLetterMessage
{
    private readonly IEventSystemTime _systemTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityDeadLetterMessageStore{TMessage}"/> class.
    /// </summary>
    public EntityDeadLetterMessageStore(
        DeadLetterDbContext context,
        IEventSystemTime? systemTime = null,
        ILogger<EntityDeadLetterMessageStore<TMessage>>? logger = null)
        : base(context, null, logger)
    {
        _systemTime = systemTime ?? EventSystemTime.Instance;
    }

    /// <summary>
    /// Gets the Entity Framework dead-letter context backing this store.
    /// </summary>
    protected new DeadLetterDbContext Context => (DeadLetterDbContext)base.Context;

    /// <summary>
    /// Marks the specified dead-letter message as currently being replayed and persists the change.
    /// </summary>
    /// <param name="message">The dead-letter message to update.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task SetReplayingAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Replaying;
        message.LastStatusAt = _systemTime.UtcNow;
        return SaveAsync(message, cancellationToken);
    }

    /// <summary>
    /// Marks the specified dead-letter message as successfully replayed, clears the next replay time, and persists the change.
    /// </summary>
    /// <param name="message">The dead-letter message to update.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task SetReplayedAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Replayed;
        message.LastStatusAt = _systemTime.UtcNow;
        message.NextReplayAt = null;
        return SaveAsync(message, cancellationToken);
    }

    /// <summary>
    /// Schedules a new replay attempt for the specified dead-letter message, recording the error message and next replay time, and persists the change.
    /// </summary>
    /// <param name="message">The dead-letter message to update.</param>
    /// <param name="errorMessage">The error message describing the failed replay attempt.</param>
    /// <param name="nextReplayAt">The UTC time at which the next replay should be attempted.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task SetRetryAsync(
        TMessage message,
        string errorMessage,
        DateTimeOffset nextReplayAt,
        CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Pending;
        message.ErrorMessage = errorMessage;
        message.ReplayCount += 1;
        message.NextReplayAt = nextReplayAt;
        message.LastStatusAt = _systemTime.UtcNow;
        return SaveAsync(message, cancellationToken);
    }

    /// <summary>
    /// Marks the specified dead-letter message as permanently failed, recording the error message, and persists the change.
    /// </summary>
    /// <param name="message">The dead-letter message to update.</param>
    /// <param name="errorMessage">The error message describing why the message failed.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task SetFailedAsync(TMessage message, string errorMessage, CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Failed;
        message.ErrorMessage = errorMessage;
        message.LastStatusAt = _systemTime.UtcNow;
        return SaveAsync(message, cancellationToken);
    }

    /// <summary>
    /// Returns the dead-letter messages that are pending replay and eligible to be attempted now.
    /// </summary>
    /// <param name="limit">The optional maximum number of messages to return.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous read operation. The result contains the read-only list of messages pending replay and eligible for replay.</returns>
    public async Task<IReadOnlyList<TMessage>> GetPendingMessagesAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = Context.Set<TMessage>()
            .Include(message => message.Attributes)
            .Where(message => message.Status == DeadLetterMessageStatus.Pending);

        var candidates = await query.ToListAsync(cancellationToken);
        var now = _systemTime.UtcNow;

        IEnumerable<TMessage> eligible = candidates
            .Where(message => message.NextReplayAt is null || message.NextReplayAt <= now)
            .OrderBy(message => message.CreatedAt);

        if (limit.HasValue)
            eligible = eligible.Take(limit.Value);

        return eligible.ToList().AsReadOnly();
    }

    async Task IDeadLetterMessageStore.AddAsync(IDeadLetterMessage entity, CancellationToken cancellationToken)
    {
        await AddAsync(GetTypedMessage(entity), cancellationToken);
    }

    async Task IDeadLetterMessageStore.SetReplayingAsync(IDeadLetterMessage message, CancellationToken cancellationToken)
    {
        await SetReplayingAsync(GetTypedMessage(message), cancellationToken);
    }

    async Task IDeadLetterMessageStore.SetReplayedAsync(IDeadLetterMessage message, CancellationToken cancellationToken)
    {
        await SetReplayedAsync(GetTypedMessage(message), cancellationToken);
    }

    async Task IDeadLetterMessageStore.SetRetryAsync(
        IDeadLetterMessage message,
        string errorMessage,
        DateTimeOffset nextReplayAt,
        CancellationToken cancellationToken)
    {
        await SetRetryAsync(GetTypedMessage(message), errorMessage, nextReplayAt, cancellationToken);
    }

    async Task IDeadLetterMessageStore.SetFailedAsync(IDeadLetterMessage message, string errorMessage, CancellationToken cancellationToken)
    {
        await SetFailedAsync(GetTypedMessage(message), errorMessage, cancellationToken);
    }

    async Task<IReadOnlyList<IDeadLetterMessage>> IDeadLetterMessageStore.GetPendingMessagesAsync(int? limit, CancellationToken cancellationToken)
        => (await GetPendingMessagesAsync(limit, cancellationToken)).Cast<IDeadLetterMessage>().ToList();

    private async Task SaveAsync(TMessage message, CancellationToken cancellationToken)
    {
        await UpdateAsync(message, cancellationToken);
    }

    private static TMessage GetTypedMessage(IDeadLetterMessage message)
    {
        if (message is TMessage typed)
            return typed;

        throw new InvalidOperationException(
            $"The dead-letter message must be assignable to '{typeof(TMessage).FullName}'.");
    }
}
