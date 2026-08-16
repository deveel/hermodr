//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// A <see cref="DelegatingHandler"/> that attaches an API key to a configurable
    /// HTTP header (default <c>X-Api-Key</c>) on every request.
    /// </summary>
    /// <remarks>
    /// Used by the <see cref="HttpPublishChannel"/> to apply authentication to an
    /// endpoint's named client when
    /// <see cref="HttpEndpointAuthentication.AuthenticationMode"/> is
    /// <see cref="HttpAuthenticationMode.ApiKey"/>. It can also be attached to any
    /// named client manually via <c>ConfigureHttpClient</c>.
    /// </remarks>
    public class ApiKeyHttpHandler : DelegatingHandler
    {
        /// <summary>
        /// Gets the API key attached to outgoing requests.
        /// </summary>
        public string ApiKey { get; }

        /// <summary>
        /// Gets the name of the HTTP header carrying the API key.
        /// </summary>
        public string HeaderName { get; }

        /// <summary>
        /// Initializes the handler with the API key and the header that carries it.
        /// </summary>
        /// <param name="apiKey">The API key value.</param>
        /// <param name="headerName">
        /// The name of the header that carries the key. Defaults to <c>X-Api-Key</c>.
        /// </param>
        public ApiKeyHttpHandler(string apiKey, string headerName = "X-Api-Key")
        {
            ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            HeaderName = !string.IsNullOrWhiteSpace(headerName)
                ? headerName
                : throw new ArgumentException("The header name must not be empty.", nameof(headerName));
        }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Headers.TryAddWithoutValidation(HeaderName, ApiKey);
            return base.SendAsync(request, cancellationToken);
        }
    }
}