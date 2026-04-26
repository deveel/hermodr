namespace Deveel.Events {
	public static class EventSchemaBuilderTests {

		// ─── basic fluent build ───────────────────────────────────────────────

		[Fact]
		public static void Build_SimpleSchema_Success() {
			var schema = EventSchema.Build("order.placed")
				.WithVersion("1.0")
				.WithContentType("application/json")
				.WithDescription("Raised when an order is placed")
				.AddProperty("order_id", "guid")
				.AddProperty("amount", "money")
				.Build();

			Assert.Equal("order.placed", schema.EventType);
			Assert.Equal("1.0", schema.Version.ToString());
			Assert.Equal("application/json", schema.ContentType);
			Assert.Equal("Raised when an order is placed", schema.Description);
			Assert.Equal(2, schema.Properties.Count);
			Assert.True(schema.Properties.Contains("order_id"));
			Assert.True(schema.Properties.Contains("amount"));
		}

		[Fact]
		public static void Build_SchemaWithConfiguredProperties_Success() {
			var schema = EventSchema.Build("user.registered")
				.WithVersion("1.0")
				.AddProperty("email", p => p
					.OfType("string")
					.Required()
					.WithDescription("User email address"))
				.AddProperty("age", p => p
					.OfType("int")
					.WithRange<int>(18, 120))
				.AddProperty("nickname", p => p
					.OfType("string")
					.Nullable())
				.Build();

			Assert.Equal(3, schema.Properties.Count);

			var email = schema.Properties["email"]!;
			Assert.Equal("string", email.DataType);
			Assert.True(email.IsRequired);
			Assert.Equal("User email address", email.Description);

			var age = schema.Properties["age"]!;
			Assert.Equal("int", age.DataType);
			Assert.Single(age.Constraints);
			Assert.IsType<RangeConstraint<int>>(age.Constraints[0]);

			var nickname = schema.Properties["nickname"]!;
			Assert.True(nickname.IsNullable);
			Assert.False(nickname.IsRequired);
		}

		[Fact]
		public static void Build_DefaultVersion_Is1_0() {
			var schema = EventSchema.Build("test.event").Build();
			Assert.Equal("1.0", schema.Version.ToString());
		}

		[Fact]
		public static void Build_DefaultContentType_IsApplicationJson() {
			var schema = EventSchema.Build("test.event").Build();
			Assert.Equal("application/json", schema.ContentType);
		}

		[Fact]
		public static void Build_WithPrebuiltProperty_Success() {
			var property = new EventPropertyBuilder("status")
				.OfType("string")
				.WithAllowedValues(new[] { "active", "inactive", "pending" })
				.Build();

			var schema = EventSchema.Build("user.status.changed")
				.AddProperty(property)
				.Build();

			Assert.Single(schema.Properties);
			var p = schema.Properties["status"]!;
			Assert.Single(p.Constraints);
			Assert.Equal("allowedValues", p.Constraints[0].ConstraintType);
		}

		[Fact]
		public static void Build_WithNestedProperties_Success() {
			var schema = EventSchema.Build("order.shipped")
				.WithVersion("1.0")
				.AddProperty("address", p => p
					.OfType("object")
					.AddProperty("street", nested => nested.OfType("string").Required())
					.AddProperty("city",   nested => nested.OfType("string").Required())
					.AddProperty("zip",    nested => nested.OfType("string")))
				.Build();

			var address = schema.Properties["address"]!;
			Assert.Equal(3, address.Properties.Count);
			Assert.True(address.Properties[0].IsRequired);
			Assert.Equal("street", address.Properties[0].Name);
		}

		// ─── EventPropertyBuilder standalone ─────────────────────────────────

		[Fact]
		public static void PropertyBuilder_NullName_Throws() {
			Assert.Throws<ArgumentNullException>(() => new EventPropertyBuilder(null!));
		}

		[Fact]
		public static void PropertyBuilder_WithConstraint_Success() {
			var property = new EventPropertyBuilder("score")
				.OfType("int")
				.WithConstraint(new RangeConstraint<int>(0, 10))
				.Build();

			Assert.Single(property.Constraints);
			Assert.Equal("range", property.Constraints[0].ConstraintType);
		}

		[Fact]
		public static void PropertyBuilder_Required_AddsRequiredConstraint() {
			var property = new EventPropertyBuilder("name")
				.OfType("string")
				.Required()
				.Build();

			Assert.True(property.IsRequired);
		}

		[Fact]
		public static void PropertyBuilder_Required_CalledTwice_OnlyOneConstraint() {
			// calling .Required() twice should not duplicate the constraint
			var property = new EventPropertyBuilder("name")
				.OfType("string")
				.Required()
				.Required()
				.Build();

			Assert.Single(property.Constraints);
		}

		[Fact]
		public static void PropertyBuilder_WithVersion_PropertyCarriesVersion() {
			var schema = EventSchema.Build("test")
				.WithVersion("2.0")
				.AddProperty("legacy_field", p => p.OfType("string").WithVersion("1.0"))
				.Build();

			var prop = schema.Properties["legacy_field"]!;
			Assert.Equal("1.0", prop.Version!.ToString());
		}

		[Fact]
		public static void PropertyBuilder_WithDescription_Success() {
			var property = new EventPropertyBuilder("notes")
				.OfType("string")
				.WithDescription("Optional free-text notes")
				.Build();

			Assert.Equal("Optional free-text notes", property.Description);
		}

		[Fact]
		public static void PropertyBuilder_DefaultDataType_IsString() {
			var property = new EventPropertyBuilder("x").Build();
			Assert.Equal("string", property.DataType);
		}
	}
}

