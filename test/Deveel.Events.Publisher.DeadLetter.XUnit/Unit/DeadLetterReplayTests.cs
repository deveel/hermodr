//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events;

[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
[Trait("Feature", "DeadLetterReplay")]
public class DeadLetterReplayTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 01, 15, 12, 00, 00, TimeSpan.Zero);

    private static CloudEvent MakeEvent(string type = "test.event") => new()
    {
        Type = type,
        Source = new Uri("https://example.com"),
        Id = Guid.NewGuid().ToString("N"),
        Time = MutableSystemTime.UtcNowValue,
    };

    [Fact]
    public async Task WithReplay_UsesDefaultInMemoryStore()
    {
        MutableSystemTime.UtcNowValue = FixedNow;
        var services = new ServiceCollection().AddLogging();

        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .UseSystemTime<MutableSystemTime>()
            .AddChannel(new ThrowingChannel())
            .AddDeadLetter()
            .WithReplay();

        await using var provider = services.BuildServiceProvider();

        await Assert.ThrowsAsync<EventPublishChannelException>(() =>
            provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken));

        var store = provider.GetRequiredService<InMemoryDeadLetterMessageStore>();
        var stored = Assert.Single(await store.GetPendingMessagesAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(DeadLetterMessageStatus.Pending, stored.Status);
        Assert.Equal("test.event", stored.Event.Type);
    }

    [Fact]
    public async Task ReplayAsync_ReplaysStoredMessage_ThroughConfiguredPublisher()
    {
        MutableSystemTime.UtcNowValue = FixedNow;
        var replayed = new List<CloudEvent>();

        var services = new ServiceCollection().AddLogging();

        services.AddEventPublisher("transport", builder => builder
            .Configure(options =>
            {
                options.Source = new Uri("https://example.com/transport");
                options.ThrowOnErrors = true;
            })
            .UseSystemTime<MutableSystemTime>()
            .AddChannel(new CollectingChannel(replayed)));

        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .UseSystemTime<MutableSystemTime>()
            .AddChannel(new ThrowingChannel())
            .AddDeadLetter()
            .UseRepository<FakeDeadLetterMessage, FakeDeadLetterStore>(ServiceLifetime.Singleton)
            .WithFactory<FakeDeadLetterMessage, FakeDeadLetterMessageFactory>()
            .WithReplay(options => options.TransportPublisherName = "transport");

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();
        var registeredStore = Assert.IsType<FakeDeadLetterStore>(
            provider.GetRequiredService<IDeadLetterMessageStore>());

        await Assert.ThrowsAsync<EventPublishChannelException>(() =>
            publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken));
        var stored = Assert.Single(registeredStore.Messages);

        await provider.GetRequiredService<IDeadLetterMessageReplayer>()
            .ReplayAsync(stored, TestContext.Current.CancellationToken);

        Assert.Single(replayed);
        Assert.Equal(DeadLetterMessageStatus.Replayed, stored.Status);
    }

    [Fact]
    public async Task ReplayAsync_WhenReplayFails_DoesNotStoreDuplicateDeadLetter()
    {
        MutableSystemTime.UtcNowValue = FixedNow;
        var services = new ServiceCollection().AddLogging();

        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .UseSystemTime<MutableSystemTime>()
            .AddChannel(new ThrowingChannel())
            .AddDeadLetter()
            .UseRepository<FakeDeadLetterMessage, FakeDeadLetterStore>(ServiceLifetime.Singleton)
            .WithFactory<FakeDeadLetterMessage, FakeDeadLetterMessageFactory>()
            .WithReplay(options => options.RetryInterval = TimeSpan.FromSeconds(30));

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();
        var store = Assert.IsType<FakeDeadLetterStore>(
            provider.GetRequiredService<IDeadLetterMessageStore>());

        await Assert.ThrowsAsync<EventPublishChannelException>(() =>
            publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken));
        var stored = Assert.Single(store.Messages);

        await Assert.ThrowsAsync<EventPublishChannelException>(() =>
            provider.GetRequiredService<IDeadLetterMessageReplayer>()
                .ReplayAsync(stored, TestContext.Current.CancellationToken));

        Assert.Single(store.Messages);
        Assert.Equal(1, stored.ReplayCount);
        Assert.Equal(DeadLetterMessageStatus.Pending, stored.Status);
        Assert.Equal(FixedNow.AddSeconds(30), stored.NextReplayAt);
    }

    [Fact]
    public async Task ReplayProcessor_ReplaysPendingMessages()
    {
        MutableSystemTime.UtcNowValue = FixedNow;
        var replayed = new List<CloudEvent>();

        var services = new ServiceCollection().AddLogging();

        services.AddEventPublisher("transport", builder => builder
            .Configure(options =>
            {
                options.Source = new Uri("https://example.com/transport");
                options.ThrowOnErrors = true;
            })
            .UseSystemTime<MutableSystemTime>()
            .AddChannel(new CollectingChannel(replayed)));

        services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
                options.ThrowOnErrors = false;
            })
            .UseSystemTime<MutableSystemTime>()
            .AddChannel(new ThrowingChannel())
            .AddDeadLetter()
            .UseRepository<FakeDeadLetterMessage, FakeDeadLetterStore>(ServiceLifetime.Singleton)
            .WithFactory<FakeDeadLetterMessage, FakeDeadLetterMessageFactory>()
            .WithReplayWorker(options =>
            {
                options.TransportPublisherName = "transport";
                options.MaxBatchSize = 10;
            });

        await using var provider = services.BuildServiceProvider();
        var registeredStore = Assert.IsType<FakeDeadLetterStore>(
            provider.GetRequiredService<IDeadLetterMessageStore>());
        await Assert.ThrowsAsync<EventPublishChannelException>(() =>
            provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken));

        await provider.GetRequiredService<IDeadLetterReplayProcessor>()
            .ProcessPendingMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Single(replayed);
        Assert.Equal(DeadLetterMessageStatus.Replayed, registeredStore.Messages[0].Status);
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
                .Where(message => message.NextReplayAt is null || message.NextReplayAt <= MutableSystemTime.UtcNowValue);

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

    private sealed class ThrowingChannel : IEventPublishChannel
    {
        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("channel failure");
    }

    private sealed class CollectingChannel(List<CloudEvent> events) : IEventPublishChannel
    {
        private readonly List<CloudEvent> _events = events;

        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
        {
            _events.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableSystemTime : IEventSystemTime
    {
        public static DateTimeOffset UtcNowValue { get; set; }

        public DateTimeOffset UtcNow => UtcNowValue;
    }
}
