//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//


using NJsonSchema;

using Saunter.AsyncApiSchema.v2;

namespace Hermodr {
    /// <summary>
    /// Tests for data-type to JSON Schema mapping, exercised via
    /// <see cref="EventSchemaAsyncApiExtensions.ToJsonSchemaProperty"/>.
    /// </summary>
    public class DataTypeMappingTests {
        private static JsonSchemaProperty PropOf(string dataType) {
            var prop = new EventProperty("p", dataType);
            return prop.ToJsonSchemaProperty();
        }

        [Theory]
        [InlineData("string",         JsonObjectType.String,  null)]
        [InlineData("int",            JsonObjectType.Integer, "int32")]
        [InlineData("long",           JsonObjectType.Integer, "int64")]
        [InlineData("float",          JsonObjectType.Number,  "float")]
        [InlineData("double",         JsonObjectType.Number,  "double")]
        [InlineData("money",          JsonObjectType.Number,  "decimal")]
        [InlineData("boolean",        JsonObjectType.Boolean, null)]
        [InlineData("dateTime",       JsonObjectType.String,  "date-time")]
        [InlineData("dateTimeOffset", JsonObjectType.String,  "date-time")]
        [InlineData("date",           JsonObjectType.String,  "date")]
        [InlineData("time",           JsonObjectType.String,  "time")]
        [InlineData("duration",       JsonObjectType.String,  "duration")]
        [InlineData("guid",           JsonObjectType.String,  "uuid")]
        [InlineData("SomeCustomType", JsonObjectType.Object,  null)]
        public void KnownTypes_MappedCorrectly(string dataType, JsonObjectType expectedType, string? expectedFormat) {
            var prop = PropOf(dataType);

            Assert.Equal(expectedType, prop.Type);
            Assert.Equal(expectedFormat, prop.Format);
        }

        [Theory]
        [InlineData("string[]")]
        [InlineData("int[]")]
        [InlineData("guid[]")]
        public void ArraySuffix_MapsToArray(string dataType) {
            var prop = PropOf(dataType);
            Assert.Equal(JsonObjectType.Array, prop.Type);
        }
    }

    /// <summary>
    /// Tests for <see cref="EventSchemaAsyncApiExtensions.ToJsonSchema"/>.
    /// </summary>
    public class ToJsonSchemaTests {
        [Fact]
        public void SimpleSchema_TitleAndTypeSet() {
            var schema = TestSchemas.SimpleSchema();
            var json = schema.ToJsonSchema();

            Assert.Equal(JsonObjectType.Object, json.Type);
            Assert.Equal("user.registered", json.Title);
            Assert.Equal("User registration event", json.Description);
        }

        [Fact]
        public void SimpleSchema_PropertiesMapped() {
            var schema = TestSchemas.SimpleSchema();
            var json = schema.ToJsonSchema();

            Assert.True(json.Properties.ContainsKey("user_id"));
            Assert.True(json.Properties.ContainsKey("email"));
            Assert.True(json.Properties.ContainsKey("age"));
            Assert.True(json.Properties.ContainsKey("nickname"));
        }

        [Fact]
        public void RequiredProperties_AddedToRequiredSet() {
            var schema = TestSchemas.SimpleSchema();
            var json = schema.ToJsonSchema();

            Assert.Contains("user_id", json.RequiredProperties);
            Assert.Contains("email", json.RequiredProperties);
            Assert.DoesNotContain("nickname", json.RequiredProperties);
        }

        [Fact]
        public void NullableProperty_HasNullableTrue() {
            var schema = TestSchemas.SimpleSchema();
            var json = schema.ToJsonSchema();

            Assert.True(json.Properties["nickname"].IsNullableRaw);
        }

        [Fact]
        public void SchemaWithNoDescription_DescriptionNull() {
            var schema = EventSchema.Build("no.desc")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .Build();
            var json = schema.ToJsonSchema();

            Assert.Null(json.Description);
        }

        [Fact]
        public void AllDataTypes_PropertiesPresentWithCorrectTypes() {
            var schema = TestSchemas.AllDataTypesSchema();
            var json = schema.ToJsonSchema();

            Assert.Equal(JsonObjectType.String,  json.Properties["f_string"].Type);
            Assert.Equal(JsonObjectType.Integer, json.Properties["f_int"].Type);
            Assert.Equal(JsonObjectType.Integer, json.Properties["f_long"].Type);
            Assert.Equal(JsonObjectType.Number,  json.Properties["f_float"].Type);
            Assert.Equal(JsonObjectType.Number,  json.Properties["f_double"].Type);
            Assert.Equal(JsonObjectType.Number,  json.Properties["f_money"].Type);
            Assert.Equal(JsonObjectType.Boolean, json.Properties["f_boolean"].Type);
            Assert.Equal(JsonObjectType.String,  json.Properties["f_dateTime"].Type);
            Assert.Equal(JsonObjectType.Array,   json.Properties["f_array"].Type);
            Assert.Equal(JsonObjectType.Object,  json.Properties["f_unknown"].Type);
        }

        [Fact]
        public void ArrayProperty_HasItemsSchema() {
            var schema = TestSchemas.AllDataTypesSchema();
            var json = schema.ToJsonSchema();

            var arrayProp = json.Properties["f_array"];
            Assert.Equal(JsonObjectType.Array, arrayProp.Type);
            Assert.NotNull(arrayProp.Item);
            Assert.Equal(JsonObjectType.String, arrayProp.Item!.Type);
        }

        [Fact]
        public void NestedSchema_ObjectPropertyContainsSubProperties() {
            var schema = TestSchemas.NestedSchema();
            var json = schema.ToJsonSchema();

            var addrProp = json.Properties["address"];
            Assert.Equal(JsonObjectType.Object, addrProp.Type);
            Assert.True(addrProp.Properties.ContainsKey("street"));
            Assert.True(addrProp.Properties.ContainsKey("city"));
            Assert.True(addrProp.Properties.ContainsKey("zip"));
        }

        [Fact]
        public void NestedSchema_RequiredSubPropertiesRecorded() {
            var schema = TestSchemas.NestedSchema();
            var json = schema.ToJsonSchema();

            var addrProp = json.Properties["address"];
            Assert.Contains("street", addrProp.RequiredProperties);
            Assert.Contains("city", addrProp.RequiredProperties);
            Assert.DoesNotContain("zip", addrProp.RequiredProperties);
        }
    }

    /// <summary>
    /// Tests for <see cref="EventSchemaAsyncApiExtensions.ToJsonSchemaProperty"/>
    /// with constraint handling.
    /// </summary>
    public class PropertyConstraintTests {
        [Fact]
        public void EnumConstraint_PopulatesEnumeration() {
            var schema = TestSchemas.EnumConstraintSchema();
            var json = schema.ToJsonSchema();

            var statusProp = json.Properties["status"];
            Assert.Contains("Pending",   statusProp.Enumeration);
            Assert.Contains("Confirmed", statusProp.Enumeration);
            Assert.Contains("Cancelled", statusProp.Enumeration);
        }

        [Fact]
        public void RangeConstraint_MinMaxSet() {
            var schema = TestSchemas.SimpleSchema();
            var json = schema.ToJsonSchema();

            var ageProp = json.Properties["age"];
            Assert.Equal(18,  (int?)ageProp.Minimum);
            Assert.Equal(120, (int?)ageProp.Maximum);
        }

        [Fact]
        public void RangeConstraint_OnlyMin_MaxNotSet() {
            var schema = EventSchema.Build("test")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .AddProperty("score", p => p.OfType("int").WithRange<int>(0, null))
                .Build();
            var json = schema.ToJsonSchema();

            Assert.Equal(0, (int?)json.Properties["score"].Minimum);
            Assert.Null(json.Properties["score"].Maximum);
        }

        [Fact]
        public void RangeConstraint_OnlyMax_MinNotSet() {
            var schema = EventSchema.Build("test")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .AddProperty("price", p => p.OfType("double").WithRange<double>(null, 9999.99))
                .Build();
            var json = schema.ToJsonSchema();

            Assert.Null(json.Properties["price"].Minimum);
            Assert.Equal((decimal)9999.99, json.Properties["price"].Maximum);
        }

        [Fact]
        public void RequiredConstraint_IsHandledAtParentLevel() {
            var schema = EventSchema.Build("test")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .AddProperty("id", p => p.OfType("guid").Required())
                .Build();
            var json = schema.ToJsonSchema();

            Assert.True(json.Properties["id"].IsRequired);
            Assert.Contains("id", json.RequiredProperties);
        }

        [Fact]
        public void UnknownConstraint_IgnoredGracefully() {
            var prop = new EventProperty("val", "int");
            prop.Constraints.Add(new UnknownConstraintStub());

            var ex = Record.Exception(() => prop.ToJsonSchemaProperty());
            Assert.Null(ex);
        }

        private sealed class UnknownConstraintStub : IEventPropertyConstraint {
            public string ConstraintType => "unknownXYZ";
            public bool IsValid(object? value) => true;
        }
    }

    /// <summary>
    /// Tests for <see cref="EventSchemaAsyncApiExtensions.ToAsyncApiMessage"/>.
    /// </summary>
    public class ToAsyncApiMessageTests {
        [Fact]
        public void Message_HasCorrectName() {
            var schema = TestSchemas.SimpleSchema();
            var msg = schema.ToAsyncApiMessage();

            Assert.Equal("user.registered", msg.Name);
            Assert.Equal("user.registered", msg.Title);
        }

        [Fact]
        public void Message_ContentTypePreserved() {
            var schema = TestSchemas.SimpleSchema();
            var msg = schema.ToAsyncApiMessage();

            Assert.Equal("application/json", msg.ContentType);
        }

        [Fact]
        public void Message_SummaryIsDescription() {
            var schema = TestSchemas.SimpleSchema();
            var msg = schema.ToAsyncApiMessage();

            Assert.Equal("User registration event", msg.Summary);
        }

        [Fact]
        public void Message_PayloadIsJsonSchema() {
            var schema = TestSchemas.SimpleSchema();
            var msg = schema.ToAsyncApiMessage();

            Assert.NotNull(msg.Payload);
            Assert.Equal(JsonObjectType.Object, msg.Payload!.Type);
        }

        [Fact]
        public void Message_NoDescription_SummaryNull() {
            var schema = EventSchema.Build("no.desc")
                .WithVersion("1.0")
                .WithContentType("text/plain")
                .Build();
            var msg = schema.ToAsyncApiMessage();

            Assert.Null(msg.Summary);
        }
    }

    /// <summary>
    /// Tests for <see cref="EventSchemaAsyncApiExtensions.ToAsyncApiDocument"/>
    /// and <see cref="EventSchemaAsyncApiExtensions.AddSchema"/>.
    /// </summary>
    public class ToAsyncApiDocumentTests {
        [Fact]
        public void Document_InfoTitleDefaultsToEventType() {
            var schema = TestSchemas.SimpleSchema();
            var doc = schema.ToAsyncApiDocument();

            Assert.Equal("user.registered", doc.Info.Title);
        }

        [Fact]
        public void Document_InfoVersionDefaultsToSchemaVersion() {
            var schema = TestSchemas.SimpleSchema();
            var doc = schema.ToAsyncApiDocument();

            Assert.Equal("1.0", doc.Info.Version);
        }

        [Fact]
        public void Document_CustomTitleAndVersionOverride() {
            var schema = TestSchemas.SimpleSchema();
            var doc = schema.ToAsyncApiDocument(title: "My Service", version: "3.0");

            Assert.Equal("My Service", doc.Info.Title);
            Assert.Equal("3.0", doc.Info.Version);
        }

        [Fact]
        public void Document_ChannelRegistered() {
            var schema = TestSchemas.SimpleSchema();
            var doc = schema.ToAsyncApiDocument();

            Assert.True(doc.Channels.ContainsKey("user-registered"));
        }

        [Fact]
        public void Document_ComponentSchemaRegistered() {
            var schema = TestSchemas.SimpleSchema();
            var doc = schema.ToAsyncApiDocument();

            Assert.True(doc.Components.Schemas.ContainsKey("user-registered"));
        }

        [Fact]
        public void Document_ComponentMessageRegistered() {
            var schema = TestSchemas.SimpleSchema();
            var doc = schema.ToAsyncApiDocument();

            Assert.True(doc.Components.Messages.ContainsKey("user-registered"));
        }

        [Fact]
        public void Document_ChannelHasSubscribeOperation() {
            var schema = TestSchemas.SimpleSchema();
            var doc = schema.ToAsyncApiDocument();

            Assert.NotNull(doc.Channels["user-registered"].Subscribe);
        }

        [Fact]
        public void AddSchema_AppendToExistingDocument() {
            var doc = new AsyncApiDocument {
                Info = new Info("Multi-schema", "1.0")
            };

            var s1 = TestSchemas.SimpleSchema();
            var s2 = TestSchemas.NestedSchema();

            doc.AddSchema(s1);
            doc.AddSchema(s2);

            Assert.True(doc.Components.Schemas.ContainsKey("user-registered"));
            Assert.True(doc.Components.Schemas.ContainsKey("order-shipped"));
            Assert.True(doc.Channels.ContainsKey("user-registered"));
            Assert.True(doc.Channels.ContainsKey("order-shipped"));
        }

        [Fact]
        public void AddSchema_DotInEventType_SanitizedToHyphen() {
            var schema = EventSchema.Build("my.service.event")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .Build();
            var doc = schema.ToAsyncApiDocument();

            Assert.True(doc.Channels.ContainsKey("my-service-event"));
            Assert.True(doc.Components.Schemas.ContainsKey("my-service-event"));
        }

        [Fact]
        public void AddSchema_SlashInEventType_SanitizedToHyphen() {
            var schema = EventSchema.Build("my/service/event")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .Build();
            var doc = schema.ToAsyncApiDocument();

            Assert.True(doc.Channels.ContainsKey("my-service-event"));
        }

        [Fact]
        public void AddSchema_SpaceInEventType_SanitizedToHyphen() {
            var schema = EventSchema.Build("my service event")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .Build();
            var doc = schema.ToAsyncApiDocument();

            Assert.True(doc.Channels.ContainsKey("my-service-event"));
        }

        [Fact]
        public void FromDataType_PersonCreated_ProducesCorrectSchema() {
            var schema = EventSchema.FromDataType<PersonCreatedData>();
            var doc = schema.ToAsyncApiDocument();

            Assert.Equal("person.created", doc.Info.Title);
            Assert.True(doc.Components.Schemas.ContainsKey("person-created"));
        }

        [Fact]
        public void FromDataType_OrderPlaced_EnumConstraintPresent() {
            var schema = EventSchema.FromDataType<OrderPlacedData>();
            var json = schema.ToJsonSchema();

            var statusProp = json.Properties["status"];
            Assert.Contains("Pending",   statusProp.Enumeration);
            Assert.Contains("Confirmed", statusProp.Enumeration);
            Assert.Contains("Cancelled", statusProp.Enumeration);
        }

        [Fact]
        public void FromDataType_OrderPlaced_ArrayPropertyMapped() {
            var schema = EventSchema.FromDataType<OrderPlacedData>();
            var json = schema.ToJsonSchema();

            Assert.Equal(JsonObjectType.Array, json.Properties["tags"].Type);
        }
    }
}

