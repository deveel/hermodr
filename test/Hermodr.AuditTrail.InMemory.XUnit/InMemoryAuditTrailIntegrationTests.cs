using System.Text.Json;
using CloudNative.CloudEvents;

namespace Hermodr;

[Trait("Category", "Integration")]
[Trait("Feature", "AuditTrail")]
[Trait("Layer", "Infrastructure")]
public class InMemoryAuditTrailIntegrationTests
{
    private readonly InMemorySharedBag<AuditTrailEntry> _bag = new();
    private readonly InMemoryAuditTrail _auditTrail;

    public InMemoryAuditTrailIntegrationTests()
    {
        _auditTrail = new InMemoryAuditTrail(_bag);
    }

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
    public async Task Should_AppendEvent_ToSharedBag()
    {
        var cloudEvent = CreateCloudEvent();

        await _auditTrail.AppendAsync(cloudEvent, TestContext.Current.CancellationToken);

        var entries = _bag.GetAll();
        Assert.Single(entries);
        Assert.Equal(cloudEvent.Id, entries[0].EventId);
        Assert.Equal(cloudEvent.Type, entries[0].EventType);
    }

    [Fact]
    public async Task Should_ReadAllEntries_FromSharedBag()
    {
        await _auditTrail.AppendAsync(CreateCloudEvent(type: "event.type.1"), TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(CreateCloudEvent(type: "event.type.2"), TestContext.Current.CancellationToken);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Should_FilterByEventType()
    {
        await _auditTrail.AppendAsync(CreateCloudEvent(type: "order.created"), TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(CreateCloudEvent(type: "order.shipped"), TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(CreateCloudEvent(type: "order.created"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { EventType = "order.created" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.Equal("order.created", e.EventType));
    }

    [Fact]
    public async Task Should_FilterBySource()
    {
        await _auditTrail.AppendAsync(CreateCloudEvent(source: "https://service-a.com/"), TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(CreateCloudEvent(source: "https://service-b.com/"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { Source = "https://service-a.com/" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("https://service-a.com/", results[0].Source);
    }

    [Fact]
    public async Task Should_FilterBySubject()
    {
        await _auditTrail.AppendAsync(CreateCloudEvent(subject: "order-123"), TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(CreateCloudEvent(subject: "order-456"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { Subject = "order-123" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("order-123", results[0].Subject);
    }

    [Fact]
    public async Task Should_FilterByTimeRange()
    {
        var now = DateTimeOffset.UtcNow;
        await _auditTrail.AppendAsync(CreateCloudEvent(time: now.AddHours(-5)), TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(CreateCloudEvent(time: now.AddHours(-1)), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { From = now.AddHours(-6), To = now.AddHours(-2) };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
    }

    [Fact]
    public async Task Should_FilterByEventDataClassName()
    {
        var entry1 = AuditTrailEntry.FromCloudEvent(CreateCloudEvent(), dataClassName: "MyApp.Events.OrderCreatedEvent");
        var entry2 = AuditTrailEntry.FromCloudEvent(CreateCloudEvent(), dataClassName: "MyApp.Events.OrderShippedEvent");

        _bag.Add(entry1);
        _bag.Add(entry2);

        var query = new AuditTrailStreamQuery { EventDataClassName = "MyApp.Events.OrderCreatedEvent" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("MyApp.Events.OrderCreatedEvent", results[0].EventDataClassName);
    }

    [Fact]
    public async Task Should_FilterByExtensionAttributes()
    {
        var cloudEvent1 = CreateCloudEvent();
        cloudEvent1["correlationid"] = "corr-123";
        var cloudEvent2 = CreateCloudEvent();

        await _auditTrail.AppendAsync(cloudEvent1, TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(cloudEvent2, TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery
        {
            Attributes = new Dictionary<string, string> { ["correlationid"] = "corr-123" }
        };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
    }

    [Fact]
    public async Task Should_StoreExtensionAttributes()
    {
        var cloudEvent = CreateCloudEvent();
        cloudEvent["customkey"] = "customvalue";
        cloudEvent["anotherkey"] = "anothervalue";

        await _auditTrail.AppendAsync(cloudEvent, TestContext.Current.CancellationToken);

        var entry = _bag.GetAll().First();
        Assert.NotNull(entry.ExtensionAttributes);
        Assert.Equal(2, entry.ExtensionAttributes.Count);
        Assert.Equal("customvalue", entry.ExtensionAttributes["customkey"]);
        Assert.Equal("anothervalue", entry.ExtensionAttributes["anotherkey"]);
    }

    [Fact]
    public async Task Should_ReturnEntriesInChronologicalOrder()
    {
        var now = DateTimeOffset.UtcNow;
        await _auditTrail.AppendAsync(CreateCloudEvent(time: now.AddMinutes(-10)), TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(CreateCloudEvent(time: now.AddMinutes(-5)), TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(CreateCloudEvent(time: now), TestContext.Current.CancellationToken);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
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
        var cloudEvent = CreateCloudEvent();

        await _auditTrail.AppendAsync(cloudEvent, TestContext.Current.CancellationToken);

        var entry = _bag.GetAll().First();
        Assert.NotNull(entry.EventData);
        Assert.Contains("OrderId", entry.EventData);
        Assert.Contains("123", entry.EventData);
    }

    [Fact]
    public async Task Should_ReturnEmpty_WhenNoMatchingEvents()
    {
        var query = new AuditTrailStreamQuery { EventType = "nonexistent.type" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Empty(results);
    }

    [Fact]
    public async Task Should_CombineMultipleFilters()
    {
        await _auditTrail.AppendAsync(CreateCloudEvent(type: "order.created", subject: "order-1"), TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(CreateCloudEvent(type: "order.created", subject: "order-2"), TestContext.Current.CancellationToken);
        await _auditTrail.AppendAsync(CreateCloudEvent(type: "order.shipped", subject: "order-1"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { EventType = "order.created", Subject = "order-1" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
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
        for (int i = 0; i < 10; i++)
        {
            await _auditTrail.AppendAsync(CreateCloudEvent(type: $"event.type.{i}"), TestContext.Current.CancellationToken);
        }

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task Should_StoreEntry_WithAllFieldsPopulated()
    {
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

        await _auditTrail.AppendAsync(cloudEvent, TestContext.Current.CancellationToken);

        var entry = _bag.GetAll().First();
        Assert.Equal("test-event-id", entry.EventId);
        Assert.Equal("com.example.order.created", entry.EventType);
        Assert.Equal("https://example.com/orders", entry.Source);
        Assert.Equal("order-123", entry.Subject);
        Assert.Equal("application/json", entry.DataContentType);
        Assert.Equal("https://schemas.example.com/order.json", entry.DataSchema);
        Assert.NotNull(entry.EventData);
        Assert.True(entry.StoredAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task Should_ShareData_BetweenWriterAndReader()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var writer = new InMemoryAuditTrail(bag);
        var reader = new InMemoryAuditTrail(bag);

        await writer.AppendAsync(CreateCloudEvent(type: "event.1"), TestContext.Current.CancellationToken);
        await writer.AppendAsync(CreateCloudEvent(type: "event.2"), TestContext.Current.CancellationToken);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in reader.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Should_MultipleFilters_ShouldReturnEmpty_WhenNoMatch()
    {
        await _auditTrail.AppendAsync(CreateCloudEvent(type: "order.created", subject: "order-1"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { EventType = "order.created", Subject = "order-999" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Empty(results);
    }

    [Fact]
    public async Task Should_Entry_HasStoredAtTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        await _auditTrail.AppendAsync(CreateCloudEvent(), TestContext.Current.CancellationToken);
        var after = DateTimeOffset.UtcNow;

        var entry = _bag.GetAll().First();
        Assert.True(entry.StoredAt >= before);
        Assert.True(entry.StoredAt <= after);
    }
}
