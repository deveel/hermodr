//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Runtime extensions for <see cref="EventPublisher"/>.
    /// </summary>
    public static class EventPublisherExtensions
    {
        /// <summary>
        /// Enables the subscription dispatcher middleware in the current publisher pipeline.
        /// </summary>
        /// <param name="publisher">The publisher to configure.</param>
        /// <returns>The same <paramref name="publisher"/> instance for chaining.</returns>
        public static EventPublisher UseDispatcher(this EventPublisher publisher)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            return publisher.Use<EventDispatcher>();
        }

        /// <summary>
        /// Enables the subscription dispatcher middleware with explicit runtime options.
        /// </summary>
        /// <param name="publisher">The publisher to configure.</param>
        /// <param name="options">The dispatcher options to use.</param>
        /// <returns>The same <paramref name="publisher"/> instance for chaining.</returns>
        public static EventPublisher UseDispatcher(
            this EventPublisher publisher,
            EventDispatcherOptions options)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(options);
            return publisher.Use<EventDispatcher>(options);
        }
    }
}


