//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events {
    /// <summary>
    /// The interface that defines a property of 
	/// an event that can be used to describe the data
	/// that is part of the event.
    /// </summary>
    public interface IEventProperty {
        /// <summary>
        /// The name used to identify the property
        /// within the event.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A description of the property that can be used
        /// for documentation purposes.
        /// </summary>
		string? Description { get; }

        /// <summary>
        /// The data type that the property can hold.
        /// </summary>
		string DataType { get; }

        /// <summary>
        /// The version of the event schema in which this property
        /// was introduced. When <c>null</c>, the property inherits
        /// the version of the owning schema.
        /// </summary>
		string? Version { get; }

        /// <summary>
        /// Indicates whether this property is required to have
        /// a non-<c>null</c> value. Equivalent to checking for a
        /// <see cref="PropertyRequiredConstraint"/> in <see cref="Constraints"/>.
        /// </summary>
        bool IsRequired { get; }

        /// <summary>
        /// Indicates whether the property accepts <c>null</c> values,
        /// typically derived from the CLR nullability of the source member.
        /// </summary>
        bool IsNullable { get; }

        /// <summary>
        /// The constraints that are applied to the property
        /// to restrict the values that can be assigned to it.
        /// </summary>
		IReadOnlyList<IEventPropertyConstraint> Constraints { get; }

        /// <summary>
        /// The collection of nested properties that are part of this
        /// property, when the data type is a complex object.
        /// </summary>
		IReadOnlyList<IEventProperty> Properties { get; }
	}
}
