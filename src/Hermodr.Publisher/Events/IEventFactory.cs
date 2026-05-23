//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// A service that creates a <see cref="CloudEvent"/> from a given data object.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface are typically using
    /// reflection to extract the metadata of the event from the
    /// object and create a <see cref="CloudEvent"/> instance.
    /// </remarks>
    public interface IEventFactory
    {
        /// <summary>
        /// Creates a <see cref="CloudEvent"/> from the given 
        /// data object.
        /// </summary>
        /// <param name="dataType">
        /// The type of the data object.
        /// </param>
        /// <param name="data">
        /// The data object that is transported by the event.
        /// </param>
        /// <returns>
        /// Returns a <see cref="CloudEvent"/> instance that is
        /// built from the given data object.
        /// </returns>
        CloudEvent CreateEventFromData(Type dataType, object? data);
    }
}
