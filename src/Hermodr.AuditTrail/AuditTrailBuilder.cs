using Microsoft.Extensions.DependencyInjection;

namespace Hermodr;

/// <summary>
/// A builder for configuring the audit trail storage backend.
/// </summary>
public class AuditTrailBuilder
{
    /// <summary>
    /// Creates a new instance of <see cref="AuditTrailBuilder"/>.
    /// </summary>
    /// <param name="services">
    /// The service collection to configure.
    /// </param>
    public AuditTrailBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Gets the service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; }
}
