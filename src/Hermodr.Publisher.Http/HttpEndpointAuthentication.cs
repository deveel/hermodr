//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Specifies how requests to an <see cref="HttpEndpoint"/> are authenticated.
    /// </summary>
    /// <remarks>
    /// Authentication is applied to the endpoint's named
    /// <see cref="System.Net.Http.HttpClient"/> via a built-in
    /// <see cref="System.Net.Http.DelegatingHandler"/> registered at DI setup time,
    /// so no per-request logic is required by the channel. For any authentication
    /// scheme not covered here (OAuth2 flows, mTLS, custom signing, ...) attach a
    /// custom <see cref="System.Net.Http.DelegatingHandler"/> to the endpoint's
    /// named client via <c>ConfigureHttpClient</c>.
    /// </remarks>
    public class HttpEndpointAuthentication
    {
        /// <summary>
        /// Gets or sets the authentication scheme. Defaults to <see cref="HttpAuthenticationMode.None"/>.
        /// </summary>
        public HttpAuthenticationMode AuthenticationMode { get; set; }
            = HttpAuthenticationMode.None;

        /// <summary>
        /// Gets or sets the bearer token sent in the <c>Authorization</c> header when
        /// <see cref="AuthenticationMode"/> is <see cref="HttpAuthenticationMode.Bearer"/>.
        /// </summary>
        public string? BearerToken { get; set; }

        /// <summary>
        /// Gets or sets the API key value sent when
        /// <see cref="AuthenticationMode"/> is <see cref="HttpAuthenticationMode.ApiKey"/>.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the name of the HTTP header carrying the API key when
        /// <see cref="AuthenticationMode"/> is <see cref="HttpAuthenticationMode.ApiKey"/>.
        /// Defaults to <c>X-Api-Key</c>.
        /// </summary>
        public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
    }

    /// <summary>
    /// The authentication schemes supported out-of-the-box by the
    /// <see cref="HttpPublishChannel"/> endpoints.
    /// </summary>
    public enum HttpAuthenticationMode
    {
        /// <summary>No authentication; requests are sent anonymously.</summary>
        None,

        /// <summary>Bearer token authentication via the <c>Authorization: Bearer &lt;token&gt;</c> header.</summary>
        Bearer,

        /// <summary>API key authentication via a configurable header.</summary>
        ApiKey
    }
}