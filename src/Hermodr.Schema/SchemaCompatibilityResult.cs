//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// The result of comparing two versions of an event schema for compatibility.
    /// </summary>
    public sealed class SchemaCompatibilityResult
    {
        /// <summary>
        /// Creates a new <see cref="SchemaCompatibilityResult"/>.
        /// </summary>
        /// <param name="level">The overall compatibility level.</param>
        /// <param name="changes">The individual changes detected between the schemas.</param>
        public SchemaCompatibilityResult(SchemaCompatibilityLevel level, IEnumerable<SchemaChange> changes)
        {
            Level = level;
            Changes = (changes ?? throw new ArgumentNullException(nameof(changes))).ToList().AsReadOnly();
        }

        /// <summary>The overall compatibility level between the two schemas.</summary>
        public SchemaCompatibilityLevel Level { get; }

        /// <summary>The individual changes detected between the schemas.</summary>
        public IReadOnlyList<SchemaChange> Changes { get; }

        /// <summary>
        /// Returns <c>true</c> when the result satisfies the requested <paramref name="level"/>.
        /// </summary>
        /// <param name="level">The minimum compatibility level required.</param>
        public bool IsCompatible(SchemaCompatibilityLevel level)
            => Level >= level;
    }
}
