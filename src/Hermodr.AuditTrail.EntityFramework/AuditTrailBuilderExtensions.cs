using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for configuring the audit trail with an Entity Framework Core
/// storage backend.
/// </summary>
public static class AuditTrailBuilderExtensions
{
    /// <summary>
    /// Configures the audit trail to use Entity Framework Core as the storage backend.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="AuditTrailBuilder"/> instance to extend.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="DbContextOptionsBuilder"/> for
    /// the <see cref="AuditTrailDbContext"/>.
    /// </param>
    /// <param name="lifetime">
    /// The service lifetime for the repository registration. Defaults to <see cref="ServiceLifetime.Scoped"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="AuditTrailBuilder"/> instance for further configuration chaining.
    /// </returns>
    public static AuditTrailBuilder UseEntityFramework(
        this AuditTrailBuilder builder,
        Action<DbContextOptionsBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContext<AuditTrailDbContext>(configure ?? (_ => { }), lifetime);
        builder.Services.TryAdd(new ServiceDescriptor(typeof(EntityAuditTrail), typeof(EntityAuditTrail), lifetime));
        builder.Services.TryAdd(new ServiceDescriptor(typeof(IAuditTrailWriter), sp => sp.GetRequiredService<EntityAuditTrail>(), lifetime));
        builder.Services.TryAdd(new ServiceDescriptor(typeof(IAuditTrailReader<DbAuditTrailEntry>), sp => sp.GetRequiredService<EntityAuditTrail>(), lifetime));

        return builder;
    }
}
