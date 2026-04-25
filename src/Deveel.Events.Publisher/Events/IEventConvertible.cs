//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Provides a factory for creating events.
    /// </summary>
    /// <remarks>
    /// This contract is a provision for allowing
    /// the creation of events not using reflection.
    /// </remarks>
    public interface IEventConvertible
    {
        /// <summary>
        /// Creates a new event from the object.
        /// </summary>
        /// <returns>
        /// Returns a new <see cref="CloudEvent"/>.
        /// </returns>
        CloudEvent ToCloudEvent();
    }
}
