//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deveel.Events;

/// <summary>
/// An <see cref="IOutboxMessageRepository{TMessage}"/> backed by Entity Framework Core,
/// built on top of <see cref="EntityRepository{TEntity,TKey}"/> from
/// <c>Deveel.Repository.EntityFramework</c>.
/// </summary>
/// <typeparam name="TMessage">
/// The outbox message entity type.  Must be a reference type and implement
/// <see cref="IOutboxMessageEntity"/> so the repository can mutate its delivery state.
/// </typeparam>
/// <typeparam name="TContext">
/// The <see cref="DbContext"/> type that contains the <typeparamref name="TMessage"/>
/// entity set.
/// </typeparam>
/// <remarks>
/// <para>
/// Register this repository via
/// <see cref="OutboxChannelBuilderExtensions.WithEntityFrameworkRepository{TMessage,TContext}"/>
/// so that the DI container resolves the correct <see cref="DbContext"/> type:
/// </para>
/// <code language="csharp">
/// services.AddEventPublisher()
///     .AddOutbox&lt;MyMessage&gt;()
///     .WithEntityFrameworkRepository&lt;MyMessage, MyDbContext&gt;()
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
public class EntityOutboxMessageRepository<TMessage, TContext>
    : EntityRepository<TMessage, string>, IOutboxMessageRepository<TMessage>
    where TMessage : DbOutboxMessage
    where TContext : DbContext
{
    /// <summary>
    /// Initialises a new instance of
    /// <see cref="EntityOutboxMessageRepository{TMessage,TContext}"/>.
    /// </summary>
    /// <param name="context">
    /// The <typeparamref name="TContext"/> instance used to access the database.
    /// </param>
    /// <param name="loggerFactory">
    /// An optional <see cref="ILoggerFactory"/> used to create loggers both for the
    /// base <see cref="EntityRepository{TEntity,TKey}"/> and for this derived class.
    /// When <c>null</c>, <see cref="NullLogger"/> is used and no output is produced.
    /// In production the DI container will typically supply an <see cref="ILoggerFactory"/>
    /// automatically.
    /// </param>
    public EntityOutboxMessageRepository(TContext context, ILoggerFactory? loggerFactory = null)
        : base(context, CreateBaseLogger(loggerFactory))
    {
    }

    /// <summary>
    /// Gets the strongly-typed <typeparamref name="TContext"/> instance.
    /// </summary>
    protected new TContext Context => (TContext)base.Context;

    // ── Helpers ──────────────────────────────────────────────────────

    private static ILogger<EntityRepository<TMessage, string>> CreateBaseLogger(ILoggerFactory? factory)
        => factory?.CreateLogger<EntityRepository<TMessage, string>>()
           ?? NullLogger<EntityRepository<TMessage, string>>.Instance;

    // ── IOutboxMessageRepository<TMessage> ───────────────────────────

    public Task<OutboxMessageStatus> GetStatusAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(message.Status);
    }

    /// <inheritdoc/>
    public Task SetSendingAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Marking outbox message as {Status}", OutboxMessageStatus.Sending);

        message.Status = OutboxMessageStatus.Sending;
        message.LastStatusAt = SystemTime.Default.UtcNow;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetDeliveredAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Marking outbox message as {Status}", OutboxMessageStatus.Delivered);

        message.Status = OutboxMessageStatus.Delivered;
        message.LastStatusAt = SystemTime.Default.UtcNow;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetRetryAsync(
        TMessage message,
        string errorMessage,
        DateTimeOffset nextRetryAt,
        CancellationToken cancellationToken = default)
    {
        Logger.LogDebug(
            "Scheduling outbox message retry at {NextRetryAt} (attempt {RetryCount})",
            nextRetryAt,
            message.RetryCount + 1);

        message.Status = OutboxMessageStatus.Pending;
        message.ErrorMessage = errorMessage;
        message.RetryCount += 1;
        message.NextRetryAt = nextRetryAt;
        message.LastStatusAt = SystemTime.Default.UtcNow;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetFailedAsync(
        TMessage message,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Marking outbox message as {Status}: {ErrorMessage}",
            OutboxMessageStatus.Failed, errorMessage);

        message.Status = OutboxMessageStatus.Failed;
        message.ErrorMessage = errorMessage;
        message.LastStatusAt = SystemTime.Default.UtcNow;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TMessage>> GetPendingMessagesAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var query = Context.Set<TMessage>()
            .Where(m =>
                m.Status == OutboxMessageStatus.Pending &&
                (m.NextRetryAt == null || m.NextRetryAt <= now));

        if (limit.HasValue)
            query = query.Take(limit.Value);

        var result = await query.ToListAsync(cancellationToken);

        Logger.LogDebug("Retrieved {Count} pending outbox message(s)", result.Count);

        return result.AsReadOnly();
    }
}

