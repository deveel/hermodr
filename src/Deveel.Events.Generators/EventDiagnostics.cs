//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.CodeAnalysis;

namespace Deveel.Events
{
    /// <summary>
    /// Diagnostic descriptors reported by the <see cref="EventConvertibleGenerator"/>.
    /// </summary>
    internal static class EventDiagnostics
    {
        private const string Category = "Deveel.Events";

        /// <summary>
        /// DLEVT001 — <c>[Event]</c> is applied to a non-partial class.
        /// The source generator cannot emit <c>IEventConvertible</c> for it;
        /// the runtime reflection path in <c>EventFactory</c> is used as a silent fallback.
        /// </summary>
        public static readonly DiagnosticDescriptor EventClassNotPartial =
            new DiagnosticDescriptor(
                id: "DLEVT001",
                title: "[Event] class must be partial",
                messageFormat: "Class '{0}' is annotated with [Event] but is not declared as 'partial'. " +
                               "The source generator cannot emit a compile-time IEventConvertible implementation; " +
                               "the runtime reflection fallback will be used instead.",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: "Add the 'partial' modifier to the class declaration so the Deveel Events source " +
                             "generator can emit a zero-reflection IEventConvertible.ToCloudEvent() implementation.");

        /// <summary>
        /// DLEVT002 — <c>[Event]</c> specifies neither a <c>DataVersion</c> nor an absolute
        /// <c>DataSchema</c> URI.  At least one is required to construct a valid CloudEvent.
        /// </summary>
        public static readonly DiagnosticDescriptor MissingDataVersionOrSchema =
            new DiagnosticDescriptor(
                id: "DLEVT002",
                title: "[Event] must specify DataVersion or an absolute DataSchema URI",
                messageFormat: "Class '{0}' is annotated with [Event] but specifies neither a DataVersion nor " +
                               "an absolute DataSchema URI. At least one must be provided.",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Provide either an absolute DataSchema URI " +
                             "(e.g. 'https://schemas.example.com/events/my-event/1.0') " +
                             "or a non-empty DataVersion string as the second argument to [Event].");

        /// <summary>
        /// DLEVT003 — The <c>[Event]</c>-annotated class is not <c>public</c>.
        /// Event data classes must be publicly accessible so they can be used across assembly boundaries.
        /// </summary>
        public static readonly DiagnosticDescriptor EventClassNotPublic =
            new DiagnosticDescriptor(
                id: "DLEVT003",
                title: "[Event] class must be public",
                messageFormat: "Class '{0}' is annotated with [Event] but is not declared as 'public'. " +
                               "Event data classes must be public to be usable across assembly boundaries.",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: "Add the 'public' modifier to the class declaration so it can be referenced " +
                             "by the EventPublisher and transport channels in other assemblies.");

        /// <summary>
        /// DLEVT004 — An <c>[EventAttributes]</c> entry uses a name that is already
        /// controlled by the <c>[Event]</c> annotation (<c>type</c>, <c>dataschema</c>,
        /// <c>datacontenttype</c>, <c>dataversion</c>).
        /// These names are reserved for the generator and must not be overridden via
        /// <c>[EventAttributes]</c>.
        /// </summary>
        public static readonly DiagnosticDescriptor AttributeCollidesWithEventMetadata =
            new DiagnosticDescriptor(
                id: "DLEVT004",
                title: "[EventAttributes] name collides with event metadata",
                messageFormat: "Class '{0}': [EventAttributes] uses the reserved name '{1}' which is " +
                               "already managed by the [Event] annotation. " +
                               "Remove this [EventAttributes] entry and set the value through [Event] instead.",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "The attribute names 'type', 'dataschema', 'datacontenttype', and 'dataversion' " +
                             "are reserved because they are derived from the [Event] annotation at code-generation " +
                             "time. Using [EventAttributes] to set them would produce conflicting values. " +
                             "Use the corresponding [Event] constructor arguments or named properties instead.");

        /// <summary>
        /// DLEVT005 — An <c>[EventAttributes]</c> entry uses an invalid extension name.
        /// Non-standard names must be valid CloudEvents extension attribute names so they can
        /// be created via <c>CloudEventAttribute.CreateExtension</c>.
        /// </summary>
        public static readonly DiagnosticDescriptor InvalidExtensionAttributeName =
            new DiagnosticDescriptor(
                id: "DLEVT005",
                title: "[EventAttributes] invalid extension attribute name",
                messageFormat: "Class '{0}': [EventAttributes] uses invalid extension name '{1}'. " +
                               "Extension names must contain only lowercase letters and digits.",
                category: Category,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Non-standard CloudEvent attribute names are treated as extension names and " +
                             "must be valid according to CloudEvents naming rules. " +
                             "Use lowercase letters and digits only.");
    }
}

