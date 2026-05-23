using Deveel.Data;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Deveel.Events;

/// <summary>
/// Provides extension methods for adding the delivery log feature to an
/// <see cref="EventPublisherBuilder"/>.
/// </summary>
public static class EventPublisherBuilderExtensions
{
    /// <summary>
    /// Adds the delivery log middleware and optional storage configuration to the publisher pipeline.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="EventPublisherBuilder"/> to configure.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the delivery log storage backend.
    /// </param>
    /// <returns>
    /// The same <see cref="EventPublisherBuilder"/> instance for further chaining.
    /// </returns>
    public static EventPublisherBuilder AddDeliveryLog(
        this EventPublisherBuilder builder,
        Action<DeliveryLogBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ServicesTryAddInMemory(builder.Services);

        builder.Use<DeliveryLogMiddleware>();

        var logBuilder = new DeliveryLogBuilder(builder.Services, builder.Name);
        configure?.Invoke(logBuilder);

        return builder;
    }

    private static void ServicesTryAddInMemory(IServiceCollection services)
    {
        services.TryAddSingleton<InMemoryEventDeliveryLogRepository>();
        services.TryAddSingleton<IEventDeliveryLogRepository>(
            sp => sp.GetRequiredService<InMemoryEventDeliveryLogRepository>());
        services.TryAddSingleton<IEventPublishDeliveryLog>(
            sp => sp.GetRequiredService<InMemoryEventDeliveryLogRepository>());
    }
}
