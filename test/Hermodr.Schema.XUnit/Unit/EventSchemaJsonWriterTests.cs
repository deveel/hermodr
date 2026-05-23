using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hermodr {
	public static class EventSchemaJsonWriterTests {
		// ─── existing tests ───────────────────────────────────────────────────

		[Fact]
		public static async Task WriteSimpleSchemaToJson() {
			var schema = new EventSchema("test", "1.0", "binary");
			schema.Properties.Add(new EventProperty("id", "string"));
			schema.Properties.Add(new EventProperty("name", "string"));
			schema.Properties.Add(new EventProperty("age", "int"));

			var writer = new EventSchemaJsonWriter();
			using var stream = new MemoryStream();
			await writer.WriteToAsync(stream, schema);

			stream.Position = 0;
			using var reader = new StreamReader(stream);
			var json = await reader.ReadToEndAsync();

			var obj = JsonSerializer.Deserialize<JsonNode>(json)!;
			Assert.NotNull(obj);
			Assert.Equal("test", obj["type"]!.GetValue<string>());
			Assert.Equal("1.0", obj["version"]!.GetValue<string>());
			Assert.Equal("binary", obj["contentType"]!.GetValue<string>());

			var properties = obj["properties"]!.AsObject();
			Assert.NotNull(properties["id"]);
			Assert.Equal("string", properties["id"]!["dataType"]!.GetValue<string>());
			Assert.NotNull(properties["name"]);
			Assert.Equal("string", properties["name"]!["dataType"]!.GetValue<string>());
			Assert.NotNull(properties["age"]);
			Assert.Equal("int", properties["age"]!["dataType"]!.GetValue<string>());
		}

		[Fact]
		public static async Task WriteSchemaWithConstraintsToJson() {
			var schema = new EventSchema("test", "1.0", "binary");
			schema.Properties.Add(new EventProperty("id", "string"));
			schema.Properties.Add(new EventProperty("name", "string"));
			schema.Properties.Add(new EventProperty("age", "int") {
				Constraints = {
					new PropertyRequiredConstraint(),
					new RangeConstraint<int>(14, 32)
				}
			});

			var writer = new EventSchemaJsonWriter();
			using var stream = new MemoryStream();
			await writer.WriteToAsync(stream, schema);

			stream.Position = 0;
			var json = await new StreamReader(stream).ReadToEndAsync();
			var obj = JsonSerializer.Deserialize<JsonNode>(json)!;
			var properties = obj["properties"]!.AsObject();

			Assert.Equal("int",  properties["age"]!["dataType"]!.GetValue<string>());
			Assert.True(         properties["age"]!["required"]!.GetValue<bool>());
			Assert.Equal(14,     properties["age"]!["min"]!.GetValue<int>());
			Assert.Equal(32,     properties["age"]!["max"]!.GetValue<int>());
		}

		// ─── new tests ────────────────────────────────────────────────────────

		[Fact]
		public static async Task WriteSchema_Description_EmittedWhenSet() {
			var schema = EventSchema.Build("described.event")
				.WithDescription("A test event")
				.Build();

			var json = await WriteToJsonAsync(schema);
			var obj = JsonSerializer.Deserialize<JsonNode>(json)!;
			Assert.Equal("A test event", obj["description"]!.GetValue<string>());
		}

		[Fact]
		public static async Task WriteSchema_NoDescription_KeyOmitted() {
			var schema = new EventSchema("test", "1.0", "application/json");
			var json = await WriteToJsonAsync(schema);
			var obj = JsonSerializer.Deserialize<JsonNode>(json)!;
			Assert.Null(obj["description"]);
		}

		[Fact]
		public static async Task WriteSchema_NullableProperty_EmitsNullableTrue() {
			var schema = EventSchema.Build("test")
				.AddProperty("notes", p => p.OfType("string").Nullable())
				.Build();

			var json = await WriteToJsonAsync(schema);
			var obj = JsonSerializer.Deserialize<JsonNode>(json)!;
			Assert.True(obj["properties"]!["notes"]!["nullable"]!.GetValue<bool>());
		}

		[Fact]
		public static async Task WriteSchema_NonNullableProperty_NullableKeyOmitted() {
			var schema = EventSchema.Build("test")
				.AddProperty("name", p => p.OfType("string"))
				.Build();

			var json = await WriteToJsonAsync(schema);
			var obj = JsonSerializer.Deserialize<JsonNode>(json)!;
			Assert.Null(obj["properties"]!["name"]!["nullable"]);
		}

		[Fact]
		public static async Task WriteSchema_AllowedValuesConstraint_EmittedCorrectly() {
			var schema = EventSchema.Build("test")
				.AddProperty("status", p => p
					.OfType("string")
					.WithAllowedValues(new[] { "active", "inactive" }))
				.Build();

			var json = await WriteToJsonAsync(schema);
			var obj = JsonSerializer.Deserialize<JsonNode>(json)!;
			var allowed = obj["properties"]!["status"]!["allowedValues"]!.AsArray();
			Assert.Equal(2, allowed.Count);
			Assert.Equal("active",   allowed[0]!.GetValue<string>());
			Assert.Equal("inactive", allowed[1]!.GetValue<string>());
		}

		[Fact]
		public static async Task WriteSchema_PropertyVersion_EmittedOnlyWhenSet() {
			var schema = new EventSchema("test", "2.0", "application/json");
			schema.Properties.Add(new EventProperty("old_field", "string", "1.0"));
			schema.Properties.Add(new EventProperty("new_field", "string")); // no explicit version

			var json = await WriteToJsonAsync(schema);
			var obj = JsonSerializer.Deserialize<JsonNode>(json)!;
			var props = obj["properties"]!;

			// explicitly versioned property includes "version"
			Assert.Equal("1.0", props["old_field"]!["version"]!.GetValue<string>());
			// property without explicit version (inherits schema version) still emits version
			Assert.NotNull(props["new_field"]!["version"]);
		}

		[Fact]
		public static async Task WriteSchema_RangeWithMinOnly_OnlyMinEmitted() {
			var schema = EventSchema.Build("test")
				.AddProperty("score", p => p.OfType("int").WithConstraint(new RangeConstraint<int>(0, null)))
				.Build();

			var json = await WriteToJsonAsync(schema);
			var obj = JsonSerializer.Deserialize<JsonNode>(json)!;
			var score = obj["properties"]!["score"]!;
			Assert.NotNull(score["min"]);
			Assert.Null(score["max"]);
		}

		[Fact]
		public static async Task WriteSchema_NestedProperties_EmittedRecursively() {
			var schema = EventSchema.Build("test")
				.AddProperty("address", p => p
					.OfType("object")
					.AddProperty("street", n => n.OfType("string").Required())
					.AddProperty("city",   n => n.OfType("string")))
				.Build();

			var json = await WriteToJsonAsync(schema);
			var obj = JsonSerializer.Deserialize<JsonNode>(json)!;
			var address = obj["properties"]!["address"]!;
			var nested = address["properties"]!.AsObject();
			Assert.NotNull(nested["street"]);
			Assert.True(nested["street"]!["required"]!.GetValue<bool>());
			Assert.NotNull(nested["city"]);
		}

		[Fact]
		public static async Task WriteSchema_IndentedOutput_ValidJson() {
			var schema = new EventSchema("test", "1.0", "application/json");
			schema.Properties.Add(new EventProperty("id", "guid"));

			var writer = new EventSchemaJsonWriter(new JsonWriterOptions { Indented = true });
			using var stream = new MemoryStream();
			await writer.WriteToAsync(stream, schema);
			stream.Position = 0;
			var json = await new StreamReader(stream).ReadToEndAsync();

			// indented JSON contains newlines
			Assert.Contains('\n', json);
			// still valid JSON
			var obj = JsonSerializer.Deserialize<JsonNode>(json);
			Assert.NotNull(obj);
		}

		// ─── helper ───────────────────────────────────────────────────────────

		private static async Task<string> WriteToJsonAsync(IEventSchema schema) {
			var writer = new EventSchemaJsonWriter();
			using var stream = new MemoryStream();
			await writer.WriteToAsync(stream, schema);
			stream.Position = 0;
			return await new StreamReader(stream).ReadToEndAsync();
		}
	}
}
