//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;

namespace Hermodr
{
    /// <summary>
    /// Options for the <see cref="GrpcPublishChannel"/>, used both as the
    /// channel-level configuration (injected via
    /// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>) and as
    /// per-delivery overrides passed directly to
    /// <see cref="EventPublishChannel{TOptions}.PublishAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The channel delivers every published event to <strong>all</strong>
    /// endpoints in <see cref="Endpoints"/> concurrently, each with its own
    /// <c>GrpcChannel</c>, authentication and sender configuration. Endpoints
    /// are statically configured at registration time.
    /// </para>
    /// <para>
    /// When used as a per-delivery override, only non-<c>null</c> properties
    /// replace the channel-level configuration; any nullable property left
    /// <c>null</c> retains the channel default.
    /// </para>
    /// </remarks>
    public class GrpcPublishOptions : NamedChannelPublishOptions, INamedChannelFilter, IChannelMetadataSource, IValidatableObject
    {
        /// <summary>
        /// Merges <paramref name="baseOptions"/> with <paramref name="typedOptions"/>,
        /// where every non-<c>null</c> property in <paramref name="typedOptions"/>
        /// overrides the corresponding property from <paramref name="baseOptions"/>.
        /// For <see cref="Endpoints"/> an <em>empty</em> list counts as unset, so that
        /// typed or per-call options that did not configure endpoints fall back to the
        /// base endpoints (an empty endpoint list can never be valid anyway).
        /// </summary>
        /// <param name="baseOptions">The base (channel-level) options to merge from.</param>
        /// <param name="typedOptions">The typed per-event overrides to apply.</param>
        /// <returns>A new <see cref="GrpcPublishOptions"/> with the merged values.</returns>
        public static GrpcPublishOptions Merge(
            GrpcPublishOptions baseOptions,
            GrpcPublishOptions typedOptions)
        {
            return new GrpcPublishOptions
            {
                ChannelName        = typedOptions.ChannelName        ?? baseOptions.ChannelName,
                Endpoints          = HasEndpoints(typedOptions.Endpoints)
                                         ? typedOptions.Endpoints
                                         : baseOptions.Endpoints,
                ScheduleDeliveryAt = typedOptions.ScheduleDeliveryAt ?? baseOptions.ScheduleDeliveryAt,
            };
        }

        private static bool HasEndpoints(IReadOnlyList<GrpcEndpoint>? endpoints)
            => endpoints is { Count: > 0 };

        /// <summary>
        /// Gets or sets the statically-configured gRPC endpoints the events are
        /// delivered to. Must contain at least one endpoint.
        /// </summary>
        [Required(AllowEmptyStrings = false,
            ErrorMessage = "At least one gRPC endpoint must be configured.")]
        [MinLength(1, ErrorMessage = "At least one gRPC endpoint must be configured.")]
        public IReadOnlyList<GrpcEndpoint> Endpoints { get; set; }
            = Array.Empty<GrpcEndpoint>();

        /// <inheritdoc/>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Endpoints == null || Endpoints.Count == 0)
            {
                yield return new ValidationResult(
                    "At least one gRPC endpoint must be configured.",
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

                if (string.IsNullOrWhiteSpace(endpoint.Address))
                {
                    yield return new ValidationResult(
                        $"The address of the endpoint at index {i} is required and must not be empty.",
                        [$"{nameof(Endpoints)}[{i}].Address"]);
                }
                else if (!Uri.TryCreate(endpoint.Address, UriKind.Absolute, out var endpointUri)
                         || (!endpointUri.IsAbsoluteUri))
                {
                    yield return new ValidationResult(
                        $"The address '{endpoint.Address}' of the endpoint at index {i} must be a valid absolute URL.",
                        [$"{nameof(Endpoints)}[{i}].Address"]);
                }
                else if (!string.Equals(endpointUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new ValidationResult(
                        $"The address '{endpoint.Address}' of the endpoint at index {i} must use the http or https scheme (found '{endpointUri.Scheme}').",
                        [$"{nameof(Endpoints)}[{i}].Address"]);
                }

                foreach (var result in ValidateHeaders(endpoint, i))
                    yield return result;
            }
        }

        private static IEnumerable<ValidationResult> ValidateHeaders(GrpcEndpoint endpoint, int index)
        {
            if (endpoint.Headers is not { Count: > 0 })
                yield break;

            foreach (var header in endpoint.Headers)
            {
                var failure = ValidateHeaderEntry(header.Key, header.Value);
                if (failure is not null)
                {
                    yield return new ValidationResult(
                        $"The header '{header.Key}' of the endpoint at index {index} is not a valid gRPC metadata entry: {failure}",
                        [$"{nameof(Endpoints)}[{index}].Headers"]);
                }
            }
        }

        /// <summary>
        /// Validates a single header entry against the gRPC metadata rules that
        /// grpc-dotnet enforces (key charset, no binary keys on the string surface),
        /// plus the value constraints enforced by the HTTP/2 layer (printable ASCII
        /// only, no control characters such as CR/LF which would allow header
        /// injection). Returns the failure reason, or <c>null</c> when valid.
        /// </summary>
        /// <remarks>
        /// Binary metadata (keys with a <c>-bin</c> suffix) cannot be carried as
        /// string values: grpc-dotnet rejects them, so they are invalid on this
        /// options surface regardless of value encoding.
        /// </remarks>
        private static string? ValidateHeaderEntry(string? key, string? value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "the key is null or empty.";

            if (key.EndsWith(BinaryKeySuffix, StringComparison.OrdinalIgnoreCase))
                return "binary metadata keys (with a '-bin' suffix) cannot be carried as string values.";

            foreach (var c in key)
            {
                if (!char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-' and not '.')
                    return "keys can only contain alphanumeric characters, underscores, hyphens and dots.";
            }

            if (value is null)
                return "the value is null.";

            foreach (var c in value)
            {
                if ((c < 0x20 && c != '\t') || c > 0x7E)
                    return "the value must only contain printable ASCII characters (no control characters such as CR/LF, no non-ASCII characters).";
            }

            return null;
        }

        private const string BinaryKeySuffix = "-bin";

        /// <inheritdoc/>
        IEventPublishChannelMetadata IChannelMetadataSource.GetChannelMetadata()
        {
            var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < Endpoints.Count; i++)
            {
                var endpoint = Endpoints[i];
                if (string.IsNullOrEmpty(endpoint.Address))
                    continue;

                if (i == 0)
                    properties["address"] = endpoint.Address;
                else
                    properties[$"address.{i}"] = endpoint.Address;
            }

            return new EventPublishChannelMetadata(ChannelName, EventTransports.Grpc, properties);
        }
    }
}
