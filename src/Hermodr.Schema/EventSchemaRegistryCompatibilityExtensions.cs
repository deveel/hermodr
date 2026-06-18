//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Extension methods for <see cref="IEventSchemaRegistry"/> that provide compatibility
    /// checking between registered schema versions.
    /// </summary>
    public static class EventSchemaRegistryCompatibilityExtensions
    {
        /// <summary>
        /// Compares two registered versions of the specified event type for compatibility.
        /// </summary>
        /// <param name="registry">The schema registry.</param>
        /// <param name="eventType">The event type to compare.</param>
        /// <param name="olderVersion">The older schema version.</param>
        /// <param name="newerVersion">The newer schema version.</param>
        /// <param name="checker">The compatibility checker to use. Defaults to <see cref="EventSchemaCompatibilityChecker"/>.</param>
        /// <returns>A <see cref="SchemaCompatibilityResult"/> describing the detected changes.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when one or both of the requested schema versions are not registered.
        /// </exception>
        public static SchemaCompatibilityResult CheckCompatibility(
            this IEventSchemaRegistry registry,
            string eventType,
            string olderVersion,
            string newerVersion,
            IEventSchemaCompatibilityChecker? checker = null)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentException.ThrowIfNullOrEmpty(eventType);
            ArgumentException.ThrowIfNullOrEmpty(olderVersion);
            ArgumentException.ThrowIfNullOrEmpty(newerVersion);

            var older = registry.GetSchema(eventType, olderVersion)
                ?? throw new ArgumentException(
                    $"Schema for event type '{eventType}' version '{olderVersion}' is not registered.",
                    nameof(olderVersion));

            var newer = registry.GetSchema(eventType, newerVersion)
                ?? throw new ArgumentException(
                    $"Schema for event type '{eventType}' version '{newerVersion}' is not registered.",
                    nameof(newerVersion));

            checker ??= new EventSchemaCompatibilityChecker();
            return checker.Check(older, newer);
        }
    }
}
