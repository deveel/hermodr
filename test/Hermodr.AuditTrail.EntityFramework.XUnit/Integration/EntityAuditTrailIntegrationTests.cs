using System.Text.Json;
using CloudNative.CloudEvents;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hermodr;

[Trait("Category", "Integration")]
[Trait("Feature", "AuditTrail")]
[Trait("Layer", "Infrastructure")]
public class EntityAuditTrailIntegrationTests : IClassFixture<SqliteAuditTrailFixture>, IAsyncLifetime
{
    private readonly SqliteAuditTrailFixture _db;

    public EntityAuditTrailIntegrationTests(SqliteAuditTrailFixture db)
    {
        _db = db;
    }

    public async ValueTask InitializeAsync()
    {
        using var ctx = _db.CreateContext();
        ctx.AuditTrailEntries.RemoveRange(ctx.AuditTrailEntries);
        await ctx.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CloudEvent CreateCloudEvent(
        string? type = null,
        string? source = null,
        string? subject = null,
        DateTimeOffset? time = null)
    {
        return new CloudEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type ?? "com.example.order.created",
            Source = new Uri(source ?? "https://example.com/orders"),
            Subject = subject,
            Time = time ?? DateTimeOffset.UtcNow,
            Data = JsonSerializer.Serialize(new { OrderId = 123, Amount = 99.99 })
        };
    }

    [Fact]
    public async Task Should_ResolveServices_ThroughPipeline()
    {
        var writer = _db.GetService<IAuditTrailWriter>();
        var reader = _db.GetService<IAuditTrailReader<DbAuditTrailEntry>>();
        var context = _db.GetService<AuditTrailDbContext>();

        Assert.NotNull(writer);
        Assert.NotNull(reader);
        Assert.NotNull(context);
        Assert.IsType<EntityAuditTrail>(writer);
    }

    [Fact]
    public async Task Should_AppendEvent_ToDatabase()
    {
        var auditTrail = _db.CreateAuditTrail();
        var cloudEvent = CreateCloudEvent();

        await auditTrail.AppendAsync(cloudEvent, TestContext.Current.CancellationToken);

        using var ctx = _db.CreateContext();
        var entry = await ctx.AuditTrailEntries.FirstOrDefaultAsync(e => e.EventId == cloudEvent.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(entry);
        Assert.Equal(cloudEvent.Id, entry.EventId);
        Assert.Equal(cloudEvent.Type, entry.EventType);
    }

    [Fact]
    public async Task Should_ReadAllEntries_FromDatabase()
    {
        var auditTrail = _db.CreateAuditTrail();

        await auditTrail.AppendAsync(CreateCloudEvent(type: "event.type.1"), TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(CreateCloudEvent(type: "event.type.2"), TestContext.Current.CancellationToken);

        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Should_FilterByEventType()
    {
        var auditTrail = _db.CreateAuditTrail();

        await auditTrail.AppendAsync(CreateCloudEvent(type: "order.created"), TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(CreateCloudEvent(type: "order.shipped"), TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(CreateCloudEvent(type: "order.created"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { EventType = "order.created" };
        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.Equal("order.created", e.EventType));
    }

    [Fact]
    public async Task Should_FilterBySource()
    {
        var auditTrail = _db.CreateAuditTrail();

        await auditTrail.AppendAsync(CreateCloudEvent(source: "https://service-a.com/"), TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(CreateCloudEvent(source: "https://service-b.com/"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { Source = "https://service-a.com/" };
        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("https://service-a.com/", results[0].Source);
    }

    [Fact]
    public async Task Should_FilterBySubject()
    {
        var auditTrail = _db.CreateAuditTrail();

        await auditTrail.AppendAsync(CreateCloudEvent(subject: "order-123"), TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(CreateCloudEvent(subject: "order-456"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { Subject = "order-123" };
        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("order-123", results[0].Subject);
    }

    [Fact]
    public async Task Should_FilterByTimeRange()
    {
        var auditTrail = _db.CreateAuditTrail();
        var now = DateTimeOffset.UtcNow;

        await auditTrail.AppendAsync(CreateCloudEvent(time: now.AddHours(-5)), TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(CreateCloudEvent(time: now.AddHours(-1)), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { From = now.AddHours(-6), To = now.AddHours(-2) };
        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
    }

    [Fact]
    public async Task Should_FilterByEventDataClassName()
    {
        var auditTrail = _db.CreateAuditTrail();

        var entry1 = AuditTrailEntry.FromCloudEvent(CreateCloudEvent(), dataClassName: "MyApp.Events.OrderCreatedEvent");
        var entry2 = AuditTrailEntry.FromCloudEvent(CreateCloudEvent(), dataClassName: "MyApp.Events.OrderShippedEvent");

        // Manually append with data class name
        var dbEntry1 = DbAuditTrailEntry.FromEntry(entry1);
        var dbEntry2 = DbAuditTrailEntry.FromEntry(entry2);

        using var ctx = _db.CreateContext();
        ctx.AuditTrailEntries.Add(dbEntry1);
        ctx.AuditTrailEntries.Add(dbEntry2);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { EventDataClassName = "MyApp.Events.OrderCreatedEvent" };
        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("MyApp.Events.OrderCreatedEvent", results[0].EventDataClassName);
    }

    [Fact]
    public async Task Should_FilterByExtensionAttributes()
    {
        var auditTrail = _db.CreateAuditTrail();

        var cloudEvent1 = CreateCloudEvent();
        cloudEvent1["correlationid"] = "corr-123";
        var cloudEvent2 = CreateCloudEvent();

        await auditTrail.AppendAsync(cloudEvent1, TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(cloudEvent2, TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery
        {
            Attributes = new Dictionary<string, string> { ["correlationid"] = "corr-123" }
        };
        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
    }

    [Fact]
    public async Task Should_StoreExtensionAttributes_AsChildRows()
    {
        var auditTrail = _db.CreateAuditTrail();
        var cloudEvent = CreateCloudEvent();
        cloudEvent["customkey"] = "customvalue";
        cloudEvent["anotherkey"] = "anothervalue";

        await auditTrail.AppendAsync(cloudEvent, TestContext.Current.CancellationToken);

        using var ctx = _db.CreateContext();
        var entry = await ctx.AuditTrailEntries
            .Include(e => e.Attributes)
            .FirstOrDefaultAsync(e => e.EventId == cloudEvent.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(entry);
        Assert.Equal(2, entry.Attributes.Count);
        Assert.Contains(entry.Attributes, a => a.Key == "customkey" && a.Value == "customvalue");
        Assert.Contains(entry.Attributes, a => a.Key == "anotherkey" && a.Value == "anothervalue");
    }

    [Fact]
    public async Task Should_ReturnEntriesInChronologicalOrder()
    {
        var auditTrail = _db.CreateAuditTrail();
        var now = DateTimeOffset.UtcNow;

        await auditTrail.AppendAsync(CreateCloudEvent(time: now.AddMinutes(-10)), TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(CreateCloudEvent(time: now.AddMinutes(-5)), TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(CreateCloudEvent(time: now), TestContext.Current.CancellationToken);

        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(3, results.Count);
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Timestamp <= results[i].Timestamp);
    }

    [Fact]
    public async Task Should_StoreEventJson_InEventData()
    {
        var auditTrail = _db.CreateAuditTrail();
        var cloudEvent = CreateCloudEvent();

        await auditTrail.AppendAsync(cloudEvent, TestContext.Current.CancellationToken);

        using var ctx = _db.CreateContext();
        var entry = await ctx.AuditTrailEntries.FirstOrDefaultAsync(e => e.EventId == cloudEvent.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(entry);
        Assert.NotNull(entry.EventData);
        Assert.Contains("OrderId", entry.EventData);
        Assert.Contains("123", entry.EventData);
    }

    [Fact]
    public async Task Should_ReturnEmpty_WhenNoMatchingEvents()
    {
        var auditTrail = _db.CreateAuditTrail();

        var query = new AuditTrailStreamQuery { EventType = "nonexistent.type" };
        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Empty(results);
    }

    [Fact]
    public async Task Should_CombineMultipleFilters()
    {
        var auditTrail = _db.CreateAuditTrail();

        await auditTrail.AppendAsync(CreateCloudEvent(type: "order.created", subject: "order-1"), TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(CreateCloudEvent(type: "order.created", subject: "order-2"), TestContext.Current.CancellationToken);
        await auditTrail.AppendAsync(CreateCloudEvent(type: "order.shipped", subject: "order-1"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { EventType = "order.created", Subject = "order-1" };
        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("order.created", results[0].EventType);
        Assert.Equal("order-1", results[0].Subject);
    }

    [Fact]
    public async Task Should_StoreMultipleEvents_Sequentially()
    {
        var auditTrail = _db.CreateAuditTrail();

        for (int i = 0; i < 10; i++)
        {
            await auditTrail.AppendAsync(CreateCloudEvent(type: $"event.type.{i}"), TestContext.Current.CancellationToken);
        }

        var results = new List<DbAuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task Should_StoreEntry_WithAllFieldsPopulated()
    {
        var auditTrail = _db.CreateAuditTrail();
        var cloudEvent = new CloudEvent
        {
            Id = "test-event-id",
            Type = "com.example.order.created",
            Source = new Uri("https://example.com/orders"),
            Subject = "order-123",
            Time = new DateTimeOffset(2026, 5, 15, 14, 30, 0, TimeSpan.Zero),
            DataContentType = "application/json",
            DataSchema = new Uri("https://schemas.example.com/order.json"),
            Data = JsonSerializer.Serialize(new { OrderId = 123, Amount = 99.99 })
        };

        await auditTrail.AppendAsync(cloudEvent, TestContext.Current.CancellationToken);

        using var ctx = _db.CreateContext();
        var entry = await ctx.AuditTrailEntries
            .Include(e => e.Attributes)
            .FirstOrDefaultAsync(e => e.EventId == "test-event-id", TestContext.Current.CancellationToken);

        Assert.NotNull(entry);
        Assert.Equal("test-event-id", entry.EventId);
        Assert.Equal("com.example.order.created", entry.EventType);
        Assert.Equal("https://example.com/orders", entry.Source);
        Assert.Equal("order-123", entry.Subject);
        Assert.Equal("application/json", entry.DataContentType);
        Assert.Equal("https://schemas.example.com/order.json", entry.DataSchema);
        Assert.NotNull(entry.EventData);
        Assert.True(entry.StoredAt > DateTimeOffset.MinValue);
    }
}
