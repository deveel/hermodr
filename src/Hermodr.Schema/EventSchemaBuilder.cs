//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
	/// <summary>
	/// A fluent builder for constructing an <see cref="EventSchema"/>.
	/// </summary>
	/// <example>
	/// <code language="csharp">
	/// var schema = EventSchema.Build("person.created")
	///     .WithVersion("1.0")
	///     .WithContentType("application/json")
	///     .AddProperty("first_name", p => p.OfType("string").Required())
	///     .AddProperty("last_name",  p => p.OfType("string").Required())
	///     .AddProperty("age",        p => p.OfType("int").WithRange(0, 150))
	///     .Build();
	/// </code>
	/// </example>
	public sealed class EventSchemaBuilder {
		private readonly string _eventType;
		private string _version = "1.0";
		private string _contentType = "application/json";
		private string? _description;
		private readonly List<EventProperty> _properties = new();

		/// <summary>
		/// Constructs a builder targeting the event type identified by
		/// <paramref name="eventType"/>.
		/// </summary>
		public EventSchemaBuilder(string eventType) {
			ArgumentNullException.ThrowIfNull(eventType, nameof(eventType));
			_eventType = eventType;
		}

		/// <summary>Sets the schema version string (e.g. <c>"1.0"</c>).</summary>
		public EventSchemaBuilder WithVersion(string version) {
			ArgumentNullException.ThrowIfNull(version, nameof(version));
			_version = version;
			return this;
		}

		/// <summary>Sets the MIME content type of the event payload.</summary>
		public EventSchemaBuilder WithContentType(string contentType) {
			ArgumentNullException.ThrowIfNull(contentType, nameof(contentType));
			_contentType = contentType;
			return this;
		}

		/// <summary>Sets a human-readable description for the event.</summary>
		public EventSchemaBuilder WithDescription(string description) {
			_description = description;
			return this;
		}

		/// <summary>
		/// Adds a property to the schema, optionally configuring it via the
		/// <paramref name="configure"/> delegate.
		/// </summary>
		/// <param name="name">The property name.</param>
		/// <param name="dataType">
		/// The data type of the property (e.g. <c>"string"</c>, <c>"int"</c>).
		/// </param>
		/// <param name="configure">
		/// An optional delegate to apply further configuration with an
		/// <see cref="EventPropertyBuilder"/>.
		/// </param>
		public EventSchemaBuilder AddProperty(string name, string dataType = "string", Action<EventPropertyBuilder>? configure = null) {
			var builder = new EventPropertyBuilder(name).OfType(dataType);
			configure?.Invoke(builder);
			_properties.Add(builder.Build());
			return this;
		}

		/// <summary>
		/// Adds a property to the schema using a fully configured
		/// <see cref="EventPropertyBuilder"/> delegate.
		/// </summary>
		public EventSchemaBuilder AddProperty(string name, Action<EventPropertyBuilder> configure) {
			ArgumentNullException.ThrowIfNull(configure, nameof(configure));
			var builder = new EventPropertyBuilder(name);
			configure(builder);
			_properties.Add(builder.Build());
			return this;
		}

		/// <summary>Adds a pre-built <see cref="EventProperty"/> to the schema.</summary>
		public EventSchemaBuilder AddProperty(EventProperty property) {
			ArgumentNullException.ThrowIfNull(property, nameof(property));
			_properties.Add(property);
			return this;
		}

		/// <summary>
		/// Constructs and returns the <see cref="EventSchema"/> described
		/// by this builder.
		/// </summary>
		/// <returns>A fully configured <see cref="EventSchema"/>.</returns>
		public EventSchema Build() {
			var schema = new EventSchema(_eventType, _version, _contentType) {
				Description = _description
			};

			foreach (var property in _properties)
				schema.Properties.Add(property);

			return schema;
		}
	}
}

