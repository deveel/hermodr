//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Contains the constants used in the Cloud Event attributes
    /// to define the AMQP exchange and routing key.
    /// </summary>
    public static class AmqpCloudEventAttributes
    {
        /// <summary>
        /// The attribute name used to define the exchange name 
        /// in the Cloud Event.
        /// </summary>
        public const string AmqpExchangeNameAttribute = "amqpexchange";

        /// <summary>
        /// The attribute name used to define the routing key
        /// in the Cloud Event.
        /// </summary>
        public const string AmqpRoutingKeyAttribute = "amqproutingkey";
    }
}
