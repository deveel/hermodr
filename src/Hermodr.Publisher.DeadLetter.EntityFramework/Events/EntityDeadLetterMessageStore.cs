//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Data;

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
        : base(context, logger)
    {
        _systemTime = systemTime ?? EventSystemTime.Instance;
    }

    protected new DeadLetterDbContext Context => (DeadLetterDbContext)base.Context;

    public Task SetReplayingAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Replaying;
        message.LastStatusAt = _systemTime.UtcNow;
        return SaveAsync(message, cancellationToken);
    }

    public Task SetReplayedAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Replayed;
        message.LastStatusAt = _systemTime.UtcNow;
        message.NextReplayAt = null;
        return SaveAsync(message, cancellationToken);
    }

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

    public Task SetFailedAsync(TMessage message, string errorMessage, CancellationToken cancellationToken = default)
    {
        message.Status = DeadLetterMessageStatus.Failed;
        message.ErrorMessage = errorMessage;
        message.LastStatusAt = _systemTime.UtcNow;
        return SaveAsync(message, cancellationToken);
    }

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
