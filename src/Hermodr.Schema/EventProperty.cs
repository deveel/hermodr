//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// A property of an event that can be used to describe the data
	/// that is part of the event.
    /// </summary>
    public class EventProperty : IEventProperty, IVersionedElement {
        /// <summary>
        /// Constructs an event property with the given name, data type
		/// and optionally the version of the event this property belongs to.
        /// </summary>
        /// <param name="name">
		/// The name of the property that is used to identify it.
        /// </param>
        /// <param name="dataType">
		/// The data type that the property can hold.
		/// </param>
        /// <param name="version">
		/// The version of the event schema in which this property was introduced.
		/// When <c>null</c>, the property inherits the version of the owning schema.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// Thrown when the name or data type is <c>null</c>.
		/// </exception>
        /// <exception cref="ArgumentException">
		/// Thrown when the version string is provided and it is
		/// not a valid version.
		/// </exception>
        public EventProperty(string name, string dataType, string? version = null) {
			ArgumentNullException.ThrowIfNull(name, nameof(name));
			ArgumentNullException.ThrowIfNull(dataType, nameof(dataType));

			Version? propertyVersion = null;
			if (!String.IsNullOrWhiteSpace(version) &&
				!System.Version.TryParse(version, out propertyVersion))
				throw new ArgumentException("The version string is not valid", nameof(version));

			Name = name;
			DataType = dataType;
			Version = propertyVersion;

			Properties = new EventPropertyCollection(this);
			Constraints = new EventPropertyConstraintCollection();
		}

		/// <inheritdoc/>
		public string Name { get; }

        /// <inheritdoc/>
        public string? Description { get; set; }

        /// <inheritdoc/>
        public string DataType { get; }

		/// <summary>
		/// The version of the schema in which this property was introduced,
		/// or <c>null</c> if the property inherits the owner schema's version.
		/// </summary>
		public Version? Version { get; internal set; }

		string? IEventProperty.Version => Version?.ToString();

		/// <inheritdoc/>
		public bool IsRequired => Constraints.Any(c => c.ConstraintType == "required");

		/// <summary>
		/// Indicates whether this property accepts <c>null</c> values,
		/// derived from the CLR nullability of the source member.
		/// </summary>
		public bool IsNullable { get; set; }

		IReadOnlyList<IEventPropertyConstraint> IEventProperty.Constraints => Constraints;

        /// <summary>
        /// The constraints that restrict the values assignable to this property.
        /// </summary>
        public EventPropertyConstraintCollection Constraints { get; }

		IReadOnlyList<IEventProperty> IEventProperty.Properties => Properties;

		/// <summary>
        /// The collection of nested properties, present when <see cref="DataType"/>
        /// represents a complex object.
        /// </summary>
        public EventPropertyCollection Properties { get; }

		/// <summary>
		/// Returns a new <see cref="EventProperty"/> identical to this one but
		/// carrying the specified <paramref name="version"/>.
		/// </summary>
		internal EventProperty WithVersion(Version version) {
			var copy = new EventProperty(Name, DataType, version.ToString()) {
				Description = Description,
				IsNullable = IsNullable
			};
			foreach (var c in Constraints)
				copy.Constraints.Add(c);
			foreach (var p in Properties)
				copy.Properties.Add(p);
			return copy;
		}
	}
}
