using CloudNative.CloudEvents;
using Microsoft.Extensions.Options;

namespace Hermodr.Publisher.AuditTrail.XUnit.Unit;

public class AuditTrailPublishChannelGenericTests
{
    private readonly InMemorySharedBag<AuditTrailEntry> _bag = new();
    private readonly InMemoryAuditTrail _auditTrail;

    public AuditTrailPublishChannelGenericTests()
    {
        _auditTrail = new InMemoryAuditTrail(_bag);
    }

    [Fact]
    public void ShouldImplementIEventPublishChannel()
    {
        var channel = new AuditTrailPublishChannel<TestEvent>(
            _auditTrail,
            Options.Create(new AuditTrailPublishOptions()));

        Assert.IsAssignableFrom<IEventPublishChannel<TestEvent>>(channel);
        Assert.IsAssignableFrom<IEventPublishChannel>(channel);
    }

    [Fact]
    public async Task PublishAsync_ShouldAppendEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var channel = new AuditTrailPublishChannel<TestEvent>(
            _auditTrail,
            Options.Create(new AuditTrailPublishOptions()));

        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        await channel.PublishAsync(cloudEvent, cancellationToken: cancellationToken);

        var entries = _bag.GetAll();
        Assert.Single(entries);
        Assert.Equal("test-id", entries[0].EventId);
    }

    [Fact]
    public async Task PublishAsync_WithBypassOptions_ShouldNotAppend()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var channel = new AuditTrailPublishChannel<TestEvent>(
            _auditTrail,
            Options.Create(new AuditTrailPublishOptions()));

        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        await channel.PublishAsync(cloudEvent, new BypassAuditTrailOptions(), cancellationToken: cancellationToken);

        var entries = _bag.GetAll();
        Assert.Empty(entries);
    }

    [Fact]
    public async Task PublishAsync_MultipleEvents_ShouldAppendAll()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var channel = new AuditTrailPublishChannel<TestEvent>(
            _auditTrail,
            Options.Create(new AuditTrailPublishOptions()));

        for (int i = 0; i < 5; i++)
        {
            await channel.PublishAsync(new CloudEvent
            {
                Id = $"event-{i}",
                Type = "test-type",
                Source = new Uri("https://test.com")
            }, cancellationToken: cancellationToken);
        }

        var entries = _bag.GetAll();
        Assert.Equal(5, entries.Count);
    }

    [Fact]
    public void Constructor_NullWriter_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AuditTrailPublishChannel<TestEvent>(null!, Options.Create(new AuditTrailPublishOptions())));
    }

    [Fact]
    public void Inheritance_ShouldPreserveBaseBehavior()
    {
        var channel = new AuditTrailPublishChannel<TestEvent>(
            _auditTrail,
            Options.Create(new AuditTrailPublishOptions()));

        // Should be assignable to base type
        Assert.IsAssignableFrom<AuditTrailPublishChannel>(channel);
    }

    private sealed class TestEvent { }
}
