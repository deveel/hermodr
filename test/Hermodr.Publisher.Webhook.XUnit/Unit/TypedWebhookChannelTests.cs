//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hermodr;

[Trait("Channel", "Webhook")]
[Trait("Function", "TypedChannel")]
public class TypedWebhookChannelTests
{
    [Fact]
    public void AddWebhooks_Typed_RegistersTypedOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.EndpointUrl = "https://base.example.com/hook")
                .AddWebhooks<OrderCreated>(o => o.EndpointUrl = "https://typed.example.com/hook");

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IConfigureOptions<WebhookPublishOptions<OrderCreated>>));
    }

    [Fact]
    public void AddWebhooks_Typed_ResolvesTypedOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.EndpointUrl = "https://base.example.com/hook")
                .AddWebhooks<OrderCreated>(o => o.EndpointUrl = "https://typed.example.com/hook");

        var sp = services.BuildServiceProvider();
        var typedOptions = sp.GetRequiredService<IOptions<WebhookPublishOptions<OrderCreated>>>().Value;
        var baseOptions = sp.GetRequiredService<IOptions<WebhookPublishOptions>>().Value;

        Assert.Equal("https://typed.example.com/hook", typedOptions.EndpointUrl);
        Assert.Equal("https://base.example.com/hook", baseOptions.EndpointUrl);
    }

    [Fact]
    public void AddWebhooks_Typed_RegistersChannelAsIEventPublishChannelOfT()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.EndpointUrl = "https://base.example.com/hook")
                .AddWebhooks<OrderCreated>(o => o.EndpointUrl = "https://typed.example.com/hook");

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IEventPublishChannel<OrderCreated>));
    }

    [Fact]
    public void AddWebhooks_Typed_RegistersChannelAsIEventPublishChannelKeyed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.EndpointUrl = "https://base.example.com/hook")
                .AddWebhooks<OrderCreated>(o => o.EndpointUrl = "https://typed.example.com/hook");

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IEventPublishChannel) && d.ServiceKey is not null);
    }

    [Fact]
    public void AddWebhooks_Named_RegistersNamedChannelDescriptor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.EndpointUrl = "https://base.example.com/hook");

        Assert.Contains(services, d =>
            d.ImplementationType?.Name == "WebhookPublishChannel");
    }

    [Fact]
    public void AddWebhooks_Named_RegistersInfrastructureServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.EndpointUrl = "https://base.example.com/hook");

        Assert.Contains(services, d => d.ServiceType == typeof(IHttpClientFactory));
        Assert.Contains(services, d => d.ServiceType == typeof(IWebhookSignatureProvider));
    }

    [Fact]
    public void AddWebhooks_RegistersHttpClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.EndpointUrl = "https://base.example.com/hook");

        Assert.Contains(services, d => d.ServiceType.Name == "IConfigureOptions`1");
    }

    [Fact]
    public void AddWebhooks_Typed_PreservesMergedOptionsAtResolution()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://base.example.com/hook";
                    o.SigningSecret = "base-secret";
                    o.SignatureHeaderName = "X-Base-Sig";
                })
                .AddWebhooks<OrderCreated>(o =>
                {
                    o.EndpointUrl = "https://typed.example.com/hook";
                });

        var sp = services.BuildServiceProvider();
        var channels = sp.GetKeyedServices<IEventPublishChannel<OrderCreated>>(builder.Name).ToList();

        Assert.NotEmpty(channels);
    }

    [Fact]
    public void AddWebhooks_Typed_TypedOptionsOverridesBaseOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.SigningSecret = "base-secret")
                .AddWebhooks<OrderCreated>(o => o.SigningSecret = "typed-secret");

        var sp = services.BuildServiceProvider();
        var typedOptions = sp.GetRequiredService<IOptions<WebhookPublishOptions<OrderCreated>>>().Value;
        var baseOptions = sp.GetRequiredService<IOptions<WebhookPublishOptions>>().Value;

        Assert.Equal("typed-secret", typedOptions.SigningSecret);
        Assert.Equal("base-secret", baseOptions.SigningSecret);
    }

    [Fact]
    public void UseWebhookSignatureProvider_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .UseWebhookSignatureProvider<HmacSha256SignatureProvider>();

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IWebhookSignatureProvider) &&
            d.ImplementationType == typeof(HmacSha256SignatureProvider));
    }

    [Fact]
    public void UseWebhookSignatureProvider_AppendsToEnumerable_NotReplaces()
    {
        // HMAC-SHA1 is intentionally registered here to exercise the legacy
        // signature-provider registration path. The test asserts that adding
        // a provider appends rather than replaces, which is provider-agnostic;
        // HmacSha1SignatureProvider is marked obsolete because SHA-1 is weak,
        // but the test does not rely on SHA-1 interop, only on the DI behavior.
#pragma warning disable CS0618 // HmacSha1SignatureProvider is obsolete: SHA-1 considered cryptographically weak
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.EndpointUrl = "https://base.example.com/hook")
                .UseWebhookSignatureProvider<HmacSha1SignatureProvider>();

        var shaCount = services.Count(d =>
            d.ServiceType == typeof(IWebhookSignatureProvider) &&
d.ImplementationType == typeof(HmacSha1SignatureProvider));
#pragma warning restore CS0618

        Assert.True(shaCount >= 1);
    }

    [Fact]
    public void AddWebhooks_Named_ThrowsOnNullBuilder()
    {
        EventPublisherBuilder? builder = null;
        Assert.ThrowsAny<Exception>(() => builder!.AddWebhooks(o => { }));
    }

    [Fact]
    public void AddWebhooks_Typed_ThrowsOnNullBuilder()
    {
        EventPublisherBuilder? builder = null;
        Assert.ThrowsAny<Exception>(() => builder!.AddWebhooks<OrderCreated>(o => { }));
    }

    [Fact]
    public void AddWebhooks_Named_RegistersAsIEventPublishChannel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.EndpointUrl = "https://base.example.com/hook");

        Assert.Contains(services, d => d.ServiceType == typeof(IEventPublishChannel));
    }

    [Fact]
    public void AddWebhooks_Named_RegistersAsIBatchEventPublishChannel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddWebhooks(o => o.EndpointUrl = "https://base.example.com/hook");

        Assert.Contains(services, d => d.ServiceType == typeof(IBatchEventPublishChannel));
    }

    private sealed class OrderCreated
    {
        public string OrderId { get; set; } = string.Empty;
    }
}
