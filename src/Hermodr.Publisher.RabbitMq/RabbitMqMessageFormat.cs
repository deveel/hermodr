//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// The format of the message to be published to 
    /// a RabbitMQ server.
    /// </summary>
    public enum RabbitMqMessageFormat
    {
        /// <summary>
        /// The message is serialized as a JSON object.
        /// </summary>
        Json = 1,

        /// <summary>
        /// The message is serialized as a binary object.
        /// </summary>
        Binary = 2
    }
}
