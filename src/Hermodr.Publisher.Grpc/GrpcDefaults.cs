//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Default constant values used by the gRPC publisher channel.
    /// </summary>
    public static class GrpcDefaults
    {
        /// <summary>
        /// Default name for the named <see cref="System.Net.Http.HttpClient"/>
        /// registered for the gRPC publisher (wrapped by a
        /// <c>Grpc.Net.Client.GrpcChannel</c>).
        /// </summary>
        public const string HttpClientName = "Hermodr.Grpc";

        /// <summary>
        /// The default deadline applied to each individual gRPC call when an
        /// endpoint does not specify one.
        /// </summary>
        public static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(30);
    }
}
