namespace Hermodr.AuditTrail.EntityFramework.XUnit.Unit;

public class DbAuditTrailEntryTests
{
    [Fact]
    public void FromEntry_NullEntry_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DbAuditTrailEntry.FromEntry(null!));
    }

    [Fact]
    public void FromEntry_WithNullExtensionAttributes_ShouldReturnEmptyAttributes()
    {
        var entry = new AuditTrailEntry
        {
            Id = "id-1",
            EventId = "event-1",
            EventType = "test.type",
            Source = "https://test.com",
            Timestamp = DateTimeOffset.UtcNow,
            StoredAt = DateTimeOffset.UtcNow
        };

        var dbEntry = DbAuditTrailEntry.FromEntry(entry);

        Assert.NotNull(dbEntry);
        Assert.Empty(dbEntry.Attributes);
    }

    [Fact]
    public void FromEntry_WithExtensionAttributes_ShouldMapAllAttributes()
    {
        var entry = new AuditTrailEntry
        {
            Id = "id-1",
            EventId = "event-1",
            EventType = "test.type",
            Source = "https://test.com",
            Timestamp = DateTimeOffset.UtcNow,
            StoredAt = DateTimeOffset.UtcNow,
            ExtensionAttributes = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };

        var dbEntry = DbAuditTrailEntry.FromEntry(entry);

        Assert.Equal(2, dbEntry.Attributes.Count);
        Assert.Contains(dbEntry.Attributes, a => a.Key == "key1" && a.Value == "value1");
        Assert.Contains(dbEntry.Attributes, a => a.Key == "key2" && a.Value == "value2");
    }

    [Fact]
    public void FromEntry_ShouldMapAllFields()
    {
        var entry = new AuditTrailEntry
        {
            Id = "id-1",
            EventId = "event-1",
            EventType = "test.type",
            EventDataClassName = "MyClass",
            Source = "https://test.com",
            Subject = "subject-1",
            Timestamp = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DataContentType = "application/json",
            DataSchema = "https://schema.com",
            EventData = "{\"key\":\"value\"}",
            StoredAt = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero)
        };

        var dbEntry = DbAuditTrailEntry.FromEntry(entry);

        Assert.Equal("id-1", dbEntry.Id);
        Assert.Equal("event-1", dbEntry.EventId);
        Assert.Equal("test.type", dbEntry.EventType);
        Assert.Equal("MyClass", dbEntry.EventDataClassName);
        Assert.Equal("https://test.com", dbEntry.Source);
        Assert.Equal("subject-1", dbEntry.Subject);
        Assert.Equal(entry.Timestamp, dbEntry.Timestamp);
        Assert.Equal("application/json", dbEntry.DataContentType);
        Assert.Equal("https://schema.com", dbEntry.DataSchema);
        Assert.Equal("{\"key\":\"value\"}", dbEntry.EventData);
        Assert.Equal(entry.StoredAt, dbEntry.StoredAt);
    }

    [Fact]
    public void ToEntry_ShouldMapAllFields()
    {
        var dbEntry = new DbAuditTrailEntry
        {
            Id = "id-1",
            EventId = "event-1",
            EventType = "test.type",
            EventDataClassName = "MyClass",
            Source = "https://test.com",
            Subject = "subject-1",
            Timestamp = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DataContentType = "application/json",
            DataSchema = "https://schema.com",
            EventData = "{\"key\":\"value\"}",
            StoredAt = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero),
            Attributes = new List<DbAuditTrailAttribute>
            {
                new() { Id = "attr-1", Key = "key1", Value = "value1" }
            }
        };

        var entry = dbEntry.ToEntry();

        Assert.Equal("id-1", entry.Id);
        Assert.Equal("event-1", entry.EventId);
        Assert.Equal("test.type", entry.EventType);
        Assert.Equal("MyClass", entry.EventDataClassName);
        Assert.Equal("https://test.com", entry.Source);
        Assert.Equal("subject-1", entry.Subject);
        Assert.Equal(dbEntry.Timestamp, entry.Timestamp);
        Assert.Equal("application/json", entry.DataContentType);
        Assert.Equal("https://schema.com", entry.DataSchema);
        Assert.Equal("{\"key\":\"value\"}", entry.EventData);
        Assert.Equal(dbEntry.StoredAt, entry.StoredAt);
    }

    [Fact]
    public void ToEntry_WithAttributes_ShouldMapExtensionAttributes()
    {
        var dbEntry = new DbAuditTrailEntry
        {
            Id = "id-1",
            EventId = "event-1",
            EventType = "test.type",
            Source = "https://test.com",
            Timestamp = DateTimeOffset.UtcNow,
            StoredAt = DateTimeOffset.UtcNow,
            Attributes = new List<DbAuditTrailAttribute>
            {
                new() { Id = "a1", Key = "key1", Value = "value1" },
                new() { Id = "a2", Key = "key2", Value = "value2" }
            }
        };

        var entry = dbEntry.ToEntry();

        Assert.NotNull(entry.ExtensionAttributes);
        Assert.Equal(2, entry.ExtensionAttributes.Count);
        Assert.Equal("value1", entry.ExtensionAttributes["key1"]);
        Assert.Equal("value2", entry.ExtensionAttributes["key2"]);
    }

    [Fact]
    public void ToEntry_WithEmptyAttributes_ShouldReturnEmptyDictionary()
    {
        var dbEntry = new DbAuditTrailEntry
        {
            Id = "id-1",
            EventId = "event-1",
            EventType = "test.type",
            Source = "https://test.com",
            Timestamp = DateTimeOffset.UtcNow,
            StoredAt = DateTimeOffset.UtcNow,
            Attributes = new List<DbAuditTrailAttribute>()
        };

        var entry = dbEntry.ToEntry();

        Assert.NotNull(entry.ExtensionAttributes);
        Assert.Empty(entry.ExtensionAttributes);
    }

    [Fact]
    public void ToEntry_WithNullAttributes_ShouldReturnNullExtensionAttributes()
    {
        var dbEntry = new DbAuditTrailEntry
        {
            Id = "id-1",
            EventId = "event-1",
            EventType = "test.type",
            Source = "https://test.com",
            Timestamp = DateTimeOffset.UtcNow,
            StoredAt = DateTimeOffset.UtcNow,
            Attributes = null!
        };

        var entry = dbEntry.ToEntry();

        Assert.Null(entry.ExtensionAttributes);
    }

    [Fact]
    public void ExtensionAttributes_ViaInterface_ShouldReturnDictionary()
    {
        var dbEntry = new DbAuditTrailEntry
        {
            Id = "id-1",
            EventId = "event-1",
            EventType = "test.type",
            Source = "https://test.com",
            Timestamp = DateTimeOffset.UtcNow,
            StoredAt = DateTimeOffset.UtcNow,
            Attributes = new List<DbAuditTrailAttribute>
            {
                new() { Id = "a1", Key = "k", Value = "v" }
            }
        };

        IAuditTrailEntry interfaceEntry = dbEntry;
        var attrs = interfaceEntry.ExtensionAttributes;

        Assert.NotNull(attrs);
        Assert.Single(attrs);
        Assert.Equal("v", attrs["k"]);
    }

    [Fact]
    public void FromEntry_ToEntry_Roundtrip_ShouldPreserveAllFields()
    {
        var original = new AuditTrailEntry
        {
            Id = "id-1",
            EventId = "event-1",
            EventType = "test.type",
            EventDataClassName = "MyClass",
            Source = "https://test.com",
            Subject = "subject-1",
            Timestamp = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DataContentType = "application/json",
            DataSchema = "https://schema.com",
            EventData = "{\"key\":\"value\"}",
            StoredAt = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero),
            ExtensionAttributes = new Dictionary<string, string> { ["k"] = "v" }
        };

        var dbEntry = DbAuditTrailEntry.FromEntry(original);
        var roundtripped = dbEntry.ToEntry();

        Assert.Equal(original.Id, roundtripped.Id);
        Assert.Equal(original.EventId, roundtripped.EventId);
        Assert.Equal(original.EventType, roundtripped.EventType);
        Assert.Equal(original.EventDataClassName, roundtripped.EventDataClassName);
        Assert.Equal(original.Source, roundtripped.Source);
        Assert.Equal(original.Subject, roundtripped.Subject);
        Assert.Equal(original.Timestamp, roundtripped.Timestamp);
        Assert.Equal(original.DataContentType, roundtripped.DataContentType);
        Assert.Equal(original.DataSchema, roundtripped.DataSchema);
        Assert.Equal(original.EventData, roundtripped.EventData);
        Assert.Equal(original.StoredAt, roundtripped.StoredAt);
        Assert.NotNull(roundtripped.ExtensionAttributes);
        Assert.Equal("v", roundtripped.ExtensionAttributes["k"]);
    }
}
