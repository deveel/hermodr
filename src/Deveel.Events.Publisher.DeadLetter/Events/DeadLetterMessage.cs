//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// The default in-memory dead-letter message representation.
/// </summary>
public class DeadLetterMessage : IDeadLetterMessage
{
    /// <inheritdoc />
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <inheritdoc />
    public CloudEvent Event { get; set; } = new();

    /// <inheritdoc />
    public string PublisherName { get; set; } = String.Empty;

    /// <inheritdoc />
    public string? ChannelName { get; set; }

    /// <inheritdoc />
    public string? ChannelType { get; set; }

    /// <inheritdoc />
    public string? ErrorMessage { get; set; }

    /// <inheritdoc />
    public DeadLetterMessageStatus Status { get; set; } = DeadLetterMessageStatus.Pending;

    /// <inheritdoc />
    public int ReplayCount { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? NextReplayAt { get; set; }
}
