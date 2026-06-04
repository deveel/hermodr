using Microsoft.Extensions.DependencyInjection;

namespace Hermodr.AuditTrail.NDJson.XUnit.Unit;

public class NDJsonAuditTrailBuilderExtensionsTests
{
    [Fact]
    public void UseNDJson_ShouldRegisterWriter()
    {
        var services = new ServiceCollection();
        var builder = new AuditTrailBuilder(services);
        builder.UseNDJson();
        var provider = services.BuildServiceProvider();

        var writer = provider.GetService<IAuditTrailWriter>();
        Assert.NotNull(writer);
        Assert.IsType<NdJsonAuditTrail>(writer);
    }

    [Fact]
    public void UseNDJson_ShouldRegisterReader()
    {
        var services = new ServiceCollection();
        var builder = new AuditTrailBuilder(services);
        builder.UseNDJson();
        var provider = services.BuildServiceProvider();

        var reader = provider.GetService<IAuditTrailReader<AuditTrailEntry>>();
        Assert.NotNull(reader);
    }

    [Fact]
    public void UseNDJson_ShouldApplyConfigureDelegate()
    {
        var services = new ServiceCollection();
        var builder = new AuditTrailBuilder(services);
        builder.UseNDJson(o =>
        {
            o.DirectoryPath = "/custom/audit";
            o.MaxFileSizeBytes = 1024;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NdJsonAuditTrailOptions>>().Value;

        Assert.Equal("/custom/audit", options.DirectoryPath);
        Assert.Equal(1024, options.MaxFileSizeBytes);
    }

    [Fact]
    public void UseNDJson_ShouldThrow_OnNullBuilder()
    {
        Assert.Throws<ArgumentNullException>(() => ((AuditTrailBuilder)null!).UseNDJson());
    }
}
