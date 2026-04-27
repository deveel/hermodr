//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Defines the criteria used to match a <see cref="CloudEvent"/> against a subscription.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All non-null filter properties must match for the overall filter to pass.
    /// Setting a property to <c>null</c> means "any value" (i.e. no restriction on that attribute).
    /// </para>
    /// <para>
    /// The <see cref="Predicate"/> property, when set, is evaluated last and can be used to
    /// implement advanced matching logic that goes beyond simple string comparisons.
    /// </para>
    /// </remarks>
    public sealed class EventSubscriptionFilter
    {
        /// <summary>
        /// Gets or sets the filter applied to the CloudEvents <c>type</c> attribute.
        /// When <c>null</c> the type is not restricted.
        /// </summary>
        public EventAttributeFilter? TypeFilter { get; set; }

        /// <summary>
        /// Gets or sets the filter applied to the CloudEvents <c>source</c> attribute (as a URI string).
        /// When <c>null</c> the source is not restricted.
        /// </summary>
        public EventAttributeFilter? SourceFilter { get; set; }

        /// <summary>
        /// Gets or sets the filter applied to the CloudEvents <c>subject</c> attribute.
        /// When <c>null</c> the subject is not restricted.
        /// </summary>
        public EventAttributeFilter? SubjectFilter { get; set; }

        /// <summary>
        /// Gets or sets filters applied to CloudEvents extension attributes, keyed by attribute name.
        /// Each filter is matched against the string representation of the extension attribute value.
        /// When <c>null</c> or empty no extension attribute restrictions are applied.
        /// </summary>
        public IDictionary<string, EventAttributeFilter>? ExtensionFilters { get; set; }

        /// <summary>
        /// Gets or sets a filter applied to the body (data payload) of the event.
        /// Supported implementations are <see cref="JsonPathDataFilter"/>,
        /// <see cref="JsonPredicateDataFilter"/>, and <see cref="TypedDataFilter{T}"/>.
        /// When <c>null</c> no body inspection is performed.
        /// </summary>
        /// <remarks>
        /// Body filters are evaluated after all envelope-attribute filters pass.
        /// <see cref="TypedDataFilter{T}"/> dispatches deserialization to the first
        /// <see cref="IEventDataDeserializer"/> registered in its
        /// <see cref="EventDataDeserializerProvider"/> whose
        /// <see cref="IEventDataDeserializer.CanDeserialize"/> matches the event's
        /// <c>datacontenttype</c> — supporting JSON, Protobuf, MessagePack, and any
        /// other format for which an <see cref="IEventDataDeserializer"/> has been registered.
        /// </remarks>
        public EventDataFilter? DataFilter { get; set; }

        /// <summary>
        /// Gets or sets an advanced predicate that is evaluated after all attribute filters
        /// and the <see cref="DataFilter"/> pass.
        /// When <c>null</c> no additional predicate check is performed.
        /// </summary>
        public Func<CloudEvent, bool>? Predicate { get; set; }

        /// <summary>
        /// Returns <c>true</c> when the supplied <paramref name="event"/> satisfies every
        /// constraint defined in this filter.
        /// </summary>
        public bool Matches(CloudEvent @event)
            => MatchesCore(@event, services: null);

        /// <summary>
        /// Returns <c>true</c> when the supplied <paramref name="event"/> satisfies every
        /// constraint, using <paramref name="services"/> to resolve runtime dependencies
        /// needed by <see cref="DataFilter"/> (e.g. a DI-registered
        /// <see cref="EventDataDeserializerProvider"/>).
        /// </summary>
        /// <param name="event">The event to match.</param>
        /// <param name="services">
        /// An optional <see cref="IServiceProvider"/> forwarded to
        /// <see cref="EventDataFilter.Matches(CloudEvent, IServiceProvider?)"/>.
        /// Pass <c>null</c> to fall back to design-time providers and
        /// <see cref="EventDataDeserializerProvider.Default"/>.
        /// </param>
        public bool Matches(CloudEvent @event, IServiceProvider? services)
            => MatchesCore(@event, services);

        private bool MatchesCore(CloudEvent @event, IServiceProvider? services)
        {
            if (@event is null)
                return false;

            if (TypeFilter is not null && !TypeFilter.Matches(@event.Type))
                return false;

            if (SourceFilter is not null && !SourceFilter.Matches(@event.Source?.ToString()))
                return false;

            if (SubjectFilter is not null && !SubjectFilter.Matches(@event.Subject))
                return false;

            if (ExtensionFilters is { Count: > 0 })
            {
                foreach (var (name, filter) in ExtensionFilters)
                {
                    var attribute = CloudEventAttribute.CreateExtension(name, CloudEventAttributeType.String);
                    var raw = @event[attribute];
                    var value = raw?.ToString();
                    if (!filter.Matches(value))
                        return false;
                }
            }

            if (DataFilter is not null && !DataFilter.Matches(@event, services))
                return false;

            if (Predicate is not null && !Predicate(@event))
                return false;

            return true;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Fluent factory helpers
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new <see cref="EventSubscriptionFilterBuilder"/> for building a filter
        /// fluently.
        /// </summary>
        public static EventSubscriptionFilterBuilder Builder => new();

        /// <summary>
        /// Creates a filter that matches only events whose <c>type</c> attribute is
        /// exactly <paramref name="type"/>.
        /// </summary>
        public static EventSubscriptionFilter ForType(string type)
            => new() { TypeFilter = EventAttributeFilter.Exact(type) };

        /// <summary>
        /// Creates a filter that matches events whose <c>type</c> attribute satisfies
        /// the given <paramref name="pattern"/>.
        /// A trailing <c>*</c> creates a prefix match; a leading <c>*</c> creates a suffix
        /// match; no wildcard creates an exact match.
        /// </summary>
        public static EventSubscriptionFilter ForTypePattern(string pattern)
            => new() { TypeFilter = EventAttributeFilter.Parse(pattern) };
    }
}



