//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Deveel.Events
{
    /// <summary>
    /// Roslyn incremental source generator that detects every <c>partial</c> class decorated
    /// with <c>[Event]</c> in the current compilation and emits a generated partial class body
    /// that implements <c>IEventConvertible.ToCloudEvent()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All CloudEvents envelope values (<c>Type</c>, <c>DataSchema</c>, <c>DataContentType</c>)
    /// are sourced from annotation values captured at compile time; the <c>Data</c> field is
    /// populated via <c>System.Text.Json</c> serialisation - zero reflection at call time.
    /// </para>
    /// <para>
    /// Additional CloudEvent attributes are supplied via <c>[EventAttributes]</c>:
    /// names that match CloudEvents 1.0 standard attributes are set via the plain string
    /// indexer; everything else is registered as an extension attribute via
    /// <c>CloudEventAttribute.CreateExtension()</c>.
    /// Attempting to set a reserved metadata name (<c>type</c>, <c>dataschema</c>,
    /// <c>datacontenttype</c>, <c>dataversion</c>) triggers a DLEVT004 error.
    /// Non-standard names must also be valid CloudEvents extension names (lowercase letters
    /// and digits only), otherwise DLEVT005 is reported.
    /// </para>
    /// <para>Diagnostics emitted:</para>
    /// <list type="bullet">
    ///   <item><term>DLEVT001</term><description><c>[Event]</c> applied to a non-partial class.</description></item>
    ///   <item><term>DLEVT002</term><description><c>[Event]</c> specifies neither DataVersion nor an absolute DataSchema URI.</description></item>
    ///   <item><term>DLEVT003</term><description><c>[Event]</c>-annotated class is not public.</description></item>
    ///   <item><term>DLEVT004</term><description><c>[EventAttributes]</c> name collides with a reserved event-metadata attribute.</description></item>
    ///   <item><term>DLEVT005</term><description><c>[EventAttributes]</c> non-standard name is not a valid CloudEvents extension attribute name.</description></item>
    /// </list>
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public sealed class EventConvertibleGenerator : IIncrementalGenerator
    {
        internal const string EventAttributeFullName                    = "Deveel.Events.EventAttribute";
        internal const string EventAttributesAttributeFullName          = "Deveel.Events.EventAttributesAttribute";
        internal const string EventDataSchemaUriAttributeFullName       = "Deveel.Events.EventDataSchemaUriAttribute";
        internal const string EventJsonSerializationOptionsFullName     = "Deveel.Events.EventJsonSerializationOptionsAttribute";

        // CloudEvents 1.0 standard attribute names (spec §3.1).
        // Standard attributes are set via the plain string indexer on CloudEvent.
        // Everything else is registered as an extension via CloudEventAttribute.CreateExtension().
        private static readonly string[] StandardCloudEventAttributeNames =
        {
            "id", "source", "specversion", "type",
            "datacontenttype", "dataschema", "subject", "time"
        };

        // Attribute names reserved because they are already controlled by [Event].
        // Using [EventAttributes] with any of these names is a DLEVT004 error.
        private static readonly string[] EventMetadataReservedNames =
        {
            "type", "dataschema", "datacontenttype", "dataversion"
        };

        /// <inheritdoc/>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Read the optional assembly-level [EventGeneratorDefaults] once per compilation.
            var assemblyDefaults = context.CompilationProvider.Select(static (compilation, _) =>
                ReadAssemblyDefaults(compilation));

            var classInfos = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    EventAttributeFullName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: TransformClass)
                .Where(static info => info is not null);

            // Combine each class info with the assembly-level defaults.
            var combined = classInfos.Combine(assemblyDefaults);

            context.RegisterSourceOutput(combined, static (ctx, pair) =>
                Generate(ctx, pair.Left!, pair.Right));
        }

        // ------------------------------------------------------------------ assembly defaults

        private static AssemblyDefaults ReadAssemblyDefaults(Compilation compilation)
        {
            string? dataSchemaBaseUri = null;
            string? jsonOptionsProviderTypeName = null;

            var eventDataSchemaUriAttribute = compilation.GetTypeByMetadataName(EventDataSchemaUriAttributeFullName);
            var eventJsonSerializationOptionsAttribute = compilation.GetTypeByMetadataName(EventJsonSerializationOptionsFullName);

            foreach (var attr in compilation.Assembly.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                    continue;

                if (eventDataSchemaUriAttribute is not null && SymbolEqualityComparer.Default.Equals(attrClass, eventDataSchemaUriAttribute))
                {
                    // Constructor arg 0: baseUri string
                    if (attr.ConstructorArguments.Length >= 1 &&
                        attr.ConstructorArguments[0].Value is string u &&
                        !string.IsNullOrWhiteSpace(u))
                    {
                        dataSchemaBaseUri = u;
                    }
                }
                else if (eventJsonSerializationOptionsAttribute is not null && SymbolEqualityComparer.Default.Equals(attrClass, eventJsonSerializationOptionsAttribute))
                {
                    // Constructor arg 0: providerType
                    if (attr.ConstructorArguments.Length >= 1 &&
                        attr.ConstructorArguments[0].Value is INamedTypeSymbol t)
                    {
                        jsonOptionsProviderTypeName = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    }
                }
            }

            return new AssemblyDefaults
            {
                DataSchemaBaseUri           = dataSchemaBaseUri,
                JsonOptionsProviderTypeName = jsonOptionsProviderTypeName,
            };
        }

        // ------------------------------------------------------------------ transform

        private static EventClassInfo? TransformClass(
            GeneratorAttributeSyntaxContext ctx,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
                return null;

            // partial modifier
            bool isPartial = false;
            foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
            {
                ct.ThrowIfCancellationRequested();
                if (syntaxRef.GetSyntax(ct) is ClassDeclarationSyntax decl)
                {
                    foreach (var m in decl.Modifiers)
                    {
                        if (m.IsKind(SyntaxKind.PartialKeyword))
                        {
                            isPartial = true;
                            break;
                        }
                    }
                }
                if (isPartial) break;
            }

            // accessibility
            bool isPublic = symbol.DeclaredAccessibility == Accessibility.Public;

            // [Event] attribute data
            AttributeData? eventAttr = null;
            foreach (var a in ctx.Attributes)
            {
                if (a.AttributeClass?.ToDisplayString() == EventAttributeFullName)
                {
                    eventAttr = a;
                    break;
                }
            }

            if (eventAttr is null)
                return null;

            string? eventType     = null;
            string? dataSchemaUri = null;
            string? dataVersion   = null;
            string? contentType   = null;

            // Constructor arg 0: eventType
            if (eventAttr.ConstructorArguments.Length >= 1)
                eventType = eventAttr.ConstructorArguments[0].Value as string;

            // Constructor arg 1: dataSchemaOrVersion
            if (eventAttr.ConstructorArguments.Length >= 2)
            {
                var raw = eventAttr.ConstructorArguments[1].Value as string;
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (Uri.TryCreate(raw, UriKind.Absolute, out _))
                        dataSchemaUri = raw;
                    else
                        dataVersion = raw;
                }
            }

            // Named arguments
            foreach (var named in eventAttr.NamedArguments)
            {
                if (named.Key == "DataVersion" && named.Value.Value is string dv && !string.IsNullOrWhiteSpace(dv))
                    dataVersion = dv;
                else if (named.Key == "ContentType" && named.Value.Value is string ct2)
                    contentType = ct2;
            }

            bool hasSchemaOrVersion = !string.IsNullOrWhiteSpace(dataSchemaUri)
                                   || !string.IsNullOrWhiteSpace(dataVersion);

            // [EventAttributes] - static CloudEvent attributes (class-level).
            var extraAttributes = new List<EventAttributeData>();
            var collidingNames = new List<string>();
            var invalidExtensionNames = new List<string>();

            foreach (var attrData in symbol.GetAttributes())
            {
                if (attrData.AttributeClass?.ToDisplayString() != EventAttributesAttributeFullName)
                    continue;
                if (attrData.ConstructorArguments.Length < 2)
                    continue;

                var attrName = attrData.ConstructorArguments[0].Value as string;
                if (attrName is null)
                    continue;

                if (IsEventMetadataReservedName(attrName))
                {
                    collidingNames.Add(attrName);
                    continue;
                }

                if (!IsStandardCloudEventAttribute(attrName) && !IsValidCloudEventExtensionName(attrName))
                {
                    invalidExtensionNames.Add(attrName);
                    continue;
                }

                var valueConst = attrData.ConstructorArguments[1];
                var (valueExpression, attributeType) = GetAttributeValueInfo(valueConst);

                extraAttributes.Add(new EventAttributeData
                {
                    Name                    = attrName,
                    ValueExpression         = valueExpression,
                    CloudEventAttributeType = attributeType,
                    IsStandardAttribute     = IsStandardCloudEventAttribute(attrName)
                });
            }

            // namespace
            string? ns = symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : symbol.ContainingNamespace.ToDisplayString();

            return new EventClassInfo
            {
                Namespace              = ns,
                ClassName              = symbol.Name,
                EventType              = eventType,
                DataSchemaUri          = dataSchemaUri,
                DataVersion            = dataVersion,
                ContentType            = contentType,
                ExtraAttributes        = extraAttributes,
                CollidingAttributeNames = collidingNames,
                InvalidExtensionAttributeNames = invalidExtensionNames,
                IsPartial              = isPartial,
                IsPublic               = isPublic,
                HasSchemaOrVersion     = hasSchemaOrVersion,
                Location               = ctx.TargetNode.GetLocation()
            };
        }

        // ------------------------------------------------------------------ output

        private static void Generate(SourceProductionContext ctx, EventClassInfo info, AssemblyDefaults defaults)
        {
            // Diagnostics are always evaluated, even when CanGenerate is false.

            if (!info.IsPartial)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    EventDiagnostics.EventClassNotPartial,
                    info.Location,
                    info.ClassName));
            }

            if (!info.HasSchemaOrVersion)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    EventDiagnostics.MissingDataVersionOrSchema,
                    info.Location,
                    info.ClassName));
            }

            if (!info.IsPublic)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    EventDiagnostics.EventClassNotPublic,
                    info.Location,
                    info.ClassName));
            }

            foreach (var colliding in info.CollidingAttributeNames)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    EventDiagnostics.AttributeCollidesWithEventMetadata,
                    info.Location,
                    info.ClassName,
                    colliding));
            }

            foreach (var invalidName in info.InvalidExtensionAttributeNames)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    EventDiagnostics.InvalidExtensionAttributeName,
                    info.Location,
                    info.ClassName,
                    invalidName));
            }

            if (!info.CanGenerate)
                return;

            // ---------------------------------------------------------------- code generation

            // When DataVersion is used together with a DataSchemaBaseUri from the assembly
            // defaults attribute, we can compute the full schema URI at compile time and
            // avoid any runtime context lookup for the DataSchema property.
            string? bakedDataSchemaUri = null;
            if (info.DataSchemaUri is null &&
                info.DataVersion is not null &&
                info.EventType is not null &&
                defaults.DataSchemaBaseUri is not null)
            {
                // Compose: base/eventType/dataVersion  (tolerant of trailing slash)
                var raw = defaults.DataSchemaBaseUri.TrimEnd('/') + "/" + info.EventType + "/" + info.DataVersion;
                if (Uri.TryCreate(raw, UriKind.Absolute, out _))
                    bakedDataSchemaUri = raw;
            }

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Generated by Deveel.Events.Generators - do not edit manually.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();

            bool hasNs     = info.Namespace is not null;
            int baseIndent = hasNs ? 1 : 0;

            if (hasNs)
            {
                sb.AppendLine("namespace " + info.Namespace);
                sb.AppendLine("{");
            }

            string I0 = Indent(baseIndent);
            string I1 = Indent(baseIndent + 1);
            string I2 = Indent(baseIndent + 2);
            string I3 = Indent(baseIndent + 3);

            sb.AppendLine(I0 + "partial class " + info.ClassName + " : global::Deveel.Events.IEventConvertible");
            sb.AppendLine(I0 + "{");

            // compile-time constants
            if (info.EventType is not null)
                sb.AppendLine(I1 + "private const string __eventType = " + Escape(info.EventType) + ";");

            // Prefer baked URI (from assembly defaults), then explicit schema URI, then DataVersion-only (runtime lookup).
            if (bakedDataSchemaUri is not null)
                sb.AppendLine(I1 + "private const string __dataSchema = " + Escape(bakedDataSchemaUri) + ";");
            else if (info.DataSchemaUri is not null)
                sb.AppendLine(I1 + "private const string __dataSchema = " + Escape(info.DataSchemaUri) + ";");

            if (info.DataVersion is not null && bakedDataSchemaUri is null)
                sb.AppendLine(I1 + "private const string __dataVersion = " + Escape(info.DataVersion) + ";");

            if (info.ContentType is not null)
                sb.AppendLine(I1 + "private const string __contentType = " + Escape(info.ContentType) + ";");

            sb.AppendLine();

            // IEventConvertible.ToCloudEvent()
            sb.AppendLine(I1 + "/// <summary>");
            sb.AppendLine(I1 + "/// Converts this object to a <see cref=\"global::CloudNative.CloudEvents.CloudEvent\"/>.");
            sb.AppendLine(I1 + "/// All envelope values are sourced from compile-time constants captured from the [Event] annotation.");
            sb.AppendLine(I1 + "/// </summary>");
            sb.AppendLine(I1 + "global::CloudNative.CloudEvents.CloudEvent global::Deveel.Events.IEventConvertible.ToCloudEvent()");
            sb.AppendLine(I1 + "{");

            // Build DataSchema URI
            if (bakedDataSchemaUri is not null || info.DataSchemaUri is not null)
            {
                // Fully compile-time: just parse the constant string.
                sb.AppendLine(I2 + "var __schemaUri = new global::System.Uri(__dataSchema, global::System.UriKind.Absolute);");
            }
            else
            {
                // DataVersion-only path: resolve base URI from the runtime context.
                sb.AppendLine(I2 + "global::System.Uri? __schemaUri = null;");
                sb.AppendLine(I2 + "var __baseUri = global::Deveel.Events.EventGeneratorContext.DataSchemaBaseUri;");
                sb.AppendLine(I2 + "if (__baseUri != null)");
                sb.AppendLine(I2 + "{");
                sb.AppendLine(I3 + "var __b = new global::System.UriBuilder(__baseUri);");
                sb.AppendLine(I3 + "__b.Path = __b.Path.TrimEnd(\'/\') + \"/\" + __eventType + \"/\" + __dataVersion;");
                sb.AppendLine(I3 + "__schemaUri = __b.Uri;");
                sb.AppendLine(I2 + "}");
            }

            // Resolve JSON serializer options expression:
            // 1. Static provider type from [assembly: EventJsonSerializationOptions(...)] (compile-time baked call).
            // 2. Runtime context (EventGeneratorContext.JsonSerializerOptions).
            string jsonOptionsExpression = defaults.JsonOptionsProviderTypeName is not null
                ? defaults.JsonOptionsProviderTypeName + ".GetOptions()"
                : "global::Deveel.Events.EventGeneratorContext.JsonSerializerOptions";

            // CloudEvent object initializer
            sb.AppendLine(I2 + "var @event = new global::CloudNative.CloudEvents.CloudEvent");
            sb.AppendLine(I2 + "{");

            if (info.EventType is not null)
                sb.AppendLine(I3 + "Type = __eventType,");

            sb.AppendLine(I3 + "DataSchema = __schemaUri,");

            if (info.ContentType is not null)
                sb.AppendLine(I3 + "DataContentType = __contentType,");

            sb.AppendLine(I3 + "Data = global::System.Text.Json.JsonSerializer.Serialize(this, " + jsonOptionsExpression + "),");
            sb.AppendLine(I2 + "};");

            // [EventAttributes] extra attributes.
            foreach (var attr in info.ExtraAttributes)
            {
                if (attr.IsStandardAttribute)
                {
                    sb.AppendLine(I2 + "@event[" + Escape(attr.Name) + "] = " + attr.ValueExpression + ";");
                }
                else
                {
                    sb.AppendLine(
                        I2 + "@event[global::CloudNative.CloudEvents.CloudEventAttribute.CreateExtension(" +
                        Escape(attr.Name) + ", " +
                        "global::CloudNative.CloudEvents.CloudEventAttributeType." + attr.CloudEventAttributeType +
                        ")] = " + attr.ValueExpression + ";");
                }
            }

            sb.AppendLine(I2 + "return @event;");
            sb.AppendLine(I1 + "}");
            sb.AppendLine(I0 + "}");

            if (hasNs)
                sb.AppendLine("}");

            string hintName = hasNs
                ? info.Namespace + "." + info.ClassName + ".EventConvertible.g.cs"
                : info.ClassName + ".EventConvertible.g.cs";

            ctx.AddSource(hintName, SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        // ------------------------------------------------------------------ helpers

        private static string Indent(int level) => new string(' ', level * 4);

        private static string Escape(string value)
            => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static bool IsStandardCloudEventAttribute(string name)
        {
            foreach (var s in StandardCloudEventAttributeNames)
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool IsEventMetadataReservedName(string name)
        {
            foreach (var s in EventMetadataReservedNames)
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool IsValidCloudEventExtensionName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            foreach (char c in name)
            {
                if ((c < 'a' || c > 'z') && (c < '0' || c > '9'))
                    return false;
            }

            return true;
        }

        private static (string ValueExpression, string AttributeType) GetAttributeValueInfo(TypedConstant constant)
        {
            if (constant.IsNull)
                return ("null", "String");

            if (constant.Type is null)
                return (Escape(constant.Value?.ToString() ?? string.Empty), "String");

            switch (constant.Type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return ((bool)constant.Value! ? "true" : "false", "Boolean");

                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                    return (constant.Value!.ToString()!, "Integer");

                default:
                    return (Escape(constant.Value?.ToString() ?? string.Empty), "String");
            }
        }
    }
}
