//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.ObjectModel;

namespace Hermodr {
    /// <summary>
    /// A collection of properties that are part of an event.
    /// </summary>
	/// <remarks>
	/// This collection is used to store the properties of an event
	/// in a versioned manner, where each property can have a version
	/// and the collection can be used to store properties of different
	/// versions of the event.
	/// </remarks>
    public sealed class EventPropertyCollection : Collection<EventProperty> {
		private readonly IVersionedElement _owner;

		internal EventPropertyCollection(IVersionedElement owner) {
			_owner = owner;
		}

        /// <summary>
        /// Gets or sets a property with the given name from the collection.
        /// </summary>
        /// <param name="name">
		/// The name of the property.
		/// </param>
		/// <value>
		/// The property with the given name to be set.
		/// </value>
        /// <returns>
		/// Returns the property with the given name.
		/// </returns>
        /// <exception cref="KeyNotFoundException">
		/// Thrown when the setter is called and no property with the given
		/// <paramref name="name"/> exists in the collection.
		/// </exception>
        public EventProperty? this[string name] {
			get {
				ArgumentNullException.ThrowIfNull(name, nameof(name));
				return base.Items.FirstOrDefault(x => x.Name == name);
			}
			set {
                ArgumentNullException.ThrowIfNull(name, nameof(name));
                ArgumentNullException.ThrowIfNull(value, nameof(value));

				if (value.Name != name)
					throw new ArgumentException("The name of the property does not match the key", nameof(value));

                for (var i = 0; i < base.Items.Count; i++) {
					if (base.Items[i].Name == name) {
						SetItem(i, value);
						return;
					}
				}

				throw new KeyNotFoundException($"The property {name} does not exist in the collection");
			}
		}

        /// <summary>
        /// Checks if the collection contains a property with the given name.
        /// </summary>
        /// <param name="name">
		/// The name of the property to check for.
		/// </param>
		/// <returns>
		/// Returns <c>true</c> if the collection contains a property with the given name,
		/// otherwise <c>false</c>.
		/// </returns>
        public bool Contains(string name) {
			return base.Items.Any(x => x.Name == name);
		}

		/// <inheritdoc/>
		protected override void InsertItem(int index, EventProperty item) {
			ArgumentNullException.ThrowIfNull(item, nameof(item));

			// Only auto-assign / compare version when the owner itself has a version
			if (_owner.Version != null) {
				if (item.Version == null)
					item = item.WithVersion(_owner.Version);

				if (item.Version > _owner.Version)
					throw new ArgumentException("The version of the property is not compatible with the owner", nameof(item));
			}

			if (Contains(item.Name))
				throw new ArgumentException("The property with the same name already exists", nameof(item));

			base.InsertItem(index, item);
		}

        /// <inheritdoc/>
        protected override void SetItem(int index, EventProperty item) {
			ArgumentNullException.ThrowIfNull(item, nameof(item));

			if (_owner.Version != null) {
				if (item.Version == null)
					item = item.WithVersion(_owner.Version);

				if (item.Version > _owner.Version)
					throw new ArgumentException("The version of the property is not compatible with owner", nameof(item));
			}

			for (var i = 0; i < base.Items.Count; i++) {
				if (i == index)
					continue;

				if (base.Items[i].Name == item.Name)
					throw new ArgumentException("The property with the same name already exists", nameof(item));
			}

			base.SetItem(index, item);
		}

        /// <summary>
        /// Adds a property to the collection with the given 
		/// name and data type.
        /// </summary>
        /// <param name="name">
		/// The name of the property to add.
		/// </param>
        /// <param name="dataType">
		/// The data type of the property.
		/// </param>
        /// <param name="version">
		/// The version of the event this property belongs to.
		/// </param>
        public void Add(string name, string dataType, string? version = null)
        {
            Add(new EventProperty(name, dataType, version));
        }
    }
}
