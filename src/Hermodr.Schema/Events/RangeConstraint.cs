//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// A constraint that is used to restrict the values of a
	/// property to a range of values.
    /// </summary>
    /// <typeparam name="TValue">
	/// The value type of the range bounds (e.g. <see cref="int"/>, <see cref="double"/>).
	/// Must be a value type (<c>struct</c>) so it can be made nullable via
	/// <c>Nullable&lt;TValue&gt;</c>.
	/// </typeparam>
    public sealed class RangeConstraint<TValue> : IEventPropertyConstraint, IRangeConstraint where TValue : struct {
        /// <summary>
        /// Constructs a constraint that allows only the values
		/// within the given range.
        /// </summary>
        /// <param name="min">
		/// The minimum value that is allowed by the constraint,
		/// or <c>null</c> if there should be no minimum.
		/// </param>
        /// <param name="max">
		/// The maximum value that is allowed by the constraint,
		/// or <c>null</c> if there should be no maximum.
		/// </param>
        /// <exception cref="ArgumentException">
		/// Thrown when both the minimum and maximum values are <c>null</c>.
		/// </exception>
        public RangeConstraint(TValue? min, TValue? max) {
			if (min == null && max == null)
				throw new ArgumentException("At least one of the min or max values must be specified");

			Min = min;
			Max = max;
		}

        /// <summary>
        /// The minimum value that is allowed by the constraint,
		/// or <c>null</c> if there should be no minimum.
        /// </summary>
        public TValue? Min { get; }

        /// <summary>
        /// The maximum value that is allowed by the constraint,
		/// or <c>null</c> if there should be no maximum.
        /// </summary>
        public TValue? Max { get; }

        /// <inheritdoc/>
        public string ConstraintType => "range";

		/// <inheritdoc/>
		Type IRangeConstraint.ValueType => typeof(TValue);

		/// <inheritdoc/>
		object? IRangeConstraint.Min => Min;

		/// <inheritdoc/>
		object? IRangeConstraint.Max => Max;

		bool IEventPropertyConstraint.IsValid(object? value) {
			if (value == null)
				return Min == null && Max == null;

			if (value is TValue typedValue) {
				var comparer = Comparer<TValue>.Default;
				if (Min != null && Max != null)
					return comparer.Compare(typedValue, Min.Value) >= 0
						&& comparer.Compare(typedValue, Max.Value) <= 0;

				if (Min != null && comparer.Compare(typedValue, Min.Value) >= 0)
					return true;
				if (Max != null && comparer.Compare(typedValue, Max.Value) <= 0)
					return true;
			}

			return false;
		}
	}
}
