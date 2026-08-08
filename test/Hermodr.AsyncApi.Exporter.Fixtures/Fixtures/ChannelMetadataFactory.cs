//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Hermodr;

namespace Hermodr.AsyncApi.Exporter.Fixtures;

/// <summary>
/// Static factory used by the <c>--entry</c> tests of the exporter tool.
/// The entry-point string passed to <see cref="EntryChannelResolver"/> is
/// <c>Hermodr.AsyncApi.Exporter.Fixtures.ChannelMetadataFactory, Hermodr.AsyncApi.Exporter.Fixtures::GetChannels</c>.
/// </summary>
public static class ChannelMetadataFactory
{
    /// <summary>
    /// Returns a deterministic list of channel metadata that mirrors the
    /// <c>channels.json</c> file shipped alongside this fixture.
    /// </summary>
    public static IReadOnlyList<IEventPublishChannelMetadata> GetChannels()
        => new[]
        {
            new EventPublishChannelMetadata(
                "order-placed",
                EventTransports.RabbitMq,
                new Dictionary<string, string?>
                {
                    ["exchange"] = "orders.x",
                    ["routingKey"] = "#"
                }),
            new EventPublishChannelMetadata(
                "order-confirmed",
                EventTransports.Webhook,
                new Dictionary<string, string?>
                {
                    ["url"] = "https://example.com/hook"
                })
        };
}