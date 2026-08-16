//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// Extends <see cref="EventPublisherBuilder"/> with support for the HTTP
    /// event publishing channel (lightweight CloudEvents delivery to
    /// statically-configured HTTP endpoints).
    /// </summary>
    public static class EventPublisherBuilderExtensions
    {
        private static EventPublisherBuilder AddHttpInfrastructure(this EventPublisherBuilder builder)
        {
            // Default named client: always present so that
            // IHttpClientFactory.CreateClient(HttpDefaults.HttpClientName) resolves,
            // with the standard resilience pipeline as a baseline.
            builder.Services.AddHttpClient(HttpDefaults.HttpClientName)
                .AddStandardResilienceHandler();

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
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEventSerializer, CloudEventsBinarySerializer>());

            return builder;
        }

        private static EventPublisherBuilder AddHttpChannel(
            this EventPublisherBuilder builder,
            HttpPublishOptions? eagerOptions)
        {
            builder.AddHttpInfrastructure();

            // Wire one named HttpClient per endpoint carrying the endpoint's
            // authentication and resilience settings. When an endpoint does not name
            // its own client, a unique name is assigned so its configuration does not
            // collide with other endpoints sharing the default client.
            var usedNames = new HashSet<string>(StringComparer.Ordinal)
            {
                HttpDefaults.HttpClientName
            };

            if (eagerOptions is { Endpoints.Count: > 0 })
            {
                for (var i = 0; i < eagerOptions.Endpoints.Count; i++)
                    RegisterEndpointClient(builder.Services, eagerOptions.Endpoints[i], i, usedNames);
            }

            // Register the concrete channel and add it to the publisher pipeline (so the
            // middleware and dead-letter/error-handling pipeline applies), exposing it as
            // a keyed IEventPublishChannel for this publisher.
            return builder.AddChannel<HttpPublishChannel>();
        }

        private static void RegisterEndpointClient(
            IServiceCollection services,
            HttpEndpoint endpoint,
            int index,
            ISet<string> usedNames)
        {
            if (endpoint == null)
                return;

            var name = endpoint.HttpClientName;
            if (string.IsNullOrWhiteSpace(name))
            {
                // Assign a unique per-endpoint client name so the endpoint's auth and
                // resilience configuration does not collide with other unnamed endpoints.
                name = $"{HttpDefaults.HttpClientName}#{index}";
                endpoint.HttpClientName = name;
            }

            // Endpoints that explicitly reuse an already-registered name inherit that
            // client's configuration (first registration wins).
            if (!usedNames.Add(name))
                return;

            var clientBuilder = services.AddHttpClient(name);
            clientBuilder.AddStandardResilienceHandler()
                .Configure(options => endpoint.Resilience?.Apply(options));

            if (endpoint.Authentication is not null)
            {
                switch (endpoint.Authentication.AuthenticationMode)
                {
                    case HttpAuthenticationMode.Bearer:
                        clientBuilder.AddHttpMessageHandler(
                            () => new BearerTokenHttpHandler(endpoint.Authentication.BearerToken!));
                        break;

                    case HttpAuthenticationMode.ApiKey:
                        clientBuilder.AddHttpMessageHandler(
                            () => new ApiKeyHttpHandler(
                                endpoint.Authentication.ApiKey!,
                                endpoint.Authentication.ApiKeyHeaderName));
                        break;
                }
            }
        }

        /// <summary>
        /// Adds the HTTP event publishing channel using the provided configuration action.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="configure">
        /// An action that configures <see cref="HttpPublishOptions"/> (the static
        /// endpoint set, per-endpoint authentication, resilience and formats).
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        /// <remarks>
        /// A named <see cref="System.Net.Http.HttpClient"/> is registered per endpoint
        /// (with <c>AddStandardResilienceHandler()</c> and the configured authentication
        /// handler), so the endpoints are expected to be statically known at registration
        /// time. Endpoints configured through this action are wired immediately.
        /// </remarks>
        public static EventPublisherBuilder AddHttpEventPublisherChannel(
            this EventPublisherBuilder builder,
            Action<HttpPublishOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var options = new HttpPublishOptions();
            configure(options);
            builder.Services.AddSingleton(Options.Create(options));

            return builder.AddHttpChannel(options);
        }

        /// <summary>
        /// Adds the HTTP event publishing channel, binding options from the
        /// specified configuration section.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="sectionPath">
        /// The configuration key path (e.g. <c>"Http"</c>) whose sub-keys are
        /// bound to <see cref="HttpPublishOptions"/>.
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        /// <remarks>
        /// <para>
        /// When the host has registered an <see cref="IConfiguration"/> instance in the
        /// service collection (as typical host builders do), the section is bound eagerly
        /// so that per-endpoint named clients can be registered at setup time. Otherwise
        /// the options are bound lazily and only the default
        /// <see cref="HttpDefaults.HttpClientName"/> client is pre-registered; attach
        /// per-endpoint authentication via <see cref="ConfigureHttpClient"/>.
        /// </para>
        /// </remarks>
        public static EventPublisherBuilder AddHttpEventPublisherChannel(
            this EventPublisherBuilder builder,
            string sectionPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

            HttpPublishOptions? eagerOptions = null;
            var configuration = FindConfigurationInstance(builder.Services);
            if (configuration is not null)
            {
                eagerOptions = new HttpPublishOptions();
                configuration.GetSection(sectionPath).Bind(eagerOptions);
                builder.Services.AddSingleton(Options.Create(eagerOptions));
            }
            else
            {
                builder.Services.AddOptions<HttpPublishOptions>()
                    .BindConfiguration(sectionPath);
            }

            return builder.AddHttpChannel(eagerOptions);
        }

        /// <summary>
        /// Adds a typed HTTP event publishing channel to the event publisher, so
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
        /// An action that configures the type-specific <see cref="HttpPublishOptions{TEvent}"/>
        /// for this channel. Non-<c>null</c> delivery properties override the corresponding
        /// values from the general <see cref="HttpPublishOptions"/> (registered via
        /// <c>AddHttpEventPublisherChannel(configure)</c>).
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        /// <remarks>
        /// The typed channel reuses the endpoint clients and infrastructure registered by
        /// the non-typed <c>AddHttpEventPublisherChannel</c> call; register it alongside
        /// that call to obtain per-endpoint wiring.
        /// </remarks>
        public static EventPublisherBuilder AddHttpEventPublisherChannel<TEvent>(
            this EventPublisherBuilder builder,
            Action<HttpPublishOptions<TEvent>>? configure = null)
            where TEvent : class
        {
            if (configure is not null)
                builder.Services.AddOptions<HttpPublishOptions<TEvent>>().Configure(configure);

            builder.AddHttpInfrastructure();
            return builder.AddChannel<HttpPublishChannel<TEvent>, TEvent>();
        }

        /// <summary>
        /// Adds a typed HTTP event publishing channel to the event publisher, so
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
        /// <see cref="HttpPublishOptions{TEvent}"/>.
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        /// <remarks>
        /// The typed channel reuses the endpoint clients and infrastructure registered by
        /// the non-typed <c>AddHttpEventPublisherChannel</c> call; register it alongside
        /// that call to obtain per-endpoint wiring.
        /// </remarks>
        public static EventPublisherBuilder AddHttpEventPublisherChannel<TEvent>(
            this EventPublisherBuilder builder,
            string sectionPath)
            where TEvent : class
        {
            builder.Services.AddOptions<HttpPublishOptions<TEvent>>()
                .BindConfiguration(sectionPath);

            builder.AddHttpInfrastructure();
            return builder.AddChannel<HttpPublishChannel<TEvent>, TEvent>();
        }

        /// <summary>
        /// Configures the named <see cref="System.Net.Http.HttpClient"/> with the given
        /// name, allowing endpoint clients to be customized — for example to attach a
        /// custom <see cref="System.Net.Http.DelegatingHandler"/>, configure TLS, a base
        /// address, or timeouts.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to configure the client for.
        /// </param>
        /// <param name="clientName">
        /// The name of the client to configure. Use an endpoint's
        /// <see cref="HttpEndpoint.HttpClientName"/>, or <see cref="HttpDefaults.HttpClientName"/>
        /// to configure the default client.
        /// </param>
        /// <param name="configure">
        /// A delegate that configures the <see cref="IHttpClientBuilder"/> (for example
        /// <c>builder.AddHttpMessageHandler&lt;TCustom&gt;()</c>).
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        public static EventPublisherBuilder ConfigureHttpClient(
            this EventPublisherBuilder builder,
            string clientName,
            Action<IHttpClientBuilder> configure)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
            ArgumentNullException.ThrowIfNull(configure);

            configure(builder.Services.AddHttpClient(clientName));
            return builder;
        }

        private static IConfiguration? FindConfigurationInstance(IServiceCollection services)
        {
            // Host builders register IConfiguration as an instance singleton, which makes
            // the actual instance discoverable before the container is built. Scoped/transient
            // or factory-based registrations cannot be evaluated here and are skipped.
            foreach (var descriptor in services)
            {
                if (descriptor.ServiceType == typeof(IConfiguration) &&
                    descriptor.ImplementationInstance is IConfiguration configuration)
                {
                    return configuration;
                }
            }

            return null;
        }
    }
}