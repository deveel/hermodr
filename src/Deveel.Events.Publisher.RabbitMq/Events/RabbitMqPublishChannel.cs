//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Deveel.Events
{
    /// <summary>
    /// The implementation of the <see cref="IEventPublishChannel{TOptions}"/> that
    /// is used to publish events to a RabbitMQ exchange.
    /// </summary>
    class RabbitMqPublishChannel :
        EventPublishChannel<RabbitMqPublishOptions>,
        IAsyncDisposable, IDisposable
    {
        private readonly IRabbitMqMessageFactory _messageFactory;
        private readonly ILogger _logger;
        private readonly IConnection _connection;

        // Semaphore used to serialize channel creation and publishing so that
        // a channel is not shared across concurrent callers during recovery.
        private readonly SemaphoreSlim _channelLock = new SemaphoreSlim(1, 1);
        private IChannel? _channel;
        private bool _disposed;

        /// <summary>
        /// Constructs the channel with the options and connection to the RabbitMQ server.
        /// </summary>
        /// <param name="options">
        /// The options that configure exchange name, routing key, message format and
        /// publisher-confirm settings.
        /// </param>
        /// <param name="connection">
        /// An active <see cref="RabbitMQ.Client.IConnection"/> to the RabbitMQ broker.
        /// The channel borrows this connection and does not dispose it.
        /// </param>
        /// <param name="messageFactory">
        /// The factory used to convert a <see cref="CloudNative.CloudEvents.CloudEvent"/>
        /// into a <see cref="RabbitMqMessage"/> ready for publishing.
        /// </param>
        /// <param name="validators">
        /// Optional collection of <see cref="IValidateOptions{RabbitMqPublishOptions}"/>
        /// services registered in the DI container. When the collection is empty or <c>null</c>
        /// validation falls back to DataAnnotations.
        /// </param>
        /// <param name="logger">
        /// An optional logger; when <c>null</c> a <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/> is used.
        /// </param>
        public RabbitMqPublishChannel(
            IOptions<RabbitMqPublishOptions> options,
            IConnection connection,
            IRabbitMqMessageFactory messageFactory,
            IEnumerable<IValidateOptions<RabbitMqPublishOptions>>? validators = null,
            ILogger<RabbitMqPublishChannel>? logger = null)
            : base(options.Value, validators)
        {
            _connection = connection;
            _messageFactory = messageFactory;
            _logger = logger ?? new NullLogger<RabbitMqPublishChannel>();
        }

        // ── Effective defaults for nullable value-type properties ──────────────────
        private const bool DefaultPersistentMessages = true;
        private const bool DefaultPublisherConfirms  = true;
        private static readonly TimeSpan DefaultConfirmTimeout = TimeSpan.FromSeconds(5);
        private const bool DefaultMandatory = false;

        /// <inheritdoc/>
        /// <remarks>
        /// Performs a property-level merge: each nullable property in
        /// <paramref name="perCallOptions"/> that is non-<c>null</c> overrides the
        /// corresponding property from <paramref name="defaults"/>; a <c>null</c>
        /// value signals "use the channel-level default" for that property.
        /// </remarks>
        protected override RabbitMqPublishOptions MergeOptions(
            RabbitMqPublishOptions defaults,
            RabbitMqPublishOptions? perCallOptions)
        {
            if (perCallOptions == null)
                return defaults;

            return new RabbitMqPublishOptions
            {
                ConnectionString      = perCallOptions.ConnectionString      ?? defaults.ConnectionString,
                ExchangeName          = perCallOptions.ExchangeName          ?? defaults.ExchangeName,
                RoutingKey            = perCallOptions.RoutingKey            ?? defaults.RoutingKey,
                QueueName             = perCallOptions.QueueName             ?? defaults.QueueName,
                ClientName            = perCallOptions.ClientName            ?? defaults.ClientName,
                JsonSerializerOptions = perCallOptions.JsonSerializerOptions ?? defaults.JsonSerializerOptions,
                MessageFormat         = perCallOptions.MessageFormat         ?? defaults.MessageFormat,
                MessageContent        = perCallOptions.MessageContent        ?? defaults.MessageContent,
                PersistentMessages    = perCallOptions.PersistentMessages    ?? defaults.PersistentMessages,
                PublisherConfirms     = perCallOptions.PublisherConfirms     ?? defaults.PublisherConfirms,
                ConfirmTimeout        = perCallOptions.ConfirmTimeout        ?? defaults.ConfirmTimeout,
                Mandatory             = perCallOptions.Mandatory             ?? defaults.Mandatory,
            };
        }

        /// <summary>
        /// Gets or creates a healthy channel, recreating it if it has been closed.
        /// This must be called while holding <see cref="_channelLock"/>.
        /// </summary>
        private async Task<IChannel> GetOrCreateChannelAsync(
            RabbitMqPublishOptions options,
            CancellationToken cancellationToken)
        {
            if (_channel is { IsOpen: true })
                return _channel;

            // Dispose the old (closed) channel before creating a new one.
            if (_channel != null)
            {
                _logger.LogChannelRecovery();
                await _channel.DisposeAsync();
                _channel = null;
            }

            var publisherConfirms = options.PublisherConfirms ?? DefaultPublisherConfirms;
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: publisherConfirms,
                publisherConfirmationTrackingEnabled: publisherConfirms,
                outstandingPublisherConfirmationsRateLimiter: null,
                consumerDispatchConcurrency: null);

            _channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);

            if (publisherConfirms)
                _logger.LogPublisherConfirmsEnabled();

            _logger.LogChannelCreated();
            return _channel;
        }

        /// <inheritdoc/>
        protected override async Task PublishCoreAsync(
            CloudEvent @event,
            RabbitMqPublishOptions options,
            CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RabbitMqPublishChannel));

            _logger.TracePublishingEvent(@event.Type);

            var exchangeName = GetExchangeName(@event, options);
            if (string.IsNullOrWhiteSpace(exchangeName))
                throw new InvalidOperationException("The exchange name is not defined");

            var routingKey = GetRoutingKey(@event, options);
            if (string.IsNullOrWhiteSpace(routingKey))
                throw new InvalidOperationException("The routing key is not defined");

            var message = _messageFactory.CreateMessage(@event);

            var props = new BasicProperties
            {
                ContentType = message.ContentType,
                ContentEncoding = message.ContentEncoding,
                Type = @event.Type,
                MessageId = @event.Id,
                Timestamp = new AmqpTimestamp(
                    (@event.Time ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()),
                DeliveryMode = (options.PersistentMessages ?? DefaultPersistentMessages)
                    ? DeliveryModes.Persistent
                    : DeliveryModes.Transient,
                AppId = options.ClientName ?? "Deveel.Events"
            };

            await _channelLock.WaitAsync(cancellationToken);
            try
            {
                var channel = await GetOrCreateChannelAsync(options, cancellationToken);

                var publisherConfirms = options.PublisherConfirms ?? DefaultPublisherConfirms;
                // When PublisherConfirms + tracking is on, BasicPublishAsync blocks until
                // the broker ACKs (or throws on NACK). We apply an additional timeout to
                // guarantee the call does not hang indefinitely.
                using var publishCts = publisherConfirms
                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                    : null;
                publishCts?.CancelAfter(options.ConfirmTimeout ?? DefaultConfirmTimeout);
                var publishToken = publishCts?.Token ?? cancellationToken;

                await channel.BasicPublishAsync(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    mandatory: options.Mandatory ?? DefaultMandatory,
                    basicProperties: props,
                    body: message.Body,
                    cancellationToken: publishToken);

                // When PublisherConfirmationTrackingEnabled = true, BasicPublishAsync already
                // waits for the broker ACK before completing. A broker NACK raises an exception.
                if (publisherConfirms)
                    _logger.LogEventConfirmed(@event.Type);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The publish CT timed out (confirm timeout exceeded), not the caller's token.
                throw new TimeoutException(
                    $"RabbitMQ broker did not confirm the message within {(options.ConfirmTimeout ?? DefaultConfirmTimeout).TotalSeconds}s.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TimeoutException)
            {
                _logger.LogErrorPublishingEvent(ex, @event.Type);
                throw;
            }
            finally
            {
                _channelLock.Release();
            }
        }

        private string? GetRoutingKey(CloudEvent @event, RabbitMqPublishOptions options)
        {
            var attr = @event.GetAttribute(AmqpCloudEventAttributes.AmqpRoutingKeyAttribute);
            return attr != null && attr.Type == CloudEventAttributeType.String
                ? ((string?)@event[attr.Name]) ?? options.RoutingKey
                : options.RoutingKey;
        }

        private string? GetExchangeName(CloudEvent @event, RabbitMqPublishOptions options)
        {
            var attr = @event.GetAttribute(AmqpCloudEventAttributes.AmqpExchangeNameAttribute);
            return attr != null && attr.Type == CloudEventAttributeType.String
                ? ((string?)@event[attr.Name]) ?? options.ExchangeName
                : options.ExchangeName;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            _channelLock.Dispose();

            if (_channel != null)
            {
                await _channel.DisposeAsync();
                _channel = null;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
