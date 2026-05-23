//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Defines the CloudEvent extension attribute names used by the
    /// Azure Service Bus publish channel to extract transport-specific
    /// metadata from events.
    /// </summary>
    public static class ServiceBusEventAttributes {
        /// <summary>
        /// The default CloudEvent extension attribute name for the correlation ID.
        /// </summary>
        public const string CorrelationId = "correlationid";

        /// <summary>
        /// The default CloudEvent extension attribute name for the partition key.
        /// </summary>
        public const string PartitionKey = "partitionkey";
    }
}
