using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hermodr {
	public static class EventSchemaFactoryTests {

		private static readonly EventSchemaFactory Factory = new EventSchemaFactory();

		// ─── basic creation ───────────────────────────────────────────────────

		[Fact]
		public static void Factory_CreateFromType_Simple() {
			var schema = Factory.CreateFromType<SimpleEvent>();

			Assert.Equal("simple.event", schema.EventType);
			Assert.Equal("1.0", schema.Version.ToString());
			Assert.True(schema.Properties.Contains("name"));
			Assert.True(schema.Properties.Contains("value"));
		}

		[Fact]
		public static void Factory_CreateFromType_NonAnnotated_Throws() {
			Assert.Throws<ArgumentException>(() => Factory.CreateFromType<NonAnnotatedClass>());
		}

		[Fact]
		public static void Factory_CreateFromType_ContentTypeFromAttribute() {
			var schema = Factory.CreateFromType<EventWithContentType>();
			Assert.Equal("application/json", schema.ContentType);
		}

		[Fact]
		public static void Factory_CreateFromType_ContentTypeDefaultsToObject() {
			var schema = Factory.CreateFromType<SimpleEvent>();
			Assert.Equal("object", schema.ContentType);
		}

		[Fact]
		public static void Factory_CreateFromType_Description() {
			var schema = Factory.CreateFromType<EventWithDescription>();
			Assert.Equal("A described event", schema.Description);
		}

		// ─── generic factory method ───────────────────────────────────────────

		[Fact]
		public static void Factory_Generic_EquivalentToNonGeneric() {
			var a = Factory.CreateFromType(typeof(SimpleEvent));
			var b = Factory.CreateFromType<SimpleEvent>();

			Assert.Equal(a.EventType, b.EventType);
			Assert.Equal(a.Version, b.Version);
			Assert.Equal(a.Properties.Count, b.Properties.Count);
		}

		// ─── nullable type detection ──────────────────────────────────────────

		[Fact]
		public static void Factory_NullableValueType_IsNullable() {
			var schema = Factory.CreateFromType<NullableEvent>();

			var score = schema.Properties["Score"]!;
			Assert.True(score.IsNullable);
			Assert.Equal("int", score.DataType); // underlying type, not Nullable<int>
		}

		[Fact]
		public static void Factory_NonNullableValueType_NotNullable() {
			var schema = Factory.CreateFromType<NullableEvent>();

			var count = schema.Properties["Count"]!;
			Assert.False(count.IsNullable);
		}

		// ─── collection types ─────────────────────────────────────────────────

		[Fact]
		public static void Factory_ArrayProperty_DataTypeHasArraySuffix() {
			var schema = Factory.CreateFromType<CollectionEvent>();
			var tags = schema.Properties["Tags"]!;
			Assert.Equal("string[]", tags.DataType);
		}

		[Fact]
		public static void Factory_ListProperty_DataTypeHasArraySuffix() {
			var schema = Factory.CreateFromType<CollectionEvent>();
			var items = schema.Properties["Items"]!;
			Assert.Equal("int[]", items.DataType);
		}

		// ─── known scalar types ───────────────────────────────────────────────

		[Theory]
		[InlineData("GuidProp",     "guid")]
		[InlineData("DateTimeProp", "dateTime")]
		[InlineData("DateOffProp",  "dateTimeOffset")]
		[InlineData("BoolProp",     "boolean")]
		[InlineData("LongProp",     "long")]
		[InlineData("FloatProp",    "float")]
		[InlineData("DoubleProp",   "double")]
		[InlineData("MoneyProp",    "money")]
		public static void Factory_KnownScalarTypes_MappedCorrectly(string memberName, string expectedDataType) {
			var schema = Factory.CreateFromType<ScalarTypesEvent>();
			var prop = schema.Properties[memberName]!;
			Assert.Equal(expectedDataType, prop.DataType);
		}

		// ─── constraints from DataAnnotations ────────────────────────────────

		[Fact]
		public static void Factory_RequiredAttribute_AddsRequiredConstraint() {
			var schema = Factory.CreateFromType<AnnotatedEvent>();
			var name = schema.Properties["first_name"]!;
			Assert.True(name.IsRequired);
		}

		[Fact]
		public static void Factory_RangeAttribute_AddsRangeConstraint() {
			var schema = Factory.CreateFromType<AnnotatedEvent>();
			var age = schema.Properties["age"]!;
			Assert.Single(age.Constraints);
			var range = Assert.IsType<RangeConstraint<int>>(age.Constraints[0]);
			Assert.Equal(0, range.Min);
			Assert.Equal(150, range.Max);
		}

		[Fact]
		public static void Factory_EnumProperty_AddsAllowedValuesConstraint() {
			var schema = Factory.CreateFromType<AnnotatedEvent>();
			var status = schema.Properties["status"]!;
			Assert.Single(status.Constraints);
			Assert.Equal("allowedValues", status.Constraints[0].ConstraintType);
			var enumConstraint = Assert.IsAssignableFrom<IEnumMemberConstraint>(status.Constraints[0]);
			Assert.Contains("Active", enumConstraint.AllowedValueObjects.Select(v => v?.ToString()));
		}

		// ─── IEventSchemaFactory.Default ─────────────────────────────────────

		[Fact]
		public static void Factory_Default_NotNull() {
			Assert.NotNull(EventSchemaFactory.Default);
		}

		[Fact]
		public static void Factory_Default_ProducesCorrectSchema() {
			var schema = EventSchemaFactory.Default.CreateFromType<SimpleEvent>();
			Assert.Equal("simple.event", schema.EventType);
		}

		// ─── EventSchema.FromDataType convenience ─────────────────────────────

		[Fact]
		public static void EventSchema_FromDataType_EquivalentToFactory() {
			var fromStatic  = EventSchema.FromDataType<SimpleEvent>();
			var fromFactory = Factory.CreateFromType<SimpleEvent>();

			Assert.Equal(fromStatic.EventType, fromFactory.EventType);
			Assert.Equal(fromStatic.Version,   fromFactory.Version);
		}

		// ─── Test fixtures ────────────────────────────────────────────────────

		[Event("simple.event", "1.0")]
		private class SimpleEvent {
			[EventProperty("name")]
			public string Name { get; set; } = "";

			[EventProperty("value")]
			public int Value { get; set; }
		}

		[Event("content.type.event", "1.0", ContentType = "application/json")]
		private class EventWithContentType {
			public string Data { get; set; } = "";
		}

		[Event("described.event", "1.0", Description = "A described event")]
		private class EventWithDescription {
			public string Field { get; set; } = "";
		}

		[Event("nullable.event", "1.0")]
		private class NullableEvent {
			public int? Score { get; set; }  // Nullable<int> — IsNullable = true
			public int  Count { get; set; }  // int          — IsNullable = false
		}

		[Event("collection.event", "1.0")]
		private class CollectionEvent {
			public string[]   Tags  { get; set; } = Array.Empty<string>();
			public List<int>  Items { get; set; } = new();
		}

		[Event("scalar.event", "1.0")]
		private class ScalarTypesEvent {
			public Guid           GuidProp     { get; set; }
			public DateTime       DateTimeProp { get; set; }
			public DateTimeOffset DateOffProp  { get; set; }
			public bool           BoolProp     { get; set; }
			public long           LongProp     { get; set; }
			public float          FloatProp    { get; set; }
			public double         DoubleProp   { get; set; }
			public decimal        MoneyProp    { get; set; }
		}

		[Event("annotated.event", "1.0")]
		private class AnnotatedEvent {
			[EventProperty("first_name")]
			[Required]
			public string FirstName { get; set; } = "";

			[EventProperty("age")]
			[Range(0, 150)]
			public int Age { get; set; }

			[EventProperty("status")]
			public Status Status { get; set; }
		}

		private enum Status { Active, Inactive, Pending }

		private class NonAnnotatedClass {
			public string Foo { get; set; } = "";
		}
	}
}


