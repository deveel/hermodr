//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// A set of properties (metadata) that are provided along 
    /// a message in the Azure ServiceBus.
    /// </summary>
    public static class ServiceBusMessageProperties {
        /// <summary>
        /// The version of the data that is being sent.
        /// </summary>
		public const string DataVersion = "event.dataVersion";

        /// <summary>
        /// The type of the event that is being sent.
        /// </summary>
		public const string EventType = "event.type";

        /// <summary>
        /// The timestamp of the event that is being sent.
        /// </summary>
		public const string TimeStamp = "event.timestamp";
	}
}
