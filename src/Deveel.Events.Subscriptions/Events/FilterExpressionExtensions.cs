//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel.Filters;

namespace Deveel.Events
{
    /// <summary>
    /// Extension methods for <see cref="FilterExpression"/> that add CloudEvent-specific
    /// matching capabilities.
    /// </summary>
    public static class FilterExpressionExtensions
    {
        /// <summary>
        /// Returns <c>true</c> when this <see cref="FilterExpression"/> matches
        /// <paramref name="event"/> within the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="filter">The filter to evaluate.</param>
        /// <param name="event">The incoming <see cref="CloudEvent"/> to test.</param>
        /// <param name="context">
        /// The subscription context that provides runtime services such as data
        /// deserialization.  Pass <see cref="EventSubscriptionContext.Empty"/> when no
        /// context is available.
        /// </param>
        public static bool Matches(
            this FilterExpression filter,
            CloudEvent @event,
            EventSubscriptionContext context)
            => CloudEventFilterEvaluator.Matches(filter, @event, context);
    }
}

