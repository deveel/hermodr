//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System;

namespace Deveel.Events
{
    /// <summary>
    /// Represents one <c>[EventAttributes("name", value)]</c> entry on an event class,
    /// carrying everything the generator needs to emit a correct CloudEvent attribute
    /// assignment without any further reflection.
    /// </summary>
    internal sealed class EventAttributeData : IEquatable<EventAttributeData>
    {
        /// <summary>The CloudEvent attribute name (e.g. <c>"subject"</c> or <c>"environment"</c>).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// A C# literal expression that evaluates to the attribute value at call time
        /// (e.g. <c>"\"production\""</c>, <c>"42"</c>, <c>"true"</c>).
        /// </summary>
        public string ValueExpression { get; set; } = string.Empty;

        /// <summary>
        /// The <c>CloudEventAttributeType</c> member name to use when creating a CloudEvent
        /// extension attribute (e.g. <c>"String"</c>, <c>"Integer"</c>, <c>"Boolean"</c>).
        /// Only relevant when <see cref="IsStandardAttribute"/> is <c>false</c>.
        /// </summary>
        public string CloudEventAttributeType { get; set; } = "String";

        /// <summary>
        /// <c>true</c> when <see cref="Name"/> matches one of the CloudEvents 1.0 standard
        /// attribute names (<c>id</c>, <c>source</c>, <c>specversion</c>, <c>type</c>,
        /// <c>datacontenttype</c>, <c>dataschema</c>, <c>subject</c>, <c>time</c>).
        /// Standard attributes are set via the plain string indexer; non-standard ones are
        /// registered as extension attributes via <c>CloudEventAttribute.CreateExtension</c>.
        /// </summary>
        public bool IsStandardAttribute { get; set; }

        // ──────────────────────────────────────────── equality (incremental pipeline cache)

        public bool Equals(EventAttributeData? other)
            => other is not null
               && Name == other.Name
               && ValueExpression == other.ValueExpression
               && CloudEventAttributeType == other.CloudEventAttributeType
               && IsStandardAttribute == other.IsStandardAttribute;

        public override bool Equals(object? obj) => Equals(obj as EventAttributeData);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Name?.GetHashCode() ?? 0;
                h = h * 31 + (ValueExpression?.GetHashCode() ?? 0);
                h = h * 31 + (CloudEventAttributeType?.GetHashCode() ?? 0);
                h = h * 31 + IsStandardAttribute.GetHashCode();
                return h;
            }
        }
    }
}

