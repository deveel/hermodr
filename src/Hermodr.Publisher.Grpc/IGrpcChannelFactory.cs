//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Grpc.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    /// <summary>
    /// A service used to resolve or create <c>Grpc.Net.Client.GrpcChannel</c>
    /// instances for the <see cref="GrpcPublishChannel"/>, one per endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default implementation (<see cref="GrpcChannelFactory"/>) wraps a
    /// named <see cref="System.Net.Http.HttpClient"/> resolved through
    /// <see cref="System.Net.Http.IHttpClientFactory"/> (via
    /// <c>GrpcChannelOptions.HttpClient</c>), so TLS, mTLS and
    /// <c>CallCredentials</c> are configured through the standard
    /// <c>IHttpClientFactory</c> / <c>SocketsHttpHandler</c> surface that
    /// <c>Grpc.Net.Client</c> already understands.
    /// </para>
    /// <para>
    /// Replace this interface to source channels from a different factory (for
    /// example a pre-built singleton <c>GrpcChannel</c> or a channel pool).
    /// </para>
    /// </remarks>
    public interface IGrpcChannelFactory
    {
        /// <summary>
        /// Creates (or resolves a cached) <c>GrpcChannel</c> for the given
        /// endpoint <paramref name="address"/>, wrapping the named
        /// <c>HttpClient</c> identified by <paramref name="httpClientName"/>.
        /// </summary>
        /// <param name="address">
        /// The absolute address of the gRPC endpoint (for example
        /// <c>https://svc.example.com:5001</c>).
        /// </param>
        /// <param name="httpClientName">
        /// An optional name that selects a named <c>HttpClient</c>
        /// configuration; when <c>null</c> the default channel
        /// (<see cref="GrpcDefaults.HttpClientName"/>) is used.
        /// </param>
        /// <returns>
        /// A <c>Grpc.Net.Client.GrpcChannel</c> to use for publishing.
        /// </returns>
        Grpc.Net.Client.GrpcChannel CreateChannel(string address, string? httpClientName = null);

        /// <summary>
        /// Creates a <see cref="CallInvoker"/> for the given endpoint
        /// <paramref name="address"/> and optional client name, suitable for
        /// passing to an <see cref="IGrpcEventSender"/>.
        /// </summary>
        /// <param name="address">
        /// The absolute address of the gRPC endpoint.
        /// </param>
        /// <param name="httpClientName">
        /// An optional name that selects a named <c>HttpClient</c>
        /// configuration; when <c>null</c> the default channel is used.
        /// </param>
        /// <returns>A <see cref="CallInvoker"/> backed by a <c>GrpcChannel</c>.</returns>
        CallInvoker CreateCallInvoker(string address, string? httpClientName = null);
    }
}
