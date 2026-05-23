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
    Task ReplayAsync(IDeadLetterMessage message, CancellationToken cancellationToken = default);
}
