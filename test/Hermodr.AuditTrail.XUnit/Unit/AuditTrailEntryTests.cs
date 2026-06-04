using System.Text.Json;
using CloudNative.CloudEvents;

namespace Hermodr.AuditTrail.XUnit.Unit;

public class AuditTrailEntryTests
{
    [Fact]
    public void FromCloudEvent_ShouldCreateEntry_FromValidCloudEvent()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-event-id",
            Type = "com.example.order.created",
            Source = new Uri("https://example.com/orders"),
            Time = DateTimeOffset.UtcNow,
            Subject = "order-123",
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { OrderId = 123, Amount = 99.99 })
        };

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);

        Assert.NotNull(entry);
        Assert.NotEmpty(entry.Id);
        Assert.Equal("test-event-id", entry.EventId);
        Assert.Equal("com.example.order.created", entry.EventType);
        Assert.Equal("https://example.com/orders", entry.Source);
        Assert.Equal("order-123", entry.Subject);
        Assert.Equal("application/json", entry.DataContentType);
        Assert.NotNull(entry.EventData);
        Assert.True(entry.StoredAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void FromCloudEvent_ShouldSetStoredAtToUtcNow()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        var before = DateTimeOffset.UtcNow;
        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);
        var after = DateTimeOffset.UtcNow;

        Assert.True(entry.StoredAt >= before);
        Assert.True(entry.StoredAt <= after);
    }

    [Fact]
    public void FromCloudEvent_ShouldCaptureExtensionAttributes()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };
        cloudEvent["customkey"] = "customvalue";

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);

        Assert.NotNull(entry.ExtensionAttributes);
        Assert.True(entry.ExtensionAttributes.TryGetValue("customkey", out var customValue));
        Assert.Equal("customvalue", customValue);
    }

    [Fact]
    public void FromCloudEvent_WithMinimalFields_ShouldWork()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);

        Assert.NotNull(entry);
        Assert.Null(entry.Subject);
        Assert.Null(entry.DataContentType);
        Assert.Null(entry.DataSchema);
        Assert.Null(entry.ExtensionAttributes);
    }
}
