//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events;

[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
[Trait("Feature", "DeadLetter")]
public class DeadLetterHandlingTests
{
    private static CloudEvent MakeEvent() => new()
    {
        Type = "test.event",
        Source = new Uri("https://example.com"),
        Id = Guid.NewGuid().ToString("N"),
        Time = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Should_InvokeDeadLetterHandler_When_ChannelFails()
    {
        var recorder = new DeadLetterRecorder();
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .AddChannel(new ThrowingNamedChannel("primary"))
            .AddDeadLetter(deadLetter => deadLetter.UseHandler(context => recorder.Entries.Add(context)));

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<EventPublisher>()
            .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

        var captured = Assert.Single(recorder.Entries);
        Assert.Equal(String.Empty, captured.PublisherName);
        Assert.Equal("test.event", captured.Event.Type);
        Assert.Equal(typeof(ThrowingNamedChannel), captured.ChannelType);
        Assert.Equal("primary", captured.ChannelName);
        Assert.IsType<InvalidOperationException>(captured.Exception);
    }

    [Fact]
    public async Task Should_InvokeDeadLetterHandler_BeforeThrowing_When_ChannelFailsAndThrowOnErrorsIsTrue()
    {
        var recorder = new DeadLetterRecorder();
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = true;
            })
            .AddChannel(new ThrowingNamedChannel("primary"))
            .AddDeadLetter(deadLetter => deadLetter.UseHandler(context => recorder.Entries.Add(context)));

        await using var provider = services.BuildServiceProvider();

        await Assert.ThrowsAsync<EventPublishException>(() =>
            provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Single(recorder.Entries);
    }

    [Fact]
    public async Task Should_ThrowEventPublishException_When_DeadLetterHandlerFails()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .AddChannel(new ThrowingNamedChannel("primary"))
            .AddDeadLetter(deadLetter => deadLetter.UseHandler(_ => throw new InvalidOperationException("dead-letter failure")))
            ;

        await using var provider = services.BuildServiceProvider();

        var exception = await Assert.ThrowsAsync<EventPublishException>(() =>
            provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken));

        var aggregate = Assert.IsType<AggregateException>(exception.InnerException);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Contains(aggregate.InnerExceptions, ex => ex.Message == "channel failure");
        Assert.Contains(aggregate.InnerExceptions, ex => ex.Message == "dead-letter failure");
    }

    [Fact]
    public async Task Should_IsolateDeadLetterHandlers_BetweenNamedPublishers()
    {
        var alpha = new DeadLetterRecorder();
        var beta = new DeadLetterRecorder();

        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher("alpha", builder => builder
            .Configure(options =>
            {
                options.Source = new Uri("https://example.com/alpha");
                options.ThrowOnErrors = false;
            })
            .AddChannel(new ThrowingNamedChannel("alpha-channel"))
            .AddDeadLetter(deadLetter => deadLetter.UseHandler(context => alpha.Entries.Add(context)))
            );

        services.AddEventPublisher("beta", builder => builder
            .Configure(options =>
            {
                options.Source = new Uri("https://example.com/beta");
                options.ThrowOnErrors = false;
            })
            .AddChannel(new ThrowingNamedChannel("beta-channel"))
            .AddDeadLetter(deadLetter => deadLetter.UseHandler(context => beta.Entries.Add(context)))
            );

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredKeyedService<IEventPublisher>("alpha")
            .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

        var captured = Assert.Single(alpha.Entries);
        Assert.Equal("alpha", captured.PublisherName);
        Assert.Equal("alpha-channel", captured.ChannelName);
        Assert.Empty(beta.Entries);
    }

    [Fact]
    public async Task Should_NotInvokeDeadLetterHandler_When_ConversionFails()
    {
        var recorder = new DeadLetterRecorder();
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .AddChannel(new CapturingChannel())
            .AddDeadLetter(deadLetter => deadLetter.UseHandler(context => recorder.Entries.Add(context)))
            ;

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<EventPublisher>()
            .PublishAsync(new BrokenConvertible(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(recorder.Entries);
    }

    [Fact]
    public async Task Should_StoreDeadLetter_When_StorageConfigured_WithoutReplay()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .AddChannel(new ThrowingNamedChannel("primary"))
            .AddDeadLetter(deadLetter => deadLetter
                .UseRepository<FakeDeadLetterMessage, FakeDeadLetterStore>(ServiceLifetime.Singleton)
                .WithFactory<FakeDeadLetterMessage, FakeDeadLetterMessageFactory>())
            ;

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<EventPublisher>()
            .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

        var store = Assert.IsType<FakeDeadLetterStore>(
            provider.GetRequiredService<IDeadLetterMessageStore>());
        var stored = Assert.Single(store.Messages);
        Assert.Equal("test.event", stored.Event.Type);
        Assert.Null(provider.GetService<IDeadLetterMessageReplayer>());
    }

    private sealed class DeadLetterRecorder
    {
        public List<DeadLetterContext> Entries { get; } = [];
    }

    private sealed class FakeDeadLetterMessage : IDeadLetterMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public CloudEvent Event { get; set; } = MakeEvent();

        public string PublisherName { get; set; } = String.Empty;

        public string? ChannelName { get; set; }

        public string? ChannelType { get; set; }

        public string? ErrorMessage { get; set; }

        public DeadLetterMessageStatus Status { get; set; } = DeadLetterMessageStatus.Pending;

        public int ReplayCount { get; set; }

        public DateTimeOffset? NextReplayAt { get; set; }
    }

    private sealed class FakeDeadLetterMessageFactory : IDeadLetterMessageFactory<FakeDeadLetterMessage>
    {
        public FakeDeadLetterMessage Create(DeadLetterContext context)
        {
            return new FakeDeadLetterMessage
            {
                Id = context.Event.Id ?? Guid.NewGuid().ToString("N"),
                Event = context.Event,
                PublisherName = context.PublisherName,
                ChannelName = context.ChannelName,
                ChannelType = context.ChannelType?.FullName,
                ErrorMessage = context.Exception.Message,
                Status = DeadLetterMessageStatus.Pending
            };
        }
    }

    private sealed class FakeDeadLetterStore : IDeadLetterMessageStore
    {
        public List<FakeDeadLetterMessage> Messages { get; } = [];

        public Task AddAsync(FakeDeadLetterMessage entity, CancellationToken cancellationToken = default)
        {
            Messages.Add(entity);
            return Task.CompletedTask;
        }

        public Task SetReplayingAsync(FakeDeadLetterMessage message, CancellationToken cancellationToken = default)
        {
            message.Status = DeadLetterMessageStatus.Replaying;
            return Task.CompletedTask;
        }

        public Task SetReplayedAsync(FakeDeadLetterMessage message, CancellationToken cancellationToken = default)
        {
            message.Status = DeadLetterMessageStatus.Replayed;
            message.NextReplayAt = null;
            return Task.CompletedTask;
        }

        public Task SetRetryAsync(FakeDeadLetterMessage message, string errorMessage, DateTimeOffset nextReplayAt, CancellationToken cancellationToken = default)
        {
            message.Status = DeadLetterMessageStatus.Pending;
            message.ErrorMessage = errorMessage;
            message.ReplayCount += 1;
            message.NextReplayAt = nextReplayAt;
            return Task.CompletedTask;
        }

        public Task SetFailedAsync(FakeDeadLetterMessage message, string errorMessage, CancellationToken cancellationToken = default)
        {
            message.Status = DeadLetterMessageStatus.Failed;
            message.ErrorMessage = errorMessage;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FakeDeadLetterMessage>> GetPendingMessagesAsync(int? limit = null, CancellationToken cancellationToken = default)
        {
            IEnumerable<FakeDeadLetterMessage> pending = Messages
                .Where(message => message.Status == DeadLetterMessageStatus.Pending)
                .Where(message => message.NextReplayAt is null || message.NextReplayAt <= DateTimeOffset.UtcNow);

            if (limit.HasValue)
                pending = pending.Take(limit.Value);

            return Task.FromResult((IReadOnlyList<FakeDeadLetterMessage>)pending.ToList());
        }

        Task IDeadLetterMessageStore.AddAsync(IDeadLetterMessage entity, CancellationToken cancellationToken)
            => AddAsync(GetTypedMessage(entity), cancellationToken);

        Task IDeadLetterMessageStore.SetReplayingAsync(IDeadLetterMessage message, CancellationToken cancellationToken)
            => SetReplayingAsync(GetTypedMessage(message), cancellationToken);

        Task IDeadLetterMessageStore.SetReplayedAsync(IDeadLetterMessage message, CancellationToken cancellationToken)
            => SetReplayedAsync(GetTypedMessage(message), cancellationToken);

        Task IDeadLetterMessageStore.SetRetryAsync(
            IDeadLetterMessage message,
            string errorMessage,
            DateTimeOffset nextReplayAt,
            CancellationToken cancellationToken)
            => SetRetryAsync(GetTypedMessage(message), errorMessage, nextReplayAt, cancellationToken);

        Task IDeadLetterMessageStore.SetFailedAsync(IDeadLetterMessage message, string errorMessage, CancellationToken cancellationToken)
            => SetFailedAsync(GetTypedMessage(message), errorMessage, cancellationToken);

        async Task<IReadOnlyList<IDeadLetterMessage>> IDeadLetterMessageStore.GetPendingMessagesAsync(int? limit, CancellationToken cancellationToken)
            => (await GetPendingMessagesAsync(limit, cancellationToken)).Cast<IDeadLetterMessage>().ToList();

        private static FakeDeadLetterMessage GetTypedMessage(IDeadLetterMessage message)
        {
            if (message is FakeDeadLetterMessage typed)
                return typed;

            throw new InvalidOperationException(
                $"The dead-letter message must be assignable to '{typeof(FakeDeadLetterMessage).FullName}'.");
        }
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
