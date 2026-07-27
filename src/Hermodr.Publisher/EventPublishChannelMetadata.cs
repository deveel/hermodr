//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.ObjectModel;

namespace Hermodr
{
    /// <summary>
    /// Default implementation of <see cref="IEventPublishChannelMetadata"/>.
    /// </summary>
    public sealed class EventPublishChannelMetadata : IEventPublishChannelMetadata
    {
        private static readonly IReadOnlyDictionary<string, string?> EmptyProperties =
            new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>(0, StringComparer.Ordinal));

        /// <summary>
        /// Creates a new metadata snapshot.
        /// </summary>
        /// <param name="channelName">The logical channel name, or <c>null</c>.</param>
        /// <param name="transport">The transport identifier (see <see cref="EventTransports"/>).</param>
        /// <param name="properties">
        /// The transport-specific addressing properties; pass <c>null</c> for an empty bag.
        /// </param>
        public EventPublishChannelMetadata(
            string? channelName,
            string transport,
            IReadOnlyDictionary<string, string?>? properties = null)
        {
            ArgumentNullException.ThrowIfNull(transport);
            ChannelName = channelName;
            Transport = transport;
            Properties = properties ?? EmptyProperties;
        }

        /// <inheritdoc/>
        public string? ChannelName { get; }

        /// <inheritdoc/>
        public string Transport { get; }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string?> Properties { get; }

        /// <summary>
        /// Builds the metadata snapshot for the given channel, unwrapping any
        /// <see cref="IEventPublishChannelDecorator"/> (including
        /// <see cref="NamedChannelDecorator"/>) and falling back to the channel's
        /// options when they implement <see cref="IChannelMetadataSource"/>.
        /// </summary>
        /// <param name="channel">The channel to introspect.</param>
        /// <returns>
        /// A metadata snapshot. When no metadata can be derived the snapshot
        /// carries <see cref="EventTransports.Other"/> and an empty
        /// <see cref="Properties"/> bag (exporters will skip it).
        /// </returns>
        public static IEventPublishChannelMetadata For(IEventPublishChannel channel)
        {
            ArgumentNullException.ThrowIfNull(channel);

            // Preserve the outer channel name (set by NamedChannelDecorator) so
            // exporters can still attribute the channel to its logical name even
            // when the inner channel is anonymous.
            string? outerName = (channel as INamedEventPublishChannel)?.Name;

            // Peel off decorators to reach the inner channel.
            var current = channel;
            while (current is IEventPublishChannelDecorator decorator)
                current = decorator.InnerChannel;

            // 1. The inner channel itself implements the metadata interface.
            if (current is IEventPublishChannelMetadata selfMetadata)
                return MergeName(selfMetadata, outerName);

            // 2. The inner channel is EventPublishChannel<TOptions> whose options
            //    implement IChannelMetadataSource.
            var metadataFromOptions = TryReadFromOptionsChannel(current);
            if (metadataFromOptions != null)
                return MergeName(metadataFromOptions, outerName);

            // 3. No metadata available: storage/deferral/recovery layer.
            return new EventPublishChannelMetadata(outerName, EventTransports.Other, null);
        }

        private static IEventPublishChannelMetadata? TryReadFromOptionsChannel(IEventPublishChannel channel)
        {
            // Walk the channel's base-type chain looking for
            // EventPublishChannel<TOptions> where TOptions : IChannelMetadataSource.
            for (var t = channel.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                if (!t.IsGenericType) continue;
                if (t.GetGenericTypeDefinition() != typeof(EventPublishChannel<>)) continue;

                var optionsType = t.GetGenericArguments()[0];
                if (!typeof(IChannelMetadataSource).IsAssignableFrom(optionsType)) continue;

                // Locate the protected DefaultOptions via reflection. This is a
                // tooling-time call (not the publish hot-path) so the reflection
                // cost is acceptable.
                var optionsProperty = t.GetProperty(
                    "DefaultOptions",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.GetProperty);
                if (optionsProperty == null) continue;

                var options = optionsProperty.GetValue(channel) as IChannelMetadataSource;
                if (options == null) continue;

                return options.GetChannelMetadata();
            }

            return null;
        }

        private static IEventPublishChannelMetadata MergeName(IEventPublishChannelMetadata metadata, string? outerName)
        {
            // If the channel already exposes a non-null name keep it; otherwise
            // promote the decorator's name (if any) so exporters can attribute
            // the channel correctly.
            if (!string.IsNullOrEmpty(metadata.ChannelName))
                return metadata;
            if (string.IsNullOrEmpty(outerName))
                return metadata;

            return new EventPublishChannelMetadata(outerName, metadata.Transport, metadata.Properties);
        }
    }
}