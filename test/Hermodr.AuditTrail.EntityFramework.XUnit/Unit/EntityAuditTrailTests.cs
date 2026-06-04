using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hermodr.AuditTrail.EntityFramework.XUnit.Unit;

public class EntityAuditTrailTests
{
    [Fact]
    public void Constructor_NullContext_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new EntityAuditTrail(null!));
    }

    [Fact]
    public void ProviderName_ShouldReturnEntityFrameworkCore()
    {
        var fixture = new SqliteAuditTrailFixture();
        fixture.InitializeAsync().GetAwaiter().GetResult();

        var auditTrail = fixture.CreateAuditTrail();
        Assert.Equal("EntityFrameworkCore", auditTrail.ProviderName);

        fixture.DisposeAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void AddEntityFrameworkAuditTrail_ShouldRegisterAllServices()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkAuditTrail(options =>
            options.UseSqlite("Data Source=:memory:"));

        var provider = services.BuildServiceProvider();

        var writer = provider.GetService<IAuditTrailWriter>();
        var reader = provider.GetService<IAuditTrailReader<DbAuditTrailEntry>>();
        var auditTrail = provider.GetService<EntityAuditTrail>();

        Assert.NotNull(writer);
        Assert.NotNull(reader);
        Assert.NotNull(auditTrail);
        Assert.IsType<EntityAuditTrail>(writer);
        Assert.IsType<EntityAuditTrail>(reader);
    }

    [Fact]
    public void AddEntityFrameworkAuditTrail_CalledTwice_ShouldNotRegisterDuplicates()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkAuditTrail(options =>
            options.UseSqlite("Data Source=:memory:"));
        services.AddEntityFrameworkAuditTrail(options =>
            options.UseSqlite("Data Source=:memory:"));

        var writerDescriptors = services.Where(d => d.ServiceType == typeof(IAuditTrailWriter)).ToList();
        Assert.Single(writerDescriptors);
    }
    
    [Fact]
    public void AddAuditTrailQuerying_WhenDbContextAlreadyRegistered_ShouldNotReRegister()
    {
        var services = new ServiceCollection();

        // Register DbContext first (simulating publisher registration)
        services.AddDbContext<AuditTrailDbContext>(options =>
            options.UseSqlite("Data Source=:memory:"));

        var dbContextCountBefore = services.Count(d => d.ServiceType == typeof(DbContextOptions<AuditTrailDbContext>));

        services.AddEntityFrameworkAuditTrail(options =>
            options.UseSqlite("Data Source=:memory:"));

        var dbContextCountAfter = services.Count(d => d.ServiceType == typeof(DbContextOptions<AuditTrailDbContext>));

        Assert.Equal(dbContextCountBefore, dbContextCountAfter);
    }

    [Fact]
    public void AddAuditTrailQuerying_CalledTwice_ShouldNotRegisterDuplicates()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkAuditTrail(options =>
            options.UseSqlite("Data Source=:memory:"));
        services.AddEntityFrameworkAuditTrail(options =>
            options.UseSqlite("Data Source=:memory:"));

        var readerDescriptors = services.Where(d => d.ServiceType == typeof(IAuditTrailReader<DbAuditTrailEntry>)).ToList();
        Assert.Single(readerDescriptors);
    }
}
