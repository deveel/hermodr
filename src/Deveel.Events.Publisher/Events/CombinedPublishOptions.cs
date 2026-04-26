//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// An <see cref="EventPublishOptions"/> implementation that bundles multiple
    /// per-channel options together so that a single instance can be passed to any
    /// <see cref="IEventPublisher"/> publish call spanning several heterogeneous
    /// channels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see cref="EventPublisher"/> is about to dispatch an event to a channel
    /// it calls <c>ResolveChannelOptions</c>.  If the caller passed a
    /// <see cref="CombinedPublishOptions"/> that method searches the bundled
    /// collection for the first entry whose concrete type is assignable to the options
    /// type declared by the target channel (i.e. the <c>TOptions</c> of
    /// <see cref="EventPublishChannel{TOptions}"/>).  The matching entry is
    /// forwarded; if none is found the channel falls back to its registered defaults.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code language="csharp">
    /// var combined = new CombinedPublishOptions(
    ///     new RabbitMqPublishOptions { RoutingKey = "orders" },
    ///     new AzureServiceBusPublishOptions { SessionId = "session-1" });
    ///
    /// await publisher.PublishEventAsync(myEvent, combined);
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class CombinedPublishOptions : EventPublishOptions
    {
        private readonly IReadOnlyList<EventPublishOptions> _options;

        /// <summary>
        /// Initialises a new instance with the given collection of per-channel options.
        /// </summary>
        /// <param name="options">
        /// The options to bundle.  The order of the elements is preserved; the first
        /// compatible entry wins when <see cref="GetOptions(Type)"/> is called.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="options"/> is <c>null</c>.
        /// </exception>
        public CombinedPublishOptions(IEnumerable<EventPublishOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _options = options.ToList().AsReadOnly();
        }

        /// <summary>
        /// Initialises a new instance with the given per-channel options.
        /// </summary>
        /// <param name="options">
        /// The options to bundle.  The order of the parameters is preserved; the first
        /// compatible entry wins when <see cref="GetOptions(Type)"/> is called.
        /// </param>
        public CombinedPublishOptions(params EventPublishOptions[] options)
            : this((IEnumerable<EventPublishOptions>)options)
        {
        }

        /// <summary>
        /// Gets the read-only list of bundled options.
        /// </summary>
        public IReadOnlyList<EventPublishOptions> Options => _options;

        /// <summary>
        /// Returns the first bundled options instance whose type is assignable to
        /// <typeparamref name="TOptions"/>, or <c>null</c> if no compatible entry exists.
        /// </summary>
        /// <typeparam name="TOptions">The expected options type.</typeparam>
        /// <returns>
        /// The first matching <typeparamref name="TOptions"/> entry, or <c>null</c>.
        /// </returns>
        public TOptions? GetOptions<TOptions>()
            where TOptions : EventPublishOptions
            => _options.OfType<TOptions>().FirstOrDefault();

        /// <summary>
        /// Returns the first bundled options instance whose type is assignable to
        /// <paramref name="optionsType"/>, or <c>null</c> if no compatible entry exists.
        /// </summary>
        /// <param name="optionsType">The expected options type.</param>
        /// <returns>
        /// The first matching <see cref="EventPublishOptions"/> entry, or <c>null</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="optionsType"/> is <c>null</c>.
        /// </exception>
        public EventPublishOptions? GetOptions(Type optionsType)
        {
            ArgumentNullException.ThrowIfNull(optionsType);
            return _options.FirstOrDefault(o => optionsType.IsInstanceOfType(o));
        }
    }
}

