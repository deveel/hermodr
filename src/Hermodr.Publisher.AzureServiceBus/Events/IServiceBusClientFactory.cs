//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Azure.Messaging.ServiceBus;

namespace Hermodr {
    /// <summary>
    /// A service used to create instances of <see cref="ServiceBusClient"/>
    /// for communication with Azure Service Bus.
    /// </summary>
    public interface IServiceBusClientFactory {
        /// <summary>
        /// Creates a new instance of <see cref="ServiceBusClient"/>
        /// from the given connection string and options.
        /// </summary>
        /// <param name="connectionString">
        /// The connection string to the Azure Service Bus.
        /// </param>
        /// <param name="options">
        /// The options to use when creating the client.
        /// </param>
        /// <returns>
        /// Returns a new instance of <see cref="ServiceBusClient"/>
        /// that can be used to communicate with the Azure Service Bus.
        /// </returns>
		ServiceBusClient CreateClient(string connectionString, ServiceBusClientOptions options);
	}
}
