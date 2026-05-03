//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.Immutable;
using System.Reflection;
using System.Text;

using Deveel.Events;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Deveel
{
    /// <summary>
    /// End-to-end tests for <see cref="EventConvertibleGenerator"/>.
    ///
    /// Each test:
    /// 1. Compiles a snippet together with the required assembly references.
    /// 2. Runs the incremental generator.
    /// 3. Asserts on the generated source text and/or the diagnostics produced.
    /// </summary>
    public class EventConvertibleGeneratorTests
    {
        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Runs the <see cref="EventConvertibleGenerator"/> against <paramref name="source"/>
        /// and returns the driver run result plus the updated compilation.
        /// </summary>
        private static (GeneratorDriverRunResult RunResult, Compilation OutputCompilation)
            RunGenerator(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(
                SourceText.From(source, Encoding.UTF8),
                new CSharpParseOptions(LanguageVersion.Latest));

            // Collect metadata references from assemblies already loaded in this process.
            var references = BuildMetadataReferences();

            var compilation = CSharpCompilation.Create(
                assemblyName: "TestAssembly",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithNullableContextOptions(NullableContextOptions.Enable));

            var generator  = new EventConvertibleGenerator();
            var driver     = CSharpGeneratorDriver
                .Create(generator)
                .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

            return (driver.GetRunResult(), updated);
        }

        private static IReadOnlyList<MetadataReference> BuildMetadataReferences()
        {
            // Assemblies whose types are referenced in both tests and generated code.
            var assemblyTypes = new[]
            {
                typeof(object),                             // System.Runtime / mscorlib
                typeof(Attribute),                          // System.Runtime
                typeof(Console),                            // System.Console
                typeof(Uri),                                // System.Uri
                typeof(System.Text.Json.JsonSerializer),    // System.Text.Json
                typeof(CloudNative.CloudEvents.CloudEvent), // CloudNative.CloudEvents
                typeof(EventAttribute),                     // Deveel.Events.Annotations
                typeof(IEventConvertible),                  // Deveel.Events.Publisher
                typeof(EventGeneratorContext),              // Deveel.Events.Publisher
            };

            var refs = new List<MetadataReference>();

            // Add the assemblies for the types above
            foreach (var t in assemblyTypes)
            {
                var loc = t.Assembly.Location;
                if (!string.IsNullOrEmpty(loc))
                    refs.Add(MetadataReference.CreateFromFile(loc));
            }

            // Add referenced assemblies of the above (transitive closure for System.Runtime etc.)
            foreach (var t in assemblyTypes)
            {
                foreach (var referencedName in t.Assembly.GetReferencedAssemblies())
                {
                    try
                    {
                        var loaded = Assembly.Load(referencedName);
                        if (!string.IsNullOrEmpty(loaded.Location))
                            refs.Add(MetadataReference.CreateFromFile(loaded.Location));
                    }
                    catch
                    {
                        // ignore unresolvable references (e.g. native shims)
                    }
                }
            }

            // Deduplicate by file path
            return refs
                .GroupBy(r => ((PortableExecutableReference)r).FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private static GeneratedSourceResult? FindGeneratedFile(
            GeneratorDriverRunResult result, string classNameFragment)
            => result.Results
                .SelectMany(r => r.GeneratedSources)
                .Cast<GeneratedSourceResult?>()
                .FirstOrDefault(s => s!.Value.HintName.Contains(classNameFragment));

        private static IReadOnlyList<Diagnostic> GeneratorDiagnostics(GeneratorDriverRunResult result)
            => result.Results.SelectMany(r => r.Diagnostics).ToList();

        // ================================================================== tests

        // ------------------------------------------------------------------ happy path

        [Fact]
        public void ValidPartialPublicClass_WithAbsoluteDataSchema_GeneratesIEventConvertible()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.order.created", "https://schemas.example.com/events/order-created/1.0")]
                    public partial class OrderCreatedEvent
                    {
                        public string OrderId { get; set; } = string.Empty;
                    }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostics = GeneratorDiagnostics(result);
            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

            var generated = FindGeneratedFile(result, "OrderCreatedEvent");
            Assert.NotNull(generated);

            var text = generated!.Value.SourceText.ToString();

            // Class declaration
            Assert.Contains("partial class OrderCreatedEvent", text);

            // Implements IEventConvertible
            Assert.Contains("global::Deveel.Events.IEventConvertible", text);

            // Compile-time constants
            Assert.Contains("__eventType", text);
            Assert.Contains("com.example.order.created", text);
            Assert.Contains("__dataSchema", text);
            Assert.Contains("https://schemas.example.com/events/order-created/1.0", text);

            // DataSchema URI constructed from the constant — no DataVersion branch
            Assert.Contains("new global::System.Uri(__dataSchema", text);
            Assert.DoesNotContain("EventGeneratorContext.DataSchemaBaseUri", text);

            // JSON serialisation
            Assert.Contains("JsonSerializer.Serialize", text);
        }

        [Fact]
        public void ValidPartialPublicClass_WithDataVersion_GeneratesIEventConvertible_WithBaseUriLookup()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.customer.registered", "2.1")]
                    public partial class CustomerRegisteredEvent
                    {
                        public string CustomerId { get; set; } = string.Empty;
                    }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostics = GeneratorDiagnostics(result);
            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

            var generated = FindGeneratedFile(result, "CustomerRegisteredEvent");
            Assert.NotNull(generated);

            var text = generated!.Value.SourceText.ToString();

            Assert.Contains("__dataVersion", text);
            Assert.Contains("2.1", text);

            // Must use the ambient context to resolve the base URI at call time
            Assert.Contains("EventGeneratorContext.DataSchemaBaseUri", text);
        }

        [Fact]
        public void ValidPartialPublicClass_WithDataVersionAndAssemblyBaseUri_EmitsBakedDataSchema()
        {
            // When the assembly has [EventDataSchemaUri] AND the class uses DataVersion,
            // the generator should bake the full URI at compile time and emit __dataSchema,
            // NOT __dataVersion / EventGeneratorContext.DataSchemaBaseUri.
            const string source = """
                using Deveel.Events;

                [assembly: EventDataSchemaUri("https://schemas.example.com/events")]

                namespace MyApp.Events
                {
                    [Event("com.example.order.placed", "3.0", ContentType = "application/json")]
                    public partial class OrderPlacedEvent
                    {
                        public string OrderId { get; set; } = string.Empty;
                    }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostics = GeneratorDiagnostics(result);
            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

            var generated = FindGeneratedFile(result, "OrderPlacedEvent");
            Assert.NotNull(generated);

            var text = generated!.Value.SourceText.ToString();

            // Must emit the baked compile-time __dataSchema constant
            Assert.Contains("__dataSchema", text);
            Assert.Contains("https://schemas.example.com/events/com.example.order.placed/3.0", text);

            // Must NOT fall back to the runtime lookup
            Assert.DoesNotContain("EventGeneratorContext.DataSchemaBaseUri", text);
            Assert.DoesNotContain("__dataVersion", text);
        }

        [Fact]
        public void ValidClass_WithContentType_EmbedsContentTypeConstant()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.ping", "1.0", ContentType = "application/json")]
                    public partial class PingEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var generated = FindGeneratedFile(result, "PingEvent");
            Assert.NotNull(generated);

            var text = generated!.Value.SourceText.ToString();
            Assert.Contains("__contentType", text);
            Assert.Contains("application/json", text);
        }

        [Fact]
        public void ValidClass_WithEventAttributesAttribute_EmitsExtraAttributeAssignments()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.shipped", "1.0")]
                    [EventAttributes("region", "eu-west-1")]
                    public partial class ShipmentEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var generated = FindGeneratedFile(result, "ShipmentEvent");
            Assert.NotNull(generated);

            var text = generated!.Value.SourceText.ToString();
            Assert.Contains("\"region\"", text);
            Assert.Contains("\"eu-west-1\"", text);
        }

        // ------------------------------------------------------------------ DLEVT001

        [Fact]
        public void NonPartialClass_ReportsDLEVT001_AndNoSourceGenerated()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.order.created", "1.0")]
                    public class NonPartialOrderEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostics = GeneratorDiagnostics(result);
            Assert.Contains(diagnostics, d => d.Id == "DLEVT001");

            // No source should be emitted when the class is not partial
            var generated = FindGeneratedFile(result, "NonPartialOrderEvent");
            Assert.Null(generated);
        }

        // ------------------------------------------------------------------ DLEVT002

        [Fact]
        public void EventAttributeWithEmptySchemaOrVersion_ReportsDLEVT002()
        {
            // Passing an empty string as the second arg triggers DLEVT002
            // (neither absolute URI nor non-empty version string).
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.bad.event", "")]
                    public partial class BadEvent { }
                }
                """;

            // Note: the attribute constructor itself will throw at runtime,
            // but at compile time the raw string "" is visible to the generator.
            var (result, _) = RunGenerator(source);

            var diagnostics = GeneratorDiagnostics(result);
            Assert.Contains(diagnostics, d => d.Id == "DLEVT002");
        }

        // ------------------------------------------------------------------ DLEVT003

        [Fact]
        public void InternalPartialClass_ReportsDLEVT003_AndNoSourceGenerated()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.internal.event", "1.0")]
                    internal partial class InternalEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostics = GeneratorDiagnostics(result);
            Assert.Contains(diagnostics, d => d.Id == "DLEVT003");

            var generated = FindGeneratedFile(result, "InternalEvent");
            Assert.Null(generated);
        }

        // ------------------------------------------------------------------ multi-diagnostic

        [Fact]
        public void NonPublicNonPartialClass_ReportsBothDLEVT001AndDLEVT003()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.bad", "1.0")]
                    internal class DoubleInvalidEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var ids = GeneratorDiagnostics(result).Select(d => d.Id).ToHashSet();
            Assert.Contains("DLEVT001", ids);
            Assert.Contains("DLEVT003", ids);
        }

        // ------------------------------------------------------------------ generated code compiles

        [Fact]
        public void GeneratedCode_CompilesWithoutErrors_ForAbsoluteDataSchema()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.compile.check", "https://schemas.example.com/events/compile/1.0")]
                    public partial class CompileCheckEvent
                    {
                        public string Payload { get; set; } = string.Empty;
                    }
                }
                """;

            var (result, updatedCompilation) = RunGenerator(source);

            // No generator errors
            Assert.DoesNotContain(GeneratorDiagnostics(result), d => d.Severity == DiagnosticSeverity.Error);

            // The updated compilation (original + generated sources) must have no errors
            var compilationErrors = updatedCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            Assert.Empty(compilationErrors);
        }

        [Fact]
        public void GeneratedCode_CompilesWithoutErrors_ForDataVersion()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.versioned", "3.0")]
                    public partial class VersionedEvent
                    {
                        public int Sequence { get; set; }
                    }
                }
                """;

            var (result, updatedCompilation) = RunGenerator(source);

            Assert.DoesNotContain(GeneratorDiagnostics(result), d => d.Severity == DiagnosticSeverity.Error);

            var compilationErrors = updatedCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            Assert.Empty(compilationErrors);
        }

        // ------------------------------------------------------------------ [EventAttributes] standard vs extension routing

        [Fact]
        public void ValidClass_WithNonStandardEventAttributes_EmitsCreateExtension()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.shipped", "1.0")]
                    [EventAttributes("region", "eu-west-1")]
                    public partial class ShipmentEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var generated = FindGeneratedFile(result, "ShipmentEvent");
            Assert.NotNull(generated);

            var text = generated!.Value.SourceText.ToString();
            // "region" is not a standard CloudEvents attribute → CreateExtension path
            Assert.Contains("CreateExtension", text);
            Assert.Contains("\"region\"", text);
            Assert.Contains("\"eu-west-1\"", text);
        }

        [Fact]
        public void ValidClass_WithStandardNameEventAttributes_UsesDirectIndexer()
        {
            // "subject" is a CloudEvents 1.0 standard attribute → plain string indexer
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.ping", "1.0")]
                    [EventAttributes("subject", "my-subject")]
                    public partial class PingEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var generated = FindGeneratedFile(result, "PingEvent");
            Assert.NotNull(generated);

            var text = generated!.Value.SourceText.ToString();
            // standard attribute → no CreateExtension, just @event["subject"] = ...
            Assert.DoesNotContain("CreateExtension", text);
            Assert.Contains("\"subject\"", text);
            Assert.Contains("\"my-subject\"", text);
            Assert.DoesNotContain("DLEVT004", text);
        }

        // ------------------------------------------------------------------ DLEVT004 collision

        [Fact]
        public void EventAttributes_WithTypeName_ReportsDLEVT004()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.foo", "1.0")]
                    [EventAttributes("type", "overridden")]
                    public partial class FooEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostics = GeneratorDiagnostics(result);
            Assert.Contains(diagnostics, d => d.Id == "DLEVT004" && d.GetMessage().Contains("'type'"));
        }

        [Fact]
        public void EventAttributes_WithDataSchemaName_ReportsDLEVT004()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.foo", "1.0")]
                    [EventAttributes("dataschema", "https://schemas.example.com/v2")]
                    public partial class FooEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostics = GeneratorDiagnostics(result);
            Assert.Contains(diagnostics, d => d.Id == "DLEVT004" && d.GetMessage().Contains("'dataschema'"));
        }

        [Fact]
        public void EventAttributes_WithDataContentTypeName_ReportsDLEVT004()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.foo", "1.0", ContentType = "application/json")]
                    [EventAttributes("datacontenttype", "text/plain")]
                    public partial class FooEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostics = GeneratorDiagnostics(result);
            Assert.Contains(diagnostics, d => d.Id == "DLEVT004" && d.GetMessage().Contains("'datacontenttype'"));
        }

        [Fact]
        public void EventAttributes_WithDataVersionName_ReportsDLEVT004()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.foo", "2.0")]
                    [EventAttributes("dataversion", "2.0")]
                    public partial class FooEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostics = GeneratorDiagnostics(result);
            Assert.Contains(diagnostics, d => d.Id == "DLEVT004" && d.GetMessage().Contains("'dataversion'"));
        }

        [Fact]
        public void EventAttributes_MultipleCollisions_ReportsOneDLEVT004PerName()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.foo", "1.0")]
                    [EventAttributes("type", "override")]
                    [EventAttributes("dataschema", "https://override.example.com/v2")]
                    public partial class FooEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var dlevt004 = GeneratorDiagnostics(result).Where(d => d.Id == "DLEVT004").ToList();
            Assert.Equal(2, dlevt004.Count);
            Assert.Contains(dlevt004, d => d.GetMessage().Contains("'type'"));
            Assert.Contains(dlevt004, d => d.GetMessage().Contains("'dataschema'"));
        }

        [Fact]
        public void EventAttributes_CollidingNames_AreNotEmittedInGeneratedCode()
        {
            // Colliding attributes must be excluded from the generated code even though
            // a DLEVT004 error is reported.
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.foo", "1.0")]
                    [EventAttributes("type", "override")]
                    [EventAttributes("environment", "production")]
                    public partial class FooEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            // DLEVT004 for "type"
            Assert.Contains(GeneratorDiagnostics(result), d => d.Id == "DLEVT004");

            var generated = FindGeneratedFile(result, "FooEvent");
            Assert.NotNull(generated);
            var text = generated!.Value.SourceText.ToString();

            // "type" is colliding → NOT in generated code as an extra attribute
            Assert.DoesNotContain("\"override\"", text);
            // "environment" is fine → in generated code
            Assert.Contains("\"environment\"", text);
            Assert.Contains("\"production\"", text);
        }

        [Fact]
        public void EventAttributes_CaseInsensitiveCollision_ReportsDLEVT004()
        {
            // Reserved names are compared case-insensitively (CloudEvents names are lowercase,
            // but defensive checking is warranted).
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.foo", "1.0")]
                    [EventAttributes("Type", "override")]
                    public partial class FooEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            Assert.Contains(GeneratorDiagnostics(result), d => d.Id == "DLEVT004");
        }

        // ------------------------------------------------------------------ DLEVT005 invalid extension name

        [Fact]
        public void EventAttributes_WithInvalidExtensionName_ReportsDLEVT005()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.foo", "1.0")]
                    [EventAttributes("trace-id", "abc")]
                    public partial class FooEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            Assert.Contains(
                GeneratorDiagnostics(result),
                d => d.Id == "DLEVT005" && d.GetMessage().Contains("'trace-id'"));
        }

        [Fact]
        public void EventAttributes_WithUppercaseExtensionName_ReportsDLEVT005()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.foo", "1.0")]
                    [EventAttributes("TraceId", "abc")]
                    public partial class FooEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            Assert.Contains(
                GeneratorDiagnostics(result),
                d => d.Id == "DLEVT005" && d.GetMessage().Contains("'TraceId'"));
        }

        [Fact]
        public void EventAttributes_InvalidExtensionName_IsNotEmittedInGeneratedCode()
        {
            const string source = """
                using Deveel.Events;

                namespace MyApp.Events
                {
                    [Event("com.example.foo", "1.0")]
                    [EventAttributes("trace-id", "abc")]
                    [EventAttributes("region", "eu-west-1")]
                    public partial class FooEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            Assert.Contains(GeneratorDiagnostics(result), d => d.Id == "DLEVT005");

            var generated = FindGeneratedFile(result, "FooEvent");
            Assert.NotNull(generated);

            var text = generated!.Value.SourceText.ToString();
            Assert.DoesNotContain("trace-id", text);
            Assert.DoesNotContain("\"abc\"", text);
            Assert.Contains("\"region\"", text);
            Assert.Contains("\"eu-west-1\"", text);
        }

        // ------------------------------------------------------------------ namespace handling

        [Fact]
        public void GlobalNamespaceClass_GeneratesWithoutNamespaceWrapper()
        {
            const string source = """
                using Deveel.Events;

                [Event("com.example.global", "https://schemas.example.com/events/global/1.0")]
                public partial class GlobalEvent { }
                """;

            var (result, _) = RunGenerator(source);

            var generated = FindGeneratedFile(result, "GlobalEvent");
            Assert.NotNull(generated);

            var text = generated!.Value.SourceText.ToString();

            // When there is no namespace the generated file must NOT contain 'namespace {'
            Assert.DoesNotContain("namespace {", text);
            Assert.Contains("partial class GlobalEvent", text);
        }

        [Fact]
        public void NestedNamespaceClass_UsesFullNamespaceInHintName()
        {
            const string source = """
                using Deveel.Events;

                namespace Company.Domain.Events
                {
                    [Event("com.company.deep", "https://schemas.example.com/events/deep/1.0")]
                    public partial class DeepEvent { }
                }
                """;

            var (result, _) = RunGenerator(source);

            var generated = FindGeneratedFile(result, "DeepEvent");
            Assert.NotNull(generated);

            // Hint name must include the full namespace
            Assert.Contains("Company.Domain.Events", generated!.Value.HintName);
        }
    }
}





