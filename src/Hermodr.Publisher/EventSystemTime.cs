//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// A default implementation of <see cref="IEventSystemTime"/> that
    /// is based on the system clock.
    /// </summary>
    public sealed class EventSystemTime : IEventSystemTime {
        /// <inheritdoc/>
		public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        /// <summary>
        /// The default instance of the <see cref="EventSystemTime"/>.
        /// </summary>
		public static readonly EventSystemTime Instance = new EventSystemTime();
	}
}
