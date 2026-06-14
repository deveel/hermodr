using CloudNative.CloudEvents;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Hermodr
{
    public static class EventSchemaValidatorTests
    {
        private static EventSchemaValidator CreateValidator()
        {
            var deserializerRegistry = new EventDataDeserializerRegistry();
            deserializerRegistry.Register(new JsonEventDataDeserializer());
            deserializerRegistry.Register(new XmlEventDataDeserializer());

            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<EventSchemaValidator>.Instance;

            return new EventSchemaValidator(deserializerRegistry, logger);
        }

        private static CloudEvent CreateCloudEvent(string data, string contentType = "application/json")
        {
            return new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://test.com"),
                Id = "test-id",
                DataContentType = contentType,
                Data = data
            };
        }

        private static CloudEvent CreateCloudEventFromBytes(byte[] data, string contentType = "application/json")
        {
            return new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://test.com"),
                Id = "test-id",
                DataContentType = contentType,
                Data = data
            };
        }

        [Fact]
        public static async Task Validate_RequiredFieldMissing_ReturnsError()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .AddProperty("email", prop => prop.OfType("string").Required())
                .Build();

            var json = """{"name": "John"}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Single(errors);
            Assert.Contains("email", errors[0].ErrorMessage);
            Assert.Contains("email", errors[0].MemberNames.First());
        }

        [Fact]
        public static async Task Validate_RangeConstraintViolated_ReturnsError()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("age", prop => prop.OfType("int").WithConstraint(new RangeConstraint<int>(0, 150)))
                .Build();

            var json = """{"age": 200}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Single(errors);
            Assert.Contains("200", errors[0].ErrorMessage);
            Assert.Contains("0", errors[0].ErrorMessage);
            Assert.Contains("150", errors[0].ErrorMessage);
        }

        [Fact]
        public static async Task Validate_EnumValueNotAllowed_ReturnsError()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("status", prop => prop.OfType("string")
                    .WithConstraint(new EnumMemberConstraint<string>(new[] { "Active", "Inactive", "Pending" })))
                .Build();

            var json = """{"status": "Deleted"}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Single(errors);
            Assert.Contains("Deleted", errors[0].ErrorMessage);
            Assert.Contains("Active", errors[0].ErrorMessage);
        }

        [Fact]
        public static async Task Validate_NullableFalseWithNull_ReturnsError()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .AddProperty("email", prop => prop.OfType("string").Nullable())
                .Build();

            var json = """{"name": null, "email": "test@test.com"}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Single(errors);
            Assert.Contains("name", errors[0].ErrorMessage);
        }

        [Fact]
        public static async Task Validate_NestedObject_ValidatesRecursively()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("user", prop => prop.OfType("object")
                    .AddProperty("name", sub => sub.OfType("string").Required())
                    .AddProperty("email", sub => sub.OfType("string").Required()))
                .Build();

            var json = """{"user": {"name": "Alice"}}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Single(errors);
            Assert.Contains("email", errors[0].ErrorMessage);
            Assert.Contains("user.email", errors[0].MemberNames.First());
        }

        [Fact]
        public static async Task Validate_MultipleErrors_ReturnsAll()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .AddProperty("age", prop => prop.OfType("int").WithConstraint(new RangeConstraint<int>(0, 150)))
                .AddProperty("status", prop => prop.OfType("string")
                    .WithConstraint(new EnumMemberConstraint<string>(new[] { "Active", "Inactive" })))
                .Build();

            var json = """{"age": 200, "status": "Unknown"}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Equal(3, errors.Count);
        }

        [Fact]
        public static async Task Validate_ValidEvent_ReturnsNoErrors()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .AddProperty("age", prop => prop.OfType("int").WithConstraint(new RangeConstraint<int>(0, 150)))
                .Build();

            var json = """{"name": "John", "age": 30}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Empty(errors);
        }

        [Fact]
        public static async Task Validate_UnsupportedContentType_ReturnsNoErrors()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .Build();

            var data = "some binary data";
            var cloudEvent = CreateCloudEvent(data, "application/octet-stream");

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Empty(errors);
        }

        [Fact]
        public static async Task Validate_XmlContentType_ValidatesCorrectly()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .AddProperty("age", prop => prop.OfType("int"))
                .Build();

            var xml = """<root><name>Alice</name><age>25</age></root>""";
            var cloudEvent = CreateCloudEvent(xml, "application/xml");

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Empty(errors);
        }

        [Fact]
        public static async Task Validate_XmlWithMissingRequiredField_ReturnsError()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .AddProperty("email", prop => prop.OfType("string").Required())
                .Build();

            var xml = """<root><name>Alice</name></root>""";
            var cloudEvent = CreateCloudEvent(xml, "application/xml");

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Single(errors);
            Assert.Contains("email", errors[0].ErrorMessage);
        }

        [Fact]
        public static async Task Validate_NullEventData_ReturnsNoErrors()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .Build();

            var cloudEvent = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://test.com"),
                Id = "test-id",
                DataContentType = "application/json",
                Data = null
            };

            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }

            Assert.Empty(errors);
        }

        // ─── Range edge cases ──────────────────────────────────────────────

        [Fact]
        public static async Task Validate_RangeBelowMinimum_ReturnsError()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("age", prop => prop.OfType("int").WithConstraint(new RangeConstraint<int>(18, 150)))
                .Build();

            var json = """{"age": 15}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Single(errors);
            Assert.Contains("15", errors[0].ErrorMessage);
            Assert.Contains("18", errors[0].ErrorMessage);
        }

        [Fact]
        public static async Task Validate_RangeMinOnly_ValidPasses()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("score", prop => prop.OfType("int").WithConstraint(new RangeConstraint<int>(0, null)))
                .Build();

            var json = """{"score": 42}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        [Fact]
        public static async Task Validate_RangeMinOnly_BelowFails()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("score", prop => prop.OfType("int").WithConstraint(new RangeConstraint<int>(0, null)))
                .Build();

            var json = """{"score": -5}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Single(errors);
            Assert.Contains("-5", errors[0].ErrorMessage);
            Assert.Contains("0", errors[0].ErrorMessage);
        }

        [Fact]
        public static async Task Validate_RangeMaxOnly_ValidPasses()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("score", prop => prop.OfType("int").WithConstraint(new RangeConstraint<int>(null, 100)))
                .Build();

            var json = """{"score": 50}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        [Fact]
        public static async Task Validate_RangeMaxOnly_AboveFails()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("score", prop => prop.OfType("int").WithConstraint(new RangeConstraint<int>(null, 100)))
                .Build();

            var json = """{"score": 150}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Single(errors);
            Assert.Contains("150", errors[0].ErrorMessage);
            Assert.Contains("100", errors[0].ErrorMessage);
        }

        [Fact]
        public static async Task Validate_RangeBorderlineValues_Valid()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("age", prop => prop.OfType("int").WithConstraint(new RangeConstraint<int>(0, 150)))
                .Build();

            var errors0 = await CollectErrors(validator, schema, CreateCloudEvent("""{"age": 0}"""));
            var errors150 = await CollectErrors(validator, schema, CreateCloudEvent("""{"age": 150}"""));

            Assert.Empty(errors0);
            Assert.Empty(errors150);
        }

        [Fact]
        public static async Task Validate_DecimalRange_Valid()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("price", prop => prop.OfType("money").WithConstraint(new RangeConstraint<decimal>(0.00m, 999.99m)))
                .Build();

            var json = """{"price": 49.99}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        // ─── Enum edge cases ──────────────────────────────────────────────

        [Fact]
        public static async Task Validate_EnumValidValue_Passes()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("status", prop => prop.OfType("string")
                    .WithConstraint(new EnumMemberConstraint<string>(new[] { "Active", "Inactive", "Pending" })))
                .Build();

            var errorsActive = await CollectErrors(validator, schema, CreateCloudEvent("""{"status": "Active"}"""));
            var errorsInactive = await CollectErrors(validator, schema, CreateCloudEvent("""{"status": "Inactive"}"""));
            var errorsPending = await CollectErrors(validator, schema, CreateCloudEvent("""{"status": "Pending"}"""));

            Assert.Empty(errorsActive);
            Assert.Empty(errorsInactive);
            Assert.Empty(errorsPending);
        }

        [Fact]
        public static async Task Validate_EnumWithEmptyString_Fails()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("status", prop => prop.OfType("string")
                    .WithConstraint(new EnumMemberConstraint<string>(new[] { "Active", "Inactive" })))
                .Build();

            var json = """{"status": ""}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Single(errors);
            Assert.Contains("not in allowed set", errors[0].ErrorMessage);
        }

        [Fact]
        public static async Task Validate_EnumNumericValue_Fails()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("status", prop => prop.OfType("string")
                    .WithConstraint(new EnumMemberConstraint<string>(new[] { "Active", "Inactive" })))
                .Build();

            var json = """{"status": 42}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Single(errors);
        }

        // ─── Nullable property scenarios ──────────────────────────────────

        [Fact]
        public static async Task Validate_NullableFieldNullValue_Passes()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .AddProperty("email", prop => prop.OfType("string").Nullable())
                .Build();

            var json = """{"name": "John", "email": null}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        [Fact]
        public static async Task Validate_AllFieldsNullableMissing_Passes()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Nullable())
                .AddProperty("email", prop => prop.OfType("string").Nullable())
                .Build();

            var json = """{}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        // ─── Deeply nested object scenarios ───────────────────────────────

        [Fact]
        public static async Task Validate_DeeplyNestedObject_ThreeLevels()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("order", prop => prop.OfType("object")
                    .AddProperty("id", sub => sub.OfType("string").Required())
                    .AddProperty("customer", sub => sub.OfType("object")
                        .AddProperty("name", sub2 => sub2.OfType("string").Required())
                        .AddProperty("address", sub2 => sub2.OfType("object")
                            .AddProperty("street", sub3 => sub3.OfType("string").Required())
                            .AddProperty("city", sub3 => sub3.OfType("string").Required()))))
                .Build();

            var json = """{"order": {"id": "123", "customer": {"name": "Alice", "address": {"street": "Main St"}}}}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Single(errors);
            Assert.Contains("order.customer.address.city", errors[0].MemberNames.First());
        }

        [Fact]
        public static async Task Validate_DeeplyNestedAllValid_Passes()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("order", prop => prop.OfType("object")
                    .AddProperty("id", sub => sub.OfType("string").Required())
                    .AddProperty("customer", sub => sub.OfType("object")
                        .AddProperty("name", sub2 => sub2.OfType("string").Required())
                        .AddProperty("address", sub2 => sub2.OfType("object")
                            .AddProperty("street", sub3 => sub3.OfType("string").Required())
                            .AddProperty("city", sub3 => sub3.OfType("string").Required()))))
                .Build();

            var json = """{"order": {"id": "123", "customer": {"name": "Alice", "address": {"street": "Main St", "city": "Springfield"}}}}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        [Fact]
        public static async Task Validate_MultipleNestedObjects_ErrorsInEach()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("shipping", prop => prop.OfType("object")
                    .AddProperty("street", sub => sub.OfType("string").Required())
                    .AddProperty("city", sub => sub.OfType("string").Required()))
                .AddProperty("billing", prop => prop.OfType("object")
                    .AddProperty("street", sub => sub.OfType("string").Required())
                    .AddProperty("city", sub => sub.OfType("string").Required()))
                .Build();

            var json = """{"shipping": {"street": "123 Main"}, "billing": {"city": "Springfield"}}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, e => e.MemberNames.First() == "shipping.city");
            Assert.Contains(errors, e => e.MemberNames.First() == "billing.street");
        }

        // ─── Multiple required fields missing ─────────────────────────────

        [Fact]
        public static async Task Validate_MultipleRequiredFieldsMissing_ReturnsAllErrors()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .AddProperty("email", prop => prop.OfType("string").Required())
                .AddProperty("phone", prop => prop.OfType("string").Required())
                .AddProperty("age", prop => prop.OfType("int").Required())
                .Build();

            var json = """{}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Equal(4, errors.Count);
            Assert.Contains(errors, e => e.MemberNames.First() == "name");
            Assert.Contains(errors, e => e.MemberNames.First() == "email");
            Assert.Contains(errors, e => e.MemberNames.First() == "phone");
            Assert.Contains(errors, e => e.MemberNames.First() == "age");
        }

        // ─── Byte array data ──────────────────────────────────────────────

        [Fact]
        public static async Task Validate_ByteArrayData_ValidatesCorrectly()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .Build();

            var json = """{"name": "Alice"}""";
            var bytes = Encoding.UTF8.GetBytes(json);
            var cloudEvent = CreateCloudEventFromBytes(bytes);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        // ─── Extra properties in data (not in schema) ────────────────────

        [Fact]
        public static async Task Validate_ExtraPropertiesInData_Ignored()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .Build();

            var json = """{"name": "John", "extra1": "value1", "extra2": 42, "extra3": {"nested": true}}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        // ─── Empty schema (no properties) ────────────────────────────────

        [Fact]
        public static async Task Validate_EmptySchema_ReturnsNoErrors()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .Build();

            var json = """{"anything": "goes"}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        // ─── Boolean values ───────────────────────────────────────────────

        [Fact]
        public static async Task Validate_BooleanRequired_Passes()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("active", prop => prop.OfType("boolean").Required())
                .Build();

            var errorsTrue = await CollectErrors(validator, schema, CreateCloudEvent("""{"active": true}"""));
            var errorsFalse = await CollectErrors(validator, schema, CreateCloudEvent("""{"active": false}"""));

            Assert.Empty(errorsTrue);
            Assert.Empty(errorsFalse);
        }

        // ─── Property name case sensitivity ──────────────────────────────

        [Fact]
        public static async Task Validate_PropertyNameCaseMismatch_ReturnsError()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("Name", prop => prop.OfType("string").Required())
                .Build();

            var json = """{"name": "John"}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Single(errors);
            Assert.Contains("Name", errors[0].ErrorMessage);
        }

        // ─── Data as Stream ───────────────────────────────────────────────

        [Fact]
        public static async Task Validate_DataAsStream_ValidatesCorrectly()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .Build();

            var json = """{"name": "StreamTest"}""";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var cloudEvent = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://test.com"),
                Id = "test-id",
                DataContentType = "application/json",
                Data = stream
            };

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        // ─── CloudEvents structured content type ─────────────────────────

        [Fact]
        public static async Task Validate_CloudEventsStructuredContentType_Validates()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .Build();

            var json = """{"name": "Alice"}""";
            var cloudEvent = CreateCloudEvent(json, "application/cloudevents+json");

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Empty(errors);
        }

        // ─── Mixed constraint types on single property ───────────────────

        [Fact]
        public static async Task Validate_MixedConstraints_RangeAndEnum()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("priority", prop => prop.OfType("int")
                    .WithConstraint(new RangeConstraint<int>(1, 5))
                    .WithConstraint(new EnumMemberConstraint<int>(new[] { 1, 2, 3, 4, 5 })))
                .Build();

            var errorsValid = await CollectErrors(validator, schema, CreateCloudEvent("""{"priority": 3}"""));
            var errorsOutOfRange = await CollectErrors(validator, schema, CreateCloudEvent("""{"priority": 6}"""));
            var errorsNotAllowed = await CollectErrors(validator, schema, CreateCloudEvent("""{"priority": 0}"""));

            Assert.Empty(errorsValid);
            Assert.NotEmpty(errorsOutOfRange);
            Assert.NotEmpty(errorsNotAllowed);
        }

        [Fact]
        public static async Task Validate_RequiredAndRange_MissingField()
        {
            var validator = CreateValidator();
            var schema = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", prop => prop.OfType("string").Required())
                .AddProperty("age", prop => prop.OfType("int").Required()
                    .WithConstraint(new RangeConstraint<int>(0, 150)))
                .Build();

            var json = """{"name": "John"}""";
            var cloudEvent = CreateCloudEvent(json);

            var errors = await CollectErrors(validator, schema, cloudEvent);

            Assert.Single(errors);
            Assert.Contains("age", errors[0].ErrorMessage);
        }

        // ─── Helper ──────────────────────────────────────────────────────

        private static async Task<List<ValidationResult>> CollectErrors(
            EventSchemaValidator validator,
            IEventSchema schema,
            CloudEvent cloudEvent)
        {
            var errors = new List<ValidationResult>();
            await foreach (var error in validator.ValidateEventAsync(schema, cloudEvent))
            {
                errors.Add(error);
            }
            return errors;
        }
    }
}
