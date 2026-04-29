//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Holds the ordered list of <see cref="IEventMiddleware"/> registrations
    /// that form the event-publish pipeline owned by an <see cref="EventPublisher"/>
    /// instance.
    /// </summary>
    internal sealed class EventPublisherPipelineDescriptor
    {
        private readonly List<MiddlewareRegistration> _middlewareRegistrations = new();

        internal sealed class MiddlewareRegistration(Type middlewareType, object[] activationArguments)
        {
            public Type MiddlewareType { get; } = middlewareType;

            public object[] ActivationArguments { get; } = activationArguments;
        }

        /// <summary>
        /// Gets the ordered list of middleware registrations for
        /// the pipeline.
        /// </summary>
        public IReadOnlyList<MiddlewareRegistration> MiddlewareRegistrations => _middlewareRegistrations;

        /// <summary>
        /// Appends a middleware type to the pipeline.
        /// </summary>
        /// <param name="middlewareType">
        /// A concrete type that implements <see cref="IEventMiddleware"/>.
        /// </param>
        /// <param name="activationArguments">
        /// Optional activation arguments forwarded to
        /// <see cref="Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(IServiceProvider,Type,object[])"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="middlewareType"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="middlewareType"/> does not implement
        /// <see cref="IEventMiddleware"/>.
        /// </exception>
        internal void Add(Type middlewareType, params object[] activationArguments)
        {
            ArgumentNullException.ThrowIfNull(middlewareType);

            if (!typeof(IEventMiddleware).IsAssignableFrom(middlewareType))
                throw new ArgumentException(
                    $"The type '{middlewareType.FullName}' does not implement {nameof(IEventMiddleware)}.",
                    nameof(middlewareType));

            _middlewareRegistrations.Add(new MiddlewareRegistration(
                middlewareType,
                activationArguments?.ToArray() ?? []));
        }
    }
}


