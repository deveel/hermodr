//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events;

[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
[Trait("Feature", "PublishError")]
public class PublishErrorHandlingTests
{
    private static CloudEvent MakeEvent() => new()
    {
        Type = "test.event",
        Source = new Uri("https://example.com"),
        Id = Guid.NewGuid().ToString("N"),
        Time = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Should_InvokePublishErrorHandler_When_ChannelFails()
    {
        var recorder = new PublishErrorRecorder();
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .UseErrorHandler(context => recorder.Entries.Add(context))
            .AddChannel(new ThrowingNamedChannel("primary"));

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<EventPublisher>()
            .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

        var captured = Assert.Single(recorder.Entries);
        Assert.Equal(EventPublishStage.ChannelPublish, captured.Stage);
        Assert.Equal("test.event", captured.Event?.Type);
        Assert.Equal(typeof(ThrowingNamedChannel), captured.ChannelType);
        Assert.Equal("primary", captured.ChannelName);
        Assert.IsType<InvalidOperationException>(captured.Exception);
    }

    [Fact]
    public async Task Should_InvokePublishErrorHandler_When_ConversionFails()
    {
        var recorder = new PublishErrorRecorder();
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .UseErrorHandler(context => recorder.Entries.Add(context))
            .AddChannel(new CapturingChannel());

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<EventPublisher>()
            .PublishAsync(new BrokenConvertible(), cancellationToken: TestContext.Current.CancellationToken);

        var captured = Assert.Single(recorder.Entries);
        Assert.Equal(EventPublishStage.EventConversion, captured.Stage);
        Assert.Null(captured.Event);
        Assert.Equal(typeof(BrokenConvertible), captured.DataType);
        Assert.IsType<InvalidOperationException>(captured.Exception);
    }

    [Fact]
    public async Task Should_ThrowEventPublishException_When_PublishErrorHandlerFails()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .UseErrorHandler(_ => throw new InvalidOperationException("handler failure"))
            .AddChannel(new ThrowingNamedChannel("primary"));

        await using var provider = services.BuildServiceProvider();

        var exception = await Assert.ThrowsAsync<EventPublishException>(() =>
            provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken));

        var aggregate = Assert.IsType<AggregateException>(exception.InnerException);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Contains(aggregate.InnerExceptions, ex => ex.Message == "channel failure");
        Assert.Contains(aggregate.InnerExceptions, ex => ex.Message == "handler failure");
    }

    [Fact]
    public async Task Should_IsolatePublishErrorHandlers_BetweenNamedPublishers()
    {
        var alpha = new PublishErrorRecorder();
        var beta = new PublishErrorRecorder();

        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher("alpha", builder => builder
            .Configure(options =>
            {
                options.Source = new Uri("https://example.com/alpha");
                options.ThrowOnErrors = false;
            })
            .UseErrorHandler(context => alpha.Entries.Add(context))
            .AddChannel(new ThrowingNamedChannel("alpha-channel")));

        services.AddEventPublisher("beta", builder => builder
            .Configure(options =>
            {
                options.Source = new Uri("https://example.com/beta");
                options.ThrowOnErrors = false;
            })
            .UseErrorHandler(context => beta.Entries.Add(context))
            .AddChannel(new ThrowingNamedChannel("beta-channel")));

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredKeyedService<IEventPublisher>("alpha")
            .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

        var captured = Assert.Single(alpha.Entries);
        Assert.Equal("alpha", captured.PublisherName);
        Assert.Equal("alpha-channel", captured.ChannelName);
        Assert.Empty(beta.Entries);
    }

    private sealed class PublishErrorRecorder
    {
        public List<EventPublishErrorContext> Entries { get; } = [];
    }

    private sealed class ThrowingNamedChannel(string? name) : INamedEventPublishChannel
    {
        public string? Name { get; } = name;

        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("channel failure");
    }

    private sealed class CapturingChannel : IEventPublishChannel
    {
        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class BrokenConvertible : IEventConvertible
    {
        public CloudEvent ToCloudEvent() => throw new InvalidOperationException("conversion failure");
    }
}
