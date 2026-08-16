//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Default constant values used by the HTTP publisher.
    /// </summary>
    public static class HttpDefaults
    {
        /// <summary>
        /// Default name for the named <see cref="System.Net.Http.HttpClient"/>
        /// registered for the HTTP publisher.
        /// </summary>
        public const string HttpClientName = "Hermodr.Http";

        /// <summary>
        /// The default <see cref="HttpEndpoint.Format"/> used when an endpoint
        /// does not specify one: CloudEvents structured JSON
        /// (<c>application/cloudevents+json</c>).
        /// </summary>
        public const string DefaultFormat = EventMessageFormat.CloudEventsJson;

        /// <summary>
        /// The default timeout applied to each individual HTTP request when an
        /// endpoint does not specify one.
        /// </summary>
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// HTTP status codes that are considered transient and eligible for a
        /// retry by the standard resilience handler when an endpoint does not
        /// customize its resilience options.
        /// </summary>
        public static readonly IReadOnlySet<int> RetryableStatusCodes =
            new HashSet<int> { 429, 500, 502, 503, 504 };
    }
}