//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.ObjectModel;

namespace Deveel.Events {
    /// <summary>
    /// A specialized collection of constraints that can be applied to
	/// an event property.
    /// </summary>
    public sealed class EventPropertyConstraintCollection : Collection<IEventPropertyConstraint> {
		internal EventPropertyConstraintCollection() {
		}

		/// <inheritdoc/>
		protected override void InsertItem(int index, IEventPropertyConstraint item) {
			ArgumentNullException.ThrowIfNull(item, nameof(item));

			if (base.Items.Any(x => x.ConstraintType == item.ConstraintType))
				throw new ArgumentException($"A constraint of type '{item.ConstraintType}' already exists in this collection", nameof(item));

			base.InsertItem(index, item);
		}

        /// <inheritdoc/>
        protected override void SetItem(int index, IEventPropertyConstraint item) {
			ArgumentNullException.ThrowIfNull(item, nameof(item));

			for (var i = 0; i < base.Items.Count; i++) {
				if (i == index)
					continue;

				if (base.Items[i].ConstraintType == item.ConstraintType)
					throw new ArgumentException($"A constraint of type '{item.ConstraintType}' already exists in this collection", nameof(item));
			}

			base.SetItem(index, item);
		}
	}
}
