//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Identifies the kind of change detected between two schema versions.
    /// </summary>
    public enum SchemaChangeType
    {
        /// <summary>A property was added to the newer schema.</summary>
        PropertyAdded,

        /// <summary>A property was removed from the newer schema.</summary>
        PropertyRemoved,

        /// <summary>The data type of a property changed.</summary>
        PropertyTypeChanged,

        /// <summary>A property changed between required and optional.</summary>
        PropertyRequirementChanged,

        /// <summary>A constraint on a property changed.</summary>
        ConstraintChanged,

        /// <summary>The nested schema of a complex property changed.</summary>
        NestedSchemaChanged
    }
}
