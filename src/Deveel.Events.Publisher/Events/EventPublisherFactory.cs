//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//
using Microsoft.Extensions.DependencyInjection;
namespace Deveel.Events
{
    /// <summary>
    /// Default implementation of <see cref="IEventPublisherFactory"/> that resolves
    /// named publishers from the DI container using keyed services.
    /// </summary>
    internal sealed class EventPublisherFactory : IEventPublisherFactory
    {
        private readonly IServiceProvider _serviceProvider;
        public EventPublisherFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        /// <inheritdoc/>
        public IEventPublisher GetPublisher(string name = "")
            => _serviceProvider.GetRequiredKeyedService<IEventPublisher>(name);
    }
}
