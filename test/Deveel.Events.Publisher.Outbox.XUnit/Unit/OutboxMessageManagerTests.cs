// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Bogus;

using CloudNative.CloudEvents;

using Deveel;
using Deveel.Data;

using Deveel.Events.Fakes;

namespace Deveel.Events;

/// <summary>
/// Unit tests for the state-transition methods of <see cref="OutboxMessageManager{TMessage}"/>:
/// <see cref="OutboxMessageManager{TMessage}.MarkFailedAsync"/>,
/// <see cref="OutboxMessageManager{TMessage}.MarkDeliveredAsync"/>, and
/// <see cref="OutboxMessageManager{TMessage}.ScheduleRetryAsync"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Domain")]
[Trait("Feature", "Outbox")]
public class OutboxMessageManagerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly Faker Faker = new("en");

    private static CloudEvent MakeEvent() => new()
    {
        Type   = "test.order.created",
        Source = new Uri("https://example.com"),
        Id     = Faker.Random.Guid().ToString("N"),
        Time   = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// Builds an <see cref="OutboxMessageManager{FakeOutboxMessage}"/> directly with the
    /// supplied repository, bypassing DI to keep tests simple.
    /// </summary>
    private static (OutboxMessageManager<FakeOutboxMessage> Manager, FakeOutboxMessageRepository Repository)
        BuildManager()
    {
        var repository = new FakeOutboxMessageRepository();
        var manager    = new OutboxMessageManager<FakeOutboxMessage>(repository);
        return (manager, repository);
    }

    /// <summary>Creates a new <see cref="FakeOutboxMessage"/> and seeds it into the repository.</summary>
    private static FakeOutboxMessage Seed(FakeOutboxMessageRepository repository, OutboxMessageStatus status = OutboxMessageStatus.Pending)
    {
        var message = new FakeOutboxMessage(MakeEvent());
        repository.SeedAsync(message);
        message.Status = status;
        return message;
    }

    // ── MarkFailedAsync ───────────────────────────────────────────────────────

    #region MarkFailedAsync

    [Fact]
    public async Task Should_MarkMessageAsFailed_When_MessageIsPending()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message      = Seed(repository, OutboxMessageStatus.Pending);
        var errorMessage = Faker.Lorem.Sentence();

        // Act
        var result = await manager.MarkFailedAsync(message, errorMessage);

        // Assert – no error is returned and the status has transitioned
        Assert.False(result.IsError());
        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
        Assert.Equal(errorMessage, message.ErrorMessage);
    }

    [Fact]
    public async Task Should_ReturnUnchanged_When_MessageIsAlreadyFailed()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message = Seed(repository, OutboxMessageStatus.Failed);

        // Act
        var result = await manager.MarkFailedAsync(message, "irrelevant");

        // Assert
        Assert.True(result.IsUnchanged());
    }

    [Fact]
    public async Task Should_ReturnError_When_MarkFailedIsCalledOnDeliveredMessage()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message = Seed(repository, OutboxMessageStatus.Delivered);

        // Act
        var result = await manager.MarkFailedAsync(message, "error");

        // Assert
        Assert.True(result.IsError());
        Assert.Equal("OUT0031", result.Error?.Code);
    }

    [Fact]
    public async Task Should_MarkMessageAsFailed_When_MessageIsSending()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message      = Seed(repository, OutboxMessageStatus.Sending);
        var errorMessage = Faker.Lorem.Sentence();

        // Act
        var result = await manager.MarkFailedAsync(message, errorMessage);

        // Assert – Sending is allowed to transition to Failed
        Assert.False(result.IsError());
        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
    }

    #endregion

    // ── MarkDeliveredAsync ────────────────────────────────────────────────────

    #region MarkDeliveredAsync

    [Fact]
    public async Task Should_MarkMessageAsDelivered_When_MessageIsPending()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message = Seed(repository, OutboxMessageStatus.Pending);

        // Act
        var result = await manager.MarkDeliveredAsync(message);

        // Assert
        Assert.False(result.IsError());
        Assert.Equal(OutboxMessageStatus.Delivered, message.Status);
    }

    [Fact]
    public async Task Should_ReturnUnchanged_When_MessageIsAlreadyDelivered()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message = Seed(repository, OutboxMessageStatus.Delivered);

        // Act
        var result = await manager.MarkDeliveredAsync(message);

        // Assert
        Assert.True(result.IsUnchanged());
    }

    [Fact]
    public async Task Should_ReturnError_When_MarkDeliveredIsCalledOnFailedMessage()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message = Seed(repository, OutboxMessageStatus.Failed);

        // Act
        var result = await manager.MarkDeliveredAsync(message);

        // Assert
        Assert.True(result.IsError());
        Assert.Equal("OUT0033", result.Error?.Code);
    }

    [Fact]
    public async Task Should_MarkMessageAsDelivered_When_MessageIsSending()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message = Seed(repository, OutboxMessageStatus.Sending);

        // Act
        var result = await manager.MarkDeliveredAsync(message);

        // Assert – Sending is a valid pre-condition for delivery
        Assert.False(result.IsError());
        Assert.Equal(OutboxMessageStatus.Delivered, message.Status);
    }

    #endregion

    // ── ScheduleRetryAsync ────────────────────────────────────────────────────

    #region ScheduleRetryAsync

    [Fact]
    public async Task Should_ScheduleRetry_When_MessageIsPending()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message    = Seed(repository, OutboxMessageStatus.Pending);
        var nextRetry  = DateTimeOffset.UtcNow.AddMinutes(Faker.Random.Int(1, 30));
        var errorMsg   = Faker.Lorem.Sentence();

        // Act
        var result = await manager.ScheduleRetryAsync(message, errorMsg, nextRetry);

        // Assert
        Assert.False(result.IsError());
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(errorMsg, message.ErrorMessage);
        Assert.Equal(nextRetry, message.NextRetryAt);
    }

    [Fact]
    public async Task Should_ReturnError_When_ScheduleRetryIsCalledOnFailedMessage()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message = Seed(repository, OutboxMessageStatus.Failed);

        // Act
        var result = await manager.ScheduleRetryAsync(message, "error", DateTimeOffset.UtcNow.AddMinutes(5));

        // Assert
        Assert.True(result.IsError());
        Assert.Equal("OUT0035", result.Error?.Code);
    }

    [Fact]
    public async Task Should_ReturnError_When_ScheduleRetryIsCalledOnDeliveredMessage()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message = Seed(repository, OutboxMessageStatus.Delivered);

        // Act
        var result = await manager.ScheduleRetryAsync(message, "error", DateTimeOffset.UtcNow.AddMinutes(5));

        // Assert
        Assert.True(result.IsError());
        Assert.Equal("OUT0036", result.Error?.Code);
    }

    [Fact]
    public async Task Should_ScheduleRetry_When_MessageIsSending()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message   = Seed(repository, OutboxMessageStatus.Sending);
        var nextRetry = DateTimeOffset.UtcNow.AddMinutes(Faker.Random.Int(1, 30));

        // Act
        var result = await manager.ScheduleRetryAsync(message, "transient error", nextRetry);

        // Assert – Sending is a valid pre-condition for scheduling a retry
        Assert.False(result.IsError());
        Assert.Equal(nextRetry, message.NextRetryAt);
    }

    #endregion

    // ── GetPendingMessagesAsync / GetStatusAsync delegation ───────────────────

    #region Delegation

    [Fact]
    public async Task Should_ReturnPendingMessages_When_GetPendingMessagesAsyncIsCalled()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var pendingMsg   = Seed(repository, OutboxMessageStatus.Pending);
        var deliveredMsg = Seed(repository, OutboxMessageStatus.Delivered);

        // Act
        var pending = await manager.GetPendingMessagesAsync();

        // Assert
        Assert.Contains(pending, m => m.Id == pendingMsg.Id);
        Assert.DoesNotContain(pending, m => m.Id == deliveredMsg.Id);
    }

    [Fact]
    public async Task Should_ReturnPending_When_GetStatusAsyncIsCalledOnPendingMessage()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message = Seed(repository, OutboxMessageStatus.Pending);

        // Act
        var status = await manager.GetStatusAsync(message);

        // Assert
        Assert.Equal(OutboxMessageStatus.Pending, status);
    }

    [Fact]
    public async Task Should_ReturnFailed_When_GetStatusAsyncIsCalledOnFailedMessage()
    {
        // Arrange
        var (manager, repository) = BuildManager();
        var message = Seed(repository, OutboxMessageStatus.Failed);

        // Act
        var status = await manager.GetStatusAsync(message);

        // Assert
        Assert.Equal(OutboxMessageStatus.Failed, status);
    }

    #endregion
}
