//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// Provides persistence operations for dead-letter messages.
/// </summary>
public interface IDeadLetterMessageStore
{
    Task AddAsync(IDeadLetterMessage message, CancellationToken cancellationToken = default);

    Task SetReplayingAsync(IDeadLetterMessage message, CancellationToken cancellationToken = default);

    Task SetReplayedAsync(IDeadLetterMessage message, CancellationToken cancellationToken = default);

    Task SetRetryAsync(
        IDeadLetterMessage message,
        string errorMessage,
        DateTimeOffset nextReplayAt,
        CancellationToken cancellationToken = default);

    Task SetFailedAsync(IDeadLetterMessage message, string errorMessage, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IDeadLetterMessage>> GetPendingMessagesAsync(int? limit = null, CancellationToken cancellationToken = default);
}
