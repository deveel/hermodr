//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deveel.Events
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
                        var mw = (IEventMiddleware)ActivatorUtilities.CreateInstance(
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


