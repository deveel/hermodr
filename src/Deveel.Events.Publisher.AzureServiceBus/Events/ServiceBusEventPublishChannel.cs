//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Runtime.Serialization;

using Azure.Messaging.ServiceBus;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Deveel.Events {
    /// <summary>
    /// A channel that publishes events to an Azure Service Bus queue.
    /// </summary>
    public class ServiceBusEventPublishChannel :
        EventPublishChannelBase<ServiceBusEventPublishOptions>,
        IAsyncDisposable, IDisposable {
		private ServiceBusSender? sender;
		private ServiceBusClient? client;
		private readonly ServiceBusMessageFactory messageCreator;
		private readonly ILogger logger;

		private bool disposed;

        /// <summary>
        /// Creates a new instance of the channel, using the specified options,
        /// client factory and message creator.
        /// </summary>
        /// <param name="options">
        /// The options to configure the channel.
        /// </param>
        /// <param name="clientFactory">
        /// A factory to create the client to the Azure Service Bus.
        /// </param>
        /// <param name="messageCreator">
        /// The factory to create the message to send to the queue.
        /// </param>
        /// <param name="validators">
        /// Optional collection of <see cref="IValidateOptions{ServiceBusEventPublishOptions}"/>
        /// services registered in the DI container. When the collection is empty or <c>null</c>
        /// validation falls back to DataAnnotations.
        /// </param>
        /// <param name="logger">
        /// A logger to record the operations of the channel.
        /// </param>
        public ServiceBusEventPublishChannel(
			IOptions<ServiceBusEventPublishOptions> options,
			IServiceBusClientFactory clientFactory,
			ServiceBusMessageFactory messageCreator,
			IEnumerable<IValidateOptions<ServiceBusEventPublishOptions>>? validators = null,
			ILogger<ServiceBusEventPublishChannel>? logger = null)
			: base(options.Value, validators) {

			var clientOptions = options.Value.ClientOptions ?? new ServiceBusClientOptions();

			client = clientFactory.CreateClient(options.Value.ConnectionString, clientOptions);

			sender = client.CreateSender(options.Value.QueueName);
			this.messageCreator = messageCreator;
			this.logger = logger ?? NullLogger<ServiceBusEventPublishChannel>.Instance;
		}

		private void ThrowIfDisposed() {
			if (disposed)
				throw new ObjectDisposedException(nameof(ServiceBusEventPublishChannel));
		}

        /// <inheritdoc/>
        /// <remarks>
        /// Performs a property-level merge: non-empty string properties in
        /// <paramref name="perCallOptions"/> override the corresponding values from
        /// <paramref name="defaults"/>; an empty or <c>null</c> string signals
        /// "use the channel-level default".  <see cref="ServiceBusEventPublishOptions.ClientOptions"/>
        /// is taken from <paramref name="perCallOptions"/> when non-<c>null</c>.
        /// </remarks>
        protected override ServiceBusEventPublishOptions MergeOptions(
            ServiceBusEventPublishOptions defaults,
            ServiceBusEventPublishOptions? perCallOptions)
        {
            if (perCallOptions == null)
                return defaults;

            return new ServiceBusEventPublishOptions
            {
                ConnectionString = !string.IsNullOrWhiteSpace(perCallOptions.ConnectionString)
                    ? perCallOptions.ConnectionString
                    : defaults.ConnectionString,
                QueueName = !string.IsNullOrWhiteSpace(perCallOptions.QueueName)
                    ? perCallOptions.QueueName
                    : defaults.QueueName,
                ClientOptions = perCallOptions.ClientOptions ?? defaults.ClientOptions,
            };
        }

        /// <inheritdoc />
        protected override async Task PublishCoreAsync(
			CloudEvent @event,
			ServiceBusEventPublishOptions options,
			CancellationToken cancellationToken) {
			ThrowIfDisposed();
			cancellationToken.ThrowIfCancellationRequested();

            logger.TracePublishingEvent(@event.Type);

			try {
				await sender!.SendMessageAsync(messageCreator.CreateMessage(@event), cancellationToken);
			} catch (ServiceBusException ex) {
				logger.LogErrorPublishingEvent(ex, @event.Type);
				throw new EventPublishException("The ServiceBus service caused an error", ex);
			} catch (SerializationException ex) {
				logger.LogErrorPublishingEvent(ex, @event.Type);
				throw new EventPublishException("It was not possible to serialize the message", ex);
			} catch (Exception ex) {
				logger.LogErrorPublishingEvent(ex, @event.Type);
				throw;
			}
		}

        /// <inheritdoc />
        public void Dispose() {
			if (disposed)
				return;

			DisposeAsyncCore().GetAwaiter().GetResult();
		}

        /// <inheritdoc />
        public async ValueTask DisposeAsync() {
			if (disposed)
				return;

			await DisposeAsyncCore();

			disposed = true;
		}

		private async Task DisposeAsyncCore() {
			if (sender != null) {
				await sender.DisposeAsync();
				sender = null;
			}

			if (client != null) {
				await client.DisposeAsync();
				client = null;
			}
		}
	}
}
