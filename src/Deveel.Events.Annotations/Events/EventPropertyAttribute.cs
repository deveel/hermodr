//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events {
    /// <summary>
    /// An attribute that is used to mark a property of a
	/// type as a property of the payload of the event.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
	public sealed class EventPropertyAttribute : Attribute {
        /// <summary>
        /// Constructs an event property attribute with the given name
		/// and the optional schema or version of the event this property
		/// belongs to.
        /// </summary>
        /// <param name="name">
		/// The name of the property that is used to identify it
		/// within the event.
		/// </param>
        /// <param name="schemaOrVersion">
        /// An optional string that is either an absolute URI pointing to the schema
        /// of the event this property belongs to, or a version string (e.g. <c>"1.0"</c>).
        /// Pass <c>null</c> when neither is applicable.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="schemaOrVersion"/> is non-empty but is neither
        /// a valid absolute URI nor a valid version string.
        /// </exception>
        public EventPropertyAttribute(string? name, string? schemaOrVersion = null) {
			if (!String.IsNullOrWhiteSpace(schemaOrVersion)) {
				if (System.Version.TryParse(schemaOrVersion, out _))
				{
					Version = schemaOrVersion;
				} else if (Uri.TryCreate(schemaOrVersion, UriKind.Absolute, out var uri))
				{
                    Schema = uri;
                } else
				{
                    throw new ArgumentException("The schema or version string is not valid", nameof(schemaOrVersion));
                }
			}

			Name = name;
			Version = schemaOrVersion;
		}

        /// <summary>
        /// The name of the property that is used to identify it
		/// within the event.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// A description of the property for documentation purposes.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The version of the event this property belongs to.
        /// </summary>
		public string? Version { get; set; }

        /// <summary>
        /// The schema of the data that is part of the property.
        /// </summary>
		public Uri? Schema { get; set; }
	}
}
