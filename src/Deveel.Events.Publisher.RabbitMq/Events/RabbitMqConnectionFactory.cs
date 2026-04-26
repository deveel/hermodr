//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Deveel.Events
{
    /// <summary>
    /// A default implementation of the <see cref="IRabbitMqConnectionFactory"/>
    /// that creates a connection to a RabbitMQ server using the connection string
    /// configured in the options.
    /// </summary>
    public class RabbitMqConnectionFactory : IRabbitMqConnectionFactory
    {
        private readonly ConnectionFactory _connectionFactory;

        /// <summary>
        /// Creates a new instance of the factory with the options
        /// that contains the connection string to the RabbitMQ server.
        /// </summary>
        /// <param name="options">
        /// The RabbitMQ channel options that contains the connection string
        /// to the RabbitMQ server.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when the connection string is not a valid URI.
        /// </exception>
        public RabbitMqConnectionFactory(IOptions<RabbitMqPublishOptions> options)
        {
            if (!Uri.TryCreate(options.Value.ConnectionString, UriKind.Absolute, out var connectionUri))
                throw new ArgumentException("The connection string is not a valid URI", nameof(options));

            _connectionFactory = new ConnectionFactory
            {
                Uri = connectionUri,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                ClientProvidedName = options.Value.ClientName ?? "Deveel.Events"
            };
        }

        /// <inheritdoc />
        public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            return _connectionFactory.CreateConnectionAsync(cancellationToken);
        }
    }
}
