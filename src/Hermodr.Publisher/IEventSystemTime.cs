//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// An object that provides the current time of the system.
    /// </summary>
    public interface IEventSystemTime {
        /// <summary>
        /// Gets the current time of the system in UTC.
        /// </summary>
		DateTimeOffset UtcNow { get; }
	}
}
