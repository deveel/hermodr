//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// A marker interface for a <see cref="IEventPublishChannel"/> that is associated
    /// with a specific annotated event data type.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The annotated event data class this channel is keyed against.
    /// <see cref="EventPublisher"/> uses <c>IEventPublishChannel&lt;TEvent&gt;</c> to locate
    /// channels registered for a particular event data class and routes the event to those
    /// channels instead of (or in addition to) the general-purpose ones registered as
    /// <see cref="IEventPublishChannel"/>.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Per-call delivery overrides (e.g. routing key, destination address, signing secret) are
    /// <em>not</em> a responsibility of this interface.  They are passed as strongly-typed
    /// <see cref="EventPublishOptions"/> subclass instances to the
    /// <see cref="IEventPublishChannel.PublishAsync"/> method or to the strongly-typed
    /// <c>PublishAsync</c> overload exposed by <see cref="EventPublishChannel{TOptions}"/>
    /// on the concrete channel.
    /// </para>
    /// <para>
    /// Register a channel under this interface when you need it to receive only events whose
    /// data class is annotated with <c>[Event("&lt;type&gt;")]</c> and you want those events
    /// to be routed to this specific channel, bypassing the general-purpose broadcast.
    /// </para>
    /// </remarks>
    public interface IEventPublishChannel<TEvent> : IEventPublishChannel
        where TEvent : class
    {
    }
}
