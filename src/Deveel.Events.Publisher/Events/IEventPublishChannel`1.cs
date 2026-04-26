//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Extends <see cref="IEventPublishChannel"/> with a strongly-typed event marker,
    /// allowing the <see cref="EventPublisher"/> to route events of type
    /// <typeparamref name="TEvent"/> exclusively to this channel.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type of event handled by this channel.  This parameter is used purely as a
    /// routing key for DI resolution; the channel still publishes <see cref="CloudEvent"/>
    /// instances via the inherited <see cref="IEventPublishChannel.PublishAsync"/> method.
    /// </typeparam>
    public interface IEventPublishChannel<TEvent> : IEventPublishChannel
        where TEvent : class
    {
    }
}
