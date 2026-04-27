//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Defines the contract for a filter that determines whether a <see cref="CloudEvent"/>
    /// satisfies a specific condition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations include:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <see cref="EventAttributeFilter"/> — matches a named CloudEvents envelope attribute
    ///       (including extension attributes via the <c>extension.</c> prefix).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <see cref="EventDataFilter"/> — navigates a dot-separated JSON path within the event
    ///       data payload and applies a <see cref="FilterOperator"/> comparison against a typed value.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <see cref="LogicalEventFilter"/> — combines multiple <see cref="IEventFilter"/> instances
    ///       with AND or OR logic.
    ///     </description>
    ///   </item>
    /// </list>
    /// </para>
    /// </remarks>
    public interface IEventFilter
    {
        /// <summary>
        /// Returns <c>true</c> when <paramref name="event"/> satisfies the filter condition.
        /// </summary>
        /// <param name="event">The incoming <see cref="CloudEvent"/> to test.</param>
        /// <param name="context">
        /// The <see cref="EventSubscriptionContext"/> providing runtime services (e.g. a DI
        /// <see cref="IServiceProvider"/> for resolving deserializers).
        /// Pass <see cref="EventSubscriptionContext.Empty"/> when no context is available.
        /// </param>
        bool Matches(CloudEvent @event, EventSubscriptionContext context);
    }
}


