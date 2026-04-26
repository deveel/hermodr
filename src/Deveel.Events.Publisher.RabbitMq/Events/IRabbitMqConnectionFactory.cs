//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using RabbitMQ.Client;
using System.Threading;
using System.Threading.Tasks;

namespace Deveel.Events
{
    /// <summary>
    /// A factory to create a connection to a RabbitMQ server
    /// to be used by the <see cref="RabbitMqPublishChannel"/>.
    /// </summary>
    public interface IRabbitMqConnectionFactory
    {
        /// <summary>
        /// Creates a new connection to the RabbitMQ server.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token to cancel the operation.
        /// </param>
        /// <returns>
        /// Returns a new instance of <see cref="IConnection"/> that represents
        /// the connection to the RabbitMQ server.
        /// </returns>
        Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
    }
}
