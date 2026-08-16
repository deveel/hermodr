//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Net.Http.Headers;

namespace Hermodr
{
    /// <summary>
    /// A <see cref="DelegatingHandler"/> that attaches a
    /// <c>Authorization: Bearer &lt;token&gt;</c> header to every request.
    /// </summary>
    /// <remarks>
    /// Used by the <see cref="HttpPublishChannel"/> to apply authentication to an
    /// endpoint's named client when
    /// <see cref="HttpEndpointAuthentication.AuthenticationMode"/> is
    /// <see cref="HttpAuthenticationMode.Bearer"/>. It can also be attached to any
    /// named client manually via <c>ConfigureHttpClient</c>.
    /// </remarks>
    public class BearerTokenHttpHandler : DelegatingHandler
    {
        /// <summary>
        /// Gets the bearer token attached to outgoing requests.
        /// </summary>
        public string Token { get; }

        /// <summary>
        /// Initializes the handler with the bearer token to attach.
        /// </summary>
        /// <param name="token">The bearer token.</param>
        public BearerTokenHttpHandler(string token)
        {
            Token = token ?? throw new ArgumentNullException(nameof(token));
        }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", Token);
            return base.SendAsync(request, cancellationToken);
        }
    }
}