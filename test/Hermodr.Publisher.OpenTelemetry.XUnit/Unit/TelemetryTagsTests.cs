namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "Diagnostics")]
public class TelemetryTagsTests
{
    [Fact]
    public void EventType_IsCorrect()
    {
        Assert.Equal("event.type", TelemetryTags.EventType);
    }

    [Fact]
    public void MessagingSystem_IsCorrect()
    {
        Assert.Equal("messaging.system", TelemetryTags.MessagingSystem);
    }

    [Fact]
    public void MessagingOperation_IsCorrect()
    {
        Assert.Equal("messaging.operation", TelemetryTags.MessagingOperation);
    }

    [Fact]
    public void MessagingDestination_IsCorrect()
    {
        Assert.Equal("messaging.destination", TelemetryTags.MessagingDestination);
    }

    [Fact]
    public void MessagingDestinationKind_IsCorrect()
    {
        Assert.Equal("messaging.destination_kind", TelemetryTags.MessagingDestinationKind);
    }

    [Fact]
    public void MessagingRabbitMqRoutingKey_IsCorrect()
    {
        Assert.Equal("messaging.rabbitmq.routing_key", TelemetryTags.MessagingRabbitMqRoutingKey);
    }

    [Fact]
    public void HttpRequestMethod_IsCorrect()
    {
        Assert.Equal("http.request.method", TelemetryTags.HttpRequestMethod);
    }

    [Fact]
    public void SubscriptionName_IsCorrect()
    {
        Assert.Equal("subscription.name", TelemetryTags.SubscriptionName);
    }

    [Fact]
    public void ChannelType_IsCorrect()
    {
        Assert.Equal("channel.type", TelemetryTags.ChannelType);
    }

    [Fact]
    public void MessageId_IsCorrect()
    {
        Assert.Equal("message.id", TelemetryTags.MessageId);
    }

    [Fact]
    public void Success_IsCorrect()
    {
        Assert.Equal("success", TelemetryTags.Success);
    }
}
