//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Describes a single difference detected between two versions of an event schema.
    /// </summary>
    public sealed class SchemaChange
    {
        /// <summary>
        /// Creates a new <see cref="SchemaChange"/>.
        /// </summary>
        /// <param name="changeType">The kind of change detected.</param>
        /// <param name="propertyPath">The dot-separated path to the affected property.</param>
        /// <param name="message">A human-readable description of the change.</param>
        /// <param name="isBackwardBreaking">
        /// <c>true</c> when the change prevents consumers using the newer schema from reading
        /// events produced with the older schema.
        /// </param>
        /// <param name="isForwardBreaking">
        /// <c>true</c> when the change prevents consumers using the older schema from reading
        /// events produced with the newer schema.
        /// </param>
        public SchemaChange(
            SchemaChangeType changeType,
            string propertyPath,
            string message,
            bool isBackwardBreaking,
            bool isForwardBreaking)
        {
            ChangeType = changeType;
            PropertyPath = propertyPath ?? throw new ArgumentNullException(nameof(propertyPath));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            IsBackwardBreaking = isBackwardBreaking;
            IsForwardBreaking = isForwardBreaking;
        }

        /// <summary>The kind of change detected.</summary>
        public SchemaChangeType ChangeType { get; }

        /// <summary>The dot-separated path to the affected property.</summary>
        public string PropertyPath { get; }

        /// <summary>A human-readable description of the change.</summary>
        public string Message { get; }

        /// <summary>
        /// <c>true</c> when the change breaks backward compatibility.
        /// </summary>
        public bool IsBackwardBreaking { get; }

        /// <summary>
        /// <c>true</c> when the change breaks forward compatibility.
        /// </summary>
        public bool IsForwardBreaking { get; }
    }
}
