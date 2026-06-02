using CloudNative.CloudEvents;

namespace Hermodr.AuditTrail.XUnit.Unit;

public class InMemoryAuditTrailTests
{
    private readonly InMemorySharedBag<AuditTrailEntry> _bag = new();
    private readonly InMemoryAuditTrail _auditTrail;

    public InMemoryAuditTrailTests()
    {
        _auditTrail = new InMemoryAuditTrail(_bag);
    }

    [Fact]
    public async Task AppendAsync_ShouldAddEntryToBag()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        await _auditTrail.AppendAsync(cloudEvent, cancellationToken: cancellationToken);

        var entries = _bag.GetAll();
        Assert.Single(entries);
        Assert.Equal("test-id", entries[0].EventId);
        Assert.Equal("test-type", entries[0].EventType);
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnAllEntries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cloudEvent1 = new CloudEvent
        {
            Id = "event-1",
            Type = "type-1",
            Source = new Uri("https://test.com")
        };
        var cloudEvent2 = new CloudEvent
        {
            Id = "event-2",
            Type = "type-2",
            Source = new Uri("https://test.com")
        };

        await _auditTrail.AppendAsync(cloudEvent1, cancellationToken: cancellationToken);
        await _auditTrail.AppendAsync(cloudEvent2, cancellationToken: cancellationToken);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(cancellationToken: cancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task ReadAsync_WithEventTypeFilter_ShouldReturnMatchingEntries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cloudEvent1 = new CloudEvent
        {
            Id = "event-1",
            Type = "order.created",
            Source = new Uri("https://test.com")
        };
        var cloudEvent2 = new CloudEvent
        {
            Id = "event-2",
            Type = "order.shipped",
            Source = new Uri("https://test.com")
        };

        await _auditTrail.AppendAsync(cloudEvent1, cancellationToken: cancellationToken);
        await _auditTrail.AppendAsync(cloudEvent2, cancellationToken: cancellationToken);

        var query = new AuditTrailStreamQuery { EventType = "order.created" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, cancellationToken: cancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("order.created", results[0].EventType);
    }

    [Fact]
    public async Task ReadAsync_WithSourceFilter_ShouldReturnMatchingEntries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cloudEvent1 = new CloudEvent
        {
            Id = "event-1",
            Type = "type-1",
            Source = new Uri("https://service-a.com/")
        };
        var cloudEvent2 = new CloudEvent
        {
            Id = "event-2",
            Type = "type-2",
            Source = new Uri("https://service-b.com/")
        };

        await _auditTrail.AppendAsync(cloudEvent1, cancellationToken: cancellationToken);
        await _auditTrail.AppendAsync(cloudEvent2, cancellationToken: cancellationToken);

        var query = new AuditTrailStreamQuery { Source = "https://service-a.com/" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(query, cancellationToken: cancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("https://service-a.com/", results[0].Source);
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnEntriesInChronologicalOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cloudEvent1 = new CloudEvent
        {
            Id = "event-1",
            Type = "type-1",
            Source = new Uri("https://test.com"),
            Time = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var cloudEvent2 = new CloudEvent
        {
            Id = "event-2",
            Type = "type-2",
            Source = new Uri("https://test.com"),
            Time = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        await _auditTrail.AppendAsync(cloudEvent1, cancellationToken: cancellationToken);
        await _auditTrail.AppendAsync(cloudEvent2, cancellationToken: cancellationToken);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in _auditTrail.ReadAsync(cancellationToken: cancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Timestamp < results[1].Timestamp);
    }
}
