using Microsoft.Extensions.DependencyInjection;

namespace Hermodr.Publisher.AuditTrail.XUnit.Unit;

public class EventPublisherBuilderExtensionsTests
{
    [Fact]
    public void AddAuditTrail_NullBuilder_ShouldThrowArgumentNullException()
    {
        EventPublisherBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddAuditTrail());
    }

    [Fact]
    public void AddAuditTrail_ShouldRegisterAuditTrailServices()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventPublisherOptions>();

        services.AddEventPublisher()
            .AddAuditTrail(audit => audit.UseInMemory());

        var bagDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(InMemorySharedBag<AuditTrailEntry>));
        Assert.NotNull(bagDescriptor);
    }

    [Fact]
    public void AddAuditTrail_WithConfigure_ShouldInvokeConfigureDelegate()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventPublisherOptions>();
        var configureCalled = false;

        services.AddEventPublisher()
            .AddAuditTrail(audit =>
            {
                configureCalled = true;
                audit.UseInMemory();
            });

        Assert.True(configureCalled);
    }

    [Fact]
    public void AddAuditTrail_Typed_ShouldRegisterAuditTrailServices()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventPublisherOptions>();

        services.AddEventPublisher()
            .AddAuditTrail<TestEvent>(audit => audit.UseInMemory());

        var bagDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(InMemorySharedBag<AuditTrailEntry>));
        Assert.NotNull(bagDescriptor);
    }

    [Fact]
    public void AddAuditTrail_Named_ShouldRegisterAuditTrailServices()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventPublisherOptions>();

        services.AddEventPublisher()
            .AddAuditTrail("audit-channel", audit => audit.UseInMemory());

        var bagDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(InMemorySharedBag<AuditTrailEntry>));
        Assert.NotNull(bagDescriptor);
    }

    [Fact]
    public void AddAuditTrail_Named_NullChannelName_ShouldThrow()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventPublisherOptions>();
        var builder = services.AddEventPublisher();

        Assert.Throws<ArgumentNullException>(() => builder.AddAuditTrail((string)null!));
    }

    [Fact]
    public void AddAuditTrail_Named_EmptyChannelName_ShouldThrow()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventPublisherOptions>();
        var builder = services.AddEventPublisher();

        Assert.Throws<ArgumentException>(() => builder.AddAuditTrail(""));
    }

    [Fact]
    public void AddAuditTrail_NamedTyped_ShouldRegisterAuditTrailServices()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventPublisherOptions>();

        services.AddEventPublisher()
            .AddAuditTrail<TestEvent>("audit-channel", audit => audit.UseInMemory());

        var bagDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(InMemorySharedBag<AuditTrailEntry>));
        Assert.NotNull(bagDescriptor);
    }

    [Fact]
    public void AddAuditTrail_NamedTyped_NullChannelName_ShouldThrow()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventPublisherOptions>();
        var builder = services.AddEventPublisher();

        Assert.Throws<ArgumentNullException>(() => builder.AddAuditTrail<TestEvent>((string)null!));
    }

    private sealed class TestEvent { }
}
