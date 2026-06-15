using CloudNative.CloudEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermodr.Publisher.AuditTrail.XUnit.Unit;

public class AuditTrailPublishChannelEdgeCaseTests
{
    private readonly InMemorySharedBag<AuditTrailEntry> _bag = new();
    private readonly InMemoryAuditTrail _auditTrail;

    public AuditTrailPublishChannelEdgeCaseTests()
    {
        _auditTrail = new InMemoryAuditTrail(_bag);
    }

    [Fact]
    public void Constructor_NullWriter_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AuditTrailPublishChannel(null!, Options.Create(new AuditTrailPublishOptions()), new ServiceCollection().BuildServiceProvider()));
    }

    [Fact]
    public async Task PublishCoreAsync_WriterThrows_ShouldPropagateException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var failingWriter = new FailingAuditTrailWriter();
        var channel = new AuditTrailPublishChannel(
            failingWriter,
            Options.Create(new AuditTrailPublishOptions()),
            new ServiceCollection().BuildServiceProvider());

        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.PublishAsync(cloudEvent, cancellationToken: cancellationToken));
    }

    [Fact]
    public async Task PublishCoreAsync_Success_ShouldAppendToWriter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var channel = new AuditTrailPublishChannel(
            _auditTrail,
            Options.Create(new AuditTrailPublishOptions()),
            new ServiceCollection().BuildServiceProvider());

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
    public async Task PublishCoreAsync_BypassOptions_ShouldNotAppend()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var channel = new AuditTrailPublishChannel(
            _auditTrail,
            Options.Create(new AuditTrailPublishOptions()),
            new ServiceCollection().BuildServiceProvider());

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
    public async Task PublishCoreAsync_AuditTrailPublishOptions_ShouldAppend()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var channel = new AuditTrailPublishChannel(
            _auditTrail,
            Options.Create(new AuditTrailPublishOptions()),
            new ServiceCollection().BuildServiceProvider());

        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        await channel.PublishAsync(cloudEvent, new AuditTrailPublishOptions(), cancellationToken: cancellationToken);

        var entries = _bag.GetAll();
        Assert.Single(entries);
    }

    [Fact]
    public async Task PublishCoreAsync_MultipleEvents_ShouldAppendAll()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var channel = new AuditTrailPublishChannel(
            _auditTrail,
            Options.Create(new AuditTrailPublishOptions()),
            new ServiceCollection().BuildServiceProvider());

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
    public void BypassAuditTrailOptions_ShouldInheritAuditTrailPublishOptions()
    {
        var options = new BypassAuditTrailOptions();
        Assert.IsAssignableFrom<AuditTrailPublishOptions>(options);
        Assert.IsAssignableFrom<EventPublishOptions>(options);
    }

    private sealed class FailingAuditTrailWriter : IAuditTrailWriter
    {
        public Task AppendAsync(CloudEvent @event, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated failure");
        }
    }
}
