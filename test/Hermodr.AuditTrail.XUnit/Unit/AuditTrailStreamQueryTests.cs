namespace Hermodr.AuditTrail.XUnit.Unit;

public class AuditTrailStreamQueryTests
{
    [Fact]
    public void DefaultValues_AllPropertiesNull()
    {
        var query = new AuditTrailStreamQuery();

        Assert.Null(query.EventType);
        Assert.Null(query.EventDataClassName);
        Assert.Null(query.Source);
        Assert.Null(query.Subject);
        Assert.Null(query.From);
        Assert.Null(query.To);
        Assert.Null(query.Attributes);
    }

    [Fact]
    public void Setters_ShouldStoreValues()
    {
        var from = DateTimeOffset.UtcNow.AddHours(-1);
        var to = DateTimeOffset.UtcNow;
        var attrs = new Dictionary<string, string> { ["key"] = "value" };

        var query = new AuditTrailStreamQuery
        {
            EventType = "order.created",
            EventDataClassName = "MyApp.Events.OrderCreated",
            Source = "https://example.com",
            Subject = "order-123",
            From = from,
            To = to,
            Attributes = attrs
        };

        Assert.Equal("order.created", query.EventType);
        Assert.Equal("MyApp.Events.OrderCreated", query.EventDataClassName);
        Assert.Equal("https://example.com", query.Source);
        Assert.Equal("order-123", query.Subject);
        Assert.Equal(from, query.From);
        Assert.Equal(to, query.To);
        Assert.Same(attrs, query.Attributes);
    }
}
