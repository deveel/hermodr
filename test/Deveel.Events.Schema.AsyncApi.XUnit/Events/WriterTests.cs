//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Newtonsoft.Json.Linq;

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
            var ex = Record.Exception(() => JObject.Parse(output));
            Assert.Null(ex);
        }

        [Fact]
        public async Task WriteJson_InfoBlockPresent() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            var doc = JObject.Parse(output);

            Assert.Equal("user.registered", doc["info"]!["title"]!.Value<string>());
            Assert.Equal("1.0",             doc["info"]!["version"]!.Value<string>());
        }

        [Fact]
        public async Task WriteJson_CustomTitleAndVersion() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema(), title: "My API", version: "2.0");
            var doc = JObject.Parse(output);

            Assert.Equal("My API", doc["info"]!["title"]!.Value<string>());
            Assert.Equal("2.0",    doc["info"]!["version"]!.Value<string>());
        }

        [Fact]
        public async Task WriteJson_ChannelPresent() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            var doc = JObject.Parse(output);

            Assert.NotNull(doc["channels"]!["user-registered"]);
        }

        [Fact]
        public async Task WriteJson_ComponentSchemaPresent() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            var doc = JObject.Parse(output);

            Assert.NotNull(doc["components"]!["schemas"]!["user-registered"]);
        }

        [Fact]
        public async Task WriteJson_ComponentMessagePresent() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            var doc = JObject.Parse(output);

            Assert.NotNull(doc["components"]!["messages"]!["user-registered"]);
        }

        [Fact]
        public async Task WriteJson_PropertiesPresent() {
            var output = await WriteToStringAsync(TestSchemas.SimpleSchema());
            var doc = JObject.Parse(output);

            var props = doc["components"]!["schemas"]!["user-registered"]!["properties"];
            Assert.NotNull(props);
            Assert.NotNull(props!["user_id"]);
            Assert.NotNull(props["email"]);
        }

        [Fact]
        public async Task WriteJson_NestedSchema_AddressPropertiesPresent() {
            var output = await WriteToStringAsync(TestSchemas.NestedSchema());
            var doc = JObject.Parse(output);

            var street = doc["components"]!["schemas"]!["order-shipped"]!["properties"]!["address"]!["properties"]!["street"];
            Assert.NotNull(street);
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
            var doc = JObject.Parse(output);

            var desc = doc["components"]!["schemas"]!["thing-happened"]!["description"]?.Value<string>();
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
            var doc = JObject.Parse(output);

            Assert.NotNull(doc["channels"]!["user-registered"]);
            Assert.NotNull(doc["channels"]!["order-shipped"]);
        }

        [Fact]
        public async Task WriteJson_MultipleSchemas_AllComponentSchemasPresent() {
            var schemas = new[] { TestSchemas.SimpleSchema(), TestSchemas.EnumConstraintSchema() };
            var output = await WriteSchemasToStringAsync(schemas);
            var doc = JObject.Parse(output);

            Assert.NotNull(doc["components"]!["schemas"]!["user-registered"]);
            Assert.NotNull(doc["components"]!["schemas"]!["order-status-changed"]);
        }

        [Fact]
        public async Task WriteJson_InfoBlockMatchesTitleAndVersion() {
            var schemas = new[] { TestSchemas.SimpleSchema() };
            var output = await WriteSchemasToStringAsync(schemas, title: "Events Platform", version: "4.0");
            var doc = JObject.Parse(output);

            Assert.Equal("Events Platform", doc["info"]!["title"]!.Value<string>());
            Assert.Equal("4.0",             doc["info"]!["version"]!.Value<string>());
        }

        [Fact]
        public async Task WriteJson_EmptySchemaList_ValidDocumentWithNoChannels() {
            var output = await WriteSchemasToStringAsync(Array.Empty<IEventSchema>());
            var doc = JObject.Parse(output);

            Assert.NotNull(doc["info"]);
        }

        [Fact]
        public async Task WriteJson_StreamLeftOpen() {
            var writer = new EventSchemasAsyncApiWriter("API", "1.0");
            using var ms = new MemoryStream();
            await writer.WriteToAsync(ms, new[] { TestSchemas.SimpleSchema() });

            Assert.True(ms.CanWrite);
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
