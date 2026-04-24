//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using MassTransit;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    /// <summary>
    /// An implementation of <see cref="IEventPublishChannel"/> that publishes
    /// CloudEvents via MassTransit.
    /// </summary>
    public sealed class MassTransitEventPublishChannel : IEventPublishChannel
    {
        private readonly MassTransitEventPublishChannelOptions _options;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ISendEndpointProvider _sendEndpointProvider;
        private readonly ILogger _logger;

        private static readonly JsonEventFormatter JsonFormatter = new JsonEventFormatter();

        /// <summary>
        /// Constructs the channel with MassTransit endpoints and options.
        /// </summary>
        /// <param name="options">
        /// The options that configure this channel (destination address, header mapping, etc.).
        /// </param>
        /// <param name="publishEndpoint">
        /// The MassTransit publish endpoint used when no
        /// <see cref="MassTransitEventPublishChannelOptions.DestinationAddress"/> is set.
        /// </param>
        /// <param name="sendEndpointProvider">
        /// The MassTransit send-endpoint provider used when a destination address is configured.
        /// </param>
        /// <param name="logger">
        /// An optional logger; when <c>null</c> a <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/> is used.
        /// </param>
        public MassTransitEventPublishChannel(
            IOptions<MassTransitEventPublishChannelOptions> options,
            IPublishEndpoint publishEndpoint,
            ISendEndpointProvider sendEndpointProvider,
            ILogger<MassTransitEventPublishChannel>? logger = null)
        {
            _options = options.Value;
            _publishEndpoint = publishEndpoint;
            _sendEndpointProvider = sendEndpointProvider;
            _logger = logger ?? NullLogger<MassTransitEventPublishChannel>.Instance;
        }

        /// <inheritdoc/>
        public async Task PublishAsync(CloudEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            _logger.TracePublishingEvent(@event.Type);

            try
            {
                var body = JsonFormatter.EncodeStructuredModeMessage(@event, out var contentType);
                var payload = body.ToArray();

                if (_options.DestinationAddress is not null)
                {
                    var endpoint = await _sendEndpointProvider.GetSendEndpoint(_options.DestinationAddress);
                    await endpoint.Send<ICloudEventMessage>(
                        new CloudEventMessage(payload, contentType.MediaType),
                        ctx => MapHeaders(ctx, @event),
                        cancellationToken);
                }
                else
                {
                    await _publishEndpoint.Publish<ICloudEventMessage>(
                        new CloudEventMessage(payload, contentType.MediaType),
                        ctx => MapHeaders(ctx, @event),
                        cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogErrorPublishingEvent(ex, @event.Type);
                throw new EventPublishException("An error occurred while publishing the event via MassTransit", ex);
            }
        }

        private void MapHeaders(SendContext ctx, CloudEvent @event)
        {
            if (!_options.MapAttributesToHeaders)
                return;

            if (@event.Id is not null)
                ctx.Headers.Set("ce-id", @event.Id);
            if (@event.Type is not null)
                ctx.Headers.Set("ce-type", @event.Type);
            if (@event.Source is not null)
                ctx.Headers.Set("ce-source", @event.Source.ToString());
            if (@event.Time.HasValue)
                ctx.Headers.Set("ce-time", @event.Time.Value.ToString("O"));
            if (@event.DataContentType is not null)
                ctx.Headers.Set("ce-datacontenttype", @event.DataContentType);
            if (@event.Subject is not null)
                ctx.Headers.Set("ce-subject", @event.Subject);
            if (@event.SpecVersion is not null)
                ctx.Headers.Set("ce-specversion", @event.SpecVersion.VersionId);

            foreach (var attr in @event.GetPopulatedAttributes())
            {
                if (attr.Key.Name is "id" or "type" or "source" or "time" or "datacontenttype" or "subject" or "specversion")
                    continue;

                var value = @event[attr.Key];
                if (value is not null)
                    ctx.Headers.Set($"ce-{attr.Key.Name}", value.ToString());
            }
        }
    }
}
