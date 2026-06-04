using System.IO.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for registering the NDJSON audit trail services
/// into an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the NDJSON audit trail services to the service collection, including both
    /// the writer and reader. Registers an <see cref="IFileSystem"/> if one has not
    /// already been registered, so the storage backend can be swapped by replacing
    /// the <see cref="IFileSystem"/> registration (e.g., for Azure Blob Storage or
    /// Amazon S3). Uses <c>TryAdd</c> for shared services to avoid duplicates when
    /// the publisher is also registered.
    /// </summary>
    /// <param name="services">
    /// The service collection to add the services to.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="NdJsonAuditTrailOptions"/>.
    /// </param>
    /// <returns>
    /// The same service collection instance for further chaining.
    /// </returns>
    public static IServiceCollection AddNDJsonAuditTrail(
        this IServiceCollection services,
        Action<NdJsonAuditTrailOptions>? configure = null)
    {
        services.AddOptions<NdJsonAuditTrailOptions>();
        if (configure != null)
        {
            services.AddOptions<NdJsonAuditTrailOptions>().Configure(configure);
        }

        services.TryAddSingleton<IFileSystem>(_ => new FileSystem());
        services.TryAddSingleton<NdJsonAuditTrail>();
        services.TryAddSingleton<IAuditTrailWriter>(sp => sp.GetRequiredService<NdJsonAuditTrail>());
        services.TryAddSingleton<IAuditTrailReader<AuditTrailEntry>>(sp => sp.GetRequiredService<NdJsonAuditTrail>());

        return services;
    }

    /// <summary>
    /// Adds the NDJSON audit trail querying infrastructure to the service collection.
    /// This registers only the <see cref="IAuditTrailReader{TEntry}"/> without the writer.
    /// Registers an <see cref="IFileSystem"/> if one has not already been registered, so
    /// the storage backend can be swapped by replacing the <see cref="IFileSystem"/>
    /// registration. Uses <c>TryAdd</c> for shared services so that publisher and
    /// reader can coexist in the same application without duplicate registrations.
    /// </summary>
    /// <param name="services">
    /// The service collection to add the services to.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="NdJsonAuditTrailOptions"/>.
    /// </param>
    /// <returns>
    /// The same service collection instance for further chaining.
    /// </returns>
    public static IServiceCollection AddNDJsonAuditTrailQuerying(
        this IServiceCollection services,
        Action<NdJsonAuditTrailOptions>? configure = null)
    {
        services.AddOptions<NdJsonAuditTrailOptions>();
        if (configure != null)
        {
            services.AddOptions<NdJsonAuditTrailOptions>().Configure(configure);
        }

        services.TryAddSingleton<IFileSystem>(_ => new FileSystem());
        services.TryAddSingleton<NdJsonAuditTrail>();
        services.TryAddSingleton<IAuditTrailReader<AuditTrailEntry>>(sp => sp.GetRequiredService<NdJsonAuditTrail>());

        return services;
    }
}
