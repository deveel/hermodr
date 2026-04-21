//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

namespace Deveel.Events {
    /// <summary>
    /// Tests for <see cref="EventSchemaAsyncApiWriter"/>.
    /// </summary>
    public class EventSchemaAsyncApiWriterTests {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static async Task<string> WriteToStringAsync(
            IEventSchema schema,
            AsyncApiFormat format = AsyncApiFormat.Json,
            string? title = null,
            string? version = null) {
            var writer = new EventSchemaAsyncApiWriter(format, title, version);
            using var ms = new MemoryStream();
            await writer.WriteToAsync(ms, schema);
            ms.Position = 0;
            return new StreamReader(ms).ReadToEnd();
        }

        // ── JSON output ───────────────────────────────────────────────────────

        [Fact]
        public async Task WriteJson_IsValidJson() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            var ex = Record.Exception(() => JsonDocument.Parse(output));
            Assert.Null(ex);
        }

        [Fact]
        public async Task WriteJson_InfoBlockPresent() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            using var doc = JsonDocument.Parse(output);

            Assert.Equal("user.registered", doc.RootElement.GetProperty("info").GetProperty("title").GetString());
            Assert.Equal("1.0",             doc.RootElement.GetProperty("info").GetProperty("version").GetString());
        }

        [Fact]
        public async Task WriteJson_CustomTitleAndVersion() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema(), title: "My API", version: "2.0");
            using var doc = JsonDocument.Parse(output);

            Assert.Equal("My API", doc.RootElement.GetProperty("info").GetProperty("title").GetString());
            Assert.Equal("2.0",    doc.RootElement.GetProperty("info").GetProperty("version").GetString());
        }

        [Fact]
        public async Task WriteJson_ChannelPresent() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            using var doc = JsonDocument.Parse(output);

            Assert.True(doc.RootElement.GetProperty("channels").TryGetProperty("user-registered", out _));
        }

        [Fact]
        public async Task WriteJson_ComponentSchemaPresent() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            using var doc = JsonDocument.Parse(output);

            Assert.True(doc.RootElement.GetProperty("components").GetProperty("schemas").TryGetProperty("user-registered", out _));
        }

        [Fact]
        public async Task WriteJson_ComponentMessagePresent() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            using var doc = JsonDocument.Parse(output);

            Assert.True(doc.RootElement.GetProperty("components").GetProperty("messages").TryGetProperty("user-registered", out _));
        }

        [Fact]
        public async Task WriteJson_PropertiesPresent() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            using var doc = JsonDocument.Parse(output);

            var props = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("user-registered").GetProperty("properties");
            Assert.True(props.TryGetProperty("user_id", out _));
            Assert.True(props.TryGetProperty("email",   out _));
        }

        [Fact]
        public async Task WriteJson_NestedSchema_AddressPropertiesPresent() {
            var output = await WriteToStringAsync(TestSchemas.NestedSchema());
            using var doc = JsonDocument.Parse(output);

            var street = doc.RootElement
                .GetProperty("components").GetProperty("schemas")
                .GetProperty("order-shipped").GetProperty("properties")
                .GetProperty("address").GetProperty("properties")
                .TryGetProperty("street", out _);
            Assert.True(street);
        }

        [Fact]
        public async Task WriteJson_StreamLeftOpen() {
            var writer = new EventSchemaAsyncApiWriter();
            using var ms = new MemoryStream();
            await writer.WriteToAsync(ms, TestSchemas.SimpleSchema());

            Assert.True(ms.CanWrite);
        }

        [Fact]
        public async Task WriteJson_WithDescription_DescriptionInSchema() {
            var output = await WriteToStringAsync(TestSchemas.WithDescriptionSchema());
            using var doc = JsonDocument.Parse(output);

            var desc = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("thing-happened").GetProperty("description").GetString();
            Assert.Equal("Something happened", desc);
        }

        // ── YAML output ───────────────────────────────────────────────────────

        [Fact]
        public async Task WriteYaml_ContainsInfoTitle() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema(), AsyncApiFormat.Yaml);

            Assert.Contains("title:", output);
            Assert.Contains("user.registered", output);
        }

        [Fact]
        public async Task WriteYaml_ContainsChannelKey() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema(), AsyncApiFormat.Yaml);

            Assert.Contains("user-registered", output);
        }

        [Fact]
        public async Task WriteYaml_ContainsComponentsSection() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema(), AsyncApiFormat.Yaml);

            Assert.Contains("components:", output);
        }

        // ── Default constructor ───────────────────────────────────────────────

        [Fact]
        public void DefaultConstructor_FormatIsJson() {
            var writer = new EventSchemaAsyncApiWriter();
            Assert.Equal(AsyncApiFormat.Json, writer.Format);
        }

        [Fact]
        public void Constructor_YamlFormat_Preserved() {
            var writer = new EventSchemaAsyncApiWriter(AsyncApiFormat.Yaml);
            Assert.Equal(AsyncApiFormat.Yaml, writer.Format);
        }

        [Fact]
        public void Constructor_TitleAndVersion_Preserved() {
            var writer = new EventSchemaAsyncApiWriter(title: "T", documentVersion: "9.9");
            Assert.Equal("T",   writer.Title);
            Assert.Equal("9.9", writer.DocumentVersion);
        }
    }

    /// <summary>
    /// Tests for <see cref="EventSchemasAsyncApiWriter"/>.
    /// </summary>
    public class EventSchemasAsyncApiWriterTests {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static async Task<string> WriteSchemasToStringAsync(
            IEnumerable<IEventSchema> schemas,
            AsyncApiFormat format = AsyncApiFormat.Json,
            string title = "Test API",
            string version = "1.0") {
            var writer = new EventSchemasAsyncApiWriter(title, version, format);
            using var ms = new MemoryStream();
            await writer.WriteToAsync(ms, schemas);
            ms.Position = 0;
            return new StreamReader(ms).ReadToEnd();
        }

        // ── Construction ──────────────────────────────────────────────────────

        [Fact]
        public void Constructor_NullTitle_Throws() {
            var ex = Assert.Throws<ArgumentNullException>(() => new EventSchemasAsyncApiWriter(null!, "1.0"));
            Assert.Equal("title", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullVersion_Throws() {
            var ex = Assert.Throws<ArgumentNullException>(() => new EventSchemasAsyncApiWriter("T", null!));
            Assert.Equal("version", ex.ParamName);
        }

        [Fact]
        public void Constructor_PropertiesPreserved() {
            var w = new EventSchemasAsyncApiWriter("My API", "2.5", AsyncApiFormat.Yaml);
            Assert.Equal("My API",          w.Title);
            Assert.Equal("2.5",             w.Version);
            Assert.Equal(AsyncApiFormat.Yaml, w.Format);
        }

        // ── JSON output ───────────────────────────────────────────────────────

        [Fact]
        public async Task WriteJson_MultipleSchemas_AllChannelsPresent() {
            var schemas = new[] { TestSchemas.SimpleSchema(), TestSchemas.NestedSchema() };
            var output = await WriteSchemasToStringAsync(schemas);
            using var doc = JsonDocument.Parse(output);
            var channels = doc.RootElement.GetProperty("channels");

            Assert.True(channels.TryGetProperty("user-registered", out _));
            Assert.True(channels.TryGetProperty("order-shipped",   out _));
        }

        [Fact]
        public async Task WriteJson_MultipleSchemas_AllComponentSchemasPresent() {
            var schemas = new[] { TestSchemas.SimpleSchema(), TestSchemas.EnumConstraintSchema() };
            var output = await WriteSchemasToStringAsync(schemas);
            using var doc = JsonDocument.Parse(output);
            var schemas2 = doc.RootElement.GetProperty("components").GetProperty("schemas");

            Assert.True(schemas2.TryGetProperty("user-registered",      out _));
            Assert.True(schemas2.TryGetProperty("order-status-changed", out _));
        }

        [Fact]
        public async Task WriteJson_InfoBlockMatchesTitleAndVersion() {
            var schemas = new[] { TestSchemas.SimpleSchema() };
            var output = await WriteSchemasToStringAsync(schemas, title: "Events Platform", version: "4.0");
            using var doc = JsonDocument.Parse(output);

            Assert.Equal("Events Platform", doc.RootElement.GetProperty("info").GetProperty("title").GetString());
            Assert.Equal("4.0",             doc.RootElement.GetProperty("info").GetProperty("version").GetString());
        }

        [Fact]
        public async Task WriteJson_EmptySchemaList_ValidDocumentWithNoChannels() {
            var output = await WriteSchemasToStringAsync(Array.Empty<IEventSchema>());
            using var doc = JsonDocument.Parse(output);

            Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("info").ValueKind);
        }

        // ── YAML output ───────────────────────────────────────────────────────

        [Fact]
        public async Task WriteYaml_MultipleSchemas_ContainsAllKeys() {
            var schemas = new[] { TestSchemas.SimpleSchema(), TestSchemas.NestedSchema() };
            var output = await WriteSchemasToStringAsync(schemas, AsyncApiFormat.Yaml);

            Assert.Contains("user-registered", output);
            Assert.Contains("order-shipped",   output);
        }

        [Fact]
        public async Task WriteYaml_ContainsInfoSection() {
            var schemas = new[] { TestSchemas.SimpleSchema() };
            var output = await WriteSchemasToStringAsync(schemas, AsyncApiFormat.Yaml, "Svc", "9.0");

            Assert.Contains("title:", output);
            Assert.Contains("Svc",    output);
        }
    }
}
