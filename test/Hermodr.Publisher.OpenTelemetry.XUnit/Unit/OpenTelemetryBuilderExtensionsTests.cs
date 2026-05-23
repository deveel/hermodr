//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr;

public class OpenTelemetryBuilderExtensionsTests
{
    private const string TraceParentKey = "traceparent";

    [Fact]
    public void AddOpenTelemetry_Throws_WhenBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ((EventPublisherBuilder)null!).AddOpenTelemetry());
    }

    [Fact]
    public void AddOpenTelemetryPublisherInstrumentation_Throws_WhenBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ((EventPublisherBuilder)null!).AddOpenTelemetryPublisherInstrumentation());
    }

    [Fact]
    public void AddOpenTelemetrySubscriptionInstrumentation_Throws_WhenBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ((EventPublisherBuilder)null!).AddOpenTelemetrySubscriptionInstrumentation());
    }

    [Fact]
    public async Task AddOpenTelemetry_RegistersBothMiddlewares()
    {
        var capturedActivities = new List<Activity>();
        var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
        using var source = new ActivitySource(sourceName);
        using var listener = CreateListener(source, capturedActivities);

        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(source);
        services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"))
            .AddOpenTelemetry(o => o.ActivitySourceName = sourceName)
            .AddSubscriptions(subs =>
            {
                subs.Subscribe("com.test.*", async (evt, ct) => await Task.CompletedTask);
            })
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        await publisher.PublishEventAsync(MakeEvent());

        var producerActivity = capturedActivities.Single(a => a.Kind == ActivityKind.Producer);
        var consumerActivity = capturedActivities.Single(a => a.Kind == ActivityKind.Consumer);

        Assert.Equal(producerActivity.TraceId, consumerActivity.TraceId);
    }

    [Fact]
    public async Task AddOpenTelemetryPublisherInstrumentation_DoesNotCreateConsumerSpan()
    {
        var capturedActivities = new List<Activity>();
        var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
        using var source = new ActivitySource(sourceName);
        using var listener = CreateListener(source, capturedActivities);

        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(source);
        services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"))
            .AddOpenTelemetryPublisherInstrumentation(o => o.ActivitySourceName = sourceName)
            .AddSubscriptions(subs =>
            {
                subs.Subscribe("com.test.*", async (evt, ct) => await Task.CompletedTask);
            })
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Single(capturedActivities);
        Assert.Equal(ActivityKind.Producer, capturedActivities[0].Kind);
    }

    [Fact]
    public async Task AddOpenTelemetrySubscriptionInstrumentation_DoesNotCreateProducerSpan()
    {
        var capturedActivities = new List<Activity>();
        var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
        using var source = new ActivitySource(sourceName);
        using var listener = CreateListener(source, capturedActivities);

        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(source);
        services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"))
            .AddOpenTelemetrySubscriptionInstrumentation(o => o.ActivitySourceName = sourceName)
            .AddSubscriptions(subs =>
            {
                subs.Subscribe("com.test.*", async (evt, ct) => await Task.CompletedTask);
            })
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Single(capturedActivities);
        Assert.Equal(ActivityKind.Consumer, capturedActivities[0].Kind);
    }

    [Fact]
    public async Task AddOpenTelemetry_UsesCustomActivitySourceName()
    {
        var customSourceName = $"Custom.Hermodr.{Guid.NewGuid():N}";
        var capturedActivities = new List<Activity>();
        using var source = new ActivitySource(customSourceName);
        using var listener = CreateListener(source, capturedActivities);

        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(source);
        services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"))
            .AddOpenTelemetry(o => o.ActivitySourceName = customSourceName)
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.NotEmpty(capturedActivities);
        Assert.All(capturedActivities, a => Assert.Equal(customSourceName, a.Source!.Name));
    }

    [Fact]
    public async Task AddOpenTelemetry_InjectsTrace_WhenFullInstrumentation()
    {
        var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
        using var source = new ActivitySource(sourceName);
        using var listener = CreateListener(source, new List<Activity>());

        var received = new List<CloudEvent>();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(source);
        services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"))
            .AddOpenTelemetry(o => o.ActivitySourceName = sourceName)
            .AddSubscriptions(subs =>
            {
                subs.Subscribe("com.test.*", async (evt, ct) => received.Add(evt));
            })
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Single(received);
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        Assert.NotNull(received[0][traceparentAttr]);
    }

    [Fact]
    public async Task AddOpenTelemetry_CanBeChainedWithOtherBuilderMethods()
    {
        var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
        using var source = new ActivitySource(sourceName);
        using var listener = CreateListener(source, new List<Activity>());

        var received = new List<CloudEvent>();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(source);
        var builder = services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"))
            .AddOpenTelemetry(o => o.ActivitySourceName = sourceName)
            .AddTestChannel(e => received.Add(e));

        Assert.NotNull(builder);

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Single(received);
    }

    [Fact]
    public void OpenTelemetryInstrumentationOptions_HasCorrectDefaults()
    {
        var options = new OpenTelemetryInstrumentationOptions();

        Assert.Equal("Hermodr", options.ActivitySourceName);
        Assert.True(options.InstrumentPublisher);
        Assert.True(options.InstrumentSubscription);
        Assert.True(options.RecordException);
        Assert.Null(options.EnrichWithEvent);
    }

    [Fact]
    public void OpenTelemetryInstrumentationOptions_CanBeConfigured()
    {
        var options = new OpenTelemetryInstrumentationOptions
        {
            ActivitySourceName = "custom",
            InstrumentPublisher = false,
            InstrumentSubscription = false,
            RecordException = false,
            EnrichWithEvent = (_, _) => { },
        };

        Assert.Equal("custom", options.ActivitySourceName);
        Assert.False(options.InstrumentPublisher);
        Assert.False(options.InstrumentSubscription);
        Assert.False(options.RecordException);
        Assert.NotNull(options.EnrichWithEvent);
    }

    private static ActivityListener CreateListener(ActivitySource source, List<Activity> captured)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a => captured.Add(a),
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static CloudEvent MakeEvent(string type = "com.test.event")
    {
        return new CloudEvent
        {
            Type = type,
            Source = new Uri("https://example.com"),
            Id = Guid.NewGuid().ToString("N"),
        };
    }
}
