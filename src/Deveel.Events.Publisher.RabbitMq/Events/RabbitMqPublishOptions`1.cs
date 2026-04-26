//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Type-specific publish options for a <see cref="RabbitMqEventPublishChannel{TEvent}"/>
    /// that routes events of type <typeparamref name="TEvent"/> to a RabbitMQ exchange.
    /// </summary>
    /// <remarks>
    /// Any property left at its default (<c>null</c>) will be inherited from the
    /// general-purpose <see cref="RabbitMqPublishOptions"/> registered alongside
    /// the non-typed channel, giving a two-level configuration hierarchy:
    /// <list type="bullet">
    ///   <item>Base channel options → shared defaults for all event types.</item>
    ///   <item>Typed channel options → type-specific overrides (routing key, exchange, etc.).</item>
    /// </list>
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this set of options is keyed against.
    /// </typeparam>
    public class RabbitMqPublishOptions<TEvent> : RabbitMqPublishOptions
        where TEvent : class
    {
    }
}
