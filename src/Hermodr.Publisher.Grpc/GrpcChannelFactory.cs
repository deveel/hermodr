//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.Concurrent;

using Grpc.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    /// <summary>
    /// Default <see cref="IGrpcChannelFactory"/> that wraps a named
    /// <see cref="System.Net.Http.HttpClient"/> resolved through
    /// <see cref="System.Net.Http.IHttpClientFactory"/> (via
    /// <c>GrpcChannelOptions.HttpClient</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Routing through <c>IHttpClientFactory</c> means TLS, mTLS,
    /// <c>DelegatingHandler</c>s and <c>CallCredentials</c> are configured with
    /// the standard <c>Grpc.Net.Client</c> + <c>HttpClient</c> tooling — no
    /// bespoke channel configuration surface is introduced.
    /// </para>
    /// <para>
    /// <c>GrpcChannel</c> instances are cached per composite
    /// (client-name, address) key so repeated publishes reuse the same
    /// underlying HTTP/2 connection pool.
    /// </para>
    /// <para>
    /// Because a cached <c>GrpcChannel</c> keeps the <c>HttpClient</c> (and its
    /// primary handler) it captured at creation time, the default gRPC client is
    /// registered with an infinite <c>HandlerLifetime</c>: handler rotation would
    /// never take effect. To keep long-running processes resilient to DNS
    /// changes, configure the underlying handler's connection lifetime instead,
    /// for example:
    /// <code>
    /// builder.ConfigureGrpcChannel("Hermodr.Grpc", http =>
    ///     http.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    ///     {
    ///         PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    ///     }));
    /// </code>
    /// </para>
    /// </remarks>
    internal sealed class GrpcChannelFactory : IGrpcChannelFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConcurrentDictionary<string, Grpc.Net.Client.GrpcChannel> _channels;

        public GrpcChannelFactory(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _channels = new ConcurrentDictionary<string, Grpc.Net.Client.GrpcChannel>(
                StringComparer.Ordinal);
        }

        /// <inheritdoc/>
        public Grpc.Net.Client.GrpcChannel CreateChannel(string address, string? httpClientName = null)
        {
            ArgumentNullException.ThrowIfNull(address);

            var clientName = string.IsNullOrEmpty(httpClientName)
                ? GrpcDefaults.HttpClientName
                : httpClientName!;

            var cacheKey = $"{clientName}|{address}";

            return _channels.GetOrAdd(cacheKey, _ => CreateGrpcChannel(address, clientName));
        }

        /// <inheritdoc/>
        public CallInvoker CreateCallInvoker(string address, string? httpClientName = null)
            => CreateChannel(address, httpClientName).CreateCallInvoker();

        private Grpc.Net.Client.GrpcChannel CreateGrpcChannel(string address, string clientName)
        {
            var httpClient = _httpClientFactory.CreateClient(clientName);

            var options = new Grpc.Net.Client.GrpcChannelOptions
            {
                HttpClient = httpClient,
            };

            return Grpc.Net.Client.GrpcChannel.ForAddress(address, options);
        }
    }
}
