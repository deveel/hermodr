using Microsoft.Extensions.DependencyInjection;

namespace Hermodr.AuditTrail.InMemory.XUnit.Unit;

public class InMemoryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInMemoryAuditTrail_ShouldRegisterWriter()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAuditTrail();
        var provider = services.BuildServiceProvider();

        var writer = provider.GetService<IAuditTrailWriter>();
        Assert.NotNull(writer);
        Assert.IsType<InMemoryAuditTrail>(writer);
    }

    [Fact]
    public void AddInMemoryAuditTrail_ShouldRegisterReader()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAuditTrail();
        var provider = services.BuildServiceProvider();

        var reader = provider.GetService<IAuditTrailReader<AuditTrailEntry>>();
        Assert.NotNull(reader);
        Assert.IsType<InMemoryAuditTrail>(reader);
    }

    [Fact]
    public void AddInMemoryAuditTrail_ShouldRegisterSharedBag()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAuditTrail();
        var provider = services.BuildServiceProvider();

        var bag = provider.GetService<InMemorySharedBag<AuditTrailEntry>>();
        Assert.NotNull(bag);
    }

    [Fact]
    public void AddInMemoryAuditTrail_CalledTwice_ShouldNotRegisterDuplicates()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAuditTrail();
        services.AddInMemoryAuditTrail();

        var descriptors = services.Where(d => d.ServiceType == typeof(IAuditTrailWriter)).ToList();
        Assert.Single(descriptors);
    }

    [Fact]
    public void AddAuditTrailQuerying_ShouldRegisterReader()
    {
        var services = new ServiceCollection();
        services.AddAuditTrailQuerying();
        var provider = services.BuildServiceProvider();

        var reader = provider.GetService<IAuditTrailReader<AuditTrailEntry>>();
        Assert.NotNull(reader);
    }

    [Fact]
    public void AddAuditTrailQuerying_ShouldNotRegisterWriter()
    {
        var services = new ServiceCollection();
        services.AddAuditTrailQuerying();
        var provider = services.BuildServiceProvider();

        var writer = provider.GetService<IAuditTrailWriter>();
        Assert.Null(writer);
    }

    [Fact]
    public void AddAuditTrailQuerying_CalledTwice_ShouldNotRegisterDuplicates()
    {
        var services = new ServiceCollection();
        services.AddAuditTrailQuerying();
        services.AddAuditTrailQuerying();

        var descriptors = services.Where(d => d.ServiceType == typeof(IAuditTrailReader<AuditTrailEntry>)).ToList();
        Assert.Single(descriptors);
    }

    [Fact]
    public void AddInMemoryAuditTrail_WriterAndReader_ShouldBeSameInstance()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAuditTrail();
        var provider = services.BuildServiceProvider();

        var writer = provider.GetRequiredService<IAuditTrailWriter>();
        var reader = provider.GetRequiredService<IAuditTrailReader<AuditTrailEntry>>();

        Assert.Same(writer, reader);
    }

    [Fact]
    public async Task AddInMemoryAuditTrail_Integration_ShouldWriteAndRead()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAuditTrail();
        var provider = services.BuildServiceProvider();

        var writer = provider.GetRequiredService<IAuditTrailWriter>();
        var reader = provider.GetRequiredService<IAuditTrailReader<AuditTrailEntry>>();

        var cloudEvent = new CloudNative.CloudEvents.CloudEvent
        {
            Id = "test-id",
            Type = "test-type",
            Source = new Uri("https://test.com")
        };

        await writer.AppendAsync(cloudEvent);

        var entries = new List<AuditTrailEntry>();
        await foreach (var entry in reader.ReadAsync())
        {
            entries.Add(entry);
        }

        Assert.Single(entries);
        Assert.Equal("test-id", entries[0].EventId);
    }
}
