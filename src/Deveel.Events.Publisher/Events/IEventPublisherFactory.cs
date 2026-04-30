//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//
namespace Deveel.Events
{
    /// <summary>
    /// A factory for obtaining named <see cref="IEventPublisher"/> instances.
    /// </summary>
    public interface IEventPublisherFactory
    {
        /// <summary>
        /// Returns the <see cref="IEventPublisher"/> registered under <paramref name="name"/>.
        /// </summary>
        /// <param name="name">
        /// The publisher pipeline name. Use an empty string for the default publisher.
        /// </param>
        IEventPublisher GetPublisher(string name = "");
    }
}
