//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Deveel.Events {
    /// <summary>
    /// A builder to configure an <see cref="EventPublisher"/> (and its services) for a
    /// specific named pipeline slot.
    /// </summary>
    public sealed class EventPublisherBuilder {
        private readonly EventPublisherPipeline _pipeline = new();
        private Type _publisherType = typeof(EventPublisher);

        // Tracks the channels for THIS builder instance only.
        // Using a per-instance list (instead of reading from keyed DI) ensures that
        // multiple registrations under the same name do not accumulate each other's
        // channels in the pipeline.
        private readonly List<Func<IServiceProvider, IEventPublishChannel>> _channelFactories = new();

        internal EventPublisherBuilder(IServiceCollection services, string name = "") {
            Services = services;
            Name = name;
            AddDefaultServices();
        }

        /// <summary>
        /// Gets the name of this publisher pipeline slot.
        /// An empty string denotes the default (unnamed) pipeline.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the collection of services used to configure the publisher.
        /// </summary>
        public IServiceCollection Services { get; }

        private void AddDefaultServices() {
            // Shared infrastructure services (registered once regardless of how many named publishers exist).
            Services.AddOptions<EventPublisherOptions>(Name).ValidateOnStart();
            Services.TryAddSingleton<IValidateOptions<EventPublisherOptions>>(
                _ => new EventPublisherOptionsValidator(Services));
            Services.TryAddSingleton<IEventIdGenerator>(EventGuidGenerator.Default);
            Services.TryAddSingleton<IEventSystemTime>(EventSystemTime.Instance);
            Services.TryAddSingleton<IEventFactory, EventFactory>();


            // Capture builder state (by-reference) so the factory lambda reads the
            // final state when it is first invoked (after all Use<T>() / AddChannel<T>()
            // calls have completed).
            var builder = this;

            // Keyed IEventPublisher[Name] ─ one per named slot.
            Services.AddKeyedSingleton<IEventPublisher>(Name, (sp, _) =>
            {
                var pipeline = builder._pipeline;
                var publisherType = builder._publisherType;

                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<EventPublisherOptions>>();
                var options = Microsoft.Extensions.Options.Options.Create(optionsMonitor.Get(builder.Name));

                // Read channels from this builder's own isolated list, not from keyed DI.
                // This guarantees that a second AddEventPublisher() call for the same name
                // (or the default empty name) does not appear in this pipeline.
                var channels = builder._channelFactories.Select(f => f(sp)).ToList();
                var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<EventPublisher>>();

                if (publisherType == typeof(EventPublisher))
                    return new EventPublisher(options, channels, sp, pipeline, logger);

                // Custom publisher sub-class: created via ActivatorUtilities so it can
                // use its own constructor while still receiving options and channels.
                return (IEventPublisher)ActivatorUtilities.CreateInstance(
                    sp, publisherType, options, (IEnumerable<IEventPublishChannel>)channels);
            });

            if (string.IsNullOrEmpty(Name)) {
                // Default (unnamed) publisher ─ also expose as non-keyed IEventPublisher
                // and as the concrete EventPublisher for backward-compatible injection.
                Services.TryAddSingleton<IEventPublisher>(
                    sp => sp.GetRequiredKeyedService<IEventPublisher>(string.Empty));
                Services.TryAddSingleton<EventPublisher>(
                    sp => (EventPublisher)sp.GetRequiredKeyedService<IEventPublisher>(string.Empty));
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Options
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Configures the options for this publisher pipeline using the given action.
        /// </summary>
        public EventPublisherBuilder Configure(Action<EventPublisherOptions> configure) {
            Services.Configure(Name, configure);
            return this;
        }

        /// <summary>
        /// Configures the options for this publisher pipeline from the configuration
        /// section at <paramref name="sectionPath"/>.
        /// </summary>
        public EventPublisherBuilder Configure(string sectionPath) {
            Services.AddOptions<EventPublisherOptions>(Name)
                .BindConfiguration(sectionPath);
            return this;
        }

        // ─────────────────────────────────────────────────────────────────
        // Pipeline middleware
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Appends a middleware component of type <typeparamref name="TMiddleware"/> to
        /// the publish pipeline for this publisher.
        /// </summary>
        /// <typeparam name="TMiddleware">
        /// A concrete type that implements <see cref="IEventMiddleware"/>. A new instance
        /// is created for every publish call via
        /// <see cref="ActivatorUtilities"/>, so constructor-injected services are fully
        /// supported.
        /// </typeparam>
        /// <param name="activationArguments">
        /// Optional explicit constructor arguments forwarded to middleware activation.
        /// </param>
        /// <returns>This <see cref="EventPublisherBuilder"/> for chaining.</returns>
        public EventPublisherBuilder Use<TMiddleware>(params object[] activationArguments)
            where TMiddleware : class, IEventMiddleware
        {
            _pipeline.Add(typeof(TMiddleware), activationArguments);
            return this;
        }

        /// <summary>
        /// Appends a middleware component of type <typeparamref name="TMiddleware"/> to the
        /// publish pipeline that is only executed when <paramref name="predicate"/> returns
        /// <c>true</c> for the current <see cref="EventContext"/>.
        /// When the predicate returns <c>false</c> the middleware step is skipped and the
        /// next registered step is invoked directly.
        /// </summary>
        /// <typeparam name="TMiddleware">
        /// A concrete type that implements <see cref="IEventMiddleware"/>. A new instance
        /// is created for every publish call (when the predicate passes) via
        /// <see cref="ActivatorUtilities"/>, so constructor-injected services are fully
        /// supported.
        /// </typeparam>
        /// <param name="predicate">
        /// A function evaluated against the current <see cref="EventContext"/>. The
        /// middleware is invoked only when the function returns <c>true</c>.
        /// </param>
        /// <param name="activationArguments">
        /// Optional explicit constructor arguments forwarded to middleware activation.
        /// </param>
        /// <returns>This <see cref="EventPublisherBuilder"/> for chaining.</returns>
        public EventPublisherBuilder UseWhen<TMiddleware>(
            Func<EventContext, bool> predicate,
            params object[] activationArguments)
            where TMiddleware : class, IEventMiddleware
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _pipeline.AddWhen(typeof(TMiddleware), predicate, activationArguments);
            return this;
        }

        // ─────────────────────────────────────────────────────────────────
        // Custom publisher type
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces the default <see cref="EventPublisher"/> with a custom sub-class.
        /// The publisher pipeline is always registered as a keyed singleton.
        /// </summary>
        public EventPublisherBuilder UsePublisher<TPublisher>()
            where TPublisher : EventPublisher
        {
            _publisherType = typeof(TPublisher);
            return this;
        }

        // ─────────────────────────────────────────────────────────────────
        // ID generator / system time
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Replaces the default ID generator with a GUID-based generator.</summary>
        public EventPublisherBuilder UseGuid(string? format = null) {
            Services.RemoveAll<IEventIdGenerator>();
            Services.AddSingleton<IEventIdGenerator, EventGuidGenerator>();
            Services.AddOptions<EventGuidGeneratorOptions>()
                .Configure(o => o.Format = format);
            return this;
        }

        /// <summary>Replaces the default <see cref="IEventSystemTime"/> implementation.</summary>
        public EventPublisherBuilder UseSystemTime<TSystemTime>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TSystemTime : class, IEventSystemTime {
            Services.RemoveAll<IEventSystemTime>();
            Services.Add(new ServiceDescriptor(typeof(IEventSystemTime), typeof(TSystemTime), lifetime));
            return this;
        }

        // ─────────────────────────────────────────────────────────────────
        // Channels
        // ─────────────────────────────────────────���───────────────────────

        /// <summary>
        /// Wraps <paramref name="channel"/> in a <see cref="NamedChannelDecorator"/> when
        /// <paramref name="channelName"/> is non-null/non-empty, otherwise returns the
        /// channel unchanged.
        /// </summary>
        private static IEventPublishChannel ApplyChannelName(IEventPublishChannel channel, string? channelName)
            => !string.IsNullOrEmpty(channelName) ? new NamedChannelDecorator(channel, channelName!) : channel;

        /// <summary>
        /// Wraps <paramref name="channel"/> in a <see cref="NamedChannelDecorator{TEvent}"/> when
        /// <paramref name="channelName"/> is non-null/non-empty, otherwise returns the
        /// channel unchanged.
        /// </summary>
        private static IEventPublishChannel<TEvent> ApplyChannelName<TEvent>(IEventPublishChannel<TEvent> channel, string? channelName)
            where TEvent : class
            => !string.IsNullOrEmpty(channelName) ? new NamedChannelDecorator<TEvent>(channel, channelName!) : channel;

        /// <summary>
        /// Registers a publish channel of type <typeparamref name="TChannel"/> for
        /// this publisher pipeline.
        /// </summary>
        /// <param name="lifetime">The service lifetime for the channel registration.</param>
        /// <param name="channelName">
        /// An optional logical name for the channel. When set the publisher will only route
        /// events to this channel when the per-call options carry the same channel name.
        /// This is applied at the builder level via a <see cref="NamedChannelDecorator"/> so
        /// the channel class itself does not need to implement
        /// <see cref="INamedEventPublishChannel"/>.
        /// </param>
        public EventPublisherBuilder AddChannel<TChannel>(
            ServiceLifetime lifetime = ServiceLifetime.Singleton,
            string? channelName = null)
            where TChannel : class, IEventPublishChannel
        {
            Services.TryAdd(new ServiceDescriptor(typeof(TChannel), typeof(TChannel), lifetime));
            Services.Add(ServiceDescriptor.KeyedSingleton<IEventPublishChannel>(
                Name, (sp, _) => ApplyChannelName(sp.GetRequiredService<TChannel>(), channelName)));
            _channelFactories.Add(sp => ApplyChannelName(sp.GetRequiredService<TChannel>(), channelName));
            return this;
        }

        /// <summary>
        /// Registers a pre-built channel instance for this publisher pipeline.
        /// </summary>
        /// <param name="channel">The channel instance to register.</param>
        /// <param name="channelName">
        /// An optional logical name for the channel. When set the publisher will only route
        /// events to this channel when the per-call options carry the same channel name.
        /// </param>
        public EventPublisherBuilder AddChannel(IEventPublishChannel channel, string? channelName = null)
        {
            var entry = ApplyChannelName(channel, channelName);
            Services.AddKeyedSingleton<IEventPublishChannel>(Name, entry);
            _channelFactories.Add(_ => entry);
            return this;
        }

        /// <summary>
        /// Registers a typed publish channel of type <typeparamref name="TChannel"/> that
        /// receives only events whose data class is <typeparamref name="TEvent"/>.
        /// </summary>
        /// <param name="lifetime">The service lifetime for the channel registration.</param>
        /// <param name="channelName">
        /// An optional logical name for the channel. When set the publisher will only route
        /// events to this channel when the per-call options carry the same channel name.
        /// </param>
        public EventPublisherBuilder AddChannel<TChannel, TEvent>(
            ServiceLifetime lifetime = ServiceLifetime.Singleton,
            string? channelName = null)
            where TChannel : class, IEventPublishChannel<TEvent>
            where TEvent : class
        {
            Services.TryAdd(new ServiceDescriptor(typeof(TChannel), typeof(TChannel), lifetime));
            Services.Add(ServiceDescriptor.KeyedSingleton<IEventPublishChannel>(
                Name, (sp, _) => (IEventPublishChannel)ApplyChannelName(sp.GetRequiredService<TChannel>(), channelName)));
            Services.Add(ServiceDescriptor.KeyedSingleton<IEventPublishChannel<TEvent>>(
                Name, (sp, _) => ApplyChannelName(sp.GetRequiredService<TChannel>(), channelName)));
            _channelFactories.Add(sp => (IEventPublishChannel)ApplyChannelName(sp.GetRequiredService<TChannel>(), channelName));
            return this;
        }

        /// <summary>
        /// Registers a pre-built typed channel instance for this publisher pipeline.
        /// </summary>
        /// <param name="channel">The typed channel instance to register.</param>
        /// <param name="channelName">
        /// An optional logical name for the channel. When set the publisher will only route
        /// events to this channel when the per-call options carry the same channel name.
        /// </param>
        public EventPublisherBuilder AddChannel<TEvent>(IEventPublishChannel<TEvent> channel, string? channelName = null)
            where TEvent : class
        {
            var entry = ApplyChannelName(channel, channelName);
            Services.AddKeyedSingleton<IEventPublishChannel>(Name, (IEventPublishChannel)entry);
            Services.AddKeyedSingleton<IEventPublishChannel<TEvent>>(Name, entry);
            _channelFactories.Add(_ => (IEventPublishChannel)entry);
            return this;
        }
    }
}
