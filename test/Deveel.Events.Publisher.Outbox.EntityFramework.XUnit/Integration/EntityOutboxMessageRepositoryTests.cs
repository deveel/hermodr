//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Bogus;

using CloudNative.CloudEvents;

using Microsoft.EntityFrameworkCore;

namespace Deveel.Events.Integration;

/// <summary>
/// Integration tests that verify <see cref="EntityOutboxMessageRepository{TMessage,TContext}"/>
/// and the <see cref="DbOutboxMessage"/> / <see cref="DbCloudEventAttribute"/> entity
/// mapping against a real MySQL database running inside a Testcontainers container.
/// </summary>
[Collection(MySqlDatabaseCollection.Name)]
[Trait("Category",  "Integration")]
[Trait("Layer",     "Infrastructure")]
[Trait("Feature",   "OutboxEntityFramework")]
[Trait("DisableCICD", "Windows")]
public class EntityOutboxMessageRepositoryTests
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly MySqlDatabaseFixture _db;

    // Single Faker<T> per test class – randomises sources, types, subjects, etc.
    private static readonly Faker Faker = new("en");

    // ── Constructor ───────────────────────────────────────────────────────────

    public EntityOutboxMessageRepositoryTests(MySqlDatabaseFixture db)
    {
        _db = db;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Builds a random, valid <see cref="CloudEvent"/> with no extension attributes.</summary>
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

    /// <summary>
    /// Persists <paramref name="message"/> and detaches it so the next load
    /// hits the database instead of the first-level cache.
    /// </summary>
    private static async Task SaveAndDetachAsync(
        OutboxDbContext ctx,
        DbOutboxMessage message,
        CancellationToken ct)
    {
        await ctx.OutboxMessages.AddAsync(message, ct);
        await ctx.SaveChangesAsync(ct);
        ctx.ChangeTracker.Clear();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Persistence – plain CloudEvent fields
    // ══════════════════════════════════════════════════════════════════════════

    #region Scalar column round-trips

    [Fact]
    public async Task Should_PersistAllScalarColumns_When_MessageIsAddedToDatabase()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var source  = BuildCloudEvent();
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(source);

        await using var writeCtx = _db.CreateContext();

        // Act
        await SaveAndDetachAsync(writeCtx, message, ct);

        // Assert – load through a fresh context so EF memory cache is bypassed
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages
            .SingleOrDefaultAsync(m => m.Id == message.Id, ct);

        Assert.NotNull(loaded);
        Assert.Equal(source.Id,                  loaded.Id);
        Assert.Equal(source.Type,                loaded.EventType);
        Assert.Equal(source.Source!.ToString(),  loaded.Source);
        Assert.Equal(source.Subject,             loaded.Subject);
        Assert.Equal(source.DataContentType,     loaded.DataContentType);
        Assert.Equal(OutboxMessageStatus.Pending, loaded.Status);
        Assert.Equal(0,                          loaded.RetryCount);
        Assert.Null(loaded.ErrorMessage);
        Assert.Null(loaded.NextRetryAt);
    }

    [Fact]
    public async Task Should_PersistDataSchema_When_CloudEventHasDataSchema()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var schema  = new Uri($"https://{Faker.Internet.DomainName()}/schema/v1");
        var source  = BuildCloudEvent(ce => ce.DataSchema = schema);
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(source);

        await using var writeCtx = _db.CreateContext();
        await SaveAndDetachAsync(writeCtx, message, ct);

        // Act
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages
            .SingleOrDefaultAsync(m => m.Id == message.Id, ct);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(schema.ToString(), loaded.DataSchema);
    }

    [Fact]
    public async Task Should_PersistTextData_When_CloudEventHasStringPayload()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var payload = $"{{\"orderId\":\"{Faker.Random.Guid()}\"}}";
        var source  = BuildCloudEvent(ce => ce.Data = payload);
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(source);

        await using var writeCtx = _db.CreateContext();
        await SaveAndDetachAsync(writeCtx, message, ct);

        // Act
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages
            .SingleOrDefaultAsync(m => m.Id == message.Id, ct);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(payload, loaded.DataText);
        Assert.Null(loaded.DataBytes);
    }

    [Fact]
    public async Task Should_PersistBinaryData_When_CloudEventHasByteArrayPayload()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var bytes   = Faker.Random.Bytes(64);
        var source  = BuildCloudEvent(ce =>
        {
            ce.DataContentType = "application/octet-stream";
            ce.Data            = bytes;
        });
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(source);

        await using var writeCtx = _db.CreateContext();
        await SaveAndDetachAsync(writeCtx, message, ct);

        // Act
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages
            .SingleOrDefaultAsync(m => m.Id == message.Id, ct);

        // Assert
        Assert.NotNull(loaded);
        Assert.Null(loaded.DataText);
        Assert.Equal(bytes, loaded.DataBytes);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // Extension attributes (one-to-many)
    // ══════════════════════════════════════════════════════════════════════════

    #region Extension-attribute round-trips

    [Fact]
    public async Task Should_PersistExtensionAttributes_When_CloudEventHasExtensions()
    {
        // Arrange
        var ct     = TestContext.Current.CancellationToken;
        var tenant = Faker.Random.AlphaNumeric(8).ToLowerInvariant();
        var seq    = Faker.Random.Int(1, 9999);
        var source = BuildCloudEvent(ce =>
        {
            ce[CloudEventAttribute.CreateExtension("tenantid",  CloudEventAttributeType.String)]  = tenant;
            ce[CloudEventAttribute.CreateExtension("sequenceid", CloudEventAttributeType.Integer)] = seq;
        });

        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(source);

        await using var writeCtx = _db.CreateContext();
        await SaveAndDetachAsync(writeCtx, message, ct);

        // Act – reload with child attributes included
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages
            .Include(m => m.Attributes)
            .SingleOrDefaultAsync(m => m.Id == message.Id, ct);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Attributes.Count);

        var tenantAttr = loaded.Attributes.Single(a => a.Name == "tenantid");
        Assert.Equal("string",  tenantAttr.ValueType);
        Assert.Equal(tenant,    tenantAttr.Value);

        var seqAttr = loaded.Attributes.Single(a => a.Name == "sequenceid");
        Assert.Equal("integer", seqAttr.ValueType);
        Assert.Equal(seq.ToString(), seqAttr.Value);
    }

    [Fact]
    public async Task Should_CascadeDeleteAttributes_When_MessageIsRemoved()
    {
        // Arrange
        var ct     = TestContext.Current.CancellationToken;
        var source = BuildCloudEvent(ce =>
            ce[CloudEventAttribute.CreateExtension("trace", CloudEventAttributeType.String)] = "abc");
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(source);

        await using var ctx = _db.CreateContext();
        await ctx.OutboxMessages.AddAsync(message, ct);
        await ctx.SaveChangesAsync(ct);

        var attrCount = await ctx.OutboxMessageAttributes
            .CountAsync(a => a.MessageId == message.Id, ct);
        Assert.Equal(1, attrCount);

        // Act
        ctx.OutboxMessages.Remove(message);
        await ctx.SaveChangesAsync(ct);

        // Assert – child rows should have been cascade-deleted
        var remaining = await ctx.OutboxMessageAttributes
            .CountAsync(a => a.MessageId == message.Id, ct);
        Assert.Equal(0, remaining);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // CloudEvent reconstruction (BuildCloudEvent)
    // ══════════════════════════════════════════════════════════════════════════

    #region CloudEvent round-trip via BuildCloudEvent

    [Fact]
    public async Task Should_ReconstructCloudEvent_When_MessageIsLoadedFromDatabase()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var original = BuildCloudEvent();
        var message  = new DbOutboxMessage();
        message.PopulateFromCloudEvent(original);

        await using var writeCtx = _db.CreateContext();
        await SaveAndDetachAsync(writeCtx, message, ct);

        // Act
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages
            .Include(m => m.Attributes)
            .SingleAsync(m => m.Id == message.Id, ct);

        var rebuilt = ((IOutboxMessage)loaded).CloudEvent;

        // Assert
        Assert.Equal(original.Id,                 rebuilt.Id);
        Assert.Equal(original.Type,               rebuilt.Type);
        Assert.Equal(original.Source,             rebuilt.Source);
        Assert.Equal(original.Subject,            rebuilt.Subject);
        Assert.Equal(original.DataContentType,    rebuilt.DataContentType);
    }

    [Fact]
    public async Task Should_ReconstructExtensionAttributes_When_MessageHasExtensions()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var envVal  = Faker.PickRandom("prod", "staging", "dev");
        var original = BuildCloudEvent(ce =>
            ce[CloudEventAttribute.CreateExtension("env", CloudEventAttributeType.String)] = envVal);

        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(original);

        await using var writeCtx = _db.CreateContext();
        await SaveAndDetachAsync(writeCtx, message, ct);

        // Act
        await using var readCtx = _db.CreateContext();
        var loaded  = await readCtx.OutboxMessages
            .Include(m => m.Attributes)
            .SingleAsync(m => m.Id == message.Id, ct);
        var rebuilt = ((IOutboxMessage)loaded).CloudEvent;

        // Assert
        var envAttr = CloudEventAttribute.CreateExtension("env", CloudEventAttributeType.String);
        Assert.Equal(envVal, rebuilt[envAttr]?.ToString());
    }

    #endregion

    // ══════════════════════════════════��═══════════════════════════════════════
    // GetPendingMessagesAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region GetPendingMessagesAsync

    [Fact]
    public async Task Should_ReturnPendingMessages_When_MessagesHavePendingStatus()
    {
        // Arrange
        var ct   = TestContext.Current.CancellationToken;
        var repo = _db.CreateRepository();

        var pending1 = new DbOutboxMessage();
        pending1.PopulateFromCloudEvent(BuildCloudEvent());

        var pending2 = new DbOutboxMessage();
        pending2.PopulateFromCloudEvent(BuildCloudEvent());

        var delivered = new DbOutboxMessage();
        delivered.PopulateFromCloudEvent(BuildCloudEvent());
        delivered.Status = OutboxMessageStatus.Delivered;

        await using var ctx = _db.CreateContext();
        await ctx.OutboxMessages.AddRangeAsync([pending1, pending2, delivered], ct);
        await ctx.SaveChangesAsync(ct);

        // Act
        var results = await repo.GetPendingMessagesAsync(cancellationToken: ct);

        // Assert – only the two pending rows should be returned
        Assert.True(results.Count >= 2,
            "Expected at least 2 pending messages (may be more from other tests).");
        Assert.DoesNotContain(results, m => m.Id == delivered.Id);
    }

    [Fact]
    public async Task Should_ExcludeFutureRetries_When_NextRetryAtIsInFuture()
    {
        // Arrange
        var ct   = TestContext.Current.CancellationToken;
        var repo = _db.CreateRepository();

        var readyNow = new DbOutboxMessage();
        readyNow.PopulateFromCloudEvent(BuildCloudEvent());
        // NextRetryAt defaults to null → eligible immediately

        var notYet = new DbOutboxMessage();
        notYet.PopulateFromCloudEvent(BuildCloudEvent());
        notYet.NextRetryAt = DateTimeOffset.UtcNow.AddHours(1); // future → must be excluded

        await using var ctx = _db.CreateContext();
        await ctx.OutboxMessages.AddRangeAsync([readyNow, notYet], ct);
        await ctx.SaveChangesAsync(ct);

        // Act
        var results = await repo.GetPendingMessagesAsync(cancellationToken: ct);

        // Assert
        Assert.Contains(results,    m => m.Id == readyNow.Id);
        Assert.DoesNotContain(results, m => m.Id == notYet.Id);
    }

    [Fact]
    public async Task Should_RespectBatchLimit_When_LimitIsSpecified()
    {
        // Arrange
        var ct   = TestContext.Current.CancellationToken;
        var repo = _db.CreateRepository();

        // Insert 5 pending messages with unique IDs so they don't collide with
        // rows from other tests.
        var messages = Enumerable.Range(0, 5).Select(_ =>
        {
            var m = new DbOutboxMessage();
            m.PopulateFromCloudEvent(BuildCloudEvent());
            return m;
        }).ToList();

        await using var ctx = _db.CreateContext();
        await ctx.OutboxMessages.AddRangeAsync(messages, ct);
        await ctx.SaveChangesAsync(ct);

        // Act
        var results = await repo.GetPendingMessagesAsync(limit: 2, cancellationToken: ct);

        // Assert – never more than the requested limit
        Assert.True(results.Count <= 2);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // Status transitions
    // ══════════════════════════════════════════════════════════════════════════

    #region Status transitions

    [Fact]
    public async Task Should_MarkMessageAsSending_When_SetSendingAsyncIsCalled()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(BuildCloudEvent());

        await using var writeCtx = _db.CreateContext();
        await writeCtx.OutboxMessages.AddAsync(message, ct);
        await writeCtx.SaveChangesAsync(ct);

        var repo = new EntityOutboxMessageRepository<DbOutboxMessage, OutboxDbContext>(writeCtx);

        // Act
        await repo.SetSendingAsync(message, ct);
        await writeCtx.SaveChangesAsync(ct);

        // Assert – verify through a fresh context
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages.SingleAsync(m => m.Id == message.Id, ct);

        Assert.Equal(OutboxMessageStatus.Sending, loaded.Status);
        Assert.NotNull(loaded.LastStatusAt);
    }

    [Fact]
    public async Task Should_MarkMessageAsDelivered_When_SetDeliveredAsyncIsCalled()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(BuildCloudEvent());

        await using var writeCtx = _db.CreateContext();
        await writeCtx.OutboxMessages.AddAsync(message, ct);
        await writeCtx.SaveChangesAsync(ct);

        var repo = new EntityOutboxMessageRepository<DbOutboxMessage, OutboxDbContext>(writeCtx);

        // Act
        await repo.SetDeliveredAsync(message, ct);
        await writeCtx.SaveChangesAsync(ct);

        // Assert
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages.SingleAsync(m => m.Id == message.Id, ct);

        Assert.Equal(OutboxMessageStatus.Delivered, loaded.Status);
        Assert.NotNull(loaded.LastStatusAt);
    }

    [Fact]
    public async Task Should_MarkMessageAsFailed_When_SetFailedAsyncIsCalled()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var errMsg  = Faker.Lorem.Sentence();
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(BuildCloudEvent());

        await using var writeCtx = _db.CreateContext();
        await writeCtx.OutboxMessages.AddAsync(message, ct);
        await writeCtx.SaveChangesAsync(ct);

        var repo = new EntityOutboxMessageRepository<DbOutboxMessage, OutboxDbContext>(writeCtx);

        // Act
        await repo.SetFailedAsync(message, errMsg, ct);
        await writeCtx.SaveChangesAsync(ct);

        // Assert
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages.SingleAsync(m => m.Id == message.Id, ct);

        Assert.Equal(OutboxMessageStatus.Failed, loaded.Status);
        Assert.Equal(errMsg, loaded.ErrorMessage);
        Assert.NotNull(loaded.LastStatusAt);
    }

    [Fact]
    public async Task Should_ScheduleRetry_When_SetRetryAsyncIsCalled()
    {
        // Arrange
        var ct        = TestContext.Current.CancellationToken;
        var errMsg    = Faker.Lorem.Sentence();
        // Use a frozen clock so that both the retryAt value and the LastStatusAt
        // timestamp written by the repository are fully predictable and survive
        // MySQL DATETIME storage without any sub-second rounding differences.
        var clock     = new TestSystemTime();
        var retryAt   = clock.UtcNow.AddMinutes(Faker.Random.Int(1, 30));
        var message   = new DbOutboxMessage();
        message.PopulateFromCloudEvent(BuildCloudEvent());

        await using var writeCtx = _db.CreateContext();
        await writeCtx.OutboxMessages.AddAsync(message, ct);
        await writeCtx.SaveChangesAsync(ct);

        var repo = new EntityOutboxMessageRepository<DbOutboxMessage, OutboxDbContext>(writeCtx, clock);

        // Act
        await repo.SetRetryAsync(message, errMsg, retryAt, ct);
        await writeCtx.SaveChangesAsync(ct);

        // Assert
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages.SingleAsync(m => m.Id == message.Id, ct);

        Assert.Equal(OutboxMessageStatus.Pending, loaded.Status);
        Assert.Equal(errMsg,                      loaded.ErrorMessage);
        Assert.Equal(1,                           loaded.RetryCount);
        Assert.NotNull(loaded.NextRetryAt);
        Assert.Equal(retryAt.ToUnixTimeSeconds(), loaded.NextRetryAt!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Should_IncrementRetryCount_When_SetRetryAsyncIsCalledMultipleTimes()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(BuildCloudEvent());

        await using var ctx = _db.CreateContext();
        await ctx.OutboxMessages.AddAsync(message, ct);
        await ctx.SaveChangesAsync(ct);

        var repo = new EntityOutboxMessageRepository<DbOutboxMessage, OutboxDbContext>(ctx);

        // Act – simulate two consecutive transient failures
        await repo.SetRetryAsync(message, "first failure",  DateTimeOffset.UtcNow.AddMinutes(1), ct);
        await ctx.SaveChangesAsync(ct);

        await repo.SetRetryAsync(message, "second failure", DateTimeOffset.UtcNow.AddMinutes(2), ct);
        await ctx.SaveChangesAsync(ct);

        // Assert
        await using var readCtx = _db.CreateContext();
        var loaded = await readCtx.OutboxMessages.SingleAsync(m => m.Id == message.Id, ct);

        Assert.Equal(2, loaded.RetryCount);
        Assert.Equal("second failure", loaded.ErrorMessage);
    }

    [Fact]
    public async Task Should_ReturnCurrentStatus_When_GetStatusAsyncIsCalled()
    {
        // Arrange
        var ct      = TestContext.Current.CancellationToken;
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(BuildCloudEvent());

        await using var ctx = _db.CreateContext();
        await ctx.OutboxMessages.AddAsync(message, ct);
        await ctx.SaveChangesAsync(ct);

        var repo = new EntityOutboxMessageRepository<DbOutboxMessage, OutboxDbContext>(ctx);

        // Act
        var status = await repo.GetStatusAsync(message, ct);

        // Assert
        Assert.Equal(OutboxMessageStatus.Pending, status);
    }

    #endregion
}

