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
        private static EventPublisherBuilder AddWebhookInfrastructure(this EventPublisherBuilder builder)
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

            return builder;
        }

        private static EventPublisherBuilder AddWebhookChannel(this EventPublisherBuilder builder)
        {
            builder.AddWebhookInfrastructure();

            // Register the concrete channel once under its own type so callers can resolve it
            // directly and supply per-call option overrides.
            builder.Services.TryAddSingleton<WebhookEventPublishChannel>();
            // Expose it as IEventPublishChannel, IOptionsEventPublishChannel and IBatchEventPublishChannel
            // (type-based so ImplementationType is preserved for service-registration assertions).
            builder.Services.AddSingleton<IEventPublishChannel, WebhookEventPublishChannel>();
            builder.Services.AddSingleton<IBatchEventPublishChannel>(sp =>
                sp.GetRequiredService<WebhookEventPublishChannel>());

            return builder;
        }

        /// <summary>
        /// Adds the webhook event publishing channel using the provided configuration action.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="configure">
        /// An action that configures <see cref="WebhookPublishOptions"/>
        /// (endpoint URL, signing secret, retry policy, format, header names, etc.).
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        public static EventPublisherBuilder AddWebhooks(
            this EventPublisherBuilder builder,
            Action<WebhookPublishOptions> configure)
        {
            builder.Services.AddOptions<WebhookPublishOptions>()
                .Configure(configure);
            return builder.AddWebhookChannel();
        }

        /// <summary>
        /// Adds the webhook event publishing channel, binding options from the
        /// specified configuration section.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="sectionPath">
        /// The configuration key path (e.g. <c>"Webhook"</c>) whose sub-keys are
        /// bound to <see cref="WebhookPublishOptions"/>.
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        public static EventPublisherBuilder AddWebhooks(
            this EventPublisherBuilder builder,
            string sectionPath)
        {
            builder.Services.AddOptions<WebhookPublishOptions>()
                .BindConfiguration(sectionPath);
            return builder.AddWebhookChannel();
        }

        /// <summary>
        /// Adds a typed webhook event publishing channel to the event publisher, so
        /// that only events whose data class is <typeparamref name="TEvent"/> are
        /// routed to this channel.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The event data class this channel is keyed against.
        /// </typeparam>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="configure">
        /// An action that configures the type-specific <see cref="WebhookPublishOptions{TEvent}"/>
        /// for this channel.  Non-<c>null</c> delivery properties override the corresponding
        /// values from the general <see cref="WebhookPublishOptions"/> (registered via
        /// <c>AddWebhooks(configure)</c>).  Channel-structural properties are always taken
        /// from the base options.
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        public static EventPublisherBuilder AddWebhooks<TEvent>(
            this EventPublisherBuilder builder,
            Action<WebhookPublishOptions<TEvent>> configure)
            where TEvent : class
        {
            builder.Services.AddOptions<WebhookPublishOptions<TEvent>>()
                .Configure(configure);
            builder.AddWebhookInfrastructure();
            return builder.AddChannel<WebhookEventPublishChannel<TEvent>, TEvent>();
        }

        /// <summary>
        /// Adds a typed webhook event publishing channel to the event publisher, so
        /// that only events whose data class is <typeparamref name="TEvent"/> are
        /// routed to this channel, binding options from the given configuration section.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The event data class this channel is keyed against.
        /// </typeparam>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="sectionPath">
        /// The configuration key path whose sub-keys are bound to the type-specific
        /// <see cref="WebhookPublishOptions{TEvent}"/>.
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        public static EventPublisherBuilder AddWebhooks<TEvent>(
            this EventPublisherBuilder builder,
            string sectionPath)
            where TEvent : class
        {
            builder.Services.AddOptions<WebhookPublishOptions<TEvent>>()
                .BindConfiguration(sectionPath);
            builder.AddWebhookInfrastructure();
            return builder.AddChannel<WebhookEventPublishChannel<TEvent>, TEvent>();
        }

        /// <summary>
        /// Adds or replaces the <see cref="IWebhookSignatureProvider"/> for a
        /// specific algorithm.
        /// </summary>
        /// <typeparam name="TProvider">
        /// The concrete <see cref="IWebhookSignatureProvider"/> implementation to register.
        /// If a provider for the same <see cref="IWebhookSignatureProvider.Algorithm"/> is
        /// already registered, it is replaced.
        /// </typeparam>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the provider to.
        /// </param>
        /// <param name="lifetime">
        /// The DI service lifetime to register the provider with.
        /// Defaults to <see cref="ServiceLifetime.Singleton"/>.
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        public static EventPublisherBuilder UseWebhookSignatureProvider<TProvider>(
            this EventPublisherBuilder builder,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TProvider : class, IWebhookSignatureProvider
        {
            builder.Services.Add(
                new ServiceDescriptor(typeof(IWebhookSignatureProvider), typeof(TProvider), lifetime));
            return builder;
        }
    }
}
