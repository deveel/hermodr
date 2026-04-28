//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Defines the Visitor contract over the <see cref="EventFilter"/> hierarchy.
    /// </summary>
    /// <typeparam name="TResult">The value returned by each visit method.</typeparam>
    /// <remarks>
    /// Implement this interface to traverse or transform an <see cref="EventFilter"/> tree
    /// without modifying the filter classes themselves.  Each concrete filter type calls back
    /// the corresponding <c>Visit*</c> method from its
    /// <see cref="EventFilter.Accept{TResult}"/> override.
    /// </remarks>
    public interface IEventFilterVisitor<out TResult>
    {
        /// <summary>Visits an <see cref="EventAttributeFilter"/>.</summary>
        TResult VisitAttribute(EventAttributeFilter filter);

        /// <summary>Visits an <see cref="EventDataFilter"/>.</summary>
        TResult VisitData(EventDataFilter filter);

        /// <summary>Visits a <see cref="LogicalEventFilter"/>.</summary>
        TResult VisitLogical(LogicalEventFilter filter);

        /// <summary>
        /// Visits a <see cref="TypedEventDataFilter{TEvent}"/> (accessed through the
        /// non-generic <see cref="ITypedEventDataFilter"/> interface).
        /// </summary>
        TResult VisitTyped(ITypedEventDataFilter filter);
    }
}

