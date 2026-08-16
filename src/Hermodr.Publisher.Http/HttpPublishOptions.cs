//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;

namespace Hermodr
{
    /// <summary>
    /// Options for the <see cref="HttpPublishChannel"/>, used both as the
    /// channel-level configuration (injected via
    /// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>) and as
    /// per-delivery overrides passed directly to
    /// <see cref="EventPublishChannel{TOptions}.PublishAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The channel delivers every published event to <strong>all</strong> endpoints
    /// in <see cref="Endpoints"/> concurrently, each with its own authentication,
    /// resilience and serialization settings. Endpoints are statically configured at
    /// registration time; there is no runtime subscriber registry.
    /// </para>
    /// <para>
    /// When used as a per-delivery override, only non-<c>null</c> properties replace
    /// the channel-level configuration; any nullable property left <c>null</c> retains
    /// the channel default.
    /// </para>
    /// </remarks>
    public class HttpPublishOptions : NamedChannelPublishOptions, INamedChannelFilter, IChannelMetadataSource, IValidatableObject
    {
        /// <summary>
        /// Merges <paramref name="baseOptions"/> with <paramref name="typedOptions"/>,
        /// where every non-<c>null</c> property in <paramref name="typedOptions"/>'
        /// overrides the corresponding property from <paramref name="baseOptions"/>.
        /// </summary>
        /// <param name="baseOptions">The base (channel-level) options to merge from.</param>
        /// <param name="typedOptions">The typed per-event overrides to apply.</param>
        /// <returns>A new <see cref="HttpPublishOptions"/> with the merged values.</returns>
        public static HttpPublishOptions Merge(
            HttpPublishOptions baseOptions,
            HttpPublishOptions typedOptions)
        {
            return new HttpPublishOptions
            {
                ChannelName    = typedOptions.ChannelName    ?? baseOptions.ChannelName,
                Endpoints      = typedOptions.Endpoints      ?? baseOptions.Endpoints,
                ScheduleDeliveryAt = typedOptions.ScheduleDeliveryAt ?? baseOptions.ScheduleDeliveryAt,
            };
        }

        /// <summary>
        /// Gets or sets the statically-configured endpoints the events are delivered to.
        /// Must contain at least one endpoint.
        /// </summary>
        [Required(AllowEmptyStrings = false,
            ErrorMessage = "At least one HTTP endpoint must be configured.")]
        [MinLength(1, ErrorMessage = "At least one HTTP endpoint must be configured.")]
        public IReadOnlyList<HttpEndpoint> Endpoints { get; set; }
            = Array.Empty<HttpEndpoint>();

        /// <inheritdoc/>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Endpoints == null || Endpoints.Count == 0)
            {
                yield return new ValidationResult(
                    "At least one HTTP endpoint must be configured.",
                    [nameof(Endpoints)]);
                yield break;
            }

            for (var i = 0; i < Endpoints.Count; i++)
            {
                var endpoint = Endpoints[i];
                if (endpoint == null)
                {
                    yield return new ValidationResult(
                        $"The endpoint at index {i} is null.",
                        [$"{nameof(Endpoints)}[{i}]"]);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(endpoint.Url))
                {
                    yield return new ValidationResult(
                        $"The URL of the endpoint at index {i} is required and must not be empty.",
                        [$"{nameof(Endpoints)}[{i}].Url"]);
                }
                else if (!Uri.TryCreate(endpoint.Url, UriKind.Absolute, out var endpointUri)
                         || (!endpointUri.IsAbsoluteUri))
                {
                    yield return new ValidationResult(
                        $"The URL '{endpoint.Url}' of the endpoint at index {i} must be a valid absolute URL.",
                        [$"{nameof(Endpoints)}[{i}].Url"]);
                }
            }
        }

        /// <inheritdoc/>
        IEventPublishChannelMetadata IChannelMetadataSource.GetChannelMetadata()
        {
            var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < Endpoints.Count; i++)
            {
                var endpoint = Endpoints[i];
                if (string.IsNullOrEmpty(endpoint.Url))
                    continue;

                if (i == 0)
                    properties["url"] = endpoint.Url;
                else
                    properties[$"url.{i}"] = endpoint.Url;
            }

            return new EventPublishChannelMetadata(ChannelName, EventTransports.Http, properties);
        }
    }
}