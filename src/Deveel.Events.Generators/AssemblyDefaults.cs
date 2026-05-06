//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Carry the values read from the assembly-level
    /// <c>[EventDataSchemaUri]</c> and <c>[EventJsonSerializationOptions]</c>
    /// attributes into the code-generation step.
    /// </summary>
    internal sealed class AssemblyDefaults
    {
        /// <summary>
        /// The absolute base URI from <c>[assembly: EventDataSchemaUri(...)]</c>,
        /// or <c>null</c> when the attribute is not present.
        /// </summary>
        public string? DataSchemaBaseUri { get; set; }

        /// <summary>
        /// The fully-qualified type name of the JSON options provider from
        /// <c>[assembly: EventJsonSerializationOptions(typeof(...))]</c>,
        /// or <c>null</c> when not set.
        /// The generated code will emit a call to <c>&lt;TypeName&gt;.GetOptions()</c>.
        /// </summary>
        public string? JsonOptionsProviderTypeName { get; set; }
    }
}

