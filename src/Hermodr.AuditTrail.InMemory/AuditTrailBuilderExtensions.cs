using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for configuring the audit trail with an in-memory
/// storage backend.
/// </summary>
public static class AuditTrailBuilderExtensions
{
    /// <summary>
    /// Configures the audit trail to use an in-memory storage backend.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="AuditTrailBuilder"/> to configure.
    /// </param>
    /// <returns>
    /// The same <see cref="AuditTrailBuilder"/> instance for further configuration chaining.
    /// </returns>
    public static AuditTrailBuilder UseInMemory(this AuditTrailBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<InMemorySharedBag<AuditTrailEntry>>();
        builder.Services.TryAddSingleton<InMemoryAuditTrail>();
        builder.Services.TryAddSingleton<IAuditTrailWriter>(sp => sp.GetRequiredService<InMemoryAuditTrail>());
        builder.Services.TryAddSingleton<IAuditTrailReader<AuditTrailEntry>>(sp => sp.GetRequiredService<InMemoryAuditTrail>());

        return builder;
    }
}
