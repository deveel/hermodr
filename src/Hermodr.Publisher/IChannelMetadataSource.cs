//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Marker interface implemented by a channel-level options class that can
    /// supply a <see cref="IEventPublishChannelMetadata"/> snapshot for the
    /// channel configured with those options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementing this interface on an options class allows
    /// <see cref="EventPublishChannel{TOptions}"/> to expose channel metadata
    /// (transport identifier + addressing properties) without the channel
    /// implementation itself having to implement
    /// <see cref="IEventPublishChannelMetadata"/> directly.
    /// </para>
    /// <para>
    /// The metadata is read at tooling time (for example by
    /// <see cref="EventPublisher.GetChannelMetadata"/>); it has no effect on the
    /// publish hot-path.
    /// </para>
    /// </remarks>
    public interface IChannelMetadataSource
    {
        /// <summary>
        /// Builds the metadata snapshot for the channel configured with this
        /// options instance.
        /// </summary>
        /// <returns>
        /// A <see cref="IEventPublishChannelMetadata"/> snapshot. Implementations
        /// should return a metadata whose <see cref="IEventPublishChannelMetadata.Transport"/>
        /// is one of the <see cref="EventTransports"/> well-known constants
        /// (or a third-party string) and whose
        /// <see cref="IEventPublishChannelMetadata.Properties"/> bag is populated
        /// with the transport-specific addressing keys.
        /// </returns>
        IEventPublishChannelMetadata GetChannelMetadata();
    }
}