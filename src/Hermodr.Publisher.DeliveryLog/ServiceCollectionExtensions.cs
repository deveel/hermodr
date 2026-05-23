using Deveel.Data;
using Hermodr;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for registering the delivery log services
/// into an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the delivery log services to the service collection, including an in-memory
    /// storage backend as the default.
    /// </summary>
    /// <param name="services">
    /// The service collection to add the delivery log services to.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the delivery log storage backend.
    /// </param>
    /// <returns>
    /// The same service collection instance for further chaining.
    /// </returns>
    public static IServiceCollection AddDeliveryLog(
        this IServiceCollection services,
        Action<DeliveryLogBuilder>? configure = null)
    {
        services.TryAddSingleton<InMemoryEventDeliveryLogRepository>();
        services.TryAddSingleton<IEventDeliveryLogRepository>(
            sp => sp.GetRequiredService<InMemoryEventDeliveryLogRepository>());
        services.TryAddSingleton<IEventPublishDeliveryLog>(
            sp => sp.GetRequiredService<InMemoryEventDeliveryLogRepository>());

        if (configure != null)
        {
            var builder = new DeliveryLogBuilder(services);
            configure(builder);
        }

        return services;
    }
}
