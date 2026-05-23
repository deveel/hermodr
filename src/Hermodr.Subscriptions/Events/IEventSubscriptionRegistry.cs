//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// A registry that stores <see cref="IEventSubscription"/> instances and can retrieve those
    /// that match a given <see cref="CloudEvent"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extends <see cref="IEventSubscriptionResolver"/> with write capabilities so that new
    /// subscriptions can be added at runtime.
    /// </para>
    /// <para>
    /// Implementations may be backed by any persistence mechanism (in-memory, relational database,
    /// document store, etc.).  All methods are asynchronous to support I/O-bound back-ends.
    /// </para>
    /// </remarks>
    public interface IEventSubscriptionRegistry : IEventSubscriptionResolver
    {
        /// <summary>
        /// Registers the given <paramref name="subscription"/>.
        /// </summary>
        /// <param name="subscription">The subscription to register.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task RegisterAsync(IEventSubscription subscription, CancellationToken cancellationToken = default);
    }
}
