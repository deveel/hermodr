//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Kista;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hermodr;

/// <summary>
/// An <see cref="IOutboxMessageStore{TMessage}"/> backed by Entity Framework Core,
/// built on top of <see cref="EntityRepository{TEntity,TKey}"/>.
/// </summary>
/// <typeparam name="TMessage">
    /// The outbox message entity type.  Must be a reference type that derives from
    /// <see cref="DbOutboxMessage"/> so the repository can mutate its delivery state.
    /// </typeparam>
    /// <remarks>
/// <para>
/// Register this repository via
/// <see cref="OutboxChannelBuilderExtensions.WithEntityFramework"/>
/// so that the DI container resolves the correct <see cref="DbContext"/> type:
/// </para>
/// <code language="csharp">
/// services.AddEventPublisher()
///     .AddOutbox&lt;MyMessage&gt;()
///     .WithEntityFramework()
///     .WithFactory&lt;MyMessageFactory&gt;()
///     .WithRelay();
/// </code>
/// <para>
/// The <c>Status</c>, <c>ErrorMessage</c>, <c>RetryCount</c> and <c>NextRetryAt</c>
/// columns must be mapped in the EF model.  The <c>CloudEvent</c> property is typically
/// stored as a serialised value in a separate column (use <c>[NotMapped]</c> on the
/// <c>CloudEvent</c> property and provide a value-object or owned-entity mapping for the
/// serialised form).
/// </para>
/// </remarks>
public class EntityOutboxMessageRepository<TMessage>
    : EntityRepository<TMessage, string>, IOutboxMessageStore<TMessage>
    where TMessage : DbOutboxMessage
{
    /// <summary>
    /// Initialises a new instance of
    /// <see cref="EntityOutboxMessageRepository{TMessage}"/>.
    /// </summary>
    /// <param name="context">
    /// The context instance used to access the database.
    /// </param>
    /// <param name="systemTime">
    /// An optional <see cref="ISystemTime"/> used to obtain the current UTC time when
    /// recording status-transition timestamps (<c>LastStatusAt</c>).
    /// When <c>null</c>, <see cref="SystemTime.Default"/> is used, which delegates to
    /// <see cref="DateTimeOffset.UtcNow"/>.
    /// Provide a test double in unit / integration tests to make timestamps deterministic.
    /// </param>
    /// <param name="logger">
    /// An optional logger used by the repository.  When <c>null</c>,
    /// <see cref="NullLogger"/> is used and no output is produced.
    /// </param>
    public EntityOutboxMessageRepository(OutboxDbContext context, ISystemTime? systemTime = null, ILogger<EntityOutboxMessageRepository<TMessage>>? logger = null)
        : base(context, null, logger)
    {
        _systemTime = systemTime ?? SystemTime.Default;
    }

    private readonly ISystemTime _systemTime;

    /// <summary>
    /// Gets the strongly-typed <see cref="OutboxDbContext"/> instance.
    /// </summary>
    protected new OutboxDbContext Context => (OutboxDbContext)base.Context;

    
    // ── IOutboxMessageStore<TMessage> ────────────────────────────

    async Task IOutboxMessageStore<TMessage>.AddAsync(TMessage message, CancellationToken cancellationToken)
    {
        await base.AddAsync(message, cancellationToken);
    }

    /// <summary>Gets the current delivery status of the supplied outbox message.</summary>
    /// <param name="message">The outbox message whose status should be retrieved.</param>
    /// <param name="cancellationToken">A token used to observe cancellation of the operation.</param>
    /// <returns>A <see cref="Task{OutboxMessageStatus}"/> result that completes with the message's current <see cref="OutboxMessageStatus"/>.</returns>
    public Task<OutboxMessageStatus> GetStatusAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(message.Status);
    }

    /// <inheritdoc/>
    public async Task SetSendingAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        Logger.LogMarkingOutboxMessageAsSending();

        message.Status = OutboxMessageStatus.Sending;
        message.LastStatusAt = _systemTime.UtcNow;
        await Context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SetDeliveredAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        Logger.LogMarkingOutboxMessageAsDelivered();

        message.Status = OutboxMessageStatus.Delivered;
        message.LastStatusAt = _systemTime.UtcNow;
        await Context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SetDeferredAsync(
        TMessage message,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken = default)
    {
        message.Status = OutboxMessageStatus.Pending;
        message.ErrorMessage = null;
        message.NextRetryAt = scheduledAt;
        message.LastStatusAt = _systemTime.UtcNow;
        await Context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SetRetryAsync(
        TMessage message,
        string errorMessage,
        DateTimeOffset nextRetryAt,
        CancellationToken cancellationToken = default)
    {
        Logger.LogSchedulingOutboxMessageRetry(nextRetryAt, message.RetryCount + 1);

        message.Status = OutboxMessageStatus.Pending;
        message.ErrorMessage = errorMessage;
        message.RetryCount += 1;
        message.NextRetryAt = nextRetryAt;
        message.LastStatusAt = _systemTime.UtcNow;
        await Context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SetFailedAsync(
        TMessage message,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        Logger.LogMarkingOutboxMessageAsFailed(errorMessage);

        message.Status = OutboxMessageStatus.Failed;
        message.ErrorMessage = errorMessage;
        message.LastStatusAt = _systemTime.UtcNow;
        await Context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TMessage>> GetPendingMessagesAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        // Phase 1: pull all Pending rows from the database.
        // Status is stored as an integer; the equality is trivially translatable
        // by every EF Core provider.
        var candidates = await Context.Set<TMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .ToListAsync(cancellationToken);

        // Phase 2: apply the time-based gate in CLR memory, then the optional limit.
        var now = _systemTime.UtcNow;
        IEnumerable<TMessage> eligible = candidates
            .Where(m => m.NextRetryAt is null || m.NextRetryAt <= now);

        if (limit.HasValue)
            eligible = eligible.Take(limit.Value);

        var result = eligible.ToList();

        Logger.LogRetrievedPendingOutboxMessages(result.Count);

        return result.AsReadOnly();
    }
}

