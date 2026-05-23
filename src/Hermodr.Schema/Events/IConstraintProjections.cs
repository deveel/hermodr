//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
	/// <summary>
	/// A non-generic projection of <see cref="RangeConstraint{TValue}"/> that allows
	/// writers and other consumers to access <c>Min</c>, <c>Max</c> and the value
	/// <see cref="ValueType"/> without reflection.
	/// </summary>
	public interface IRangeConstraint : IEventPropertyConstraint {
		/// <summary>The CLR type of the range bounds.</summary>
		Type ValueType { get; }

		/// <summary>The lower bound, or <c>null</c> when there is none.</summary>
		object? Min { get; }

		/// <summary>The upper bound, or <c>null</c> when there is none.</summary>
		object? Max { get; }
	}

	/// <summary>
	/// A non-generic projection of <see cref="EnumMemberConstraint{TValue}"/> that allows
	/// writers and other consumers to iterate the allowed values without knowing
	/// the concrete type parameter.
	/// </summary>
	public interface IEnumMemberConstraint : IEventPropertyConstraint {
		/// <summary>The allowed values as <see cref="object"/> references.</summary>
		IReadOnlyList<object?> AllowedValueObjects { get; }
	}
}

