using Microsoft.Extensions.DependencyInjection;

namespace Hermodr.AuditTrail.NDJson.XUnit.Unit;

public class NDJsonServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNDJsonAuditTrail_ShouldRegisterWriter()
    {
        var services = new ServiceCollection();
        services.AddNDJsonAuditTrail();
        var provider = services.BuildServiceProvider();

        var writer = provider.GetService<IAuditTrailWriter>();
        Assert.NotNull(writer);
        Assert.IsType<NdJsonAuditTrail>(writer);
    }

    [Fact]
    public void AddNDJsonAuditTrail_ShouldRegisterReader()
    {
        var services = new ServiceCollection();
        services.AddNDJsonAuditTrail();
        var provider = services.BuildServiceProvider();

        var reader = provider.GetService<IAuditTrailReader<AuditTrailEntry>>();
        Assert.NotNull(reader);
        Assert.IsType<NdJsonAuditTrail>(reader);
    }

    [Fact]
    public void AddNDJsonAuditTrail_CalledTwice_ShouldNotRegisterDuplicates()
    {
        var services = new ServiceCollection();
        services.AddNDJsonAuditTrail();
        services.AddNDJsonAuditTrail();

        var descriptors = services.Where(d => d.ServiceType == typeof(IAuditTrailWriter)).ToList();
        Assert.Single(descriptors);
    }

    [Fact]
    public void AddNDJsonAuditTrailQuerying_ShouldRegisterReader()
    {
        var services = new ServiceCollection();
        services.AddNDJsonAuditTrailQuerying();
        var provider = services.BuildServiceProvider();

        var reader = provider.GetService<IAuditTrailReader<AuditTrailEntry>>();
        Assert.NotNull(reader);
    }

    [Fact]
    public void AddNDJsonAuditTrailQuerying_ShouldNotRegisterWriter()
    {
        var services = new ServiceCollection();
        services.AddNDJsonAuditTrailQuerying();
        var provider = services.BuildServiceProvider();

        var writer = provider.GetService<IAuditTrailWriter>();
        Assert.Null(writer);
    }

    [Fact]
    public void AddNDJsonAuditTrailQuerying_CalledTwice_ShouldNotRegisterDuplicates()
    {
        var services = new ServiceCollection();
        services.AddNDJsonAuditTrailQuerying();
        services.AddNDJsonAuditTrailQuerying();

        var descriptors = services.Where(d => d.ServiceType == typeof(IAuditTrailReader<AuditTrailEntry>)).ToList();
        Assert.Single(descriptors);
    }

    [Fact]
    public void AddNDJsonAuditTrail_WriterAndReader_ShouldBeSameInstance()
    {
        var services = new ServiceCollection();
        services.AddNDJsonAuditTrail();
        var provider = services.BuildServiceProvider();

        var writer = provider.GetRequiredService<IAuditTrailWriter>();
        var reader = provider.GetRequiredService<IAuditTrailReader<AuditTrailEntry>>();

        Assert.Same(writer, reader);
    }

    [Fact]
    public void AddNDJsonAuditTrail_ShouldRegisterDefaultIFileSystem()
    {
        var services = new ServiceCollection();
        services.AddNDJsonAuditTrail();
        var provider = services.BuildServiceProvider();

        var fs = provider.GetService<System.IO.Abstractions.IFileSystem>();
        Assert.NotNull(fs);
    }

    [Fact]
    public void AddNDJsonAuditTrail_ShouldNotOverrideExistingIFileSystem()
    {
        var services = new ServiceCollection();
        var customFs = new System.IO.Abstractions.FileSystem();
        services.AddSingleton<System.IO.Abstractions.IFileSystem>(customFs);
        services.AddNDJsonAuditTrail();
        var provider = services.BuildServiceProvider();

        var fs = provider.GetRequiredService<System.IO.Abstractions.IFileSystem>();
        Assert.Same(customFs, fs);
    }

    [Fact]
    public async Task AddNDJsonAuditTrail_Integration_ShouldWriteAndRead()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ndjson-di-{Guid.NewGuid():N}");
        try
        {
            var services = new ServiceCollection();
            services.AddNDJsonAuditTrail(o => o.DirectoryPath = dir);
            var provider = services.BuildServiceProvider();

            var writer = provider.GetRequiredService<IAuditTrailWriter>();
            var reader = provider.GetRequiredService<IAuditTrailReader<AuditTrailEntry>>();

            var cloudEvent = new CloudNative.CloudEvents.CloudEvent
            {
                Id = "test-id",
                Type = "test-type",
                Source = new Uri("https://test.com")
            };

            await writer.AppendAsync(cloudEvent, TestContext.Current.CancellationToken);

            var entries = new List<AuditTrailEntry>();
            await foreach (var entry in reader.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
            {
                entries.Add(entry);
            }

            Assert.Single(entries);
            Assert.Equal("test-id", entries[0].EventId);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); } catch { /* best-effort */ }
            }
        }
    }
}
