using Deveel.Data;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Deveel.Events;

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
    /// Configures the delivery log to use an in-memory storage backend.
    /// </summary>
    /// <returns>
    /// This builder instance for further configuration chaining.
    /// </returns>
    public DeliveryLogBuilder UseInMemory()
    {
        Services.TryAddSingleton<InMemoryEventDeliveryLogRepository>();
        Services.Replace(ServiceDescriptor.Singleton<IEventDeliveryLogRepository>(
            sp => sp.GetRequiredService<InMemoryEventDeliveryLogRepository>()));
        Services.Replace(ServiceDescriptor.Singleton<IEventPublishDeliveryLog>(
            sp => sp.GetRequiredService<InMemoryEventDeliveryLogRepository>()));
        return this;
    }

    /// <summary>
    /// Configures the delivery log to use a newline-delimited JSON (NDJSON) file-based
    /// storage backend.
    /// </summary>
    /// <param name="configure">
    /// An optional delegate to configure the NDJSON storage options.
    /// </param>
    /// <returns>
    /// This builder instance for further configuration chaining.
    /// </returns>
    public DeliveryLogBuilder UseNDJson(Action<NdJsonDeliveryLogOptions>? configure = null)
    {
        Services.AddOptions<NdJsonDeliveryLogOptions>()
            .Configure(o => configure?.Invoke(o));
        Services.TryAddSingleton<NdJsonEventDeliveryLogRepository>();
        Services.Replace(ServiceDescriptor.Singleton<IEventDeliveryLogRepository>(
            sp => sp.GetRequiredService<NdJsonEventDeliveryLogRepository>()));
        Services.Replace(ServiceDescriptor.Singleton<IEventPublishDeliveryLog>(
            sp => sp.GetRequiredService<NdJsonEventDeliveryLogRepository>()));
        return this;
    }

    /// <summary>
    /// Configures the delivery log to use a custom repository type as the storage backend.
    /// </summary>
    /// <typeparam name="TStore">
    /// The type of the custom repository, which must implement <see cref="IEventDeliveryLogRepository"/>.
    /// </typeparam>
    /// <param name="lifetime">
    /// The service lifetime for the repository registration. Defaults to <see cref="ServiceLifetime.Singleton"/>.
    /// </param>
    /// <returns>
    /// This builder instance for further configuration chaining.
    /// </returns>
    public DeliveryLogBuilder UseStore<TStore>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TStore : class, IEventDeliveryLogRepository
    {
        Services.Add(new ServiceDescriptor(typeof(TStore), typeof(TStore), lifetime));
        Services.Replace(ServiceDescriptor.Describe(
            typeof(IEventDeliveryLogRepository),
            sp => sp.GetRequiredService<TStore>(),
            lifetime));
        Services.Replace(ServiceDescriptor.Describe(
            typeof(IEventPublishDeliveryLog),
            sp => sp.GetRequiredService<TStore>(),
            lifetime));
        return this;
    }

    /// <summary>
    /// Configures the delivery log to use a pre-configured repository instance as the storage backend.
    /// </summary>
    /// <param name="store">
    /// The repository instance to use as the storage backend.
    /// </param>
    /// <returns>
    /// This builder instance for further configuration chaining.
    /// </returns>
    public DeliveryLogBuilder UseStore(IEventDeliveryLogRepository store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Services.Replace(ServiceDescriptor.Singleton<IEventDeliveryLogRepository>(store));
        Services.Replace(ServiceDescriptor.Singleton<IEventPublishDeliveryLog>(store));
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
