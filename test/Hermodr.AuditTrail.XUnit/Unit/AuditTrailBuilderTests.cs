using Microsoft.Extensions.DependencyInjection;

namespace Hermodr.AuditTrail.XUnit.Unit;

public class AuditTrailBuilderTests
{
    [Fact]
    public void Constructor_NullServices_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditTrailBuilder(null!));
    }

    [Fact]
    public void Constructor_ValidServices_ShouldExposeServicesProperty()
    {
        var services = new ServiceCollection();
        var builder = new AuditTrailBuilder(services);
        Assert.Same(services, builder.Services);
    }
}
