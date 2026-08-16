//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//



using System.Diagnostics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Hermodr
{
    /// <summary>
    /// An <see cref="EventPublishChannel{TOptions}"/> implementation that
    /// publishes <see cref="CloudEvent"/> instances to a RabbitMQ exchange.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The exchange name and routing key are resolved in precedence order: a
    /// <c>amqp-exchange</c> / <c>amqp-routingkey</c> CloudEvent attribute on the
    /// event itself overrides the channel-level defaults set in
    /// <see cref="RabbitMqPublishOptions"/>.
    /// </para>
    /// <para>
    /// A single <see cref="RabbitMQ.Client.IChannel"/> is created lazily on the first
    /// publish and reused across calls. If the broker closes the channel it is
    /// automatically recreated on the next publish. A <see cref="SemaphoreSlim"/> ensures
    /// channel creation and publishing are never interleaved across concurrent callers.
    /// </para>
    /// <para>
    /// When <see cref="RabbitMqPublishOptions.PublisherConfirms"/> is <c>true</c> (the
    /// default) the publish call blocks until the broker acknowledges the message or the
    /// <see cref="RabbitMqPublishOptions.ConfirmTimeout"/> elapses, at which point a
    /// <see cref="TimeoutException"/> is thrown.
    /// </para>
    /// </remarks>
    class RabbitMqPublishChannel :
        EventPublishChannel<RabbitMqPublishOptions>,
        IAsyncDisposable, IDisposable
    {
        private readonly RabbitMqTransportTelemetry _telemetry = new();
        private readonly ILogger _logger;
        private readonly IConnection _connection;
        private IRabbitMqMessageFactory? _messageFactory;

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
        /// <param name="serviceProvider">
        /// The service provider used to lazily resolve <see cref="IRabbitMqMessageFactory"/>.
        /// </param>
        /// <param name="logger">
        /// An optional logger; when <c>null</c> a <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/> is used.
        /// </param>
        public RabbitMqPublishChannel(
            IOptions<RabbitMqPublishOptions> options,
            IConnection connection,
            IServiceProvider serviceProvider,
            ILogger<RabbitMqPublishChannel>? logger = null)
            : base(options.Value, serviceProvider)
        {
            _connection = connection;
            _logger = logger ?? new NullLogger<RabbitMqPublishChannel>();
        }

        private IRabbitMqMessageFactory MessageFactory =>
            _messageFactory ??= ServiceProvider.GetRequiredService<IRabbitMqMessageFactory>();

        // ── Effective defaults for nullable value-type properties ──────────────────
        private const bool DefaultPersistentMessages = true;
        private const bool DefaultPublisherConfirms  = true;
        private static readonly TimeSpan DefaultConfirmTimeout = TimeSpan.FromSeconds(5);
        private const bool DefaultMandatory = false;

        /// <inheritdoc/>
        /// <remarks>
        /// Overrides the base coarse merge with a property-level merge: each nullable
        /// property in <paramref name="perCallOptions"/> that is non-<c>null</c> overrides
        /// the corresponding property from <paramref name="defaults"/>; a <c>null</c> value
        /// signals "use the channel-level default" for that property.
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
                ScheduleDeliveryAt    = perCallOptions.ScheduleDeliveryAt    ?? defaults.ScheduleDeliveryAt,
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

            var message = MessageFactory.CreateMessage(@event);

            var props = new BasicProperties
            {
                ContentType = message.ContentType,
                ContentEncoding = message.ContentEncoding,
                Type = @event.Type,
                MessageId = @event.Id,
                Timestamp = new AmqpTimestamp(
                    (@event.Time ?? ServiceProvider.GetRequiredService<IEventSystemTime>().UtcNow).ToUnixTimeSeconds()),
                DeliveryMode = (options.PersistentMessages ?? DefaultPersistentMessages)
                    ? DeliveryModes.Persistent
                    : DeliveryModes.Transient,
                AppId = options.ClientName ?? "Hermodr"
            };

            await _channelLock.WaitAsync(cancellationToken);
            try
            {
                var channel = await GetOrCreateChannelAsync(options, cancellationToken);

                var publisherConfirms = options.PublisherConfirms ?? DefaultPublisherConfirms;
                using var publishCts = publisherConfirms
                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                    : null;
                publishCts?.CancelAfter(options.ConfirmTimeout ?? DefaultConfirmTimeout);
                var publishToken = publishCts?.Token ?? cancellationToken;

                using var activity = _telemetry.StartPublishActivity(@event.Type ?? "unknown", exchangeName, routingKey);

                await channel.BasicPublishAsync(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    mandatory: options.Mandatory ?? DefaultMandatory,
                    basicProperties: props,
                    body: message.Body,
                    cancellationToken: publishToken);

                if (publisherConfirms)
                    _logger.LogEventConfirmed(@event.Type);

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
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
