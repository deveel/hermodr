//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Hermodr;

using Xunit;

namespace Hermodr.Tool.Tests;

public class EntryChannelResolverTests
{
    private static string FixtureAssembly =>
        Path.Combine(AppContext.BaseDirectory, "Hermodr.AsyncApi.Exporter.Fixtures.dll");

    private static string FactoryTypeName =>
        typeof(Hermodr.AsyncApi.Exporter.Fixtures.ChannelMetadataFactory).AssemblyQualifiedName!;

    [Fact]
    public void Resolves_factory_method_and_returns_channels()
    {
        // Ensure the fixtures assembly is loaded into the current AppDomain
        // so Type.GetType(assemblyQualifiedName) can resolve it. The project
        // reference already loads it at test startup, but be explicit.
        Assembly.LoadFrom(FixtureAssembly);

        var entry = $"{FactoryTypeName}::GetChannels";
        var channels = EntryChannelResolver.Resolve(entry);

        Assert.Equal(2, channels.Count);
        Assert.Equal("order-placed", channels[0].ChannelName);
        Assert.Equal(EventTransports.RabbitMq, channels[0].Transport);
        Assert.Equal("order-confirmed", channels[1].ChannelName);
        Assert.Equal(EventTransports.Webhook, channels[1].Transport);
    }

    [Fact]
    public void Missing_separator_throws_argument_exception()
    {
        Assert.Throws<ArgumentException>(() => EntryChannelResolver.Resolve("NoSeparatorHere"));
    }

    [Fact]
    public void Unknown_type_throws_invalid_operation()
    {
        var entry = "Bogus.Type.That.Does.Not.Exist, BogusAssembly::GetChannels";
        Assert.Throws<InvalidOperationException>(() => EntryChannelResolver.Resolve(entry));
    }

    [Fact]
    public void Unknown_method_throws_invalid_operation()
    {
        Assembly.LoadFrom(FixtureAssembly);
        var entry = $"{FactoryTypeName}::NoSuchMethod";
        Assert.Throws<InvalidOperationException>(() => EntryChannelResolver.Resolve(entry));
    }

    [Fact]
    public void Method_returning_wrong_type_throws_invalid_operation()
    {
        Assembly.LoadFrom(FixtureAssembly);
        // object.ToString returns string, not IReadOnlyList<IEventPublishChannelMetadata>.
        var entry = $"{typeof(object).AssemblyQualifiedName}::ToString";
        Assert.Throws<InvalidOperationException>(() => EntryChannelResolver.Resolve(entry));
    }

    [Fact]
    public void Null_entry_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => EntryChannelResolver.Resolve(null!));
    }
}