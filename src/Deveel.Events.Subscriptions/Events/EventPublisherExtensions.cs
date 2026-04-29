//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//
namespace Deveel.Events
{
    /// <summary>
    /// Extension methods on <see cref="EventPublisher"/> for backward-compatibility
    /// with the legacy two-phase dispatcher activation pattern.
    /// </summary>
    /// <remarks>
    /// Prior to the DI-time pipeline registration design, callers had to invoke
    /// <c>publisher.UseDispatcher()</c> after resolving the publisher from the container.
    /// The <see cref="EventPublisherBuilderExtensions.AddSubscriptions"/> method now
    /// wires <see cref="EventDispatcher"/> into the pipeline at registration time
    /// (via <see cref="EventPublisherBuilder.Use{TMiddleware}"/>), so these extension
    /// methods are no-ops kept only for source compatibility.
    /// <para>
    /// To configure <see cref="EventDispatcherOptions"/> pass an
    /// <c>Action&lt;EventDispatcherOptions&gt;</c> argument to
    /// <see cref="EventPublisherBuilderExtensions.AddSubscriptions"/> on the builder
    /// instead of passing an <see cref="EventDispatcherOptions"/> instance here.
    /// </para>
    /// </remarks>
    public static class EventPublisherExtensions
    {
        /// <summary>
        /// Returns the <paramref name="publisher"/> unchanged.
        /// </summary>
        /// <remarks>
        /// The <see cref="EventDispatcher"/> middleware is already wired into the
        /// pipeline by <see cref="EventPublisherBuilderExtensions.AddSubscriptions"/>;
        /// this call is a no-op kept for source compatibility.
        /// </remarks>
        /// <param name="publisher">The publisher instance.</param>
        /// <returns>The same <paramref name="publisher"/> instance.</returns>
        public static EventPublisher UseDispatcher(this EventPublisher publisher)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            return publisher;
        }
        /// <summary>
        /// Returns the <paramref name="publisher"/> unchanged.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This overload is provided for source compatibility only. The supplied
        /// <paramref name="options"/> are <strong>ignored</strong> at runtime because the
        /// <see cref="EventDispatcher"/> middleware is now constructed from the DI-registered
        /// <c>IOptions&lt;EventDispatcherOptions&gt;</c> at the time the pipeline is first invoked.
        /// </para>
        /// <para>
        /// To configure <see cref="EventDispatcherOptions"/> pass an
        /// <c>Action&lt;EventDispatcherOptions&gt;</c> to
        /// <see cref="EventPublisherBuilderExtensions.AddSubscriptions"/> on the
        /// <see cref="EventPublisherBuilder"/> during DI registration instead.
        /// </para>
        /// </remarks>
        /// <param name="publisher">The publisher instance.</param>
        /// <param name="options">Ignored — configure options via the <c>configure</c> argument of <see cref="EventPublisherBuilderExtensions.AddSubscriptions"/>.</param>
        /// <returns>The same <paramref name="publisher"/> instance.</returns>
        [Obsolete(
            "Pass EventDispatcherOptions via the configure argument of AddSubscriptions() on the EventPublisherBuilder at DI-registration time. " +
            "Runtime options supplied here are ignored. This overload will be removed in a future version.",
            error: false)]
        public static EventPublisher UseDispatcher(this EventPublisher publisher, EventDispatcherOptions options)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            // Options are intentionally ignored: the EventDispatcher resolves
            // IOptions<EventDispatcherOptions> from the DI container at call time.
            return publisher;
        }
    }
}
