// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Hermodr
{
    /// <summary>
    /// Implemented by <see cref="EventPublishOptions"/> subclasses that support
    /// targeting a specific named channel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set on channel-level options, <see cref="ChannelName"/> declares the
    /// channel's own logical name, which is exposed through
    /// <see cref="INamedEventPublishChannel.Name"/>.
    /// </para>
    /// <para>
    /// When set on per-call options passed to
    /// <see cref="EventPublisher.PublishEventAsync(CloudNative.CloudEvents.CloudEvent, EventPublishOptions, System.Threading.CancellationToken)"/> or
    /// <see cref="EventPublisher.PublishAsync(System.Type, object, EventPublishOptions, System.Threading.CancellationToken)"/>, it acts as a filter: only
    /// channels whose <see cref="INamedEventPublishChannel.Name"/> equals
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

