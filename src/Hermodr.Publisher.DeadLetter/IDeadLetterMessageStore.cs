//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Provides persistence operations for dead-letter messages.
/// </summary>
public interface IDeadLetterMessageStore
{
    /// <summary>
    /// Adds a dead-letter message to the store.
    /// </summary>
    /// <param name="message">The dead-letter message to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the message has been stored.</returns>
    Task AddAsync(IDeadLetterMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a dead-letter message as currently being replayed.
    /// </summary>
    /// <param name="message">The dead-letter message being replayed.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the status has been updated.</returns>
    Task SetReplayingAsync(IDeadLetterMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a dead-letter message as successfully replayed.
    /// </summary>
    /// <param name="message">The dead-letter message that was replayed.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the status has been updated.</returns>
    Task SetReplayedAsync(IDeadLetterMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a dead-letter message for retry at a later time.
    /// </summary>
    /// <param name="message">The dead-letter message to retry.</param>
    /// <param name="errorMessage">A description of the failure that triggered the retry.</param>
    /// <param name="nextReplayAt">The UTC time at which the message becomes eligible for replay again.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the status has been updated.</returns>
    Task SetRetryAsync(
        IDeadLetterMessage message,
        string errorMessage,
        DateTimeOffset nextReplayAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a dead-letter message as permanently failed.
    /// </summary>
    /// <param name="message">The dead-letter message that failed.</param>
    /// <param name="errorMessage">A description of the final failure.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the status has been updated.</returns>
    Task SetFailedAsync(IDeadLetterMessage message, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pending dead-letter messages that are eligible for replay.
    /// </summary>
    /// <param name="limit">The maximum number of messages to retrieve, or <c>null</c> for no limit.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of pending dead-letter messages.</returns>
    Task<IReadOnlyList<IDeadLetterMessage>> GetPendingMessagesAsync(int? limit = null, CancellationToken cancellationToken = default);
}
