//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Processes pending dead-letter messages for replay.
/// </summary>
public interface IDeadLetterReplayProcessor
{
    /// <summary>
    /// Processes all pending dead-letter messages that are eligible for replay.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the processing.</param>
    /// <returns>A task that completes when all eligible messages have been processed.</returns>
    Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default);
}
