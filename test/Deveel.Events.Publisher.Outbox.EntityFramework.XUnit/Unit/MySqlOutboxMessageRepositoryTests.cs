//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Bogus;

using CloudNative.CloudEvents;

using Deveel.Data;

namespace Deveel.Events.Unit;

/// <summary>
/// Unit tests for <see cref="EntityOutboxMessageRepository{TMessage}"/> that
/// use an SQLite in-memory database instead of MySQL, so they run in CI on all
/// platforms without requiring a Docker container.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer",    "Infrastructure")]
[Trait("Feature",  "OutboxEntityFramework")]
public class MySqlOutboxMessageRepositoryTests : IClassFixture<SqliteOutboxFixture>
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly SqliteOutboxFixture _db;

    private static readonly Faker Faker = new("en");

    // ── Constructor ───────────────────────────────────────────────────────────

    public MySqlOutboxMessageRepositoryTests(SqliteOutboxFixture db)
    {
        _db = db;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CloudEvent BuildCloudEvent(Action<CloudEvent>? configure = null)
    {
        var ce = new CloudEvent
        {
            Id              = Faker.Random.Guid().ToString("N"),
            Type            = $"{Faker.Lorem.Word()}.{Faker.Lorem.Word()}.occurred",
            Source          = new Uri($"https://{Faker.Internet.DomainName()}"),
            Subject         = Faker.Lorem.Word(),
            Time            = Faker.Date.RecentOffset(days: 1),
            DataContentType = "application/json",
            Data            = $"{{\"value\":\"{Faker.Lorem.Word()}\"}}"
        };
        configure?.Invoke(ce);
        return ce;
    }

    private static DbOutboxMessage BuildMessage(Action<DbOutboxMessage>? configure = null)
    {
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(BuildCloudEvent());
        configure?.Invoke(message);
        return message;
    }

    // Repository factory shortcut.
    private EntityOutboxMessageRepository<DbOutboxMessage> CreateRepo(
        ISystemTime? clock = null) => new(_db.CreateContext(), clock);

    // ══════════════════════════════════════════════════════════════════════════
    // GetStatusAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region GetStatusAsync

    [Fact]
    public async Task Should_ReturnPendingStatus_When_MessageIsNewlyCreated()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var message = BuildMessage();

        // Act
        var status = await CreateRepo().GetStatusAsync(message, ct);

        // Assert
        Assert.Equal(OutboxMessageStatus.Pending, status);
    }

    [Fact]
    public async Task Should_ReturnCurrentStatus_When_StatusHasBeenMutated()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var message = BuildMessage(m => m.Status = OutboxMessageStatus.Delivered);

        // Act
        var status = await CreateRepo().GetStatusAsync(message, ct);

        // Assert
        Assert.Equal(OutboxMessageStatus.Delivered, status);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // SetSendingAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region SetSendingAsync

    [Fact]
    public async Task Should_MarkMessageAsSending_When_SetSendingAsyncIsCalled()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var message = BuildMessage();

        // Act
        await CreateRepo().SetSendingAsync(message, ct);

        // Assert
        Assert.Equal(OutboxMessageStatus.Sending, message.Status);
        Assert.NotNull(message.LastStatusAt);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // SetDeliveredAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region SetDeliveredAsync

    [Fact]
    public async Task Should_MarkMessageAsDelivered_When_SetDeliveredAsyncIsCalled()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var message = BuildMessage();

        // Act
        await CreateRepo().SetDeliveredAsync(message, ct);

        // Assert
        Assert.Equal(OutboxMessageStatus.Delivered, message.Status);
        Assert.NotNull(message.LastStatusAt);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // SetFailedAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region SetFailedAsync

    [Fact]
    public async Task Should_MarkMessageAsFailed_When_SetFailedAsyncIsCalled()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var errMsg  = Faker.Lorem.Sentence();
        var message = BuildMessage();

        // Act
        await CreateRepo().SetFailedAsync(message, errMsg, ct);

        // Assert
        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
        Assert.Equal(errMsg, message.ErrorMessage);
        Assert.NotNull(message.LastStatusAt);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // SetRetryAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region SetRetryAsync

    [Fact]
    public async Task Should_ScheduleRetry_When_SetRetryAsyncIsCalled()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var errMsg  = Faker.Lorem.Sentence();
        var retryAt = DateTimeOffset.UtcNow.AddMinutes(Faker.Random.Int(1, 30));
        var message = BuildMessage();

        // Act
        await CreateRepo().SetRetryAsync(message, errMsg, retryAt, ct);

        // Assert
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(errMsg,  message.ErrorMessage);
        Assert.Equal(1,       message.RetryCount);
        Assert.Equal(retryAt, message.NextRetryAt);
        Assert.NotNull(message.LastStatusAt);
    }

    [Fact]
    public async Task Should_IncrementRetryCount_When_SetRetryAsyncIsCalledMultipleTimes()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var message = BuildMessage();
        var repo    = CreateRepo();

        // Act – simulate two consecutive failures against the same repo instance
        await repo.SetRetryAsync(message, "first failure",  DateTimeOffset.UtcNow.AddMinutes(1), ct);
        await repo.SetRetryAsync(message, "second failure", DateTimeOffset.UtcNow.AddMinutes(2), ct);

        // Assert
        Assert.Equal(2,                message.RetryCount);
        Assert.Equal("second failure", message.ErrorMessage);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // GetPendingMessagesAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region GetPendingMessagesAsync

    [Fact]
    public async Task Should_ReturnOnlyPendingMessages_When_MixedStatusMessagesExist()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var pending1  = BuildMessage();
        var pending2  = BuildMessage();
        var delivered = BuildMessage(m => m.Status = OutboxMessageStatus.Delivered);

        await using var ctx = _db.CreateContext();
        await ctx.Set<DbOutboxMessage>().AddRangeAsync([pending1, pending2, delivered], ct);
        await ctx.SaveChangesAsync(ct);

        // Act
        var results = await CreateRepo().GetPendingMessagesAsync(cancellationToken: ct);

        // Assert
        Assert.Contains(results,       m => m.Id == pending1.Id);
        Assert.Contains(results,       m => m.Id == pending2.Id);
        Assert.DoesNotContain(results, m => m.Id == delivered.Id);
    }

    [Fact]
    public async Task Should_ExcludeFutureRetries_When_NextRetryAtIsInFuture()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var readyNow = BuildMessage();
        // NextRetryAt is null → eligible immediately

        var notYet = BuildMessage(m => m.NextRetryAt = DateTimeOffset.UtcNow.AddHours(1));

        await using var ctx = _db.CreateContext();
        await ctx.OutboxMessages.AddRangeAsync([readyNow, notYet], ct);
        await ctx.SaveChangesAsync(ct);

        // Act
        var results = await CreateRepo().GetPendingMessagesAsync(cancellationToken: ct);

        // Assert
        Assert.Contains(results,       m => m.Id == readyNow.Id);
        Assert.DoesNotContain(results, m => m.Id == notYet.Id);
    }

    [Fact]
    public async Task Should_IncludeEligibleRetries_When_NextRetryAtIsInThePast()
    {
        // Arrange
        var ct        = TestContext.Current.CancellationToken;
        var pastRetry = BuildMessage(m => m.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-5));

        await using var ctx = _db.CreateContext();
        await ctx.OutboxMessages.AddAsync(pastRetry, ct);
        await ctx.SaveChangesAsync(ct);

        // Act
        var results = await CreateRepo().GetPendingMessagesAsync(cancellationToken: ct);

        // Assert
        Assert.Contains(results, m => m.Id == pastRetry.Id);
    }

    [Fact]
    public async Task Should_RespectBatchLimit_When_LimitIsSpecified()
    {
        // Arrange
        var ct       = TestContext.Current.CancellationToken;
        var messages = Enumerable.Range(0, 5).Select(_ => BuildMessage()).ToList();

        await using var ctx = _db.CreateContext();
        await ctx.OutboxMessages.AddRangeAsync(messages, ct);
        await ctx.SaveChangesAsync(ct);

        // Act
        var results = await CreateRepo().GetPendingMessagesAsync(limit: 2, cancellationToken: ct);

        // Assert – count must never exceed the requested limit
        Assert.True(results.Count <= 2);
    }

    [Fact]
    public async Task Should_NotIncludeDeliveredMessage_When_OnlyDeliveredMessagesExist()
    {
        // Arrange
        var ct        = TestContext.Current.CancellationToken;
        var delivered = BuildMessage(m => m.Status = OutboxMessageStatus.Delivered);

        await using var ctx = _db.CreateContext();
        await ctx.OutboxMessages.AddAsync(delivered, ct);
        await ctx.SaveChangesAsync(ct);

        // Act
        var results = await CreateRepo().GetPendingMessagesAsync(cancellationToken: ct);

        // Assert – the delivered message must never appear in the pending results
        Assert.DoesNotContain(results, m => m.Id == delivered.Id);
    }

    #endregion
}


