//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// Extends the <see cref="EventPublisherBuilder"/> to add support for the Transactional
/// Outbox event publishing channel.
/// </summary>
public static class EventPublisherBuilderExtensions
{
    /// <summary>
    /// Adds an outbox publish channel for the given <typeparamref name="TMessage"/>
    /// entity type to the event publisher pipeline, returning a fluent
    /// <see cref="OutboxChannelBuilder{TMessage}"/> to continue configuration.
    /// </summary>
    /// <typeparam name="TMessage">
    /// The outbox message entity type that wraps a
    /// <see cref="CloudNative.CloudEvents.CloudEvent"/> for persistence.
    /// Must be a reference type and implement <see cref="IOutboxMessage"/>.
    /// </typeparam>
    /// <param name="builder">
    /// The <see cref="EventPublisherBuilder"/> to which the outbox channel is added.
    /// </param>
    /// <returns>
    /// An <see cref="OutboxChannelBuilder{TMessage}"/> that exposes further fluent
    /// methods for configuring options, the repository, and the message factory.
    /// </returns>
    /// <example>
    /// <code language="csharp">
    /// services.AddEventPublisher()
    ///     .AddOutbox&lt;MyOutboxMessage&gt;()
    ///     .Configure("Events:Outbox")
    ///     .WithRepository&lt;MyOutboxRepository&gt;()
    ///     .WithFactory&lt;MyOutboxMessageFactory&gt;();
    /// </code>
    /// </example>
    public static OutboxChannelBuilder<TMessage> AddOutbox<TMessage>(
        this EventPublisherBuilder builder)
        where TMessage : class, IOutboxMessage
    {
        return new OutboxChannelBuilder<TMessage>(builder);
    }

    /// <summary>
    /// Adds an outbox publish channel for the given <typeparamref name="TMessage"/>
    /// entity type to the event publisher pipeline and immediately applies the supplied
    /// options configuration action.
    /// </summary>
    /// <typeparam name="TMessage">
    /// The outbox message entity type.  Must be a reference type and implement
    /// <see cref="IOutboxMessage"/>.
    /// </typeparam>
    /// <param name="builder">
    /// The <see cref="EventPublisherBuilder"/> to which the outbox channel is added.
    /// </param>
    /// <param name="configure">
    /// A delegate that configures the <see cref="OutboxPublishOptions"/> for this channel.
    /// </param>
    /// <returns>
    /// An <see cref="OutboxChannelBuilder{TMessage}"/> that exposes further fluent
    /// methods for configuring the repository and the message factory.
    /// </returns>
    public static OutboxChannelBuilder<TMessage> AddOutbox<TMessage>(
        this EventPublisherBuilder builder,
        Action<OutboxPublishOptions> configure)
        where TMessage : class, IOutboxMessage
    {
        return new OutboxChannelBuilder<TMessage>(builder).Configure(configure);
    }

    /// <summary>
    /// Adds an outbox publish channel for the given <typeparamref name="TMessage"/>
    /// entity type to the event publisher pipeline and immediately binds
    /// <see cref="OutboxPublishOptions"/> from the configuration section at
    /// <paramref name="sectionPath"/>.
    /// </summary>
    /// <typeparam name="TMessage">
    /// The outbox message entity type.  Must be a reference type and implement
    /// <see cref="IOutboxMessage"/>.
    /// </typeparam>
    /// <param name="builder">
    /// The <see cref="EventPublisherBuilder"/> to which the outbox channel is added.
    /// </param>
    /// <param name="sectionPath">
    /// The dot-separated path of the configuration section (e.g. <c>"Events:Outbox"</c>)
    /// whose values are bound to <see cref="OutboxPublishOptions"/>.
    /// </param>
    /// <returns>
    /// An <see cref="OutboxChannelBuilder{TMessage}"/> that exposes further fluent
    /// methods for configuring the repository and the message factory.
    /// </returns>
    public static OutboxChannelBuilder<TMessage> AddOutbox<TMessage>(
        this EventPublisherBuilder builder,
        string sectionPath)
        where TMessage : class, IOutboxMessage
    {
        return new OutboxChannelBuilder<TMessage>(builder).Configure(sectionPath);
    }
}