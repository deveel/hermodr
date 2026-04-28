//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Holds the ordered list of <see cref="IEventMiddleware"/> implementation types
    /// that form the event-publish pipeline owned by an <see cref="EventPublisher"/>
    /// instance.
    /// </summary>
    internal sealed class EventPublisherPipelineDescriptor
    {
        private readonly List<Type> _middlewareTypes = new();

        /// <summary>
        /// Gets the ordered list of middleware implementation types registered for
        /// the pipeline.
        /// </summary>
        public IReadOnlyList<Type> MiddlewareTypes => _middlewareTypes;

        /// <summary>
        /// Appends a middleware type to the pipeline.
        /// </summary>
        /// <param name="middlewareType">
        /// A concrete type that implements <see cref="IEventMiddleware"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="middlewareType"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="middlewareType"/> does not implement
        /// <see cref="IEventMiddleware"/>.
        /// </exception>
        internal void Add(Type middlewareType)
        {
            ArgumentNullException.ThrowIfNull(middlewareType);

            if (!typeof(IEventMiddleware).IsAssignableFrom(middlewareType))
                throw new ArgumentException(
                    $"The type '{middlewareType.FullName}' does not implement {nameof(IEventMiddleware)}.",
                    nameof(middlewareType));

            _middlewareTypes.Add(middlewareType);
        }
    }
}


