using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// A builder for configuring the delivery log storage backend and error handling.
/// </summary>
public sealed class DeliveryLogBuilder
{
    internal DeliveryLogBuilder(IServiceCollection services, string publisherName = "")
    {
        Services = services;
        PublisherName = publisherName;
    }

    /// <summary>
    /// Gets the collection of services used to configure the delivery log.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the name of the publisher pipeline slot associated with this builder.
    /// </summary>
    public string PublisherName { get; }

    /// <summary>
    /// Configures the delivery log to use a custom store type as the storage backend.
    /// </summary>
    /// <typeparam name="TStore">
    /// The type of the custom store, which must implement <see cref="IEventPublishDeliveryLog"/>.
    /// </typeparam>
    /// <param name="lifetime">
    /// The service lifetime for the store registration. Defaults to <see cref="ServiceLifetime.Singleton"/>.
    /// </param>
    /// <returns>
    /// This builder instance for further configuration chaining.
    /// </returns>
    public DeliveryLogBuilder UseStore<TStore>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TStore : class, IEventPublishDeliveryLog
    {
        Services.Replace(ServiceDescriptor.Describe(
            typeof(IEventPublishDeliveryLog),
            typeof(TStore),
            lifetime));
        return this;
    }

    /// <summary>
    /// Configures the delivery log to use a pre-configured store instance as the storage backend.
    /// </summary>
    /// <param name="store">
    /// The store instance to use as the storage backend.
    /// </param>
    /// <returns>
    /// This builder instance for further configuration chaining.
    /// </returns>
    public DeliveryLogBuilder UseStore(IEventPublishDeliveryLog store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Services.Replace(ServiceDescriptor.Singleton(store));
        return this;
    }

    /// <summary>
    /// Registers a <see cref="DeliveryLogPublishErrorHandler"/> as the error handler for the
    /// publisher pipeline, so that failed deliveries are recorded automatically.
    /// </summary>
    /// <returns>
    /// This builder instance for further configuration chaining.
    /// </returns>
    public DeliveryLogBuilder UseErrorHandler()
    {
        Services.AddKeyedSingleton<IEventPublishErrorHandler>(
            PublisherName,
            (sp, _) => new DeliveryLogPublishErrorHandler(
                sp.GetRequiredService<IEventPublishDeliveryLog>(),
                sp.GetService<IEventSystemTime>()));
        return this;
    }
}
