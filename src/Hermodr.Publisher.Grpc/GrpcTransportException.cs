//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Grpc.Core;

namespace Hermodr
{
    /// <summary>
    /// Raised when a gRPC publish to an endpoint fails because of
    /// network/transport errors (connection failures, timeouts, DNS errors, ...)
    /// or when the underlying gRPC call surfaces a non-OK status.
    /// </summary>
    public class GrpcTransportException : GrpcPublishException
    {
        /// <summary>
        /// Initializes the exception with a message.
        /// </summary>
        /// <param name="message">A message describing the delivery failure.</param>
        public GrpcTransportException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes the exception with a message and the underlying transport error.
        /// </summary>
        /// <param name="message">A message describing the delivery failure.</param>
        /// <param name="innerException">The underlying transport exception.</param>
        public GrpcTransportException(string message, Exception innerException)
            : base(message, innerException)
        {
            if (innerException is RpcException rpcEx)
                StatusCode = rpcEx.StatusCode;
        }
    }
}
