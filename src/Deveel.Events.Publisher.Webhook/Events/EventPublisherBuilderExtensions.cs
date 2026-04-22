//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Deveel.Events
{
    /// <summary>
    /// Extends <see cref="EventPublisherBuilder"/> with support for the webhook
    /// event publishing channel.
    /// </summary>
    public static class EventPublisherBuilderExtensions
    {
        private static EventPublisherBuilder AddWebhookChannel(this EventPublisherBuilder builder)
        {
            builder.Services.AddHttpClient(WebhookDefaults.HttpClientName);

            // All built-in signature providers
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IWebhookSignatureProvider, HmacSha256SignatureProvider>());
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IWebhookSignatureProvider, HmacSha384SignatureProvider>());
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IWebhookSignatureProvider, HmacSha512SignatureProvider>());
#pragma warning disable CS0618
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IWebhookSignatureProvider, HmacSha1SignatureProvider>());
#pragma warning restore CS0618

            // All built-in message serializers registered as IEventSerializer
            // so they are available to any channel that resolves serializers by that interface.
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEventSerializer, JsonEventSerializer>());
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEventSerializer, XmlEventSerializer>());
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEventSerializer, CloudEventsJsonSerializer>());
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEventSerializer, CloudEventsXmlSerializer>());


            // Register the concrete channel once; expose it under all three service types
            // so consumers can resolve it as IEventPublishChannel (used by EventPublisher),
            // IEventPublishChannel<WebhookPublishOptions>, or IBatchEventPublishChannel<WebhookPublishOptions>.
            builder.Services.AddSingleton<WebhookEventPublishChannel>();
            builder.Services.AddSingleton<IEventPublishChannel, WebhookEventPublishChannel>();
            builder.Services.AddSingleton<IEventPublishChannel<WebhookPublishOptions>>(sp =>
                sp.GetRequiredService<WebhookEventPublishChannel>());
            builder.Services.AddSingleton<IBatchEventPublishChannel<WebhookPublishOptions>>(sp =>
                sp.GetRequiredService<WebhookEventPublishChannel>());

            return builder;
        }

        /// <summary>
        /// Adds the webhook event publishing channel using the provided configuration action.
        /// </summary>
        public static EventPublisherBuilder UseWebhook(
            this EventPublisherBuilder builder,
            Action<WebhookEventPublishChannelOptions> configure)
        {
            builder.Services.AddOptions<WebhookEventPublishChannelOptions>()
                .Configure(configure);
            return builder.AddWebhookChannel();
        }

        /// <summary>
        /// Adds the webhook event publishing channel, binding options from the
        /// specified configuration section.
        /// </summary>
        public static EventPublisherBuilder UseWebhook(
            this EventPublisherBuilder builder,
            string sectionPath)
        {
            builder.Services.AddOptions<WebhookEventPublishChannelOptions>()
                .BindConfiguration(sectionPath);
            return builder.AddWebhookChannel();
        }

        /// <summary>
        /// Adds or replaces the <see cref="IWebhookSignatureProvider"/> for a
        /// specific algorithm.
        /// </summary>
        public static EventPublisherBuilder UseWebhookSignatureProvider<TProvider>(
            this EventPublisherBuilder builder,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TProvider : class, IWebhookSignatureProvider
        {
            builder.Services.Add(
                new ServiceDescriptor(typeof(IWebhookSignatureProvider), typeof(TProvider), lifetime));
            return builder;
        }

        /// <summary>
        /// Registers a custom <see cref="IEventSerializer"/> for the webhook channel,
        /// replacing any previously registered serializer for the same
        /// <see cref="IEventSerializer.Format"/>.
        /// </summary>
        public static EventPublisherBuilder UseWebhookMessageSerializer<TSerializer>(
            this EventPublisherBuilder builder,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TSerializer : class, IEventSerializer
        {
            builder.Services.Add(
                new ServiceDescriptor(typeof(IEventSerializer), typeof(TSerializer), lifetime));
            return builder;
        }
    }
}
