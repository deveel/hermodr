//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Runtime.CompilerServices;
using System.Text.Json;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    /// <summary>
    /// Carries contextual information that is passed to an
    /// <see cref="IEventSubscriptionResolver"/> when resolving subscriptions for a
    /// dispatched event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The context is intentionally extensible: additional metadata and attributes can be
    /// added here in future without breaking the <see cref="IEventSubscriptionResolver"/>
    /// contract.
    /// </para>
    /// <para>
    /// Instances are created by infrastructure components (e.g. <see cref="EventDispatcher"/>)
    /// and forwarded via
    /// <see cref="IEventSubscriptionResolver.ResolveSubscriptionsAsync(CloudNative.CloudEvents.CloudEvent, EventSubscriptionContext?, System.Threading.CancellationToken)"/>.
    /// Use <see cref="EventSubscriptionContext.Empty"/> when no services or additional data
    /// are available.
    /// </para>
    /// </remarks>
    public sealed class EventSubscriptionContext
    {
        // Keyed by reference equality so that the same context can serve multiple
        // CloudEvent instances without cross-contamination (rare, but safe).
        private readonly ConditionalWeakTable<CloudEvent, JsonElementBox> _jsonCache = new();

        /// <summary>
        /// Initialises a new context with the supplied <paramref name="services"/>.
        /// </summary>
        /// <param name="services">
        /// The application <see cref="IServiceProvider"/> used by DI-aware filters to resolve
        /// runtime services.  May be <c>null</c> when no DI container is available.
        /// </param>
        internal EventSubscriptionContext(IServiceProvider? services)
        {
            Services = services;
        }

        /// <summary>
        /// Gets a shared empty context with no service provider and no additional metadata.
        /// </summary>
        public static EventSubscriptionContext Empty { get; } = new(services: null);

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> that can be used by DI-aware filters to
        /// resolve runtime services.  <c>null</c> when no DI container is associated with
        /// this context.
        /// </summary>
        public IServiceProvider? Services { get; }

        /// <summary>
        /// Returns the data payload of <paramref name="event"/> as a <see cref="JsonElement"/>,
        /// or <c>null</c> when the payload cannot be represented as JSON.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Resolution strategy:
        /// <list type="number">
        ///   <item><description>
        ///     If a prior call for the same <paramref name="event"/> instance was already made
        ///     within this context, the cached result is returned immediately.
        ///   </description></item>
        ///   <item><description>
        ///     All <see cref="IEventDataDeserializer"/> services registered with
        ///     <see cref="Services"/> are enumerated; the first one that reports
        ///     <see cref="IEventDataDeserializer.CanDeserialize"/> <c>true</c> for the event's
        ///     <c>datacontenttype</c> is used.
        ///   </description></item>
        ///   <item><description>
        ///     When no DI-registered deserializer matches, the built-in
        ///     <see cref="JsonEventDataDeserializer"/> is used as a fallback.
        ///   </description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="event">The <see cref="CloudEvent"/> whose data payload should be read.</param>
        /// <returns>
        /// A <see cref="JsonElement"/> representing the root of the payload, or <c>null</c>
        /// when deserialization fails or is not applicable.
        /// </returns>
        public JsonElement? GetJsonData(CloudEvent @event)
        {
            if (@event is null)
                return null;

            // Return cached result if available.
            if (_jsonCache.TryGetValue(@event, out var box))
                return box.Value;

            // Resolve a deserializer from DI, falling back to the built-in one.
            IEventDataDeserializer? deserializer = null;

            if (Services is not null)
            {
                var contentType = @event.DataContentType;
                foreach (var candidate in Services.GetServices<IEventDataDeserializer>())
                {
                    if (candidate.CanDeserialize(contentType))
                    {
                        deserializer = candidate;
                        break;
                    }
                }
            }

            deserializer ??= JsonEventDataDeserializer.Instance;

            JsonElement? result = deserializer.TryDeserialize(@event, out var element)
                ? element
                : null;

            // Cache and return.
            _jsonCache.Add(@event, new JsonElementBox(result));
            return result;
        }

        // Wrapper needed because ConditionalWeakTable requires reference-type values.
        private sealed class JsonElementBox(JsonElement? value)
        {
            public JsonElement? Value { get; } = value;
        }
    }
}
