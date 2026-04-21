//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events {
    /// <summary>
    /// A constraint that requires a property to have a value.
    /// </summary>
    public sealed class PropertyRequiredConstraint : IEventPropertyConstraint {
        /// <inheritdoc/>
        public string ConstraintType => "required";

		/// <inheritdoc/>
		public bool IsValid(object? value) => value != null;
	}
}
