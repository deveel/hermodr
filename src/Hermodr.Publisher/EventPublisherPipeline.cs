//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hermodr
{
    /// <summary>
    /// Holds the ordered list of <see cref="IEventMiddleware"/> registrations
    /// that form the event-publish pipeline owned by an <see cref="EventPublisher"/>
    /// instance.
    /// </summary>
    public sealed class EventPublisherPipeline
    {
        private readonly ILogger<EventPublisherPipeline> _logger;
        private readonly List<MiddlewareRegistration> _middlewareRegistrations = new();
        
        internal EventPublisherPipeline(ILogger<EventPublisherPipeline>? logger = null)
        {
            _logger = logger ?? NullLogger<EventPublisherPipeline>.Instance;
        }

        /// <summary>
        /// Gets the ordered list of middleware registrations for
        /// the pipeline.
        /// </summary>
        public IReadOnlyList<MiddlewareRegistration> MiddlewareRegistrations => _middlewareRegistrations;

        /// <summary>
        /// Appends a middleware type to the pipeline.
        /// </summary>
        /// <param name="middlewareType">
        /// A concrete type that implements <see cref="IEventMiddleware"/>.
        /// </param>
        /// <param name="activationArguments">
        /// Optional activation arguments forwarded to
        /// <see cref="Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(IServiceProvider,Type,object[])"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="middlewareType"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="middlewareType"/> does not implement
        /// <see cref="IEventMiddleware"/>.
        /// </exception>
        internal void Add(Type middlewareType, params object[] activationArguments)
        {
            ArgumentNullException.ThrowIfNull(middlewareType);

            if (!typeof(IEventMiddleware).IsAssignableFrom(middlewareType))
                throw new ArgumentException(
                    $"The type '{middlewareType.FullName}' does not implement {nameof(IEventMiddleware)}.",
                    nameof(middlewareType));

            _middlewareRegistrations.Add(new MiddlewareRegistration(
                middlewareType,
                activationArguments?.ToArray() ?? []));
        }

        /// <summary>
        /// Appends a conditionally-executed middleware type to the pipeline.
        /// The middleware is only invoked when <paramref name="predicate"/> returns
        /// <c>true</c> for the current <see cref="EventContext"/>; otherwise the
        /// next step in the pipeline is called directly.
        /// </summary>
        /// <param name="middlewareType">
        /// A concrete type that implements <see cref="IEventMiddleware"/>.
        /// </param>
        /// <param name="predicate">
        /// A function evaluated against the current <see cref="EventContext"/> that
        /// determines whether the middleware should run.
        /// </param>
        /// <param name="activationArguments">
        /// Optional activation arguments forwarded to
        /// <see cref="Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(IServiceProvider,Type,object[])"/>.
        /// </param>
        internal void AddWhen(Type middlewareType, Func<EventContext, bool> predicate, params object[] activationArguments)
        {
            ArgumentNullException.ThrowIfNull(middlewareType);
            ArgumentNullException.ThrowIfNull(predicate);

            if (!typeof(IEventMiddleware).IsAssignableFrom(middlewareType))
                throw new ArgumentException(
                    $"The type '{middlewareType.FullName}' does not implement {nameof(IEventMiddleware)}.",
                    nameof(middlewareType));

            _middlewareRegistrations.Add(new MiddlewareRegistration(
                middlewareType,
                activationArguments?.ToArray() ?? [],
                predicate));
        }

        /// <summary>
        /// Builds a pipeline factory from the registered middleware components.
        /// The returned delegate wraps a terminal <see cref="EventPublishDelegate"/>
        /// with all middleware in registration order (outermost first).
        /// Each middleware invocation is surrounded by <c>Trace</c>-level log entries
        /// so that per-call execution can be followed in diagnostic logs.
        /// </summary>
        /// <returns>
        /// A function that, given a terminal <see cref="EventPublishDelegate"/>, returns
        /// the fully composed pipeline delegate.
        /// </returns>
        public Func<EventPublishDelegate, EventPublishDelegate> Build()
        {
            _logger.TracePipelineBuildStarted();

            Func<EventPublishDelegate, EventPublishDelegate> factory = static terminal => terminal;
            var registrations = _middlewareRegistrations;

            for (var i = registrations.Count - 1; i >= 0; i--)
            {
                var registration = registrations[i];
                var position = registrations.Count - 1 - i;
                _logger.TraceMiddlewareComposed(registration.MiddlewareType, position);
                var prevFactory = factory;
                factory = terminal =>
                {
                    var inner = prevFactory(terminal);
                    return async ctx =>
                    {
                        // If the registration is conditional and the predicate is not satisfied,
                        // skip this middleware and invoke the next step directly.
                        if (registration.IsConditional && !registration.Predicate!(ctx))
                        {
                            await inner(ctx);
                            return;
                        }

                        // When no extra activation arguments are supplied prefer resolving
                        // the middleware from the DI container so that its registered lifetime
                        // (singleton / scoped / transient) is honoured and no unnecessary DI
                        // object-graph construction happens on every event publish.
                        // If the middleware type has NOT been registered in the container
                        // (e.g. ad-hoc types added via Use<T>() without a DI registration)
                        // fall back to ActivatorUtilities.CreateInstance — same behaviour as
                        // before this optimisation.
                        var mw = registration.ActivationArguments.Length == 0
                            ? (IEventMiddleware)(
                                ctx.Services.GetService(registration.MiddlewareType) ??
                                ActivatorUtilities.CreateInstance(ctx.Services, registration.MiddlewareType))
                            : (IEventMiddleware)ActivatorUtilities.CreateInstance(
                                ctx.Services,
                                registration.MiddlewareType,
                                registration.ActivationArguments);
                        _logger.TraceMiddlewareInvoking(registration.MiddlewareType);
                        try
                        {
                            await mw.InvokeAsync(ctx, inner);
                            _logger.TraceMiddlewareCompleted(registration.MiddlewareType);
                        }
                        catch (Exception ex) when (LogMiddlewareFault(ex, registration.MiddlewareType))
                        {
                            // never reached — LogMiddlewareFault always returns false
                            throw;
                        }
                    };
                };
            }

            _logger.TraceMiddlewarePipelineBuilt(registrations.Count);
            return factory;
        }

        // Returns false so it never suppresses the exception; used purely for the side-effect log.
        private bool LogMiddlewareFault(Exception ex, Type middlewareType)
        {
            _logger.TraceMiddlewareFaulted(ex, middlewareType);
            return false;
        }
    }
}


