using System.ComponentModel.DataAnnotations;

namespace Deveel.Events {
	public static class EventSchemaCreateTests {
		[Fact]
		public static void CreateSimpleSchema() {
			var schema = new EventSchema("test", "1.0", "object");
			schema.Properties.Add(new EventProperty("key1", "string"));
			schema.Properties.Add(new EventProperty("key2", "int"));

			Assert.NotNull(schema);
			Assert.Equal("test", schema.EventType);
			Assert.Equal("1.0", schema.Version.ToString());
			Assert.NotEmpty(schema.Properties);

			Assert.True(schema.Properties.Contains("key1"));
			Assert.True(schema.Properties.Contains("key2"));

			var property = schema.Properties["key1"];
			Assert.NotNull(property);
			Assert.Equal("key1", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());

			property = schema.Properties["key2"];
			Assert.NotNull(property);
			Assert.Equal("key2", property.Name);
			Assert.Equal("int", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());
		}

		[Fact]
		public static void CreateSchemaWithPropertiesInDifferentVersions() {
			var schema = new EventSchema("test", "2.0", "object");
			schema.Properties.Add(new EventProperty("key1", "string", "1.0"));
			schema.Properties.Add(new EventProperty("key2", "int", "2.0"));

			Assert.NotNull(schema);
			Assert.Equal("test", schema.EventType);
			Assert.Equal("2.0", schema.Version.ToString());
			Assert.NotEmpty(schema.Properties);
			Assert.True(schema.Properties.Contains("key1"));
			Assert.True(schema.Properties.Contains("key2"));

			var property = schema.Properties["key1"];
			Assert.NotNull(property);
			Assert.Equal("key1", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());

			property = schema.Properties["key2"];
			Assert.NotNull(property);
			Assert.Equal("key2", property.Name);
			Assert.Equal("int", property.DataType);
			Assert.Equal("2.0", property.Version.ToString());
		}

		[Fact]
		public static void CreateSchemaWithConstrainedProperties() {
			var schema = new EventSchema("test", "1.0", "object");
			schema.Properties.Add(new EventProperty("key1", "string", "1.0") {
				Constraints = {
					new PropertyRequiredConstraint()
				}
			});
			schema.Properties.Add(new EventProperty("key2", "int", "1.0") {
				Constraints = {
					new RangeConstraint<int>(0, 100)
				}
			});

			Assert.NotNull(schema);
			Assert.Equal("test", schema.EventType);
			Assert.Equal("1.0", schema.Version.ToString());
			Assert.NotEmpty(schema.Properties);

			Assert.True(schema.Properties.Contains("key1"));
			Assert.True(schema.Properties.Contains("key2"));

			var property = schema.Properties["key1"];
			Assert.NotNull(property);
			Assert.Equal("key1", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());
			Assert.NotEmpty(property.Constraints);
			Assert.True(property.IsRequired());

			property = schema.Properties["key2"];
			Assert.NotNull(property);
			Assert.Equal("key2", property.Name);
			Assert.Equal("int", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());
			Assert.NotEmpty(property.Constraints);
			Assert.IsType<RangeConstraint<int>>(property.Constraints.First());
		}

		[Fact]
		public static void CreateSchemaFromType() {
			var schema = EventSchema.FromDataType(typeof(PersonCreated));
			Assert.NotNull(schema);
			Assert.Equal("person.created", schema.EventType);
			Assert.Equal("1.0", schema.Version.ToString());
			Assert.NotEmpty(schema.Properties);

			Assert.True(schema.Properties.Contains("first_name"));
			Assert.True(schema.Properties.Contains("last_name"));
			Assert.True(schema.Properties.Contains("middle_name"));

			var property = schema.Properties["first_name"];
			Assert.NotNull(property);
			Assert.Equal("first_name", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());

			property = schema.Properties["last_name"];
			Assert.NotNull(property);
			Assert.Equal("last_name", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());

			property = schema.Properties["middle_name"];
			Assert.NotNull(property);
			Assert.Equal("middle_name", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());
		}

		[Fact]
		public static void CreateSchemaFromTypeWithInheritance() {
			var schema = EventSchema.FromDataType(typeof(PersonCreatedV2));
			Assert.NotNull(schema);
			Assert.Equal("person.created", schema.EventType);
			Assert.Equal("2.0", schema.Version.ToString());
			Assert.NotEmpty(schema.Properties);

			Assert.True(schema.Properties.Contains("first_name"));
			Assert.True(schema.Properties.Contains("last_name"));
			Assert.True(schema.Properties.Contains("middle_name"));
			Assert.True(schema.Properties.Contains("age"));

			var property = schema.Properties["first_name"];
			Assert.NotNull(property);
			Assert.Equal("first_name", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());

			property = schema.Properties["last_name"];
			Assert.NotNull(property);
			Assert.Equal("last_name", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());

			property = schema.Properties["middle_name"];
			Assert.NotNull(property);
			Assert.Equal("middle_name", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());

			property = schema.Properties["age"];
			Assert.NotNull(property);
			Assert.Equal("age", property.Name);
			Assert.Equal("int", property.DataType);
			Assert.Equal("2.0", property.Version.ToString());
			Assert.NotEmpty(property.Constraints);
			Assert.IsType<RangeConstraint<int>>(property.Constraints.First());

			var constraints = (RangeConstraint<int>)property.Constraints.First();
			Assert.Equal(14, constraints.Min);
			Assert.Equal(21, constraints.Max);
		}

		[Fact]
		public static void CreateSchemaFromTypeWithComplexProperties() {
			var schema = EventSchema.FromDataType(typeof(EmailCreated));
			Assert.NotNull(schema);
			Assert.Equal("email.created", schema.EventType);
			Assert.Equal("1.0", schema.Version.ToString());
			Assert.NotEmpty(schema.Properties);

			Assert.True(schema.Properties.Contains("Email"));

			var property = schema.Properties["Email"];
			Assert.NotNull(property);
			Assert.Equal("Email", property.Name);
			Assert.Equal(typeof(EmailAddress).FullName, property.DataType);
			Assert.Equal("1.0", property.Version.ToString());
			Assert.NotEmpty(property.Properties);

			Assert.True(property.Properties.Contains("DisplayName"));
			Assert.True(property.Properties.Contains("Address"));

			var subProperty = property.Properties["DisplayName"];
			Assert.NotNull(subProperty);
			Assert.Equal("DisplayName", subProperty.Name);
			Assert.Equal("string", subProperty.DataType);
			Assert.Equal("1.0", subProperty.Version.ToString());

			subProperty = property.Properties["Address"];
			Assert.NotNull(subProperty);
			Assert.Equal("Address", subProperty.Name);
			Assert.Equal("string", subProperty.DataType);
			Assert.Equal("1.0", subProperty.Version.ToString());
			Assert.NotEmpty(subProperty.Constraints);
			Assert.IsType<PropertyRequiredConstraint>(subProperty.Constraints.First());
		}

		[Fact]
		public static void CreateSchemaFromTypeWithEnum() {
			var schema = EventSchema.FromDataType(typeof(PersonCreatedV3));
			Assert.NotNull(schema);
			Assert.Equal("person.created", schema.EventType);
			Assert.Equal("3.0", schema.Version.ToString());
			Assert.NotEmpty(schema.Properties);

			Assert.True(schema.Properties.Contains("first_name"));
			Assert.True(schema.Properties.Contains("last_name"));
			Assert.True(schema.Properties.Contains("middle_name"));
			Assert.True(schema.Properties.Contains("age"));
			Assert.True(schema.Properties.Contains("gender"));

			var property = schema.Properties["first_name"];
			Assert.NotNull(property);
			Assert.Equal("first_name", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());
			Assert.NotEmpty(property.Constraints);
			Assert.IsType<PropertyRequiredConstraint>(property.Constraints.First());

			property = schema.Properties["last_name"];
			Assert.NotNull(property);
			Assert.Equal("last_name", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());
			Assert.NotEmpty(property.Constraints);
			Assert.IsType<PropertyRequiredConstraint>(property.Constraints.First());

			property = schema.Properties["middle_name"];
			Assert.NotNull(property);
			Assert.Equal("middle_name", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("1.0", property.Version.ToString());
			Assert.Empty(property.Constraints);

			property = schema.Properties["age"];
			Assert.NotNull(property);
			Assert.Equal("age", property.Name);
			Assert.Equal("int", property.DataType);
			Assert.Equal("2.0", property.Version.ToString());
			Assert.NotEmpty(property.Constraints);
			Assert.IsType<RangeConstraint<int>>(property.Constraints.First());

			var constraints = (RangeConstraint<int>)property.Constraints.First();
			Assert.Equal(14, constraints.Min);
			Assert.Equal(21, constraints.Max);

			property = schema.Properties["gender"];
			Assert.NotNull(property);
			Assert.Equal("gender", property.Name);
			Assert.Equal("string", property.DataType);
			Assert.Equal("3.0", property.Version.ToString());
			Assert.NotEmpty(property.Constraints);
			Assert.IsType<EnumMemberConstraint<string>>(property.Constraints.First());

			var enumConstraints = (EnumMemberConstraint<string>)property.Constraints.First();
			Assert.Equal(3, enumConstraints.AllowedValues.Count);
			Assert.Equal("Unknown", enumConstraints.AllowedValues[0]);
			Assert.Equal("Male", enumConstraints.AllowedValues[1]);
			Assert.Equal("Female", enumConstraints.AllowedValues[2]);
		}

		[Event("person.created", "1.0")]
		class PersonCreated {
			[EventProperty("first_name")]
			[Required]
			public string FirstName { get; set; }

			[EventProperty("last_name")]
			[Required]
			public string LastName { get; set; }

			[EventProperty("middle_name")]
			public string? MiddleName { get; set; }
		}

		[Event("person.created", "2.0")]
		class PersonCreatedV2 : PersonCreated {
			[EventProperty("age")]
			[Range(14, 21)]
			public int Age { get; set; }
		}

		[Event("person.created", "3.0")]
		class PersonCreatedV3 : PersonCreatedV2 {
			[EventProperty("gender")]
			public Gender? Gender { get; set; }
		}

		enum Gender {
			Unknown = 0,
			Male = 1,
			Female = 2
		}

		[Event("email.created", "1.0")]
		class EmailCreated {
			[Required]
			public EmailAddress Email { get; set; }
		}

		class EmailAddress {
			public string? DisplayName { get; set; }

			[Required]
			public string Address { get; set; }
		}
	}
}
