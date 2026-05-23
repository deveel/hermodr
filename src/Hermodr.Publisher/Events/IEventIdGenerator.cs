//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// A service that generates unique identifiers for events.
    /// </summary>
    public interface IEventIdGenerator {
        /// <summary>
        /// Generates a new unique identifier for an event.
        /// </summary>
        /// <returns>
        /// Returns a new unique identifier.
        /// </returns>
		string GenerateId();
	}
}
