using Kista;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for configuring the delivery log with an Entity Framework Core
/// storage backend.
/// </summary>
public static class DeliveryLogBuilderExtensions
{
    /// <summary>
    /// Configures the delivery log to use Entity Framework Core as the storage backend.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="DeliveryLogBuilder"/> instance to extend.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="DbContextOptionsBuilder"/> for
    /// the <see cref="DeliveryLogDbContext"/>.
    /// </param>
    /// <param name="lifetime">
    /// The service lifetime for the repository registration. Defaults to <see cref="ServiceLifetime.Scoped"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="DeliveryLogBuilder"/> instance for further configuration chaining.
    /// </returns>
    public static DeliveryLogBuilder UseEntityFramework(
        this DeliveryLogBuilder builder,
        Action<DbContextOptionsBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRepositoryContext()
            .UseEntityFramework<DeliveryLogDbContext>(b => b
                .ConfigureDbContext(configure ?? (_ => { }))
                .WithLifetime(lifetime))
            .AddRepository<EntityEventDeliveryLogRepository>(lifetime);

        builder.Services.TryAddSingleton<IEventDeliveryRecordFactory<DbEventDeliveryRecord>, EventDeliveryRecordFactory>();
        return builder.UseStore<EntityEventDeliveryLogRepository>(lifetime);
    }
}
