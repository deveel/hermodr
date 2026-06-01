using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for registering the Entity Framework Core audit trail services
/// into an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Entity Framework Core audit trail services to the service collection,
    /// including the writer, reader, and DbContext. Uses <c>TryAdd</c> for shared services
    /// to avoid duplicates when the publisher is also registered.
    /// </summary>
    /// <param name="services">
    /// The service collection to add the services to.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="DbContextOptionsBuilder"/> for
    /// the <see cref="AuditTrailDbContext"/>.
    /// </param>
    /// <param name="lifetime">
    /// The service lifetime for the repository registration. Defaults to <see cref="ServiceLifetime.Scoped"/>.
    /// </param>
    /// <returns>
    /// The same service collection instance for further chaining.
    /// </returns>
    public static IServiceCollection AddEntityFrameworkAuditTrail(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        services.AddDbContext<AuditTrailDbContext>(configure ?? (_ => { }), lifetime);
        services.TryAdd(new ServiceDescriptor(typeof(EntityAuditTrail), typeof(EntityAuditTrail), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IAuditTrailWriter), sp => sp.GetRequiredService<EntityAuditTrail>(), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IAuditTrailReader<DbAuditTrailEntry>), sp => sp.GetRequiredService<EntityAuditTrail>(), lifetime));

        return services;
    }

    /// <summary>
    /// Adds the audit trail querying infrastructure to the service collection.
    /// This registers the <see cref="IAuditTrailReader{TEntry}"/> and supporting services.
    /// Uses <c>TryAdd</c> for shared services so that publisher and reader can coexist
    /// in the same application without duplicate registrations.
    /// </summary>
    /// <param name="services">
    /// The service collection to add the services to.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="DbContextOptionsBuilder"/> for
    /// the <see cref="AuditTrailDbContext"/>.
    /// </param>
    /// <param name="lifetime">
    /// The service lifetime for the reader registration. Defaults to <see cref="ServiceLifetime.Scoped"/>.
    /// </param>
    /// <returns>
    /// The same service collection instance for further chaining.
    /// </returns>
    public static IServiceCollection AddAuditTrailQuerying(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        // Only add DbContext if not already registered
        if (!services.Any(d => d.ServiceType == typeof(DbContextOptions<AuditTrailDbContext>)))
        {
            services.AddDbContext<AuditTrailDbContext>(configure ?? (_ => { }), lifetime);
        }

        services.TryAdd(new ServiceDescriptor(typeof(EntityAuditTrail), typeof(EntityAuditTrail), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IAuditTrailReader<DbAuditTrailEntry>), sp => sp.GetRequiredService<EntityAuditTrail>(), lifetime));

        return services;
    }
}
