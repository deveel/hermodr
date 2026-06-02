using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermodr.AuditTrail.XUnit.Unit;

public class AuditTrailPublishChannelTests
{
    private readonly InMemorySharedBag<AuditTrailEntry> _bag = new();
    private readonly InMemoryAuditTrail _auditTrail;
    private readonly AuditTrailPublishChannel _channel;

    public AuditTrailPublishChannelTests()
    {
        _auditTrail = new InMemoryAuditTrail(_bag);
        _channel = new AuditTrailPublishChannel(
            _auditTrail,
            Options.Create(new AuditTrailPublishOptions()));
    }

    [Fact]
    public async Task PublishAsync_ShouldAppendEventToAuditTrail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        await _channel.PublishAsync(cloudEvent, cancellationToken: cancellationToken);

        var entries = _bag.GetAll();
        Assert.Single(entries);
        Assert.Equal("test-id", entries[0].EventId);
    }

    [Fact]
    public async Task PublishAsync_WithBypassOptions_ShouldSkipRecording()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        var bypassOptions = new BypassAuditTrailOptions();
        await _channel.PublishAsync(cloudEvent, bypassOptions, cancellationToken: cancellationToken);

        var entries = _bag.GetAll();
        Assert.Empty(entries);
    }
}
