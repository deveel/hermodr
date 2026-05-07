// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Bogus;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events;

/// <summary>
/// Tests for <see cref="DefaultDeadLetterMessageFactory"/> via the full publisher pipeline.
/// Because <see cref="DeadLetterContext"/> has an internal constructor the factory is
/// exercised end-to-end: a failing channel triggers dead-letter storage and the resulting
/// <see cref="DeadLetterMessage"/> is inspected for correctness.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
[Trait("Feature", "DeadLetter")]
public class DefaultDeadLetterMessageFactoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly Faker Faker = new("en");

    private static CloudEvent MakeEvent(string? id = null) => new()
    {
        Type   = "order.created",
        Source = new Uri("https://example.com"),
        Id     = id ?? Faker.Random.Guid().ToString("N"),
        Time   = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// A named channel that always throws, so every publish becomes a dead letter.
    /// </summary>
    private sealed class FailingNamedChannel : INamedEventPublishChannel
    {
        public FailingNamedChannel(string name) => Name = name;
        public string? Name { get; }
        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulated channel failure");
    }

    /// <summary>
    /// Builds a service provider configured with a failing channel, the default
    /// dead-letter factory and <see cref="InMemoryDeadLetterMessageStore"/>.
    /// </summary>
    private static (IServiceProvider Provider, InMemoryDeadLetterMessageStore Store)
        BuildProvider(string channelName = "primary-channel")
    {
        var services = new ServiceCollection().AddLogging();

        services
            .AddEventPublisher(options =>
            {
                options.Source = new Uri("https://example.com/publisher");
            })
            .AddChannel(new FailingNamedChannel(channelName))
            .AddDeadLetter()
            .WithReplay();

        var provider = services.BuildServiceProvider();
        var store    = provider.GetRequiredService<InMemoryDeadLetterMessageStore>();
        return (provider, store);
    }

    // ── Create via pipeline ───────────────────────────────────────────────────
    //
    // WithReplay() forces ThrowOnErrors = true via PostConfigure so every test
    // must catch the resulting EventPublishException before inspecting the store.

    #region Create via pipeline

    [Fact]
    public async Task Should_StoreDeadLetterMessage_When_ChannelFails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (provider, store) = BuildProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        // Act – exception is expected because WithReplay() forces ThrowOnErrors = true
        await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken));
        var pending = await store.GetPendingMessagesAsync(cancellationToken: cancellationToken);

        // Assert
        Assert.Single(pending);
    }

    [Fact]
    public async Task Should_PreserveEventType_When_DeadLetterMessageIsCreated()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (provider, store) = BuildProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();
        var @event    = MakeEvent();

        // Act
        await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(@event, cancellationToken: cancellationToken));
        var stored = (await store.GetPendingMessagesAsync(cancellationToken: cancellationToken))[0];

        // Assert
        Assert.Equal("order.created", stored.Event.Type);
    }

    [Fact]
    public async Task Should_UseEventId_When_EventHasId()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (provider, store) = BuildProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();
        var eventId   = Faker.Random.Guid().ToString("N");
        var @event    = MakeEvent(id: eventId);

        // Act
        await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(@event, cancellationToken: cancellationToken));
        var stored = (await store.GetPendingMessagesAsync(cancellationToken: cancellationToken))[0];

        // Assert – the dead-letter message Id should match the original event Id
        Assert.Equal(eventId, stored.Id);
    }

    [Fact]
    public async Task Should_SetPendingStatus_When_DeadLetterMessageIsCreated()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (provider, store) = BuildProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        // Act
        await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken));
        var stored = (await store.GetPendingMessagesAsync(cancellationToken: cancellationToken))[0];

        // Assert
        Assert.Equal(DeadLetterMessageStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task Should_SetErrorMessage_When_DeadLetterMessageIsCreated()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (provider, store) = BuildProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        // Act
        await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken));
        var stored = (await store.GetPendingMessagesAsync(cancellationToken: cancellationToken))[0];

        // Assert – the factory should copy the exception message
        Assert.NotNull(stored.ErrorMessage);
        Assert.Contains("simulated channel failure", stored.ErrorMessage);
    }

    [Fact]
    public async Task Should_SetChannelName_When_DeadLetterMessageIsCreated()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var channelName       = "my-named-channel";
        var (provider, store) = BuildProvider(channelName: channelName);
        var publisher = provider.GetRequiredService<EventPublisher>();

        // Act
        await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken));
        var stored = (await store.GetPendingMessagesAsync(cancellationToken: cancellationToken))[0];

        // Assert
        Assert.Equal(channelName, stored.ChannelName);
    }

    [Fact]
    public async Task Should_SetChannelType_When_DeadLetterMessageIsCreated()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (provider, store) = BuildProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        // Act
        await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken));
        var stored = (await store.GetPendingMessagesAsync(cancellationToken: cancellationToken))[0];

        // Assert – the channel type should be recorded
        Assert.NotNull(stored.ChannelType);
        Assert.Contains(nameof(FailingNamedChannel), stored.ChannelType);
    }

    [Fact]
    public async Task Should_HaveZeroReplayCount_When_DeadLetterMessageIsFirstCreated()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (provider, store) = BuildProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        // Act
        await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken));
        var stored = (await store.GetPendingMessagesAsync(cancellationToken: cancellationToken))[0];

        // Assert
        Assert.Equal(0, stored.ReplayCount);
    }

    #endregion
}