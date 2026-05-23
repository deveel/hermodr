//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// A factory to create a message to be published to 
    /// a RabbitMQ queue.
    /// </summary>
    public interface IRabbitMqMessageFactory
    {
        /// <summary>
        /// Creates a message to be published to a RabbitMQ queue.
        /// </summary>
        /// <param name="event">
        /// The <see cref="CloudEvent"/> to be published.
        /// </param>
        /// <returns>
        /// Returns a <see cref="RabbitMqMessage"/> that represents
        /// the message to be published.
        /// </returns>
        RabbitMqMessage CreateMessage(CloudEvent @event);
    }
}
