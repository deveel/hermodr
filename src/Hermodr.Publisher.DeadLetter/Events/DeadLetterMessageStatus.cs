//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Represents the replay lifecycle state of a stored dead-letter message.
/// </summary>
public enum DeadLetterMessageStatus
{
    /// <summary>
    /// The dead-letter message is pending replay.
    /// </summary>
    Pending,

    /// <summary>
    /// The dead-letter message is currently being replayed.
    /// </summary>
    Replaying,

    /// <summary>
    /// The dead-letter message was replayed successfully.
    /// </summary>
    Replayed,

    /// <summary>
    /// The dead-letter message could not be replayed and is no longer scheduled.
    /// </summary>
    Failed
}
