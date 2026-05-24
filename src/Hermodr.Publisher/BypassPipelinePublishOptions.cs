//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// A special <see cref="EventPublishOptions"/> subclass that signals to the
    /// <see cref="EventPublisher"/> that the middleware pipeline should be skipped
    /// entirely for this publish call, dispatching the event directly to the target
    /// channels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is intended for use by components that are themselves invoked from within
    /// the publish pipeline (e.g. the subscription router via
    /// <c>RoutingEventSubscription</c>, or the outbox relay processor) and that need
    /// to re-publish an event through the same <see cref="IEventPublisher"/> without
    /// triggering the full middleware stack again.  Running the pipeline a second time
    /// would cause re-entrant behaviour such as duplicate subscription dispatches and,
    /// in the outbox case, an infinite persist-and-relay loop.
    /// </para>
    /// <para>
    /// When <see cref="EventPublisher"/> receives a call whose options are (or wrap) a
    /// <see cref="BypassPipelinePublishOptions"/>, it skips the middleware build step
    /// and invokes the terminal channel-dispatch delegate directly.  Channel-selection
    /// an per-channel options resolution still happen normally — only middleware
    /// execution is omitted.
    /// </para>
    /// <para>
    /// To carry additional per-channel options alongside the bypass signal, supply them
    /// via the <see cref="InnerOptions"/> property.  For example:
    /// <code language="csharp">
    /// // Route to a named channel while bypassing the pipeline:
    /// var options = new BypassPipelinePublishOptions(
    ///     new RabbitMqPublishOptions { ChannelName = "rabbit-orders", RoutingKey = "orders" });
    /// await publisher.PublishEventAsync(myEvent, options);
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class BypassPipelinePublishOptions : EventPublishOptions
    {
        /// <summary>
        /// Initialises a new <see cref="BypassPipelinePublishOptions"/> with an optional
        /// set of inner options that will be forwarded to channel resolution.
        /// </summary>
        /// <param name="innerOptions">
        /// Optional channel-specific options to apply during channel selection and
        /// per-channel option resolution.  When <c>null</c> every registered channel
        /// receives the event and each channel's defaults are used.
        /// </param>
        internal BypassPipelinePublishOptions(EventPublishOptions? innerOptions = null)
        {
            InnerOptions = innerOptions;
        }

        /// <summary>
        /// Gets the inner <see cref="EventPublishOptions"/> that will be used for
        /// channel selection and per-channel option resolution, or <c>null</c> to use
        /// channel defaults.
        /// </summary>
        public EventPublishOptions? InnerOptions { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// Peels off the bypass transport wrapper and returns the effective inner options,
        /// or <c>null</c> when no inner options were supplied.  If the inner options are
        /// themselves a bypass wrapper, the unwrapping recurses.
        /// </remarks>
        public override EventPublishOptions? Unwrap()
            => InnerOptions?.Unwrap();
    }
}




