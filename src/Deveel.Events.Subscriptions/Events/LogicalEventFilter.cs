//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Specifies the logical operation used by a <see cref="LogicalEventFilter"/> to combine
    /// its child filters.
    /// </summary>
    public enum LogicalFilterOperator
    {
        /// <summary>All child filters must match (<c>true &amp;&amp; true &amp;&amp; …</c>).</summary>
        And,

        /// <summary>At least one child filter must match (<c>true || false || …</c>).</summary>
        Or
    }

    /// <summary>
    /// An <see cref="IEventFilter"/> that combines multiple child filters using AND or OR logic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="And(IEventFilter[])"/> to require that <em>all</em> child filters pass, or <see cref="Or(IEventFilter[])"/>
    /// to require that <em>at least one</em> passes.
    /// </para>
    /// <para>
    /// An AND filter with zero children evaluates to <c>true</c> (vacuous truth);
    /// an OR filter with zero children evaluates to <c>false</c>.
    /// </para>
    /// <example>
    /// <code>
    /// var filter = LogicalEventFilter.And(
    ///     new EventAttributeFilter("type", "com.example.order.placed"),
    ///     EventDataFilter.Create("Customer.Tier", FilterOperator.Equals, "gold"));
    /// </code>
    /// </example>
    /// </remarks>
    public sealed class LogicalEventFilter : EventFilter
    {
        internal LogicalEventFilter(LogicalFilterOperator kind, IReadOnlyList<EventFilter> filters)
        {
            Kind = kind;
            Filters = filters ?? throw new ArgumentNullException(nameof(filters));
        }

        // ── Properties ──────────────────────────────────────────────────────────────

        /// <summary>Gets the logical operator (<see cref="LogicalFilterOperator.And"/> or <see cref="LogicalFilterOperator.Or"/>).</summary>
        public LogicalFilterOperator Kind { get; }

        /// <summary>Gets the child filters that are combined by this logical filter.</summary>
        public IReadOnlyList<EventFilter> Filters { get; }

        // ── Factory methods ─────────────────────────────────────────────────────────
        // (factory methods have been moved to EventFilter)

        // ── EventFilter ───────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public override TResult Accept<TResult>(IEventFilterVisitor<TResult> visitor)
            => visitor.VisitLogical(this);

        /// <inheritdoc/>
        public override bool Matches(CloudEvent @event, EventSubscriptionContext context)
        {
            if (@event is null)
                return false;

            return Kind switch
            {
                LogicalFilterOperator.And => Filters.All(f => f.Matches(@event, context)),
                LogicalFilterOperator.Or  => Filters.Any(f => f.Matches(@event, context)),
                _                         => false
            };
        }
    }
}


