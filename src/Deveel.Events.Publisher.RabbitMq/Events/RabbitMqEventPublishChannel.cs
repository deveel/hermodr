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
    /// The implementation of the <see cref="IEventPublishChannel"/> that
    /// is used to publish events to a RabbitMQ exchange.
    /// </summary>
    public sealed class RabbitMqEventPublishChannel : IEventPublishChannel, IAsyncDisposable, IDisposable
    {
        private readonly RabbitMqEventPublishChannelOptions _options;
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
        public RabbitMqEventPublishChannel(
            IOptions<RabbitMqEventPublishChannelOptions> options,
            IConnection connection,
            IRabbitMqMessageFactory messageFactory,
            ILogger<RabbitMqEventPublishChannel>? logger = null)
        {
            _connection = connection;
            _messageFactory = messageFactory;
            _options = options.Value;
            _logger = logger ?? new NullLogger<RabbitMqEventPublishChannel>();
        }

        /// <summary>
        /// Gets or creates a healthy channel, recreating it if it has been closed.
        /// This must be called while holding <see cref="_channelLock"/>.
        /// </summary>
        private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken)
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

            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: _options.PublisherConfirms,
                publisherConfirmationTrackingEnabled: _options.PublisherConfirms,
                outstandingPublisherConfirmationsRateLimiter: null,
                consumerDispatchConcurrency: null);

            _channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);

            if (_options.PublisherConfirms)
                _logger.LogPublisherConfirmsEnabled();

            _logger.LogChannelCreated();
            return _channel;
        }

        /// <inheritdoc/>
        public async Task PublishAsync(CloudEvent @event, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RabbitMqEventPublishChannel));

            _logger.TracePublishingEvent(@event.Type);

            var exchangeName = GetExchangeName(@event);
            if (string.IsNullOrWhiteSpace(exchangeName))
                throw new InvalidOperationException("The exchange name is not defined");

            var routingKey = GetRoutingKey(@event);
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
                DeliveryMode = _options.PersistentMessages
                    ? DeliveryModes.Persistent
                    : DeliveryModes.Transient,
                AppId = _options.ClientName ?? "Deveel.Events"
            };

            await _channelLock.WaitAsync(cancellationToken);
            try
            {
                var channel = await GetOrCreateChannelAsync(cancellationToken);

                // When PublisherConfirms + tracking is on, BasicPublishAsync blocks until
                // the broker ACKs (or throws on NACK). We apply an additional timeout to
                // guarantee the call does not hang indefinitely.
                using var publishCts = _options.PublisherConfirms
                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                    : null;
                publishCts?.CancelAfter(_options.ConfirmTimeout);
                var publishToken = publishCts?.Token ?? cancellationToken;

                await channel.BasicPublishAsync(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    mandatory: _options.Mandatory,
                    basicProperties: props,
                    body: message.Body,
                    cancellationToken: publishToken);

                // When PublisherConfirmationTrackingEnabled = true, BasicPublishAsync already
                // waits for the broker ACK before completing. A broker NACK raises an exception.
                if (_options.PublisherConfirms)
                    _logger.LogEventConfirmed(@event.Type);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The publish CT timed out (confirm timeout exceeded), not the caller's token.
                throw new TimeoutException(
                    $"RabbitMQ broker did not confirm the message within {_options.ConfirmTimeout.TotalSeconds}s.");
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

        private string? GetRoutingKey(CloudEvent @event)
        {
            var attr = @event.GetAttribute(AmqpCloudEventAttributes.AmqpRoutingKeyAttribute);
            return attr != null && attr.Type == CloudEventAttributeType.String
                ? ((string?)@event[attr.Name]) ?? _options.RoutingKey
                : _options.RoutingKey;
        }

        private string? GetExchangeName(CloudEvent @event)
        {
            var attr = @event.GetAttribute(AmqpCloudEventAttributes.AmqpExchangeNameAttribute);
            return attr != null && attr.Type == CloudEventAttributeType.String
                ? ((string?)@event[attr.Name]) ?? _options.ExchangeName
                : _options.ExchangeName;
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
