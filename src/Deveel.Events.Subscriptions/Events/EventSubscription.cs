//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using Deveel.Filters;

namespace Deveel.Events
{
    /// <summary>
    /// A delegate-backed implementation of <see cref="IEventSubscription"/>.
    /// </summary>
    public sealed class EventSubscription : IEventSubscription
    {
        private readonly Func<CloudEvent, CancellationToken, Task> _handler;

        /// <summary>
        /// Initialises a new subscription backed by the given <paramref name="handler"/> delegate.
        /// </summary>
        /// <param name="filter">
        /// The filter criteria. When <c>null</c> an empty filter (matches every event) is used.
        /// </param>
        /// <param name="handler">
        /// The delegate invoked for every matching event.
        /// </param>
        /// <param name="name">An optional human-readable name for this subscription.</param>
        public EventSubscription(
            FilterExpression filter,
            Func<CloudEvent, CancellationToken, Task> handler,
            string? name = null)
        {
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Name = name;
        }

        /// <inheritdoc/>
        public string? Name { get; }

        /// <inheritdoc/>
        public FilterExpression Filter { get; }

        /// <inheritdoc/>
        public Task HandleAsync(CloudEvent @event, CancellationToken cancellationToken = default)
            => _handler(@event, cancellationToken);
    }
}
