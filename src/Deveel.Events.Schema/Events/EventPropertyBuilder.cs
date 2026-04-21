//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events {
	/// <summary>
	/// A fluent builder for constructing an <see cref="EventProperty"/>.
	/// </summary>
	/// <remarks>
	/// Obtain an instance via <see cref="EventSchemaBuilder.AddProperty(string, System.Action{EventPropertyBuilder}?)"/>
	/// or by calling <c>new EventPropertyBuilder(name)</c> directly.
	/// </remarks>
	public sealed class EventPropertyBuilder {
		private readonly string _name;
		private string _dataType = "string";
		private string? _version;
		private string? _description;
		private bool _nullable;
		private readonly List<IEventPropertyConstraint> _constraints = new();
		private readonly List<EventPropertyBuilder> _nestedProperties = new();

		/// <summary>
		/// Constructs a builder for the property identified by <paramref name="name"/>.
		/// </summary>
		public EventPropertyBuilder(string name) {
			ArgumentNullException.ThrowIfNull(name, nameof(name));
			_name = name;
		}

		/// <summary>Sets the data type of the property (e.g. <c>"string"</c>, <c>"int"</c>).</summary>
		public EventPropertyBuilder OfType(string dataType) {
			ArgumentNullException.ThrowIfNull(dataType, nameof(dataType));
			_dataType = dataType;
			return this;
		}

		/// <summary>Sets the schema version in which this property was introduced.</summary>
		public EventPropertyBuilder WithVersion(string version) {
			_version = version;
			return this;
		}

		/// <summary>Sets a human-readable description for the property.</summary>
		public EventPropertyBuilder WithDescription(string description) {
			_description = description;
			return this;
		}

		/// <summary>Marks the property as required (adds a <see cref="PropertyRequiredConstraint"/>).</summary>
		public EventPropertyBuilder Required() {
			if (!_constraints.Any(c => c.ConstraintType == "required"))
				_constraints.Add(new PropertyRequiredConstraint());
			return this;
		}

		/// <summary>Marks the property as accepting <c>null</c> values.</summary>
		public EventPropertyBuilder Nullable() {
			_nullable = true;
			return this;
		}

		/// <summary>Adds a range constraint to the property.</summary>
		public EventPropertyBuilder WithRange<T>(T? min, T? max) where T : struct {
			_constraints.Add(new RangeConstraint<T>(min, max));
			return this;
		}

		/// <summary>Adds an allowed-values constraint to the property.</summary>
		public EventPropertyBuilder WithAllowedValues<T>(IReadOnlyList<T> values) {
			ArgumentNullException.ThrowIfNull(values, nameof(values));
			_constraints.Add(new EnumMemberConstraint<T>(values));
			return this;
		}

		/// <summary>Adds an arbitrary constraint to the property.</summary>
		public EventPropertyBuilder WithConstraint(IEventPropertyConstraint constraint) {
			ArgumentNullException.ThrowIfNull(constraint, nameof(constraint));
			_constraints.Add(constraint);
			return this;
		}

		/// <summary>
		/// Adds a nested property to this property (for complex/object data types).
		/// </summary>
		/// <param name="name">The name of the nested property.</param>
		/// <param name="configure">
		/// An optional delegate to further configure the nested property builder.
		/// </param>
		public EventPropertyBuilder AddProperty(string name, Action<EventPropertyBuilder>? configure = null) {
			var builder = new EventPropertyBuilder(name);
			configure?.Invoke(builder);
			_nestedProperties.Add(builder);
			return this;
		}

		/// <summary>
		/// Constructs and returns the <see cref="EventProperty"/> described by this builder.
		/// </summary>
		public EventProperty Build() {
			var property = new EventProperty(_name, _dataType, _version) {
				Description = _description,
				IsNullable = _nullable
			};

			foreach (var c in _constraints)
				property.Constraints.Add(c);

			foreach (var nested in _nestedProperties)
				property.Properties.Add(nested.Build());

			return property;
		}
	}
}

