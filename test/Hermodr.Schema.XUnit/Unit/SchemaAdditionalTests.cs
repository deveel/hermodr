// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.ComponentModel.DataAnnotations;

namespace Hermodr
{
    /// <summary>
    /// Additional tests that fill coverage gaps in the schema model,
    /// builder guards, and factory edge cases.
    /// </summary>
    [Trait("Package", "Schema")]
    public static class SchemaAdditionalTests
    {
        // ── EventSchemaBuilder null-guard tests ───────────────────────────────

        [Fact]
        public static void EventSchemaBuilder_NullEventType_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => EventSchema.Build(null!));
        }

        [Fact]
        public static void EventSchemaBuilder_NullVersion_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                EventSchema.Build("test.event").WithVersion(null!));
        }

        [Fact]
        public static void EventSchemaBuilder_NullContentType_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                EventSchema.Build("test.event").WithContentType(null!));
        }

        // ── EventSchemaBuilder.AddProperty null guards ────────────────────────

        [Fact]
        public static void EventSchemaBuilder_AddProperty_NullAction_Throws()
        {
            var builder = EventSchema.Build("test.event");
            Assert.Throws<ArgumentNullException>(() =>
                builder.AddProperty("name", (Action<EventPropertyBuilder>)null!));
        }

        [Fact]
        public static void EventSchemaBuilder_AddProperty_NullProperty_Throws()
        {
            var builder = EventSchema.Build("test.event");
            Assert.Throws<ArgumentNullException>(() =>
                builder.AddProperty((EventProperty)null!));
        }

        [Fact]
        public static void EventSchemaBuilder_WithDescription_SetsDescription()
        {
            var schema = EventSchema.Build("described.event")
                .WithDescription("Hello world")
                .Build();

            Assert.Equal("Hello world", schema.Description);
        }

        // ── EventPropertyCollection.Remove ───────────────────────────────────

        [Fact]
        public static void PropertyCollection_Remove_DecreasesCount()
        {
            var schema = new EventSchema("items.removed", "1.0", "application/json");
            schema.Properties.Add("field_a", "string");
            schema.Properties.Add("field_b", "int");

            Assert.Equal(2, schema.Properties.Count);

            var prop = schema.Properties["field_a"]!;
            schema.Properties.Remove(prop);

            Assert.Single(schema.Properties);
            Assert.False(schema.Properties.Contains("field_a"));
        }

        [Fact]
        public static void PropertyCollection_Remove_NonexistentProperty_ReturnsFalse()
        {
            var schema = new EventSchema("remove.test", "1.0", "application/json");
            schema.Properties.Add("existing", "string");

            var orphan = new EventProperty("not-in-schema", "string", "1.0");
            var removed = schema.Properties.Remove(orphan);

            Assert.False(removed);
            Assert.Single(schema.Properties);
        }

        // ── EventPropertyCollection.Contains(string) ─────────────────────────

        [Fact]
        public static void PropertyCollection_Contains_IsCaseSensitive()
        {
            var schema = new EventSchema("case.test", "1.0", "application/json");
            schema.Properties.Add("MyProp", "string");

            Assert.True(schema.Properties.Contains("MyProp"));
            Assert.False(schema.Properties.Contains("myprop"));
        }

        [Fact]
        public static void PropertyCollection_Contains_ReturnsFalseForUnknownName()
        {
            var schema = new EventSchema("contains.test", "1.0", "application/json");
            schema.Properties.Add("known", "string");

            Assert.False(schema.Properties.Contains("unknown"));
        }

        // ── EventPropertyCollection indexer setter ────────────────────────────

        [Fact]
        public static void PropertyCollection_IndexerSetter_UpdatesExistingProperty()
        {
            var schema = new EventSchema("setter.test", "1.0", "application/json");
            schema.Properties.Add("field", "string");

            var updated = new EventProperty("field", "int", "1.0");
            schema.Properties["field"] = updated;

            Assert.Equal("int", schema.Properties["field"]!.DataType);
        }

        [Fact]
        public static void PropertyCollection_IndexerSetter_NonExistentName_ThrowsKeyNotFound()
        {
            var schema = new EventSchema("keyed.test", "1.0", "application/json");
            schema.Properties.Add("existing", "string");

            var prop = new EventProperty("missing", "int", "1.0");
            Assert.Throws<KeyNotFoundException>(() => schema.Properties["missing"] = prop);
        }

        [Fact]
        public static void PropertyCollection_IndexerSetter_NameMismatch_Throws()
        {
            var schema = new EventSchema("name.mismatch.test", "1.0", "application/json");
            schema.Properties.Add("field", "string");

            var wrong = new EventProperty("other", "int", "1.0");
            Assert.Throws<ArgumentException>(() => schema.Properties["field"] = wrong);
        }

        // ── EventPropertyCollection.SetItem duplicate name guard ──────────────

        [Fact]
        public static void PropertyCollection_SetItem_DuplicateName_Throws()
        {
            var schema = new EventSchema("dup.test", "1.0", "application/json");
            schema.Properties.Add("alpha", "string");
            schema.Properties.Add("beta",  "string");

            // Attempt to rename "alpha" (index 0) to "beta" (already in use at index 1)
            var renamed = new EventProperty("beta", "string", "1.0");
            Assert.Throws<ArgumentException>(() => schema.Properties["alpha"] = renamed);
        }

        // ── EventPropertyCollection InsertItem version guard ──────────────────

        [Fact]
        public static void PropertyCollection_InsertItem_VersionTooHigh_Throws()
        {
            var schema = new EventSchema("version.guard.test", "1.0", "application/json");
            var prop = new EventProperty("new_field", "string", "2.0");
            Assert.Throws<ArgumentException>(() => schema.Properties.Add(prop));
        }

        [Fact]
        public static void PropertyCollection_InsertItem_DuplicateName_Throws()
        {
            var schema = new EventSchema("dup.insert.test", "1.0", "application/json");
            schema.Properties.Add("field", "string");

            var dup = new EventProperty("field", "int", "1.0");
            Assert.Throws<ArgumentException>(() => schema.Properties.Add(dup));
        }

        // ── EventSchemaFactory edge cases ─────────────────────────────────────

        [Fact]
        public static void Factory_EventWithUriSchema_WithoutDataVersion_Throws()
        {
            var factory = new EventSchemaFactory();
            Assert.Throws<ArgumentException>(() =>
                factory.CreateFromType<EventWithUriSchema>());
        }

        [Fact]
        public static void Factory_CreateRangeConstraint_IncompatibleMaxType_Throws()
        {
            var factory = new EventSchemaFactory();
            Assert.Throws<ArgumentException>(() =>
                factory.CreateFromType<EventWithIncompatibleRange>());
        }

        [Fact]
        public static void Factory_DateOnlyProperty_MappedCorrectly()
        {
            var schema = new EventSchemaFactory().CreateFromType<DateTimeTypesEvent>();
            Assert.Equal("date", schema.Properties["DateOnlyProp"]!.DataType);
        }

        [Fact]
        public static void Factory_TimeOnlyProperty_MappedCorrectly()
        {
            var schema = new EventSchemaFactory().CreateFromType<DateTimeTypesEvent>();
            Assert.Equal("time", schema.Properties["TimeOnlyProp"]!.DataType);
        }

        [Fact]
        public static void Factory_TimeSpanProperty_MappedCorrectly()
        {
            var schema = new EventSchemaFactory().CreateFromType<DateTimeTypesEvent>();
            Assert.Equal("duration", schema.Properties["TimeSpanProp"]!.DataType);
        }

        [Fact]
        public static void Factory_FieldAnnotatedWithEventProperty_IsDiscovered()
        {
            var factory = new EventSchemaFactory();
            var schema = factory.CreateFromType<EventWithPublicField>();

            Assert.True(schema.Properties.Contains("field_one"),
                "Public field decorated with [EventProperty] should be discovered");
        }

        [Fact]
        public static void Factory_NestedObjectProperty_HasSubProperties()
        {
            var factory = new EventSchemaFactory();
            var schema = factory.CreateFromType<EventWithNestedObject>();

            var addressProp = schema.Properties["address"];
            Assert.NotNull(addressProp);
            Assert.NotEmpty(addressProp!.Properties);
        }

        // ── Test fixtures ─────────────────────────────────────────────────────

        // Uses a URI second arg → DataVersion is null → CreateFromType should throw
        [Event("uri.schema.event", "https://example.com/events/1.0")]
        private class EventWithUriSchema
        {
            public string Field { get; set; } = string.Empty;
        }

        [Event("bad.range.event", "1.0")]
        private class EventWithIncompatibleRange
        {
            // Range with string type on an int property → max value is a string,
            // which is not an instance of int → CreateRangeConstraint should throw.
            [Range(typeof(string), "0", "150")]
            public int Score { get; set; }
        }

        [Event("datetime.types.event", "1.0")]
        private class DateTimeTypesEvent
        {
            public DateOnly DateOnlyProp { get; set; }
            public TimeOnly TimeOnlyProp { get; set; }
            public TimeSpan TimeSpanProp { get; set; }
        }

        [Event("field.event", "1.0")]
        private class EventWithPublicField
        {
            [EventProperty("field_one")]
            public string FieldOne = string.Empty;

            [EventProperty("prop_one")]
            public string PropOne { get; set; } = string.Empty;
        }

        [Event("nested.event", "1.0")]
        private class EventWithNestedObject
        {
            [EventProperty("address")]
            public NestedAddress Address { get; set; } = new();
        }

        private class NestedAddress
        {
            [EventProperty("street")]
            public string Street { get; set; } = string.Empty;

            [EventProperty("city")]
            public string City { get; set; } = string.Empty;
        }
    }
}

