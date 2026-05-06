//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Represents a persisted dead-letter message that can be replayed later.
/// </summary>
public interface IDeadLetterMessage
{
    /// <summary>
    /// Gets the unique identifier of the stored dead-letter message.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the event payload to replay.
    /// </summary>
    CloudEvent Event { get; }

    /// <summary>
    /// Gets the publisher pipeline name associated with the original failed delivery.
    /// </summary>
    string PublisherName { get; }

    /// <summary>
    /// Gets the logical channel name associated with the failure, when available.
    /// </summary>
    string? ChannelName { get; }

    /// <summary>
    /// Gets the channel type name associated with the failure, when available.
    /// </summary>
    string? ChannelType { get; }

    /// <summary>
    /// Gets the last error message recorded for this dead-letter message.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Gets the replay status of this dead-letter message.
    /// </summary>
    DeadLetterMessageStatus Status { get; }

    /// <summary>
    /// Gets the number of replay attempts that have been made.
    /// </summary>
    int ReplayCount { get; }

    /// <summary>
    /// Gets the UTC time at which this dead-letter message becomes eligible for replay.
    /// </summary>
    DateTimeOffset? NextReplayAt { get; }
}
