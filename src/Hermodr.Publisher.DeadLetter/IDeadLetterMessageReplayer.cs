//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Replays persisted dead-letter messages through an event publisher pipeline.
/// </summary>
public interface IDeadLetterMessageReplayer
{
    /// <summary>
    /// Replays a persisted dead-letter message through the publisher pipeline.
    /// </summary>
    /// <param name="message">The dead-letter message to replay.</param>
    /// <param name="cancellationToken">A token to cancel the replay operation.</param>
    /// <returns>A task that completes when the message has been replayed.</returns>
    Task ReplayAsync(IDeadLetterMessage message, CancellationToken cancellationToken = default);
}
