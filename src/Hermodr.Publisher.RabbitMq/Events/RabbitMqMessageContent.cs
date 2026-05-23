//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// The format of the message content to be published to 
    /// a RabbitMQ server.
    /// </summary>
    public enum RabbitMqMessageContent
    {
        /// <summary>
        /// The message is formatted as a CloudEvent,
        /// including the metadata and the event data.
        /// </summary>
        CloudEvent = 1,

        /// <summary>
        /// Only the event data is published,
        /// while the metadata is provided in the
        /// headers of the message.
        /// </summary>
        EventData = 2
    }
}
