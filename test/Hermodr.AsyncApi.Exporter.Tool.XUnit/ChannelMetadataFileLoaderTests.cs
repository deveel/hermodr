//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hermodr;

using Xunit;

namespace Hermodr.Tool.Tests;

public class ChannelMetadataFileLoaderTests
{
    private static string WriteTempChannels(string json)
    {
        var path = Path.Join(Path.GetTempPath(), $"hermodr-channels-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public async Task Loads_multiple_channels_with_properties()
    {
        var path = WriteTempChannels("""
        {
            "channels": [
                { "name": "orders", "transport": "rabbitmq",
                  "properties": { "exchange": "orders.x", "routingKey": "#" } },
                { "name": "hooks", "transport": "webhook",
                  "properties": { "url": "https://example.com/hook" } }
            ]
        }
        """);

        try
        {
            var result = await ChannelMetadataFileLoader.LoadAsync(path, CancellationToken.None);

            Assert.Equal(2, result.Channels.Count);
            Assert.Equal("orders", result.Channels[0].ChannelName);
            Assert.Equal(EventTransports.RabbitMq, result.Channels[0].Transport);
            Assert.Equal("orders.x", result.Channels[0].Properties["exchange"]);
            Assert.Equal("#", result.Channels[0].Properties["routingKey"]);
            Assert.Equal("hooks", result.Channels[1].ChannelName);
            Assert.Equal(EventTransports.Webhook, result.Channels[1].Transport);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Missing_channels_property_returns_empty_list()
    {
        var path = WriteTempChannels("""{ "other": 1 }""");
        try
        {
            var result = await ChannelMetadataFileLoader.LoadAsync(path, CancellationToken.None);
            Assert.Empty(result.Channels);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Other_transport_is_skipped_with_a_warning()
    {
        var path = WriteTempChannels("""
        {
            "channels": [
                { "name": "audit", "transport": "other" },
                { "name": "ok", "transport": "rabbitmq" }
            ]
        }
        """);

        try
        {
            var result = await ChannelMetadataFileLoader.LoadAsync(path, CancellationToken.None);

            Assert.Single(result.Channels);
            Assert.Equal("ok", result.Channels[0].ChannelName);
            Assert.Single(result.Warnings);
            Assert.Contains("audit", result.Warnings[0]);
            Assert.Contains("skipping", result.Warnings[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Malformed_json_throws()
    {
        var path = WriteTempChannels("{ not json");
        try
        {
            // JsonDocument.ParseAsync throws JsonReaderException (a subclass
            // of JsonException) for low-level lexer errors; assert the base
            // type so the test is robust to the subclass.
            await Assert.ThrowsAnyAsync<JsonException>(() =>
                ChannelMetadataFileLoader.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Missing_file_throws()
    {
        var path = Path.Join(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json");
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            ChannelMetadataFileLoader.LoadAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task Null_property_values_are_preserved()
    {
        var path = WriteTempChannels("""
        {
            "channels": [
                { "name": "c", "transport": "rabbitmq",
                  "properties": { "key": null } }
            ]
        }
        """);

        try
        {
            var result = await ChannelMetadataFileLoader.LoadAsync(path, CancellationToken.None);
            Assert.Single(result.Channels);
            Assert.True(result.Channels[0].Properties.ContainsKey("key"));
            Assert.Null(result.Channels[0].Properties["key"]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}