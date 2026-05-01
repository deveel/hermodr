//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Data;

namespace Deveel.Events;

/// <summary>
/// Provides persistence operations for <typeparamref name="TMessage"/> outbox entities,
/// extending the standard <see cref="IRepository{TEntity,TKey}"/> CRUD surface with
/// outbox-specific state-transition operations.
/// </summary>
/// <typeparam name="TMessage">
/// The outbox message entity type.  Must be a reference type and implement
/// <see cref="IOutboxMessage"/>.
/// </typeparam>
public interface IOutboxMessageRepository<TMessage> : IRepository<TMessage, string>
     where TMessage : class, IOutboxMessage
{
    Task<OutboxMessageStatus> GetStatusAsync(TMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Claims <paramref name="message"/> for in-flight delivery by updating its
    /// <see cref="IOutboxMessage.Status"/> to <see cref="OutboxMessageStatus.Sending"/>.
    /// </summary>
    /// <remarks>
    /// Call this before dispatching to transport channels so that concurrent relay
    /// instances do not attempt to deliver the same message simultaneously.
    /// </remarks>
    /// <param name="message">The message to claim.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetSendingAsync(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks <paramref name="message"/> as successfully delivered, updating its
    /// <see cref="IOutboxMessage.Status"/> to <see cref="OutboxMessageStatus.Delivered"/>.
    /// </summary>
    /// <param name="message">The message whose status is to be updated.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetDeliveredAsync(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a transient delivery failure and schedules the next retry attempt,
    /// updating <see cref="IOutboxMessage.Status"/> back to
    /// <see cref="OutboxMessageStatus.Pending"/> with an incremented
    /// <see cref="IOutboxMessage.RetryCount"/> and the supplied
    /// <paramref name="nextRetryAt"/> timestamp.
    /// </summary>
    /// <param name="message">The message that failed delivery.</param>
    /// <param name="errorMessage">
    /// A human-readable description of the failure reason stored in
    /// <see cref="IOutboxMessage.ErrorMessage"/>.
    /// </param>
    /// <param name="nextRetryAt">
    /// The UTC time at which the relay should next attempt delivery.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetRetryAsync(
        TMessage message,
        string errorMessage,
        DateTimeOffset nextRetryAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently marks <paramref name="message"/> as failed, updating its
    /// <see cref="IOutboxMessage.Status"/> to <see cref="OutboxMessageStatus.Failed"/>
    /// and recording the <paramref name="errorMessage"/> for diagnostic purposes.
    /// </summary>
    /// <param name="message">The message whose status is to be updated.</param>
    /// <param name="errorMessage">
    /// A human-readable description of the failure reason.  Stored in
    /// <see cref="IOutboxMessage.ErrorMessage"/>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetFailedAsync(TMessage message, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns messages eligible for relay: those with
    /// <see cref="OutboxMessageStatus.Pending"/> status whose
    /// <see cref="IOutboxMessage.NextRetryAt"/> is <c>null</c> or in the past,
    /// up to the optional <paramref name="limit"/>.
    /// </summary>
    /// <param name="limit">
    /// The maximum number of messages to return.  Pass <c>null</c> (the default)
    /// to return all eligible messages.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of eligible pending messages, or an empty list when none exist.
    /// </returns>
    Task<IReadOnlyList<TMessage>> GetPendingMessagesAsync(
        int? limit = null,
        CancellationToken cancellationToken = default);
}