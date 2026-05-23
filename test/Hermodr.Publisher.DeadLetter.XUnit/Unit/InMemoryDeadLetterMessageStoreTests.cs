// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Bogus;

using CloudNative.CloudEvents;

namespace Hermodr;

/// <summary>
/// Unit tests for <see cref="InMemoryDeadLetterMessageStore"/>, verifying all
/// state-transition helpers and the pending-message query path.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "DeadLetter")]
public class InMemoryDeadLetterMessageStoreTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static readonly Faker Faker = new("en");

    private static CloudEvent MakeEvent(string? type = null) => new()
    {
        Type   = type ?? Faker.Hacker.Verb() + ".event",
        Source = new Uri("https://example.com"),
        Id     = Faker.Random.Guid().ToString("N"),
        Time   = DateTimeOffset.UtcNow,
    };

    private static DeadLetterMessage MakeMessage(
        DeadLetterMessageStatus status = DeadLetterMessageStatus.Pending,
        DateTimeOffset? nextReplayAt = null) => new()
    {
        Id           = Faker.Random.Guid().ToString("N"),
        Event        = MakeEvent(),
        PublisherName = string.Empty,
        Status       = status,
        NextReplayAt = nextReplayAt,
    };

    // ── AddAsync ──────────────────────────────────────────────────────────────

    #region AddAsync

    [Fact]
    public async Task Should_StoreMessage_When_AddAsyncIsCalled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store   = new InMemoryDeadLetterMessageStore();
        var message = MakeMessage();

        // Act
        await store.AddAsync(message, cancellationToken);
        var pending = await store.GetPendingMessagesAsync(cancellationToken: cancellationToken);

        // Assert
        Assert.Single(pending);
        Assert.Equal(message.Id, pending[0].Id);
    }

    [Fact]
    public async Task Should_ReplaceExistingMessage_When_AddAsyncIsCalledWithSameId()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store   = new InMemoryDeadLetterMessageStore();
        var message = MakeMessage();

        // Act – add twice with the same Id
        await store.AddAsync(message, cancellationToken);
        await store.AddAsync(message, cancellationToken);
        var pending = await store.GetPendingMessagesAsync(cancellationToken: cancellationToken);

        // Assert – the store uses Id as the key, so only one entry survives
        Assert.Single(pending);
    }

    #endregion

    // ── SetReplayingAsync ─────────────────────────────────────────────────────

    #region SetReplayingAsync

    [Fact]
    public async Task Should_SetReplayingStatus_When_SetReplayingAsyncIsCalled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store   = new InMemoryDeadLetterMessageStore();
        var message = MakeMessage(DeadLetterMessageStatus.Pending);
        await store.AddAsync(message, cancellationToken);

        // Act
        await store.SetReplayingAsync(message, cancellationToken);

        // Assert
        Assert.Equal(DeadLetterMessageStatus.Replaying, message.Status);
    }

    [Fact]
    public async Task Should_SetReplayingStatus_Via_InterfaceMethod()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        IDeadLetterMessageStore store = new InMemoryDeadLetterMessageStore();
        var message = MakeMessage(DeadLetterMessageStatus.Pending);
        await store.AddAsync(message, cancellationToken);

        // Act
        await store.SetReplayingAsync(message, cancellationToken);

        // Assert
        Assert.Equal(DeadLetterMessageStatus.Replaying, message.Status);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperationException_When_SetReplayingIsCalledWithWrongMessageType()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        IDeadLetterMessageStore store = new InMemoryDeadLetterMessageStore();
        IDeadLetterMessage wrongType  = new ForeignDeadLetterMessage();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SetReplayingAsync(wrongType, cancellationToken));
    }

    #endregion

    // ── SetReplayedAsync ──────────────────────────────────────────────────────

    #region SetReplayedAsync

    [Fact]
    public async Task Should_SetReplayedStatus_And_ClearNextReplayAt_When_SetReplayedAsyncIsCalled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store   = new InMemoryDeadLetterMessageStore();
        var future  = DateTimeOffset.UtcNow.AddHours(1);
        var message = MakeMessage(DeadLetterMessageStatus.Replaying, nextReplayAt: future);
        await store.AddAsync(message, cancellationToken);

        // Act
        await store.SetReplayedAsync(message, cancellationToken);

        // Assert
        Assert.Equal(DeadLetterMessageStatus.Replayed, message.Status);
        Assert.Null(message.NextReplayAt);
    }

    [Fact]
    public async Task Should_SetReplayedStatus_Via_InterfaceMethod()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        IDeadLetterMessageStore store = new InMemoryDeadLetterMessageStore();
        var message = MakeMessage(DeadLetterMessageStatus.Replaying);
        await store.AddAsync(message, cancellationToken);

        // Act
        await store.SetReplayedAsync(message, cancellationToken);

        // Assert
        Assert.Equal(DeadLetterMessageStatus.Replayed, message.Status);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperationException_When_SetReplayedIsCalledWithWrongMessageType()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        IDeadLetterMessageStore store = new InMemoryDeadLetterMessageStore();
        IDeadLetterMessage wrongType  = new ForeignDeadLetterMessage();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SetReplayedAsync(wrongType, cancellationToken));
    }

    #endregion

    // ── SetRetryAsync ─────────────────────────────────────────────────────────

    #region SetRetryAsync

    [Fact]
    public async Task Should_SetPendingStatus_AndIncrementReplayCount_When_SetRetryAsyncIsCalled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store       = new InMemoryDeadLetterMessageStore();
        var message     = MakeMessage(DeadLetterMessageStatus.Replaying);
        var nextReplay  = DateTimeOffset.UtcNow.AddMinutes(5);
        var errorMsg    = Faker.Lorem.Sentence();
        await store.AddAsync(message, cancellationToken);

        // Act
        await store.SetRetryAsync(message, errorMsg, nextReplay, cancellationToken);

        // Assert
        Assert.Equal(DeadLetterMessageStatus.Pending, message.Status);
        Assert.Equal(errorMsg, message.ErrorMessage);
        Assert.Equal(1, message.ReplayCount);
        Assert.Equal(nextReplay, message.NextReplayAt);
    }

    [Fact]
    public async Task Should_AccumulateReplayCount_When_SetRetryAsyncIsCalledMultipleTimes()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store      = new InMemoryDeadLetterMessageStore();
        var message    = MakeMessage(DeadLetterMessageStatus.Replaying);
        var nextReplay = DateTimeOffset.UtcNow.AddMinutes(5);
        await store.AddAsync(message, cancellationToken);

        // Act
        await store.SetRetryAsync(message, "first-error", nextReplay, cancellationToken);
        await store.SetRetryAsync(message, "second-error", nextReplay.AddMinutes(5), cancellationToken);

        // Assert
        Assert.Equal(2, message.ReplayCount);
        Assert.Equal("second-error", message.ErrorMessage);
    }

    [Fact]
    public async Task Should_SetRetry_Via_InterfaceMethod()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        IDeadLetterMessageStore store = new InMemoryDeadLetterMessageStore();
        var message    = MakeMessage(DeadLetterMessageStatus.Replaying);
        var nextReplay = DateTimeOffset.UtcNow.AddMinutes(5);
        await store.AddAsync(message, cancellationToken);

        // Act
        await store.SetRetryAsync(message, "error", nextReplay, cancellationToken);

        // Assert
        Assert.Equal(DeadLetterMessageStatus.Pending, message.Status);
        Assert.Equal(1, message.ReplayCount);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperationException_When_SetRetryIsCalledWithWrongMessageType()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        IDeadLetterMessageStore store = new InMemoryDeadLetterMessageStore();
        IDeadLetterMessage wrongType  = new ForeignDeadLetterMessage();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SetRetryAsync(wrongType, "error", DateTimeOffset.UtcNow.AddMinutes(1), cancellationToken));
    }

    #endregion

    // ── SetFailedAsync ────────────────────────────────────────────────────────

    #region SetFailedAsync

    [Fact]
    public async Task Should_SetFailedStatus_And_RecordErrorMessage_When_SetFailedAsyncIsCalled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store    = new InMemoryDeadLetterMessageStore();
        var message  = MakeMessage(DeadLetterMessageStatus.Replaying);
        var errorMsg = Faker.Lorem.Sentence();
        await store.AddAsync(message, cancellationToken);

        // Act
        await store.SetFailedAsync(message, errorMsg, cancellationToken);

        // Assert
        Assert.Equal(DeadLetterMessageStatus.Failed, message.Status);
        Assert.Equal(errorMsg, message.ErrorMessage);
    }

    [Fact]
    public async Task Should_SetFailed_Via_InterfaceMethod()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        IDeadLetterMessageStore store = new InMemoryDeadLetterMessageStore();
        var message = MakeMessage(DeadLetterMessageStatus.Replaying);
        await store.AddAsync(message, cancellationToken);

        // Act
        await store.SetFailedAsync(message, "permanent-failure", cancellationToken);

        // Assert
        Assert.Equal(DeadLetterMessageStatus.Failed, message.Status);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperationException_When_SetFailedIsCalledWithWrongMessageType()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        IDeadLetterMessageStore store = new InMemoryDeadLetterMessageStore();
        IDeadLetterMessage wrongType  = new ForeignDeadLetterMessage();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SetFailedAsync(wrongType, "error", cancellationToken));
    }

    #endregion

    // ── GetPendingMessagesAsync ───────────────────────────────────────────────

    #region GetPendingMessagesAsync

    [Fact]
    public async Task Should_ReturnOnlyPendingMessages_When_MultipleStatusesArePresent()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryDeadLetterMessageStore();
        var pending   = MakeMessage(DeadLetterMessageStatus.Pending);
        var replayed  = MakeMessage(DeadLetterMessageStatus.Replayed);
        var failed    = MakeMessage(DeadLetterMessageStatus.Failed);
        var replaying = MakeMessage(DeadLetterMessageStatus.Replaying);
        await store.AddAsync(pending, cancellationToken);
        await store.AddAsync(replayed, cancellationToken);
        await store.AddAsync(failed, cancellationToken);
        await store.AddAsync(replaying, cancellationToken);

        // Act
        var result = await store.GetPendingMessagesAsync(cancellationToken: cancellationToken);

        // Assert
        var single = Assert.Single(result);
        Assert.Equal(pending.Id, single.Id);
    }

    [Fact]
    public async Task Should_ExcludeMessagesWithFutureNextReplayAt_When_GetPendingMessagesAsyncIsCalled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store       = new InMemoryDeadLetterMessageStore();
        var immediate   = MakeMessage(DeadLetterMessageStatus.Pending, nextReplayAt: null);
        var scheduled   = MakeMessage(DeadLetterMessageStatus.Pending, nextReplayAt: DateTimeOffset.UtcNow.AddHours(1));
        await store.AddAsync(immediate, cancellationToken);
        await store.AddAsync(scheduled, cancellationToken);

        // Act
        var result = await store.GetPendingMessagesAsync(cancellationToken: cancellationToken);

        // Assert – only the immediately-eligible message is returned
        var single = Assert.Single(result);
        Assert.Equal(immediate.Id, single.Id);
    }

    [Fact]
    public async Task Should_IncludeMessagesWithPastNextReplayAt_When_GetPendingMessagesAsyncIsCalled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store   = new InMemoryDeadLetterMessageStore();
        var pastDue = MakeMessage(DeadLetterMessageStatus.Pending,
            nextReplayAt: DateTimeOffset.UtcNow.AddHours(-1));
        await store.AddAsync(pastDue, cancellationToken);

        // Act
        var result = await store.GetPendingMessagesAsync(cancellationToken: cancellationToken);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task Should_RespectLimit_When_LimitIsProvided()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryDeadLetterMessageStore();
        for (var i = 0; i < 5; i++)
            await store.AddAsync(MakeMessage(DeadLetterMessageStatus.Pending), cancellationToken);

        // Act
        var result = await store.GetPendingMessagesAsync(limit: 3, cancellationToken: cancellationToken);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NoPendingMessagesExist()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryDeadLetterMessageStore();
        await store.AddAsync(MakeMessage(DeadLetterMessageStatus.Failed), cancellationToken);

        // Act
        var result = await store.GetPendingMessagesAsync(cancellationToken: cancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Should_ReturnPendingMessages_Via_InterfaceMethod()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        IDeadLetterMessageStore store = new InMemoryDeadLetterMessageStore();
        var message = MakeMessage(DeadLetterMessageStatus.Pending);
        await store.AddAsync(message, cancellationToken);

        // Act
        var result = await store.GetPendingMessagesAsync(cancellationToken: cancellationToken);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperationException_When_AddAsyncInterfaceCalledWithWrongMessageType()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        IDeadLetterMessageStore store = new InMemoryDeadLetterMessageStore();
        IDeadLetterMessage wrongType  = new ForeignDeadLetterMessage();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AddAsync(wrongType, cancellationToken));
    }

    #endregion

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// A fake <see cref="IDeadLetterMessage"/> implementation that is NOT a
    /// <see cref="DeadLetterMessage"/>, used to exercise the type-guard inside the
    /// explicit interface implementations.
    /// </summary>
    private sealed class ForeignDeadLetterMessage : IDeadLetterMessage
    {
        public string Id { get; }           = Guid.NewGuid().ToString("N");
        public CloudEvent Event { get; }    = new() { Type = "foreign.event", Source = new Uri("https://foreign.example.com"), Id = Guid.NewGuid().ToString("N") };
        public string PublisherName { get; } = string.Empty;
        public string? ChannelName { get; }
        public string? ChannelType { get; }
        public string? ErrorMessage { get; }
        public DeadLetterMessageStatus Status { get; } = DeadLetterMessageStatus.Pending;
        public int ReplayCount { get; }
        public DateTimeOffset? NextReplayAt { get; }
    }
}