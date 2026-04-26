//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Implemented by a data object that can convert itself directly into a
    /// <see cref="CloudEvent"/> without requiring the reflection-based
    /// <see cref="IEventCreator"/> factory.
    /// </summary>
    /// <remarks>
    /// When <see cref="EventPublisher"/> receives a data object that implements
    /// this interface, it calls <see cref="ToCloudEvent"/> instead of delegating
    /// to <see cref="IEventCreator"/>.  This allows strongly-typed data classes to
    /// control their own CloudEvent representation without relying on
    /// <c>[Event]</c> / <c>[EventProperty]</c> annotation reflection.
    /// </remarks>
    public interface IEventConvertible
    {
        /// <summary>
        /// Converts this object to a <see cref="CloudEvent"/>.
        /// </summary>
        /// <returns>
        /// A fully populated <see cref="CloudEvent"/> that represents this object.
        /// </returns>
        CloudEvent ToCloudEvent();
    }
}
