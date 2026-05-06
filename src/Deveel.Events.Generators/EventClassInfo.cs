//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;

namespace Deveel.Events
{
    /// <summary>
    /// Immutable data model capturing every piece of per-class information that
    /// <see cref="EventConvertibleGenerator"/> needs to emit a
    /// <c>IEventConvertible.ToCloudEvent()</c> implementation and to report diagnostics.
    /// </summary>
    /// <remarks>
    /// Equality is implemented manually so that the Roslyn incremental pipeline can
    /// cache results correctly: identical models never trigger re-generation.
    /// </remarks>
    internal sealed class EventClassInfo : IEquatable<EventClassInfo>
    {
        public string? Namespace { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string? EventType { get; set; }

        /// <summary>Absolute DataSchema URI string (mutually exclusive with <see cref="DataVersion"/>).</summary>
        public string? DataSchemaUri { get; set; }

        /// <summary>DataVersion string (mutually exclusive with <see cref="DataSchemaUri"/>).</summary>
        public string? DataVersion { get; set; }

        public string? ContentType { get; set; }

        /// <summary>
        /// Attributes sourced from <c>[EventAttributes("name", value)]</c> usages on the class.
        /// Each entry knows whether the name is a CloudEvents standard attribute or a custom
        /// extension, and carries a ready-to-emit C# literal for the value.
        /// Entries whose names collide with event-metadata reserved names are excluded here
        /// (they are reported as DLEVT004 instead).
        /// </summary>
        public IReadOnlyList<EventAttributeData> ExtraAttributes { get; set; }
            = Array.Empty<EventAttributeData>();

        /// <summary>
        /// <c>[EventAttributes]</c> names that conflict with the event metadata already
        /// controlled by <c>[Event]</c> (e.g. <c>"type"</c>, <c>"dataschema"</c>,
        /// <c>"datacontenttype"</c>, <c>"dataversion"</c>).
        /// Each entry triggers a DLEVT004 error diagnostic.
        /// </summary>
        public IReadOnlyList<string> CollidingAttributeNames { get; set; }
            = Array.Empty<string>();

        /// <summary>
        /// <c>[EventAttributes]</c> names that are not CloudEvents standard attributes and are
        /// invalid extension names (cannot be created via <c>CloudEventAttribute.CreateExtension</c>).
        /// Each entry triggers a DLEVT005 error diagnostic.
        /// </summary>
        public IReadOnlyList<string> InvalidExtensionAttributeNames { get; set; }
            = Array.Empty<string>();

        public bool IsPartial { get; set; }
        public bool IsPublic { get; set; }
        public bool HasSchemaOrVersion { get; set; }

        public Location? Location { get; set; }

        /// <summary>
        /// <c>true</c> when all preconditions are met and the generator can safely emit code.
        /// </summary>
        public bool CanGenerate => IsPartial && IsPublic && HasSchemaOrVersion;

        // ------------------------------------------------------------------ equality

        public bool Equals(EventClassInfo? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            if (Namespace != other.Namespace ||
                ClassName != other.ClassName ||
                EventType != other.EventType ||
                DataSchemaUri != other.DataSchemaUri ||
                DataVersion != other.DataVersion ||
                ContentType != other.ContentType ||
                IsPartial != other.IsPartial ||
                IsPublic != other.IsPublic ||
                HasSchemaOrVersion != other.HasSchemaOrVersion)
                return false;

            if (ExtraAttributes.Count != other.ExtraAttributes.Count)
                return false;

            for (int i = 0; i < ExtraAttributes.Count; i++)
            {
                if (!ExtraAttributes[i].Equals(other.ExtraAttributes[i]))
                    return false;
            }

            if (CollidingAttributeNames.Count != other.CollidingAttributeNames.Count)
                return false;

            for (int i = 0; i < CollidingAttributeNames.Count; i++)
            {
                if (CollidingAttributeNames[i] != other.CollidingAttributeNames[i])
                    return false;
            }

            if (InvalidExtensionAttributeNames.Count != other.InvalidExtensionAttributeNames.Count)
                return false;

            for (int i = 0; i < InvalidExtensionAttributeNames.Count; i++)
            {
                if (InvalidExtensionAttributeNames[i] != other.InvalidExtensionAttributeNames[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as EventClassInfo);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Namespace?.GetHashCode() ?? 0;
                h = h * 31 + (ClassName?.GetHashCode() ?? 0);
                h = h * 31 + (EventType?.GetHashCode() ?? 0);
                h = h * 31 + (DataSchemaUri?.GetHashCode() ?? 0);
                h = h * 31 + (DataVersion?.GetHashCode() ?? 0);
                h = h * 31 + (ContentType?.GetHashCode() ?? 0);
                h = h * 31 + IsPartial.GetHashCode();
                h = h * 31 + IsPublic.GetHashCode();
                h = h * 31 + HasSchemaOrVersion.GetHashCode();
                return h;
            }
        }
    }
}

