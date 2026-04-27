//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// The comparison operation applied by a <see cref="JsonPathComparisonExpression"/>
    /// when evaluating a JSON body property.
    /// </summary>
    public enum FilterOperator
    {
        // ── String / equality ───────────────────────────────────────────────────────

        /// <summary>The property string value equals the filter value (ordinal, case-sensitive).</summary>
        Equals,

        /// <summary>The property string value does not equal the filter value.</summary>
        NotEquals,

        /// <summary>The property string value starts with the filter value.</summary>
        StartsWith,

        /// <summary>The property string value ends with the filter value.</summary>
        EndsWith,

        /// <summary>The property string value contains the filter value.</summary>
        Contains,

        // ── Numeric ─────────────────────────────────────────────────────────────────

        /// <summary>The property numeric value is greater than the filter value.</summary>
        GreaterThan,

        /// <summary>The property numeric value is less than the filter value.</summary>
        LessThan,

        /// <summary>The property numeric value is greater than or equal to the filter value.</summary>
        GreaterThanOrEqual,

        /// <summary>The property numeric value is less than or equal to the filter value.</summary>
        LessThanOrEqual,

        // ── Existence ───────────────────────────────────────────────────────────────

        /// <summary>The JSON property at the path exists (regardless of its value).</summary>
        Exists,

        /// <summary>The JSON property at the path is absent.</summary>
        NotExists
    }
}

