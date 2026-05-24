//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// The options to configure the generation of a 
    /// GUID for an event.
    /// </summary>
    public sealed class EventGuidGeneratorOptions {
        /// <summary>
        /// The format to use to generate the GUID.
        /// </summary>
		public string? Format { get; set; } = EventGuidGenerator.DefaultFormat;
	}
}
