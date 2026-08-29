//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
namespace Hermodr
{
    /// <summary>
    /// Extends <see cref="EventPublisherBuilder"/> with support for the gRPC
    /// event publishing channel (CloudEvents delivery to statically-configured
    /// gRPC endpoints via <see href="https://github.com/grpc/grpc-dotnet">grpc-dotnet</see>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The channel delegates the actual gRPC RPC to a pluggable
    /// <see cref="IGrpcEventSender"/> strategy. Callers register a sender via
    /// <see cref="AddGrpcEventSender{TSender}(EventPublisherBuilder)"/> that calls their
    /// <c>.proto</c>-generated gRPC client. A built-in sender targeting the
    /// canonical CloudEvents protobuf format is planned for v1.6.0 alongside the
    /// <c>.proto</c> generator.
    /// </para>
    /// <para>
    /// TLS, mTLS and <c>CallCredentials</c> are configured through the standard
    /// <c>IHttpClientFactory</c> / <c>SocketsHttpHandler</c> surface (each
    /// endpoint wraps a named <c>HttpClient</c> as a <c>GrpcChannel</c>), so no
    /// bespoke gRPC configuration API is introduced.
    /// </para>
    /// </remarks>
    public static class EventPublisherBuilderExtensions
    {
        private static EventPublisherBuilder AddGrpcInfrastructure(this EventPublisherBuilder builder)
        {
            // Default named HttpClient: always present so that
            // IHttpClientFactory.CreateClient(GrpcDefaults.HttpClientName) resolves.
            // Grpc.Net.Client wraps this HttpClient as a GrpcChannel.
            //
            // The handler lifetime is infinite because the GrpcChannelFactory caches
            // GrpcChannel instances (and therefore the HttpClient/handler they
            // captured) for the lifetime of the application: a rotating handler would
            // never take effect and would keep the replaced handler alive. Refresh
            // connections (and DNS) by configuring SocketsHttpHandler
            // PooledConnectionLifetime via ConfigureGrpcChannel instead.
            builder.Services.AddHttpClient(GrpcDefaults.HttpClientName)
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

            // The gRPC channel factory resolves/creates GrpcChannel instances per endpoint.
            builder.Services.TryAddSingleton<IGrpcChannelFactory, GrpcChannelFactory>();

            // Fallback sender: throws with a helpful message until the caller
            // registers a real IGrpcEventSender. TryAdd ensures user registrations win.
            builder.Services.TryAddSingleton<IGrpcEventSender, ThrowingGrpcEventSender>();

            return builder;
        }

        private static EventPublisherBuilder AddGrpcChannel(this EventPublisherBuilder builder)
        {
            if (builder.Services.Any(d => d.ServiceType == typeof(GrpcPublishChannel)))
            {
                throw new InvalidOperationException(
                    "The gRPC event publisher channel has already been added to this " +
                    "EventPublisherBuilder. Calling AddGrpcEventPublisherChannel more than once " +
                    "would register the channel twice and deliver every event multiple times; " +
                    "configure all endpoints on the existing options instead.");
            }

            builder.AddGrpcInfrastructure();

            // Register the concrete channel once under its own type so callers can resolve it
            // directly and supply per-call option overrides.
            builder.Services.TryAddSingleton<GrpcPublishChannel>();

            // Expose it as IEventPublishChannel and IBatchEventPublishChannel
            // (type-based so ImplementationType is preserved for service-registration assertions).
            builder.Services.AddSingleton<IEventPublishChannel, GrpcPublishChannel>();
            builder.Services.AddSingleton<IBatchEventPublishChannel, GrpcPublishChannel>();

            // Also add it to the publisher pipeline (keyed) so middleware/dead-letter applies.
            return builder.AddChannel<GrpcPublishChannel>();
        }

        /// <summary>
        /// Adds the gRPC event publishing channel using the provided configuration action.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="configure">
        /// An action that configures <see cref="GrpcPublishOptions"/> (the static
        /// endpoint set, per-endpoint sender names, deadlines and headers).
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        /// <remarks>
        /// Callers must also register an <see cref="IGrpcEventSender"/> via
        /// <see cref="AddGrpcEventSender{TSender}(EventPublisherBuilder)"/> that calls their
        /// <c>.proto</c>-generated gRPC client.
        /// </remarks>
        public static EventPublisherBuilder AddGrpcEventPublisherChannel(
            this EventPublisherBuilder builder,
            Action<GrpcPublishOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var options = new GrpcPublishOptions();
            configure(options);
            builder.Services.AddSingleton(Options.Create(options));

            return builder.AddGrpcChannel();
        }

        /// <summary>
        /// Adds the gRPC event publishing channel, binding options from the
        /// specified configuration section.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="sectionPath">
        /// The configuration key path (e.g. <c>"Grpc"</c>) whose sub-keys are
        /// bound to <see cref="GrpcPublishOptions"/>.
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        public static EventPublisherBuilder AddGrpcEventPublisherChannel(
            this EventPublisherBuilder builder,
            string sectionPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

            builder.Services.AddOptions<GrpcPublishOptions>()
                .BindConfiguration(sectionPath);

            return builder.AddGrpcChannel();
        }

        /// <summary>
        /// Adds a typed gRPC event publishing channel to the event publisher, so
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
        /// An optional action that configures the type-specific
        /// <see cref="GrpcPublishOptions{TEvent}"/> for this channel. Non-<c>null</c>
        /// delivery properties override the corresponding values from the general
        /// <see cref="GrpcPublishOptions"/> (registered via
        /// <c>AddGrpcEventPublisherChannel(configure)</c>).
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        /// <remarks>
        /// The typed channel reuses the gRPC infrastructure registered by the
        /// non-typed <c>AddGrpcEventPublisherChannel</c> call; register it alongside
        /// that call to obtain shared sender and channel-factory wiring.
        /// </remarks>
        public static EventPublisherBuilder AddGrpcEventPublisherChannel<TEvent>(
            this EventPublisherBuilder builder,
            Action<GrpcPublishOptions<TEvent>>? configure = null)
            where TEvent : class
        {
            if (configure is not null)
                builder.Services.AddOptions<GrpcPublishOptions<TEvent>>().Configure(configure);

            builder.AddGrpcInfrastructure();
            return builder.AddChannel<GrpcPublishChannel<TEvent>, TEvent>();
        }

        /// <summary>
        /// Adds a typed gRPC event publishing channel to the event publisher, so
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
        /// <see cref="GrpcPublishOptions{TEvent}"/>.
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        public static EventPublisherBuilder AddGrpcEventPublisherChannel<TEvent>(
            this EventPublisherBuilder builder,
            string sectionPath)
            where TEvent : class
        {
            builder.Services.AddOptions<GrpcPublishOptions<TEvent>>()
                .BindConfiguration(sectionPath);

            builder.AddGrpcInfrastructure();
            return builder.AddChannel<GrpcPublishChannel<TEvent>, TEvent>();
        }

        /// <summary>
        /// Registers an <see cref="IGrpcEventSender"/> implementation as the
        /// default sender for the gRPC publishing channel, replacing the
        /// fallback sender.
        /// </summary>
        /// <typeparam name="TSender">
        /// The concrete <see cref="IGrpcEventSender"/> implementation to register.
        /// </typeparam>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to register the sender on.
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        /// <remarks>
        /// The sender typically wraps a <c>.proto</c>-generated gRPC client and
        /// invokes the appropriate RPC method using the <see cref="GrpcCallContext"/>
        /// provided by the channel.
        /// </remarks>
        public static EventPublisherBuilder AddGrpcEventSender<TSender>(
            this EventPublisherBuilder builder)
            where TSender : class, IGrpcEventSender
        {
            builder.Services.RemoveAll<IGrpcEventSender>();
            builder.Services.AddSingleton<IGrpcEventSender, TSender>();
            return builder;
        }

        /// <summary>
        /// Registers a named <see cref="IGrpcEventSender"/> implementation that
        /// can be selected per-endpoint via <see cref="GrpcEndpoint.SenderName"/>.
        /// </summary>
        /// <typeparam name="TSender">
        /// The concrete <see cref="IGrpcEventSender"/> implementation to register.
        /// </typeparam>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to register the sender on.
        /// </param>
        /// <param name="name">
        /// The name used to select this sender on a <see cref="GrpcEndpoint"/>.
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        public static EventPublisherBuilder AddGrpcEventSender<TSender>(
            this EventPublisherBuilder builder,
            string name)
            where TSender : class, IGrpcEventSender
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            builder.Services.AddKeyedSingleton<IGrpcEventSender, TSender>(name);
            return builder;
        }

        /// <summary>
        /// Configures the named <see cref="System.Net.Http.HttpClient"/> with the given
        /// name, allowing endpoint clients to be customized — for example to attach a
        /// custom <see cref="System.Net.Http.DelegatingHandler"/>, configure TLS/mTLS,
        /// a base address, or timeouts.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to configure the client for.
        /// </param>
        /// <param name="clientName">
        /// The name of the client to configure. Use an endpoint's
        /// <see cref="GrpcEndpoint.HttpClientName"/>, or <see cref="GrpcDefaults.HttpClientName"/>
        /// to configure the default client.
        /// </param>
        /// <param name="configure">
        /// A delegate that configures the <see cref="IHttpClientBuilder"/> (for example
        /// <c>builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { ... })</c>).
        /// </param>
        /// <returns>
        /// The same <see cref="EventPublisherBuilder"/> so that additional calls can be chained.
        /// </returns>
        public static EventPublisherBuilder ConfigureGrpcChannel(
            this EventPublisherBuilder builder,
            string clientName,
            Action<IHttpClientBuilder> configure)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
            ArgumentNullException.ThrowIfNull(configure);

            configure(builder.Services.AddHttpClient(clientName));
            return builder;
        }
    }
}
