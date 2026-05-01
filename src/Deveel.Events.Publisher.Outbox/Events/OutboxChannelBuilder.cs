//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Data;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Deveel.Events;

/// <summary>
/// A fluent builder that configures the outbox publish channel and its supporting
/// services within an <see cref="EventPublisherBuilder"/> pipeline.
/// </summary>
/// <remarks>
/// The outbox message entity type is captured at construction time (via
/// <see cref="MessageType"/>) and used to validate repository and factory registrations
/// at runtime, removing the need for a generic type parameter on the builder itself.
/// </remarks>
public sealed class OutboxChannelBuilder
{
    private readonly EventPublisherBuilder _publisherBuilder;

    internal OutboxChannelBuilder(EventPublisherBuilder publisherBuilder, Type messageType)
    {
        if (!typeof(IOutboxMessage).IsAssignableFrom(messageType))
            throw new ArgumentException($"Message type must implement {typeof(IOutboxMessage).FullName}", nameof(messageType));
        
        MessageType = messageType;
        
        _publisherBuilder = publisherBuilder;
        
        var channelType = typeof(OutboxPublishChannel<>).MakeGenericType(messageType);
        
        // Register options and the channel immediately so defaults exist even when
        // no explicit Configure call follows.
        _publisherBuilder.Services.AddOptions<OutboxPublishOptions>();
        _publisherBuilder.AddChannel(channelType);
    }

    /// <summary>
    /// Gets the CLR type of the outbox message entity used by this channel.
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Gets the underlying <see cref="IServiceCollection"/> so that callers can
    /// register additional services without breaking the fluent chain.
    /// </summary>
    public IServiceCollection Services => _publisherBuilder.Services;

    // ─────────────────────────────────────────────────────────────────
    // Options
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures the <see cref="OutboxPublishOptions"/> for this channel using the
    /// supplied action.
    /// </summary>
    /// <param name="configure">
    /// A delegate that receives the options instance and applies the desired settings.
    /// </param>
    /// <returns>This builder instance for chaining.</returns>
    public OutboxChannelBuilder Configure(Action<OutboxPublishOptions> configure)
    {
        _publisherBuilder.Services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Binds the <see cref="OutboxPublishOptions"/> for this channel from the
    /// configuration section at <paramref name="sectionPath"/>.
    /// </summary>
    /// <param name="sectionPath">
    /// The dot-separated path of the configuration section (e.g.
    /// <c>"Events:Outbox"</c>) whose values are bound to
    /// <see cref="OutboxPublishOptions"/>.
    /// </param>
    /// <returns>This builder instance for chaining.</returns>
    public OutboxChannelBuilder Configure(string sectionPath)
    {
        _publisherBuilder.Services
            .AddOptions<OutboxPublishOptions>()
            .BindConfiguration(sectionPath);
        return this;
    }

    // ─────────────────────────────────────────────────────────────────
    // Repository
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a concrete <see cref="IOutboxMessageRepository{TMessage}"/>
    /// implementation that the channel uses to persist outbox messages.
    /// </summary>
    /// <typeparam name="TRepository">
    /// The repository implementation type.  It must implement
    /// <see cref="IOutboxMessageRepository{TMessage}"/>.
    /// </typeparam>
    /// <param name="lifetime">
    /// The DI service lifetime for the repository registration.
    /// Defaults to <see cref="ServiceLifetime.Scoped"/> because repositories
    /// typically wrap a scoped database context.
    /// </param>
    /// <returns>This builder instance for chaining.</returns>
    public OutboxChannelBuilder WithRepository<TRepository>(
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var repositoryType = typeof(IOutboxMessageRepository<>).MakeGenericType(MessageType);
        if (!repositoryType.IsAssignableFrom(typeof(TRepository)))
            throw new ArgumentException($"Repository type must implement {repositoryType.FullName}", nameof(TRepository));
        
        Services.AddRepository(typeof(TRepository), lifetime);
        Services.TryAdd(ServiceDescriptor.Describe(
            repositoryType,
            sp => sp.GetRequiredService<TRepository>(),
            lifetime));

        var managerType = typeof(OutboxMessageManager<>).MakeGenericType(MessageType);
        Services.TryAdd(ServiceDescriptor.Describe(managerType, managerType, lifetime));

        var validatorType = typeof(OutboxMessageValidator<>).MakeGenericType(MessageType);
        Services.TryAdd(ServiceDescriptor.Describe(validatorType, validatorType, lifetime));
        
        return this;
    }
    
    // ─────────────────────────────────────────────────────────────────
    // Message factory
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a concrete <see cref="IOutboxMessageFactory{TMessage}"/>
    /// implementation that the channel uses to create outbox message entities
    /// from <see cref="CloudNative.CloudEvents.CloudEvent"/> instances.
    /// </summary>
    /// <typeparam name="TFactory">
    /// The factory implementation type.  It must implement
    /// <see cref="IOutboxMessageFactory{TMessage}"/>.
    /// </typeparam>
    /// <param name="lifetime">
    /// The DI service lifetime for the factory registration.
    /// Defaults to <see cref="ServiceLifetime.Singleton"/> because factories
    /// are typically stateless.
    /// </param>
    /// <returns>This builder instance for chaining.</returns>
    public OutboxChannelBuilder WithFactory<TFactory>(
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        var factoryType = typeof(IOutboxMessageFactory<>).MakeGenericType(MessageType);
        if (!factoryType.IsAssignableFrom(typeof(TFactory)))
            throw new ArgumentException($"Factory type must implement {factoryType.FullName}", nameof(TFactory));
        
        Services.Add(new ServiceDescriptor(
            factoryType,
            typeof(TFactory),
            lifetime));
        return this;
    }

    // ─────────────────────────────────────────────────────────────────
    // Relay background service
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers the <see cref="OutboxRelayService{TMessage}"/> background service that
    /// periodically dequeues pending outbox messages and publishes their
    /// <see cref="CloudNative.CloudEvents.CloudEvent"/> payloads through the configured
    /// <see cref="IEventPublisher"/> pipeline using an
    /// <see cref="OutboxRelayPublishOptions"/> skip signal.
    /// </summary>
    /// <param name="configure">
    /// An optional delegate to configure <see cref="OutboxRelayOptions"/>
    /// (e.g., set the <see cref="OutboxRelayOptions.Interval"/> or
    /// <see cref="OutboxRelayOptions.TransportPublisherName"/>). When <c>null</c> the
    /// defaults are used (30-second polling interval, default publisher, no batch-size cap).
    /// </param>
    /// <returns>This builder instance for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The relay dispatches each dequeued event via <see cref="IEventPublisher.PublishEventAsync"/>
    /// with an <see cref="OutboxRelayPublishOptions"/> instance.  Any
    /// <see cref="OutboxPublishChannel{TMessage}"/> in the same pipeline detects this
    /// signal and skips persistence, preventing circular re-persistence in same-process
    /// deployments.  In the typical cross-process model (separate relay service) no outbox
    /// channel is registered and the signal is harmless.
    /// </para>
    /// <para>
    /// Because the <see cref="IOutboxMessageRepository{TMessage}"/> is typically scoped,
    /// the relay creates a fresh DI scope on every polling tick.
    /// </para>
    /// </remarks>
    public OutboxChannelBuilder WithRelay(Action<OutboxRelayOptions>? configure = null)
    {
        // Register relay options (may be called multiple times safely).
        Services.AddOptions<OutboxRelayOptions>();

        if (configure != null)
            Services.Configure<OutboxRelayOptions>(configure);

        // The relay MUST see transport-channel errors in order to mark messages as
        // Failed.  EventPublisher swallows channel errors by default, so we enable
        // ThrowOnErrors here.  This only affects publishers registered in the same
        // service collection that calls WithRelay(); cross-process relay applications
        // own their own container and configure their publisher independently.
        Services.PostConfigure<EventPublisherOptions>(opts => opts.ThrowOnErrors = true);

        // Register the processor as a singleton (it scopes internally per tick).
        // Also expose it via the public IOutboxRelayProcessor interface so callers
        // (e.g. tests) can resolve and invoke it without needing InternalsVisibleTo.
        Services.TryAddSingleton(typeof(OutboxRelayProcessor<>).MakeGenericType(MessageType));
        Services.TryAddSingleton(typeof(IOutboxRelayProcessor), typeof(OutboxRelayProcessor<>).MakeGenericType(MessageType));

        // Register the hosted service.
        // AddHostedService<T> is sugar for AddSingleton<IHostedService, T>(); we replicate
        // that here using the runtime-constructed closed generic type.
        Services.AddSingleton(typeof(IHostedService), typeof(OutboxRelayService<>).MakeGenericType(MessageType));
        
        return this;
    }

    /// <summary>
    /// Registers the <see cref="OutboxRelayService{TMessage}"/> background service and
    /// binds <see cref="OutboxRelayOptions"/> from the configuration section at
    /// <paramref name="sectionPath"/>.
    /// </summary>
    /// <param name="sectionPath">
    /// The dot-separated path of the configuration section (e.g.
    /// <c>"Events:OutboxRelay"</c>) whose values are bound to
    /// <see cref="OutboxRelayOptions"/>.
    /// </param>
    /// <returns>This builder instance for chaining.</returns>
    public OutboxChannelBuilder WithRelay(string sectionPath)
    {
        Services.AddOptions<OutboxRelayOptions>()
            .BindConfiguration(sectionPath);

        return WithRelay();
    }
}