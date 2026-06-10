using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

public static class DeliveryLogBuilderExtensions
{
    public static DeliveryLogBuilder UseInMemory(this DeliveryLogBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<InMemoryEventDeliveryLogRepository>();
        builder.Services.Replace(ServiceDescriptor.Singleton<IEventPublishDeliveryLog>(
            sp => sp.GetRequiredService<InMemoryEventDeliveryLogRepository>()));
        return builder;
    }
}
