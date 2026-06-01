using System.Text.Json;
using CloudNative.CloudEvents;

namespace Hermodr.AuditTrail.XUnit.Unit;

public class AuditTrailEntryEdgeCaseTests
{
    [Fact]
    public void FromCloudEvent_NullCloudEvent_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AuditTrailEntry.FromCloudEvent(null!));
    }

    [Fact]
    public void FromCloudEvent_WithDataClassName_ShouldPopulateEventDataClassName()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent, dataClassName: "MyApp.Events.OrderCreated");

        Assert.Equal("MyApp.Events.OrderCreated", entry.EventDataClassName);
    }

    [Fact]
    public void FromCloudEvent_WithExplicitStoredAt_ShouldUseProvidedValue()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };
        var explicitTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent, storedAt: explicitTime);

        Assert.Equal(explicitTime, entry.StoredAt);
    }

    [Fact]
    public void FromCloudEvent_WithoutTime_ShouldFallbackToUtcNow()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };
        // Time is not set (null)

        var before = DateTimeOffset.UtcNow;
        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);
        var after = DateTimeOffset.UtcNow;

        Assert.True(entry.Timestamp >= before);
        Assert.True(entry.Timestamp <= after);
    }

    [Fact]
    public void FromCloudEvent_ShouldGenerateGuid16CharId()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);

        Assert.NotNull(entry.Id);
        Assert.Equal(32, entry.Id.Length); // GUID "N" format = 32 hex chars
        Assert.True(Guid.TryParse(entry.Id, out _));
    }

    [Fact]
    public void FromCloudEvent_WithNullSubject_ShouldSetNull()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);

        Assert.Null(entry.Subject);
    }

    [Fact]
    public void FromCloudEvent_WithDataSchema_ShouldPopulateDataSchema()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com"),
            DataSchema = new Uri("https://schemas.example.com/order.json")
        };

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);

        Assert.Equal("https://schemas.example.com/order.json", entry.DataSchema);
    }

    [Fact]
    public void FromCloudEvent_WithNullExtensionAttributes_ShouldSetNull()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);

        Assert.Null(entry.ExtensionAttributes);
    }

    [Fact]
    public void FromCloudEvent_WithMultipleExtensionAttributes_ShouldCaptureAll()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };
        cloudEvent["attr1"] = "value1";
        cloudEvent["attr2"] = "value2";
        cloudEvent["attr3"] = "value3";

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);

        Assert.NotNull(entry.ExtensionAttributes);
        Assert.Equal(3, entry.ExtensionAttributes.Count);
        Assert.Equal("value1", entry.ExtensionAttributes["attr1"]);
        Assert.Equal("value2", entry.ExtensionAttributes["attr2"]);
        Assert.Equal("value3", entry.ExtensionAttributes["attr3"]);
    }

    [Fact]
    public void FromCloudEvent_WithNullData_ShouldStillSerializeCloudEvent()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        var entry = AuditTrailEntry.FromCloudEvent(cloudEvent);

        // Even with null Data, the full CloudEvent envelope is serialized
        Assert.NotNull(entry.EventData);
    }

    [Fact]
    public void AuditTrailEntry_AllProperties_ShouldBeSettable()
    {
        var entry = new AuditTrailEntry
        {
            Id = "id",
            EventId = "event-id",
            EventType = "event-type",
            EventDataClassName = "class-name",
            Source = "source",
            Subject = "subject",
            Timestamp = DateTimeOffset.UtcNow,
            DataContentType = "content-type",
            DataSchema = "schema",
            EventData = "event-data",
            ExtensionAttributes = new Dictionary<string, string> { ["key"] = "value" },
            StoredAt = DateTimeOffset.UtcNow
        };

        Assert.Equal("id", entry.Id);
        Assert.Equal("event-id", entry.EventId);
        Assert.Equal("event-type", entry.EventType);
        Assert.Equal("class-name", entry.EventDataClassName);
        Assert.Equal("source", entry.Source);
        Assert.Equal("subject", entry.Subject);
        Assert.Equal("content-type", entry.DataContentType);
        Assert.Equal("schema", entry.DataSchema);
        Assert.Equal("event-data", entry.EventData);
        Assert.NotNull(entry.ExtensionAttributes);
    }
}
