//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Marks a publish call as a dead-letter replay operation while forwarding any inner channel options.
/// </summary>
public sealed class DeadLetterReplayPublishOptions : EventPublishOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeadLetterReplayPublishOptions"/> class.
    /// </summary>
    public DeadLetterReplayPublishOptions(EventPublishOptions? innerOptions = null)
    {
        InnerOptions = innerOptions;
    }

    /// <summary>
    /// Gets the inner channel-specific options forwarded to the replayed publish call.
    /// </summary>
    public EventPublishOptions? InnerOptions { get; }

    /// <inheritdoc />
    public override EventPublishOptions? Unwrap() => InnerOptions?.Unwrap();
}
