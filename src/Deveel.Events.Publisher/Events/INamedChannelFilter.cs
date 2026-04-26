// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Deveel.Events
{
    /// <summary>
    /// Implemented by <see cref="EventPublishOptions"/> subclasses that support
    /// targeting a specific named channel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set on channel-level options, <see cref="ChannelName"/> declares the
    /// channel's own logical name, which is exposed through
    /// <see cref="IEventPublishChannel.Name"/>.
    /// </para>
    /// <para>
    /// When set on per-call options passed to
    /// <see cref="IEventPublisher.PublishEventAsync"/> or
    /// <see cref="IEventPublisher.PublishAsync"/>, it acts as a filter: only
    /// channels whose <see cref="IEventPublishChannel.Name"/> equals
    /// <see cref="ChannelName"/> (case-insensitive) will receive the event.
    /// </para>
    /// <para>
    /// <see cref="CombinedPublishOptions"/> intentionally does <strong>not</strong>
    /// implement this interface to avoid ambiguity. Name-based filtering for combined
    /// options is handled per bundled entry inside
    /// <c>EventPublisher.ResolveChannelOptions</c>.
    /// </para>
    /// </remarks>
    public interface INamedChannelFilter
    {
        /// <summary>
        /// Gets or sets the logical name of the target channel.
        /// </summary>
        string? ChannelName { get; set; }
    }
}

