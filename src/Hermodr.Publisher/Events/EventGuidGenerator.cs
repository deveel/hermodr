//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Options;

namespace Hermodr {
    /// <summary>
    /// A service that generates unique identifiers for events
    /// in the form of a GUID.
    /// </summary>
    public sealed class EventGuidGenerator : IEventIdGenerator {
		private readonly EventGuidGeneratorOptions _options;

        /// <summary>
        /// The default format to use to generate the GUID.
        /// </summary>
		public const string DefaultFormat = "N";

        /// <summary>
        /// Constructs the generator with the specified options.
        /// </summary>
        /// <param name="options">The options to use to configure the generator.</param>
		public EventGuidGenerator(IOptions<EventGuidGeneratorOptions> options) {
			_options = options?.Value ?? new EventGuidGeneratorOptions();
		}

        /// <summary>
        /// A default static instance of the <see cref="EventGuidGenerator"/>.
        /// </summary>
        public static readonly EventGuidGenerator Default 
			= new EventGuidGenerator(Options.Create(new EventGuidGeneratorOptions()));

        /// <inheritdoc/>
        public string GenerateId() => Guid.NewGuid().ToString(_options.Format ?? DefaultFormat);
	}
}
