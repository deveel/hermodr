//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Type-specific publish options for a <see cref="MassTransitEventPublishChannel{TEvent}"/>
    /// that routes events of type <typeparamref name="TEvent"/> via MassTransit.
    /// </summary>
    /// <remarks>
    /// Any property left at its default (<c>null</c>) will be inherited from the
    /// general-purpose <see cref="MassTransitEventPublishOptions"/> registered alongside
    /// the non-typed channel.
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this set of options is keyed against.
    /// </typeparam>
    public class MassTransitEventPublishOptions<TEvent> : MassTransitEventPublishOptions
        where TEvent : class
    {
    }
}
