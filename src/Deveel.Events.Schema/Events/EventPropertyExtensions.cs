//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events {
    /// <summary>
    /// Extensions for the <see cref="IEventProperty"/> contract.
    /// </summary>
    public static class EventPropertyExtensions {
        /// <summary>
        /// Checks if the property is required.
        /// </summary>
        /// <param name="property">The property to check.</param>
        /// <returns>
        /// <c>true</c> if a <see cref="PropertyRequiredConstraint"/> is present
        /// in <see cref="IEventProperty.Constraints"/>; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This extension delegates to <see cref="IEventProperty.IsRequired"/> and
        /// is kept for backward compatibility.
        /// </remarks>
		public static bool IsRequired(this IEventProperty property)
			=> property.IsRequired;

		/// <summary>
		/// Checks if the property accepts <c>null</c> values.
		/// </summary>
        /// <param name="property">The property to check.</param>
        /// <returns>
        /// <c>true</c> if the property is nullable; otherwise <c>false</c>.
        /// </returns>
		public static bool IsNullable(this IEventProperty property)
			=> property.IsNullable;
	}
}
