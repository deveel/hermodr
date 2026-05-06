//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events;

/// <summary>
/// Extension methods that register publish-error handlers for an <see cref="EventPublisherBuilder"/>.
/// </summary>
public static class EventPublisherErrorHandlingExtensions
{
    /// <summary>
    /// Registers a publish-error handler of type <typeparamref name="THandler"/> for the
    /// current publisher pipeline.
    /// </summary>
    public static EventPublisherBuilder UseErrorHandler<THandler>(
        this EventPublisherBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where THandler : class, IEventPublishErrorHandler
    {
        ArgumentNullException.ThrowIfNull(builder);

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                builder.Services.AddKeyedSingleton<IEventPublishErrorHandler, THandler>(builder.Name);
                break;
            case ServiceLifetime.Scoped:
                builder.Services.AddKeyedScoped<IEventPublishErrorHandler, THandler>(builder.Name);
                break;
            case ServiceLifetime.Transient:
                builder.Services.AddKeyedTransient<IEventPublishErrorHandler, THandler>(builder.Name);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported service lifetime.");
        }

        return builder;
    }

    /// <summary>
    /// Registers a specific publish-error handler instance for the current publisher pipeline.
    /// </summary>
    public static EventPublisherBuilder UseErrorHandler(
        this EventPublisherBuilder builder,
        IEventPublishErrorHandler handler)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(handler);

        builder.Services.AddKeyedSingleton<IEventPublishErrorHandler>(builder.Name, handler);
        return builder;
    }

    /// <summary>
    /// Registers an asynchronous publish-error callback for the current publisher pipeline.
    /// </summary>
    public static EventPublisherBuilder UseErrorHandler(
        this EventPublisherBuilder builder,
        Func<EventPublishErrorContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return builder.UseErrorHandler(new DelegatedEventPublishErrorHandler(handler));
    }

    /// <summary>
    /// Registers a synchronous publish-error callback for the current publisher pipeline.
    /// </summary>
    public static EventPublisherBuilder UseErrorHandler(
        this EventPublisherBuilder builder,
        Action<EventPublishErrorContext> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return builder.UseErrorHandler(context =>
        {
            handler(context);
            return Task.CompletedTask;
        });
    }
}
