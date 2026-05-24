//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Describes the schema of an event that is used to
    /// validate and describe the properties that are part
    /// of the event.
    /// </summary>
    public class EventSchema : IEventSchema, IVersionedElement {
        /// <summary>
        /// Constructs the schema of an event with the given
        /// type, version and content type.
        /// </summary>
        /// <param name="eventType">
        /// The type of the event that is being described
        /// by the schema.
        /// </param>
        /// <param name="version">
        /// The version of the schema that is being used
        /// to describe the event.
        /// </param>
        /// <param name="contentType">
        /// The content type of the event that is used to
        /// represent the format of the data.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="eventType"/>, <paramref name="version"/>,
        /// or <paramref name="contentType"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="version"/> is not a valid version string
        /// (i.e. cannot be parsed by <see cref="System.Version.TryParse"/>).
        /// </exception>
        public EventSchema(string eventType, string version, string contentType) {
			ArgumentNullException.ThrowIfNull(eventType, nameof(eventType));
			ArgumentNullException.ThrowIfNull(version, nameof(version));
			ArgumentNullException.ThrowIfNull(contentType, nameof(contentType));

			if (!Version.TryParse(version, out var schemaVersion))
				throw new ArgumentException("The version string is not valid", nameof(version));

			EventType = eventType;
			Version = schemaVersion;

			Properties = new EventPropertyCollection(this);
			ContentType = contentType;
		}

		/// <inheritdoc/>
        public string EventType { get; }

		string IEventSchema.Version => Version.ToString();

        /// <summary>
        /// The schema version as a <see cref="System.Version"/> instance.
        /// </summary>
        public Version Version { get; }

        /// <inheritdoc/>
        public string? Description { get; set; }

        /// <inheritdoc/>
        public string ContentType { get; }

        /// <summary>
        /// A collection of properties that are part of the schema.
        /// </summary>
		public EventPropertyCollection Properties { get; }

		IReadOnlyList<IEventProperty> IEventSchema.Properties => Properties;

        /// <summary>
        /// Creates a new schema for an event from the given
        /// data type using reflection to extract the properties
        /// and constraints.
        /// </summary>
        /// <param name="dataType">
        /// The type of the data that is used to describe the
        /// properties of the event.
        /// </param>
        /// <returns>
        /// Returns a new instance of <see cref="EventSchema"/>
        /// that is created from the given data type.
        /// </returns>
        public static EventSchema FromDataType(Type dataType)
			=> EventSchemaCreator.FromEventData(dataType);

        /// <summary>
        /// Creates a new schema for an event from the given
        /// data type using reflection to extract the properties
        /// and constraints.
        /// </summary>
        /// <typeparam name="TData">
        /// The type of the data that is used to describe the
        /// properties of the event.
        /// </typeparam>
        /// <returns>
        /// Returns a new instance of <see cref="EventSchema"/>
        /// that is created from the given data type.
        /// </returns>
        /// <seealso cref="FromDataType(Type)"/>
        public static EventSchema FromDataType<TData>()
			where TData : class
			=> FromDataType(typeof(TData));

        /// <summary>
        /// Returns a fluent <see cref="EventSchemaBuilder"/> to construct
        /// an <see cref="EventSchema"/> for the given <paramref name="eventType"/>.
        /// </summary>
        /// <param name="eventType">The type identifier of the event.</param>
        /// <returns>
        /// A new <see cref="EventSchemaBuilder"/> targeting <paramref name="eventType"/>.
        /// </returns>
        public static EventSchemaBuilder Build(string eventType)
			=> new EventSchemaBuilder(eventType);
	}
}
