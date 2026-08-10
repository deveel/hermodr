//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// An <see cref="EventPublishChannel{TOptions}"/> implementation that publishes
    /// CloudEvents through the Dapr pub/sub building block on top of
    /// <see cref="DaprClient"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The CloudEvent is serialised in structured mode (canonical CloudEvents
    /// JSON envelope) and published as the message payload so consumers receive
    /// the canonical envelope regardless of the underlying Dapr-backed broker.
    /// </para>
    /// <para>
    /// CloudEvent attributes are projected onto Dapr metadata as
    /// <c>&lt;MetadataPrefix&gt;&lt;attribute-name&gt;</c> keys (default prefix
    /// <c>"ce-"</c>) when <see cref="DaprPublishOptions.MapAttributesToMetadata"/>
    /// is in effect (default <c>true</c>).
    /// </para>
    /// <para>
    /// Transport failures are wrapped in <see cref="DaprPublishException"/> so
    /// the <see cref="EventPublisher"/> error-handling pipeline (including the
    /// dead-letter channel) is engaged automatically.
    /// </para>
    /// </remarks>
    internal class DaprPublishChannel :
        EventPublishChannel<DaprPublishOptions>
    {
        private readonly DaprTransportTelemetry _telemetry = new();
        private readonly IDaprClientBuilder _clientBuilder;
        private readonly ILogger _logger;

        private static readonly JsonEventFormatter JsonFormatter = new JsonEventFormatter();

        /// <summary>
        /// Constructs the channel with a Dapr client builder and options.
        /// </summary>
        /// <param name="options">
        /// The options that configure this channel (pub/sub component, topic, metadata mapping, ...).
        /// </param>
        /// <param name="clientBuilder">
        /// The <see cref="IDaprClientBuilder"/> used to resolve the
        /// <see cref="DaprClient"/> for each publish operation.
        /// </param>
        /// <param name="serviceProvider">
        /// The service provider used to resolve <see cref="IValidateOptions{TOptions}"/> validators.
        /// </param>
        /// <param name="logger">
        /// An optional logger; when <c>null</c> a <see cref="NullLogger{T}"/> is used.
        /// </param>
        public DaprPublishChannel(
            IOptions<DaprPublishOptions> options,
            IDaprClientBuilder clientBuilder,
            IServiceProvider serviceProvider,
            ILogger<DaprPublishChannel>? logger = null)
            : base(options.Value, serviceProvider)
        {
            _clientBuilder = clientBuilder;
            _logger = logger ?? NullLogger<DaprPublishChannel>.Instance;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Performs a property-level merge: each nullable property in
        /// <paramref name="perCallOptions"/> that is non-<c>null</c> overrides the
        /// corresponding property from <paramref name="defaults"/>; a <c>null</c>
        /// value signals "use the channel-level default" for that property.
        /// </remarks>
        protected override DaprPublishOptions MergeOptions(
            DaprPublishOptions defaults,
            DaprPublishOptions? perCallOptions)
        {
            if (perCallOptions == null)
                return defaults;

            return DaprPublishOptions.Merge(defaults, perCallOptions);
        }

        /// <inheritdoc/>
        protected override async Task PublishCoreAsync(
            CloudEvent @event,
            DaprPublishOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var topic = ResolveTopic(@event, options);
            if (string.IsNullOrEmpty(topic))
            {
                throw new DaprPublishException(
                    $"Cannot resolve a topic for the event of type '{@event.Type}': set {nameof(DaprPublishOptions.Topic)}, " +
                    $"{nameof(DaprPublishOptions.TopicSelector)}, or supply a CloudEvent with a non-null 'subject' attribute.",
                    null);
            }

            _logger.TracePublishingEvent(@event.Type, options.PubSubName, topic);

            using var activity = _telemetry.StartPublishActivity(@event.Type ?? "unknown", options.PubSubName, topic);

            try
            {
                // Encode the CloudEvent in structured mode so the canonical
                // CloudEvents JSON envelope is preserved as the message payload;
                // Dapr carries it verbatim through PublishByteEventAsync.
                var body = JsonFormatter.EncodeStructuredModeMessage(@event, out var contentType);
                var payload = new ReadOnlyMemory<byte>(body.ToArray());

                var metadata = BuildMetadata(@event, options);

                var client = _clientBuilder.CreateClient(options.DaprClientName);
                await client.PublishByteEventAsync(
                    options.PubSubName,
                    topic,
                    payload,
                    contentType.MediaType,
                    metadata,
                    cancellationToken);

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not DaprPublishException)
            {
                _logger.LogErrorPublishingEvent(ex, @event.Type);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw new DaprPublishException("An error occurred while publishing the event via Dapr", ex);
            }
        }

        private static string? ResolveTopic(CloudEvent @event, DaprPublishOptions options)
        {
            if (!string.IsNullOrEmpty(options.Topic))
                return options.Topic;

            var selected = options.TopicSelector?.Invoke(@event);
            if (!string.IsNullOrEmpty(selected))
                return selected;

            // Final fallback: the CloudEvent subject attribute.
            return @event.Subject;
        }

        private Dictionary<string, string> BuildMetadata(CloudEvent @event, DaprPublishOptions options)
        {
            if (!(options.MapAttributesToMetadata ?? true))
                return EmptyMetadata;

            var prefix = options.MetadataPrefix ?? "ce-";
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

            if (@event.Id is not null)
                metadata[$"{prefix}id"] = @event.Id;
            if (@event.Type is not null)
                metadata[$"{prefix}type"] = @event.Type;
            if (@event.Source is not null)
                metadata[$"{prefix}source"] = @event.Source.ToString();
            if (@event.Time.HasValue)
                metadata[$"{prefix}time"] = @event.Time.Value.ToString("O");
            if (@event.DataContentType is not null)
                metadata[$"{prefix}datacontenttype"] = @event.DataContentType;
            if (@event.Subject is not null)
                metadata[$"{prefix}subject"] = @event.Subject;
            if (@event.SpecVersion is not null)
                metadata[$"{prefix}specversion"] = @event.SpecVersion.VersionId;

            foreach (var attr in @event.GetPopulatedAttributes())
            {
                if (attr.Key.Name is "id" or "type" or "source" or "time" or "datacontenttype" or "subject" or "specversion")
                    continue;

                var value = @event[attr.Key];
                if (value is not null)
                    metadata[$"{prefix}{attr.Key.Name}"] = value.ToString() ?? string.Empty;
            }

            return metadata;
        }

        private static readonly Dictionary<string, string> EmptyMetadata =
            new Dictionary<string, string>(0, StringComparer.Ordinal);
    }
}