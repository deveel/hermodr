//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events {
    /// <summary>
    /// The contract that defines the schema of an event
	/// describing the properties and constraints that are
	/// contained in the event.
    /// </summary>
    public interface IEventSchema {
        /// <summary>
        /// The type of the event the schema is describing.
        /// </summary>
        string EventType { get; }

        /// <summary>
        /// The version of the event described by the schema.
        /// </summary>
		string Version { get; }

        /// <summary>
        /// A description of the event that can be used for
        /// documentation purposes.
        /// </summary>
		string? Description { get; }

        /// <summary>
        /// The content type of the event that is used to
        /// identify the format of the data.
        /// </summary>
		string ContentType { get; }

        /// <summary>
        /// The properties that are part of the event schema.
        /// </summary>
		IReadOnlyList<IEventProperty> Properties { get; }
	}
}
