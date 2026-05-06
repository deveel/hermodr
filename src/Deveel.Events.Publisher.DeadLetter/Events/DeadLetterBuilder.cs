//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Deveel.Events;

/// <summary>
/// A fluent builder that configures dead-letter handling for an
/// <see cref="EventPublisherBuilder"/> pipeline.
/// </summary>
public sealed class DeadLetterBuilder
{
    private readonly EventPublisherBuilder _publisherBuilder;

    internal DeadLetterBuilder(EventPublisherBuilder publisherBuilder)
    {
        _publisherBuilder = publisherBuilder ?? throw new ArgumentNullException(nameof(publisherBuilder));
    }

    /// <summary>
    /// Gets the underlying <see cref="IServiceCollection"/>.
    /// </summary>
    public IServiceCollection Services => _publisherBuilder.Services;

    /// <summary>
    /// Registers a dead-letter handler of type <typeparamref name="THandler"/> for the
    /// current publisher pipeline.
    /// </summary>
    public DeadLetterBuilder UseHandler<THandler>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where THandler : class, IEventDeadLetterHandler
    {
        return lifetime switch
        {
            ServiceLifetime.Singleton => Register(ServiceDescriptor.KeyedSingleton<IEventPublishErrorHandler>(
                _publisherBuilder.Name,
                (sp, _) => new DeadLetterPublishErrorHandler(
                    (IEventDeadLetterHandler)ActivatorUtilities.CreateInstance(sp, typeof(THandler))))),
            ServiceLifetime.Scoped => Register(ServiceDescriptor.KeyedScoped<IEventPublishErrorHandler>(
                _publisherBuilder.Name,
                (sp, _) => new DeadLetterPublishErrorHandler(
                    (IEventDeadLetterHandler)ActivatorUtilities.CreateInstance(sp, typeof(THandler))))),
            ServiceLifetime.Transient => Register(ServiceDescriptor.KeyedTransient<IEventPublishErrorHandler>(
                _publisherBuilder.Name,
                (sp, _) => new DeadLetterPublishErrorHandler(
                    (IEventDeadLetterHandler)ActivatorUtilities.CreateInstance(sp, typeof(THandler))))),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported service lifetime.")
        };
    }

    /// <summary>
    /// Registers a specific dead-letter handler instance for the current publisher pipeline.
    /// </summary>
    public DeadLetterBuilder UseHandler(IEventDeadLetterHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Register(ServiceDescriptor.KeyedSingleton<IEventPublishErrorHandler>(
            _publisherBuilder.Name,
            (_, _) => new DeadLetterPublishErrorHandler(handler)));
    }

    /// <summary>
    /// Registers an asynchronous dead-letter callback for the current publisher pipeline.
    /// </summary>
    public DeadLetterBuilder UseHandler(Func<DeadLetterContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return UseHandler(new DelegatedEventDeadLetterHandler(handler));
    }

    /// <summary>
    /// Registers a synchronous dead-letter callback for the current publisher pipeline.
    /// </summary>
    public DeadLetterBuilder UseHandler(Action<DeadLetterContext> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return UseHandler(context =>
        {
            handler(context);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Registers a repository-backed dead-letter message store implementation.
    /// This enables persistent dead-letter capture independently of replay.
    /// </summary>
    public DeadLetterBuilder UseRepository<TMessage, TStore>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TMessage : class, IDeadLetterMessage
        where TStore : class, IDeadLetterMessageStore
    {
        EnsureStorageHandlerRegistered();
        Services.Add(new ServiceDescriptor(typeof(TStore), typeof(TStore), lifetime));
        Services.Replace(ServiceDescriptor.Describe(
            typeof(IDeadLetterMessageStore),
            sp => sp.GetRequiredService<TStore>(),
            lifetime));
        return this;
    }

    /// <summary>
    /// Registers a dead-letter message factory implementation.
    /// </summary>
    public DeadLetterBuilder WithFactory<TMessage, TFactory>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TMessage : class, IDeadLetterMessage
        where TFactory : class, IDeadLetterMessageFactory<TMessage>
    {
        EnsureStorageHandlerRegistered();
        Services.Replace(new ServiceDescriptor(typeof(IDeadLetterMessageFactory<TMessage>), typeof(TFactory), lifetime));
        Services.Replace(ServiceDescriptor.Describe(
            typeof(IDeadLetterMessageFactory),
            sp => new DeadLetterMessageFactoryAdapter<TMessage>(sp.GetRequiredService<IDeadLetterMessageFactory<TMessage>>()),
            lifetime));
        return this;
    }

    /// <summary>
    /// Registers replay services backed by the default in-memory dead-letter store.
    /// </summary>
    public DeadLetterReplayBuilder WithReplay(Action<DeadLetterReplayOptions>? configure = null)
        => new DeadLetterReplayBuilder(_publisherBuilder).ConfigureReplay(configure);

    /// <summary>
    /// Registers a background replay worker backed by the default in-memory dead-letter store.
    /// </summary>
    public DeadLetterReplayBuilder WithReplayWorker(Action<DeadLetterReplayOptions>? configure = null)
        => new DeadLetterReplayBuilder(_publisherBuilder).ConfigureReplayWorker(configure);

    internal void EnsureStorageHandlerRegistered()
    {
        var publisherName = _publisherBuilder.Name ?? String.Empty;

        if (Services.Any(descriptor =>
                descriptor.ServiceType == typeof(DeadLetterStorageRegistrationMarker) &&
                descriptor.ImplementationInstance is DeadLetterStorageRegistrationMarker marker &&
                marker.PublisherName == publisherName))
            return;

        Services.AddSingleton(new DeadLetterStorageRegistrationMarker(publisherName));
        Services.AddKeyedScoped<IEventPublishErrorHandler, DeadLetterStoragePublishErrorHandler>(_publisherBuilder.Name);
    }

    private DeadLetterBuilder Register(ServiceDescriptor descriptor)
    {
        Services.Add(descriptor);
        return this;
    }
}
