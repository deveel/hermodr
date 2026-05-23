using YamlDotNet.RepresentationModel;

namespace Hermodr {
    public static class EventSchemaYamlWriterTests {
        // ─── helpers ──────────────────────────────────────────────────────────

        private static async Task<YamlMappingNode> WriteToYamlAsync(IEventSchema schema) {
            var writer = new EventSchemaYamlWriter();
            using var stream = new MemoryStream();
            await writer.WriteToAsync(stream, schema);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var yaml = new YamlStream();
            yaml.Load(reader);
            return (YamlMappingNode)yaml.Documents[0].RootNode;
        }

        private static string Scalar(YamlNode node, string key)
            => ((YamlScalarNode)((YamlMappingNode)node)[key]).Value!;

        // ─── tests ────────────────────────────────────────────────────────────

        [Fact]
        public static async Task WriteSimpleSchemaToYaml() {
            var schema = new EventSchema("test", "1.0", "binary");
            schema.Properties.Add(new EventProperty("id", "string"));
            schema.Properties.Add(new EventProperty("name", "string"));
            schema.Properties.Add(new EventProperty("age", "int"));

            var root = await WriteToYamlAsync(schema);

            Assert.Equal("test",   Scalar(root, "type"));
            Assert.Equal("1.0",    Scalar(root, "version"));
            Assert.Equal("binary", Scalar(root, "contentType"));

            var props = (YamlMappingNode)root["properties"];
            Assert.Equal("string", Scalar(props["id"],   "dataType"));
            Assert.Equal("string", Scalar(props["name"], "dataType"));
            Assert.Equal("int",    Scalar(props["age"],  "dataType"));
        }

        [Fact]
        public static async Task WriteSchema_Description_EmittedWhenSet() {
            var schema = EventSchema.Build("described.event")
                .WithDescription("A test event")
                .Build();

            var root = await WriteToYamlAsync(schema);
            Assert.Equal("A test event", Scalar(root, "description"));
        }

        [Fact]
        public static async Task WriteSchema_NoDescription_KeyOmitted() {
            var schema = new EventSchema("test", "1.0", "application/json");
            var root = await WriteToYamlAsync(schema);
            Assert.False(((YamlMappingNode)root).Children.ContainsKey("description"));
        }

        [Fact]
        public static async Task WriteSchema_NullableProperty_EmitsNullableTrue() {
            var schema = EventSchema.Build("test")
                .AddProperty("notes", p => p.OfType("string").Nullable())
                .Build();

            var root = await WriteToYamlAsync(schema);
            var props = (YamlMappingNode)root["properties"];
            Assert.Equal("true", Scalar(props["notes"], "nullable"));
        }

        [Fact]
        public static async Task WriteSchema_RequiredAndRangeConstraints() {
            var schema = new EventSchema("test", "1.0", "binary");
            schema.Properties.Add(new EventProperty("age", "int") {
                Constraints = {
                    new PropertyRequiredConstraint(),
                    new RangeConstraint<int>(14, 32)
                }
            });

            var root = await WriteToYamlAsync(schema);
            var props = (YamlMappingNode)root["properties"];
            var age = (YamlMappingNode)props["age"];

            Assert.Equal("true", Scalar(age, "required"));
            Assert.Equal("14",   Scalar(age, "min"));
            Assert.Equal("32",   Scalar(age, "max"));
        }

        [Fact]
        public static async Task WriteSchema_AllowedValuesConstraint_EmittedCorrectly() {
            var schema = EventSchema.Build("test")
                .AddProperty("status", p => p
                    .OfType("string")
                    .WithAllowedValues(new[] { "active", "inactive" }))
                .Build();

            var root = await WriteToYamlAsync(schema);
            var props = (YamlMappingNode)root["properties"];
            var allowed = (YamlSequenceNode)props["status"]["allowedValues"];
            var values = allowed.Children.Cast<YamlScalarNode>().Select(n => n.Value).ToList();

            Assert.Equal(2, values.Count);
            Assert.Contains("active",   values);
            Assert.Contains("inactive", values);
        }

        [Fact]
        public static async Task WriteSchema_NestedProperties_EmittedRecursively() {
            var schema = EventSchema.Build("test")
                .AddProperty("address", p => p
                    .OfType("object")
                    .AddProperty("street", n => n.OfType("string").Required())
                    .AddProperty("city",   n => n.OfType("string")))
                .Build();

            var root = await WriteToYamlAsync(schema);
            var props = (YamlMappingNode)root["properties"];
            var nested = (YamlMappingNode)props["address"]["properties"];

            Assert.True(nested.Children.ContainsKey(new YamlScalarNode("street")));
            Assert.Equal("true", Scalar(nested["street"], "required"));
            Assert.True(nested.Children.ContainsKey(new YamlScalarNode("city")));
        }

        [Fact]
        public static async Task WriteSchema_CustomSerializer_UsedForOutput() {
            var schema = new EventSchema("test", "1.0", "application/json");
            schema.Properties.Add(new EventProperty("id", "guid"));

            var customSerializer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();

            var writer = new EventSchemaYamlWriter(customSerializer);
            using var stream = new MemoryStream();
            await writer.WriteToAsync(stream, schema);
            stream.Position = 0;
            var yaml = await new StreamReader(stream).ReadToEndAsync();

            Assert.NotEmpty(yaml);
            Assert.Contains("type:", yaml);
        }
    }
}

