using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

public static class DeliveryLogBuilderExtensions
{
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
