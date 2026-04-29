//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Defines a composable step in the event-publish middleware pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Middleware implementations receive an <see cref="EventContext"/> and
    /// an <see cref="EventPublishDelegate"/> (<c>next</c>) that represents the remainder
    /// of the pipeline.  They may:
    /// <list type="bullet">
    ///   <item><description>Inspect or mutate the context before calling <c>next</c>
    ///     (e.g. inject a correlation ID, add CloudEvent extension attributes).</description></item>
    ///   <item><description>Short-circuit the pipeline by <em>not</em> calling
    ///     <c>next</c> (e.g. a deduplication filter).</description></item>
    ///   <item><description>Observe or handle the outcome after <c>next</c> returns
    ///     (e.g. structured logging, metrics).</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Middleware types are registered via
    /// <see cref="EventPublisherBuilder.Use{TMiddleware}"/> and a fresh
    /// instance is created for every publish call using
    /// <see cref="Microsoft.Extensions.DependencyInjection.ActivatorUtilities"/>,
    /// resolving constructor dependencies from <see cref="EventContext.Services"/>.
    /// Middleware runs in registration order; the last step in the chain is the
    /// built-in terminal step that validates the <see cref="CloudNative.CloudEvents.CloudEvent"/>
    /// and fans out to all resolved channels.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// public class CorrelationIdMiddleware : IEventMiddleware
    /// {
    ///     public Task InvokeAsync(EventContext context, EventPublishDelegate next)
    ///     {
    ///         // Resolve dependencies from the ambient service provider.
    ///         var accessor = context.Services.GetService&lt;ICorrelationContextAccessor&gt;();
    ///         if (accessor?.CorrelationId is { } id)
    ///         {
    ///             var attr = CloudEventAttribute.CreateExtension("correlationid", CloudEventAttributeType.String);
    ///             context.Event[attr] = id;
    ///         }
    ///         return next(context);
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IEventMiddleware
    {
        /// <summary>
        /// Executes this middleware step.
        /// </summary>
        /// <param name="context">
        /// The <see cref="EventContext"/> for the current publish operation.
        /// It carries the <see cref="CloudNative.CloudEvents.CloudEvent"/>, the ambient
        /// <see cref="IServiceProvider"/> for resolving dependencies, and the
        /// <see cref="EventContext.CancellationToken"/> for the operation.
        /// </param>
        /// <param name="next">
        /// The next step in the pipeline. Must be awaited to continue the chain;
        /// skip it to short-circuit publishing.
        /// </param>
        /// <returns>A <see cref="Task"/> that completes when this step has finished.</returns>
        Task InvokeAsync(EventContext context, EventPublishDelegate next);
    }
}
