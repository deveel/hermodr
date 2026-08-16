//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;

namespace Hermodr
{
    /// <summary>
    /// Describes an individual, statically-configured HTTP endpoint that the
    /// <see cref="HttpPublishChannel"/> delivers CloudEvents to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each endpoint is delivered to independently: the channel fans out a single
    /// <c>PublishAsync</c> call to every configured endpoint concurrently, using the
    /// endpoint's own named <see cref="System.Net.Http.HttpClient"/>, authentication and
    /// resilience configuration.
    /// </para>
    /// <para>
    /// The <see cref="Format"/> property selects the wire format of the request body
    /// using the well-known <see cref="EventMessageFormat"/> identifiers (or a custom
    /// <see cref="IEventSerializer"/> format string). Structured content modes
    /// (<c>cloudevents+json</c>, <c>cloudevents+xml</c>) embed the whole CloudEvent in a
    /// single message body; the binary content mode (<c>cloudevents+binary</c>) carries the
    /// event attributes in <c>ce-*</c> HTTP headers and the raw <c>data</c> as the body.
    /// </para>
    /// </remarks>
    public class HttpEndpoint
    {
        /// <summary>
        /// Gets or sets the absolute URL of the endpoint the event is delivered to.
        /// </summary>
        [Required(AllowEmptyStrings = false,
            ErrorMessage = "The endpoint URL is required and must not be empty.")]
        [Url(ErrorMessage = "The endpoint URL must be a valid absolute URL.")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialization format of the request body, using the
        /// well-known <see cref="EventMessageFormat"/> identifiers or a custom
        /// <see cref="IEventSerializer"/> format string.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="EventMessageFormat.CloudEventsJson"/> (CloudEvents
        /// structured JSON, <c>application/cloudevents+json</c>). Structured formats
        /// serialize the event attributes and data into a single body; the binary content
        /// mode (<see cref="EventMessageFormat.CloudEventsBinary"/>) sends the event data
        /// as the body and the attributes as <c>ce-*</c> headers.
        /// </remarks>
        public string Format { get; set; } = HttpDefaults.DefaultFormat;

        /// <summary>
        /// Gets or sets an optional name used to resolve the named
        /// <see cref="System.Net.Http.HttpClient"/> for this endpoint through
        /// <see cref="System.Net.Http.IHttpClientFactory"/>. When <c>null</c> the
        /// default channel name (<see cref="HttpDefaults.HttpClientName"/>) is used.
        /// </summary>
        public string? HttpClientName { get; set; }

        /// <summary>
        /// Gets or sets the authentication applied to requests for this endpoint.
        /// When <c>null</c> requests are sent unauthenticated.
        /// </summary>
        public HttpEndpointAuthentication? Authentication { get; set; }

        /// <summary>
        /// Gets or sets the resilience settings for this endpoint's named
        /// <see cref="System.Net.Http.HttpClient"/> (retry and circuit-breaker).
        /// When <c>null</c> the standard resilience defaults are used.
        /// </summary>
        public HttpResilienceOptions? Resilience { get; set; }

        /// <summary>
        /// Gets or sets the timeout applied to each individual HTTP request for this
        /// endpoint. When <c>null</c> the channel default
        /// (<see cref="HttpDefaults.DefaultRequestTimeout"/>) is used.
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// Gets or sets additional HTTP headers merged into every request for this
        /// endpoint.
        /// </summary>
        public IDictionary<string, string> AdditionalHeaders { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}