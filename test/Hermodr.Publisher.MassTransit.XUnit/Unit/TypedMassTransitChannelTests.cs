//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using MassTransit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hermodr;

[Trait("Channel", "MassTransit")]
[Trait("Function", "TypedChannel")]
public class TypedMassTransitChannelTests
{
    [Fact]
    public void AddMassTransit_Typed_RegistersTypedOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit(o => { })
                .AddMassTransit<OrderPlaced>(o => { });

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IConfigureOptions<MassTransitPublishOptions<OrderPlaced>>));
    }

    [Fact]
    public void AddMassTransit_Typed_RegistersChannelAsIEventPublishChannelOfT()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit(o => { })
                .AddMassTransit<OrderPlaced>(o => { });

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IEventPublishChannel<OrderPlaced>));
    }

    [Fact]
    public void AddMassTransit_Typed_ResolvesTypedOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit(o => { })
                .AddMassTransit<OrderPlaced>(o => { });

        var sp = services.BuildServiceProvider();
        var typedOptions = sp.GetRequiredService<IOptions<MassTransitPublishOptions<OrderPlaced>>>().Value;
        var baseOptions = sp.GetRequiredService<IOptions<MassTransitPublishOptions>>().Value;

        Assert.NotNull(typedOptions);
        Assert.NotNull(baseOptions);
    }

    [Fact]
    public void AddMassTransit_Typed_RegistersTypedChannelImplementation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit(o => { })
                .AddMassTransit<OrderPlaced>(o => { });

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IEventPublishChannel<OrderPlaced>) &&
            d.ServiceKey is not null);
    }

    [Fact]
    public void AddMassTransit_Named_RegistersNamedChannelDescriptor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit(o => { });

        Assert.Contains(services, d =>
            d.ImplementationType?.Name == "MassTransitPublishChannel");
    }

    [Fact]
    public void AddMassTransit_Named_RegistersChannelAsIEventPublishChannelKeyed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit(o => { });

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IEventPublishChannel) && d.ServiceKey is not null);
    }

    [Fact]
    public void AddMassTransit_Named_ThrowsOnNullBuilder()
    {
        EventPublisherBuilder? builder = null;
        Assert.ThrowsAny<Exception>(() => builder!.AddMassTransit(o => { }));
    }

    [Fact]
    public void AddMassTransit_Typed_ThrowsOnNullBuilder()
    {
        EventPublisherBuilder? builder = null;
        Assert.ThrowsAny<Exception>(() => builder!.AddMassTransit<OrderPlaced>(o => { }));
    }

    [Fact]
    public void AddMassTransit_WithNoConfigure_StillRegistersChannel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit();

        Assert.Contains(services, d =>
            d.ImplementationType?.Name == "MassTransitPublishChannel");
    }

    [Fact]
    public void AddMassTransit_Typed_WithNoConfigure_StillRegistersChannel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit<OrderPlaced>();

        Assert.Contains(services, d => d.ServiceType == typeof(IEventPublishChannel<OrderPlaced>));
    }

    [Fact]
    public void AddMassTransit_Typed_RegistersKeyedTypedChannel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit<OrderPlaced>();

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IEventPublishChannel<OrderPlaced>) &&
            d.ServiceKey?.Equals(builder.Name) == true);
    }

    [Fact]
    public void AddMassTransit_Typed_CanResolveTypedChannel_With_MassTransitDeps()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IPublishEndpoint>());
        services.AddSingleton(Substitute.For<ISendEndpointProvider>());

        var builder = services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit<OrderPlaced>();

        var sp = services.BuildServiceProvider();
        var channels = sp.GetKeyedServices<IEventPublishChannel<OrderPlaced>>(builder.Name).ToList();

        Assert.NotEmpty(channels);
    }

    [Fact]
    public void AddMassTransit_Named_RegistersAsIEventPublishChannel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddMassTransit(o => { });

        Assert.Contains(services, d => d.ServiceType == typeof(IEventPublishChannel));
    }

    private sealed class OrderPlaced
    {
        public string OrderId { get; set; } = string.Empty;
    }
}
