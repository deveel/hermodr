//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Dapr.Client;

namespace Hermodr
{
    /// <summary>
    /// A service used to resolve <see cref="DaprClient"/> instances for the
    /// <see cref="DaprPublishChannel"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations are free to source the client from the standard
    /// <c>AddDaprClient()</c> DI flow, a named <c>IHttpClientFactory</c>
    /// registration, or a custom factory that configures mTLS / token
    /// credentials through the Dapr SDK builder surface.
    /// </para>
    /// <para>
    /// The default implementation (<see cref="DaprClientBuilder"/>) resolves the
    /// unnamed <see cref="DaprClient"/> registered through
    /// <c>AddDaprClient()</c>; replace it to support named clients keyed by
    /// <see cref="DaprPublishOptions.DaprClientName"/>.
    /// </para>
    /// </remarks>
    public interface IDaprClientBuilder
    {
        /// <summary>
        /// Creates (or resolves) a <see cref="DaprClient"/> for the given
        /// optional name.
        /// </summary>
        /// <param name="name">
        /// An optional name that selects a named client configuration; when
        /// <c>null</c> the default (unnamed) client is returned.
        /// </param>
        /// <returns>
        /// A <see cref="DaprClient"/> to use for publishing.
        /// </returns>
        DaprClient CreateClient(string? name = null);
    }
}