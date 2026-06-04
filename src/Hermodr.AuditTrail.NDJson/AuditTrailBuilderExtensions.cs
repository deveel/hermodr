using System.IO.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for configuring the audit trail with a
/// newline-delimited JSON (NDJSON) file storage backend.
/// </summary>
public static class AuditTrailBuilderExtensions
{
    /// <summary>
    /// Configures the audit trail to use an NDJSON file storage backend.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="AuditTrailBuilder"/> to configure.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="NdJsonAuditTrailOptions"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="AuditTrailBuilder"/> instance for further configuration chaining.
    /// </returns>
    public static AuditTrailBuilder UseNDJson(
        this AuditTrailBuilder builder,
        Action<NdJsonAuditTrailOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<NdJsonAuditTrailOptions>();
        if (configure != null)
        {
            builder.Services.AddOptions<NdJsonAuditTrailOptions>().Configure(configure);
        }

        builder.Services.TryAddSingleton<IFileSystem>(_ => new FileSystem());
        builder.Services.TryAddSingleton<NdJsonAuditTrail>();
        builder.Services.TryAddSingleton<IAuditTrailWriter>(sp => sp.GetRequiredService<NdJsonAuditTrail>());
        builder.Services.TryAddSingleton<IAuditTrailReader<AuditTrailEntry>>(sp => sp.GetRequiredService<NdJsonAuditTrail>());

        return builder;
    }
}
