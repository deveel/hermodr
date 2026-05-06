//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// Configuration options for dead-letter replay services.
/// </summary>
public sealed class DeadLetterReplayOptions
{
    /// <summary>
    /// Gets or sets how often the replay worker polls for pending messages.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of pending messages processed in a single cycle.
    /// </summary>
    public int MaxBatchSize { get; set; } = 0;

    /// <summary>
    /// Gets or sets the publisher pipeline name used for replay.
    /// When empty, the stored message publisher name is used.
    /// </summary>
    public string TransportPublisherName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts before a dead-letter message is marked failed.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay before the next replay attempt after a failure.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMinutes(1);
}
