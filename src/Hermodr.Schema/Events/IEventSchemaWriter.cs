//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// A service that is used to write an event schema to a stream
    /// in a specific format.
    /// </summary>
    public interface IEventSchemaWriter {
        /// <summary>
        /// Writes the schema to the given stream in the format
        /// that is specific to the implementation.
        /// </summary>
        /// <param name="stream">
        /// The stream to write the schema to.
        /// </param>
        /// <param name="schema">
        /// The instance of the schema to write.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that can be used to cancel the writing.
        /// </param>
        /// <returns>
        /// Returns an asynchronous task that represents the writing
        /// of the schema to the stream.
        /// </returns>
		Task WriteToAsync(Stream stream, IEventSchema schema, CancellationToken cancellationToken = default);
	}
}
