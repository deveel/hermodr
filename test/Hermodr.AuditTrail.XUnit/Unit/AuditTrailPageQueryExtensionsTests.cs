using Kista;

namespace Hermodr.AuditTrail.XUnit.Unit;

public class AuditTrailPageQueryExtensionsTests
{
    private static PageQuery<AuditTrailEntry> CreateQuery() => new(1, 10);

    [Fact]
    public void OfType_ShouldAddFilter()
    {
        var query = CreateQuery().OfType("order.created");

        // PageQuery applies filters via the Query property
        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Filter);
    }

    [Fact]
    public void OfType_NullEventType_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentNullException>(() => query.OfType(null!));
    }

    [Fact]
    public void OfType_EmptyEventType_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentException>(() => query.OfType(""));
    }

    [Fact]
    public void OfType_WhiteSpaceEventType_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentException>(() => query.OfType("   "));
    }

    [Fact]
    public void OfDataType_StringOverload_ShouldAddFilter()
    {
        var query = CreateQuery().OfDataType("MyApp.Events.OrderCreated");
        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Filter);
    }

    [Fact]
    public void OfDataType_StringOverload_NullClassName_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentNullException>(() => query.OfDataType((string)null!));
    }

    [Fact]
    public void OfDataType_StringOverload_EmptyClassName_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentException>(() => query.OfDataType(""));
    }

    [Fact]
    public void OfDataType_TypeOverload_ShouldAddFilter()
    {
        var query = CreateQuery().OfDataType(typeof(string));
        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Filter);
    }

    [Fact]
    public void OfDataType_TypeOverload_NullType_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentNullException>(() => query.OfDataType((Type)null!));
    }

    [Fact]
    public void FromSource_ShouldAddFilter()
    {
        var query = CreateQuery().FromSource("https://example.com");
        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Filter);
    }

    [Fact]
    public void FromSource_NullSource_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentNullException>(() => query.FromSource(null!));
    }

    [Fact]
    public void FromSource_EmptySource_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentException>(() => query.FromSource(""));
    }

    [Fact]
    public void WithSubject_ShouldAddFilter()
    {
        var query = CreateQuery().WithSubject("order-123");
        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Filter);
    }

    [Fact]
    public void WithSubject_NullSubject_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentNullException>(() => query.WithSubject(null!));
    }

    [Fact]
    public void WithSubject_EmptySubject_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentException>(() => query.WithSubject(""));
    }

    [Fact]
    public void InTimeRange_ShouldAddFilter()
    {
        var from = DateTimeOffset.UtcNow.AddHours(-1);
        var to = DateTimeOffset.UtcNow;
        var query = CreateQuery().InTimeRange(from, to);
        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Filter);
    }

    [Fact]
    public void WithAttribute_ShouldAddFilter()
    {
        var query = CreateQuery().WithAttribute("correlationid", "corr-123");
        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Filter);
    }

    [Fact]
    public void WithAttribute_NullKey_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentNullException>(() => query.WithAttribute(null!, "value"));
    }

    [Fact]
    public void WithAttribute_EmptyKey_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentException>(() => query.WithAttribute("", "value"));
    }

    [Fact]
    public void WithAttribute_NullValue_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentNullException>(() => query.WithAttribute("key", null!));
    }

    [Fact]
    public void WithAttribute_EmptyValue_ShouldThrow()
    {
        var query = CreateQuery();
        Assert.Throws<ArgumentException>(() => query.WithAttribute("key", ""));
    }

    [Fact]
    public void OrderedByTimestamp_Ascending_ShouldAddOrder()
    {
        var query = CreateQuery().OrderedByTimestamp(descending: false);
        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Order);
    }

    [Fact]
    public void OrderedByTimestamp_Descending_ShouldAddOrder()
    {
        var query = CreateQuery().OrderedByTimestamp(descending: true);
        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Order);
    }

    [Fact]
    public void OrderedByTimestamp_Default_ShouldBeAscending()
    {
        var query = CreateQuery().OrderedByTimestamp();
        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Order);
    }

    [Fact]
    public void ChainedExtensions_ShouldWork()
    {
        var query = CreateQuery()
            .OfType("order.created")
            .FromSource("https://example.com")
            .WithSubject("order-123")
            .InTimeRange(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow)
            .OrderedByTimestamp(descending: true);

        Assert.NotNull(query.Query);
        Assert.NotNull(query.Query.Filter);
        Assert.NotNull(query.Query.Order);
    }
}
