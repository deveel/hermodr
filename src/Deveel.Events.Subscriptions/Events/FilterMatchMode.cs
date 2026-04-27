//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Specifies the strategy used to match a filter value against an event attribute string.
    /// </summary>
    public enum FilterMatchMode
    {
        /// <summary>
        /// The filter value must equal the attribute value exactly (ordinal, case-sensitive).
        /// </summary>
        Exact,

        /// <summary>
        /// The attribute value must start with the filter value (ordinal, case-sensitive).
        /// Trailing <c>*</c> characters in the filter value are stripped before comparison.
        /// </summary>
        Prefix,

        /// <summary>
        /// The attribute value must end with the filter value (ordinal, case-sensitive).
        /// Leading <c>*</c> characters in the filter value are stripped before comparison.
        /// </summary>
        Suffix
    }
}

