//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.OpenApi;
using System.Text.Json;

namespace Hermodr {
    /// <summary>
    /// Tests for <see cref="EventSchemaOpenApiExtensions"/>.
    /// </summary>
    public class OpenApiExtensionsTests {
        [Fact]
        public void ToOpenApiSchema_TitleAndTypeSet() {
            var schema = OpenApiTestSchemas.SimpleSchema();
            var open = schema.ToOpenApiSchema();

            Assert.Equal(JsonSchemaType.Object, open.Type);
            Assert.Equal("user.registered", open.Title);
            Assert.Equal("User registration event", open.Description);
        }

        [Fact]
        public void ToOpenApiSchema_PropertiesMapped() {
            var schema = OpenApiTestSchemas.SimpleSchema();
            var open = schema.ToOpenApiSchema();

            Assert.True(open.Properties!.ContainsKey("user_id"));
            Assert.True(open.Properties.ContainsKey("email"));
            Assert.True(open.Properties.ContainsKey("age"));
            Assert.True(open.Properties.ContainsKey("nickname"));
        }

        [Fact]
        public void ToOpenApiSchema_RequiredAdded() {
            var schema = OpenApiTestSchemas.SimpleSchema();
            var open = schema.ToOpenApiSchema();

            Assert.Contains("user_id", open.Required!);
            Assert.Contains("email", open.Required!);
            Assert.DoesNotContain("nickname", open.Required!);
        }

        [Fact]
        public void ToOpenApiSchema_ArrayHasItems() {
            var schema = EventSchema.Build("with.array")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .AddProperty("tags", p => p.OfType("string[]"))
                .Build();
            var open = schema.ToOpenApiSchema();

            Assert.Equal(JsonSchemaType.Array, open.Properties!["tags"].Type);
            Assert.NotNull(open.Properties["tags"].Items);
            Assert.Equal(JsonSchemaType.String, open.Properties["tags"].Items!.Type);
        }

        [Fact]
        public void ToOpenApiSchema_NestedObjectHasSubProperties() {
            var schema = OpenApiTestSchemas.NestedSchema();
            var open = schema.ToOpenApiSchema();

            var addr = open.Properties!["address"];
            Assert.Equal(JsonSchemaType.Object, addr.Type);
            Assert.True(addr.Properties!.ContainsKey("street"));
            Assert.True(addr.Properties.ContainsKey("city"));
            Assert.Contains("street", addr.Required!);
            Assert.Contains("city", addr.Required!);
        }

        [Fact]
        public void ToOpenApiSchema_EnumConstraintPopulated() {
            var schema = EventSchema.Build("order.status.changed")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .AddProperty("status", p =>
                    p.OfType("string")
                     .WithAllowedValues<string>(new []{"Pending", "Confirmed", "Cancelled"}))
                .Build();
            var open = schema.ToOpenApiSchema();

            Assert.NotEmpty(open.Properties!["status"].Enum!);
        }

        [Fact]
        public void ToOpenApiSchema_RangeConstraintSetsMinMax() {
            var schema = OpenApiTestSchemas.SimpleSchema();
            var open = schema.ToOpenApiSchema();

            var age = open.Properties!["age"];
            Assert.Equal("18", age.Minimum);
            Assert.Equal("120", age.Maximum);
        }
    }

    /// <summary>
    /// Tests for <see cref="OpenApiWebhookDocumentBuilder"/>.
    /// </summary>
    public class OpenApiWebhookDocumentBuilderTests {
        [Fact]
        public void Build_RequiresTitle() {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new OpenApiWebhookDocumentBuilder().WithVersion("1.0").Build());
            Assert.Contains("title", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_RequiresVersion() {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new OpenApiWebhookDocumentBuilder().WithTitle("T").Build());
            Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_AddsWebhookPerSchema() {
            var doc = new OpenApiWebhookDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithSchemas(new[] { OpenApiTestSchemas.SimpleSchema(), OpenApiTestSchemas.NestedSchema() })
                .Build();

            Assert.Equal(2, doc.Webhooks!.Count);
            Assert.True(doc.Webhooks.ContainsKey("user-registered"));
            Assert.True(doc.Webhooks.ContainsKey("order-shipped"));
        }

        [Fact]
        public void Build_ComponentsSchemaRegistered() {
            var doc = new OpenApiWebhookDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithSchemas(new[] { OpenApiTestSchemas.SimpleSchema() })
                .Build();

            Assert.True(doc.Components!.Schemas!.ContainsKey("user-registered"));
        }

        [Fact]
        public void Build_WebhookHasPostOperation() {
            var doc = new OpenApiWebhookDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithSchemas(new[] { OpenApiTestSchemas.SimpleSchema() })
                .Build();

            var pathItem = doc.Webhooks!["user-registered"];
            Assert.True(pathItem.Operations!.ContainsKey(HttpMethod.Post));
        }

        [Fact]
        public void Build_OtherTransportChannelExcluded() {
            var other = new EventPublishChannelMetadata(null, EventTransports.Other);

            var doc = new OpenApiWebhookDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithSchemas(new[] { OpenApiTestSchemas.SimpleSchema() })
                .WithChannels(new[] { other })
                .Build();

            // The webhook is still emitted (it's schema-driven), but no
            // transport extension should be present.
            var op = doc.Webhooks!["user-registered"].Operations![HttpMethod.Post];
            Assert.Null(op.Extensions);
        }

        [Fact]
        public void Build_RabbitMqChannelAddsTransportExtension() {
            var rabbit = new EventPublishChannelMetadata(
                "user-registered",
                EventTransports.RabbitMq,
                new Dictionary<string, string?> { ["exchange"] = "users.x" });

            var doc = new OpenApiWebhookDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithSchemas(new[] { OpenApiTestSchemas.SimpleSchema() })
                .WithChannels(new[] { rabbit })
                .Build();

            var op = doc.Webhooks!["user-registered"].Operations![HttpMethod.Post];
            Assert.NotNull(op.Extensions);
            Assert.True(op.Extensions!.ContainsKey("x-hermodr-transport"));
        }

        [Fact]
        public void Build_UnknownTransportChannelAddsTransportExtension() {
            var kafka = new EventPublishChannelMetadata(
                "user-registered",
                "kafka",
                new Dictionary<string, string?> { ["topic"] = "users.v1" });

            var doc = new OpenApiWebhookDocumentBuilder()
                .WithTitle("Svc")
                .WithVersion("1.0")
                .WithSchemas(new[] { OpenApiTestSchemas.SimpleSchema() })
                .WithChannels(new[] { kafka })
                .Build();

            var op = doc.Webhooks!["user-registered"].Operations![HttpMethod.Post];
            Assert.NotNull(op.Extensions);
            Assert.True(op.Extensions!.ContainsKey("x-hermodr-transport"));
        }
    }

    /// <summary>
    /// Tests for <see cref="EventSchemaOpenApiWriter"/> and
    /// <see cref="EventSchemasOpenApiWriter"/>.
    /// </summary>
    public class OpenApiWriterTests {
        private static async Task<string> WriteToStringAsync(
            IEventSchema schema,
            OpenApiFormat format = OpenApiFormat.Json,
            string? title = null,
            string? version = null) {
            var writer = new EventSchemaOpenApiWriter(format, title, version);
            using var ms = new MemoryStream();
            await writer.WriteToAsync(ms, schema);
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            return reader.ReadToEnd();
        }

        [Fact]
        public async Task WriteJson_IsValidJson() {
            var output = await WriteToStringAsync(OpenApiTestSchemas.SimpleSchema());
            var ex = Record.Exception(() => JsonDocument.Parse(output));
            Assert.Null(ex);
        }

        [Fact]
        public async Task WriteJson_InfoBlockPresent() {
            var output = await WriteToStringAsync(OpenApiTestSchemas.SimpleSchema());
            using var doc = JsonDocument.Parse(output);

            Assert.Equal("user.registered", doc.RootElement.GetProperty("info").GetProperty("title").GetString());
            Assert.Equal("1.0", doc.RootElement.GetProperty("info").GetProperty("version").GetString());
        }

        [Fact]
        public async Task WriteJson_WebhookPresent() {
            var output = await WriteToStringAsync(OpenApiTestSchemas.SimpleSchema());
            using var doc = JsonDocument.Parse(output);

            Assert.True(doc.RootElement.GetProperty("webhooks").TryGetProperty("user-registered", out _));
        }

        [Fact]
        public async Task WriteJson_ComponentsSchemaPresent() {
            var output = await WriteToStringAsync(OpenApiTestSchemas.SimpleSchema());
            using var doc = JsonDocument.Parse(output);

            Assert.True(doc.RootElement.GetProperty("components").GetProperty("schemas")
                .TryGetProperty("user-registered", out _));
        }

        [Fact]
        public async Task WriteYaml_ContainsWebhookKey() {
            var output = await WriteToStringAsync(OpenApiTestSchemas.SimpleSchema(), OpenApiFormat.Yaml);
            Assert.Contains("user-registered", output);
        }

        [Fact]
        public async Task WriteMultiple_AllWebhooksPresent() {
            var writer = new EventSchemasOpenApiWriter("Svc", "1.0");
            using var ms = new MemoryStream();
            await writer.WriteToAsync(ms, new[] { OpenApiTestSchemas.SimpleSchema(), OpenApiTestSchemas.NestedSchema() });
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            var output = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(output);

            Assert.True(doc.RootElement.GetProperty("webhooks").TryGetProperty("user-registered", out _));
            Assert.True(doc.RootElement.GetProperty("webhooks").TryGetProperty("order-shipped", out _));
        }
    }
}