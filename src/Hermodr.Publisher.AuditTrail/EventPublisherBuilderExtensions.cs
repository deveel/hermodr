using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Provides extension methods for adding the audit trail channel to an
/// <see cref="EventPublisherBuilder"/>.
/// </summary>
public static class EventPublisherBuilderExtensions
{
    /// <summary>
    /// Adds the audit trail channel that records ALL published events.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="EventPublisherBuilder"/> to configure.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the audit trail storage backend.
    /// </param>
    /// <returns>
    /// The same <see cref="EventPublisherBuilder"/> instance for further chaining.
    /// </returns>
    public static EventPublisherBuilder AddAuditTrail(
        this EventPublisherBuilder builder,
        Action<AuditTrailBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var auditBuilder = new AuditTrailBuilder(builder.Services);
        configure?.Invoke(auditBuilder);

        return builder.AddChannel<AuditTrailPublishChannel>();
    }

    /// <summary>
    /// Adds a typed audit trail channel that records only events whose data class
    /// is <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The annotated event data class this channel is keyed against.
    /// </typeparam>
    /// <param name="builder">
    /// The <see cref="EventPublisherBuilder"/> to configure.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the audit trail storage backend.
    /// </param>
    /// <returns>
    /// The same <see cref="EventPublisherBuilder"/> instance for further chaining.
    /// </returns>
    public static EventPublisherBuilder AddAuditTrail<TEvent>(
        this EventPublisherBuilder builder,
        Action<AuditTrailBuilder>? configure = null)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        var auditBuilder = new AuditTrailBuilder(builder.Services);
        configure?.Invoke(auditBuilder);

        return builder.AddChannel<AuditTrailPublishChannel<TEvent>, TEvent>();
    }

    /// <summary>
    /// Adds a named audit trail channel that records ALL published events.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="EventPublisherBuilder"/> to configure.
    /// </param>
    /// <param name="channelName">
    /// The logical name for the audit trail channel.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the audit trail storage backend.
    /// </param>
    /// <returns>
    /// The same <see cref="EventPublisherBuilder"/> instance for further chaining.
    /// </returns>
    public static EventPublisherBuilder AddAuditTrail(
        this EventPublisherBuilder builder,
        string channelName,
        Action<AuditTrailBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        var auditBuilder = new AuditTrailBuilder(builder.Services);
        configure?.Invoke(auditBuilder);

        return builder.AddChannel<AuditTrailPublishChannel>(channelName: channelName);
    }

    /// <summary>
    /// Adds a named typed audit trail channel that records only events whose data class
    /// is <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The annotated event data class this channel is keyed against.
    /// </typeparam>
    /// <param name="builder">
    /// The <see cref="EventPublisherBuilder"/> to configure.
    /// </param>
    /// <param name="channelName">
    /// The logical name for the audit trail channel.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to configure the audit trail storage backend.
    /// </param>
    /// <returns>
    /// The same <see cref="EventPublisherBuilder"/> instance for further chaining.
    /// </returns>
    public static EventPublisherBuilder AddAuditTrail<TEvent>(
        this EventPublisherBuilder builder,
        string channelName,
        Action<AuditTrailBuilder>? configure = null)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        var auditBuilder = new AuditTrailBuilder(builder.Services);
        configure?.Invoke(auditBuilder);

        return builder.AddChannel<AuditTrailPublishChannel<TEvent>, TEvent>(channelName: channelName);
    }
}
