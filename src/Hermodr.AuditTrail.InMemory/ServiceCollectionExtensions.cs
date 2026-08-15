using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for registering the in-memory audit trail services
/// into an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory audit trail services to the service collection,
    /// including both the writer and reader. Uses the <c>TryAddSingleton&lt;TService&gt;</c>
    /// family of extension methods for shared services to avoid duplicates when the
    /// publisher is also registered.
    /// </summary>
    /// <param name="services">
    /// The service collection to add the services to.
    /// </param>
    /// <returns>
    /// The same service collection instance for further chaining.
    /// </returns>
    public static IServiceCollection AddInMemoryAuditTrail(this IServiceCollection services)
    {
        services.TryAddSingleton<InMemorySharedBag<AuditTrailEntry>>();
        services.TryAddSingleton<InMemoryAuditTrail>();
        services.TryAddSingleton<IAuditTrailWriter>(sp => sp.GetRequiredService<InMemoryAuditTrail>());
        services.TryAddSingleton<IAuditTrailReader<AuditTrailEntry>>(sp => sp.GetRequiredService<InMemoryAuditTrail>());

        return services;
    }

    /// <summary>
    /// Adds the in-memory audit trail querying infrastructure to the service collection.
    /// This registers only the <see cref="IAuditTrailReader{TEntry}"/> without the writer.
    /// Uses the <c>TryAddSingleton&lt;TService&gt;</c> family of extension methods for
    /// shared services so that publisher and reader can coexist in the same application
    /// without duplicate registrations.
    /// </summary>
    /// <param name="services">
    /// The service collection to add the services to.
    /// </param>
    /// <returns>
    /// The same service collection instance for further chaining.
    /// </returns>
    public static IServiceCollection AddAuditTrailQuerying(this IServiceCollection services)
    {
        services.TryAddSingleton<InMemorySharedBag<AuditTrailEntry>>();
        services.TryAddSingleton<InMemoryAuditTrail>();
        services.TryAddSingleton<IAuditTrailReader<AuditTrailEntry>>(sp => sp.GetRequiredService<InMemoryAuditTrail>());

        return services;
    }
}
