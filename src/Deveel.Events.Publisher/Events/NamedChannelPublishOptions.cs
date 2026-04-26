// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Deveel.Events
{
    /// <summary>
    /// A lightweight <see cref="EventPublishOptions"/> that carries only a
    /// <see cref="INamedChannelFilter.ChannelName"/> filter with no other
    /// channel-specific settings.
    /// </summary>
    /// <remarks>
    /// Use this when you want to target a specific named channel without providing
    /// channel-type-specific options. The convenience extension methods on
    /// <see cref="IEventPublisher"/> (e.g.
    /// <c>PublishAsync(event, channelName, cancellationToken)</c>) wrap their
    /// <paramref name="channelName"/> argument in this type internally.
    /// </remarks>
    public sealed class NamedChannelPublishOptions : EventPublishOptions, INamedChannelFilter
    {
        /// <summary>
        /// Initialises a new (empty) instance. Set <see cref="INamedChannelFilter.ChannelName"/>
        /// after construction, or use the
        /// <see cref="NamedChannelPublishOptions(string)"/> constructor.
        /// </summary>
        public NamedChannelPublishOptions()
        {
        }

        /// <summary>
        /// Initialises a new instance targeting the channel with the given
        /// <paramref name="channelName"/>.
        /// </summary>
        /// <param name="channelName">The logical name of the target channel.</param>
        public NamedChannelPublishOptions(string channelName)
        {
            ChannelName = channelName;
        }

        /// <inheritdoc/>
        public string? ChannelName { get; set; }
    }
}

