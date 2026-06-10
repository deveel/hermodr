using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for <see cref="DeliveryLogBuilder"/> to configure NDJson-based delivery log storage.
/// </summary>
public static class DeliveryLogBuilderExtensions
{
    /// <summary>
    /// Configures the delivery log to use an NDJson file-backed repository.
    /// </summary>
    /// <param name="builder">The <see cref="DeliveryLogBuilder"/> to extend.</param>
    /// <param name="configure">An optional delegate to configure <see cref="NdJsonDeliveryLogOptions"/>.</param>
    /// <returns>The builder instance for chaining.</returns>
    public static DeliveryLogBuilder UseNDJson(this DeliveryLogBuilder builder, Action<NdJsonDeliveryLogOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<NdJsonDeliveryLogOptions>()
            .Configure(o => configure?.Invoke(o));
        builder.Services.Replace(ServiceDescriptor.Singleton<IEventPublishDeliveryLog,
            NdJsonEventDeliveryLogRepository>());
        return builder;
    }
}
