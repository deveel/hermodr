//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Azure.Messaging.ServiceBus;

namespace Hermodr {
	class ServiceBusClientFactory : IServiceBusClientFactory {
		public ServiceBusClient CreateClient(string connectionString, ServiceBusClientOptions options)
			=> new ServiceBusClient(connectionString, options);
	}
}
