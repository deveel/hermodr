using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for <see cref="DeliveryLogBuilder"/> to configure in-memory delivery log storage.
/// </summary>
public static class DeliveryLogBuilderExtensions
{
    /// <summary>
    /// Configures the delivery log to use an in-memory repository.
    /// </summary>
    /// <param name="builder">The <see cref="DeliveryLogBuilder"/> to extend.</param>
    /// <returns>The builder instance for chaining.</returns>
    public static DeliveryLogBuilder UseInMemory(this DeliveryLogBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<InMemoryEventDeliveryLogRepository>();
        builder.Services.Replace(ServiceDescriptor.Singleton<IEventPublishDeliveryLog>(
            sp => sp.GetRequiredService<InMemoryEventDeliveryLogRepository>()));
        return builder;
    }
}
