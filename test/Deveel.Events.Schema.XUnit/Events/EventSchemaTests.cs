namespace Deveel.Events
{
    public static class EventSchemaTests
    {
		// ─── existing tests (unchanged) ───────────────────────────────────────

        [Fact]
        public static void VersionedSchema_PropertiesWithoutVersion_Success()
        {
            var schema = new EventSchema("test", "1.0", "application/json");

            schema.Properties.Add("name", "string");
            schema.Properties.Add(new EventProperty("age", "int"));

            var name = schema.Properties["name"];
            Assert.NotNull(name);
            Assert.Equal("1.0", name.Version.ToString());

            var age = schema.Properties["age"];
            Assert.NotNull(age);
            Assert.Equal("1.0", age.Version.ToString());
        }

        [Fact]
        public static void VersionedSchema_PropertiesWithVersionLowerThanEvent_Success()
        {
            var schema = new EventSchema("test", "2.0", "application/json");

            schema.Properties.Add("name", "string", "1.0");
            schema.Properties.Add(new EventProperty("age", "int", "1.2"));

            var name = schema.Properties["name"];
            Assert.NotNull(name);
            Assert.Equal("1.0", name.Version.ToString());

            var age = schema.Properties["age"];
            Assert.NotNull(age);
            Assert.Equal("1.2", age.Version.ToString());
        }

        [Fact]
        public static void VersionedSchema_PropertiesWithVersionHigherThanEvent_Fail()
        {
            var schema = new EventSchema("test", "1.0", "application/json");

            Assert.Throws<ArgumentException>(() => schema.Properties.Add("name", "string", "2.0"));
            Assert.Throws<ArgumentException>(() => schema.Properties.Add(new EventProperty("age", "int", "2.0")));
        }

        [Fact]
        public static void VersionedSchema_PropertiesWithSameName_Fail()
        {
            var schema = new EventSchema("test", "1.0", "application/json");

            schema.Properties.Add("name", "string");

            Assert.Throws<ArgumentException>(() => schema.Properties.Add("name", "int"));
        }

        [Fact]
        public static void VersionedSchema_SetNewProperty_Fail()
        {
            var schema = new EventSchema("test", "1.0", "application/json");

            schema.Properties.Add("name", "string");

            var name = schema.Properties["name"];
            Assert.NotNull(name);
            Assert.Equal("1.0", name.Version.ToString());

            Assert.Throws<KeyNotFoundException>(() => schema.Properties["age"] = new EventProperty("age", "int", "1.0"));

            var age = schema.Properties["age"];
            Assert.Null(age);
        }

        [Fact]
        public static void VersionedSchema_SetExistingProperty_Success()
        {
            var schema = new EventSchema("test", "2.0", "application/json");

            schema.Properties.Add("name", "string", "1.0");

            var name = schema.Properties["name"];
            Assert.NotNull(name);
            Assert.Equal("1.0", name.Version.ToString());

            var newName = new EventProperty("name", "string", "1.1");
            newName.Constraints.Add(new EnumMemberConstraint<string>(new[] { "John", "Jane" }));

            schema.Properties["name"] = newName;

            name = schema.Properties["name"];
            Assert.NotNull(name);
            Assert.Equal("1.1", name.Version.ToString());
        }

		// ─── IEventProperty interface contract ───────────────────────────────

		[Fact]
		public static void Property_IsRequired_TrueWhenConstraintPresent()
		{
			var property = new EventProperty("name", "string");
			property.Constraints.Add(new PropertyRequiredConstraint());

			Assert.True(property.IsRequired);
		}

		[Fact]
		public static void Property_IsRequired_FalseWhenNoConstraint()
		{
			var property = new EventProperty("name", "string");
			Assert.False(property.IsRequired);
		}

		[Fact]
		public static void Property_IsRequired_TrueViaInterfaceProjection()
		{
			IEventProperty property = new EventProperty("name", "string") {
				Constraints = { new PropertyRequiredConstraint() }
			};

			Assert.True(property.IsRequired);
		}

		[Fact]
		public static void Property_IsNullable_DefaultFalse()
		{
			var property = new EventProperty("name", "string");
			Assert.False(property.IsNullable);
		}

		[Fact]
		public static void Property_IsNullable_SetTrue()
		{
			var property = new EventProperty("name", "string") { IsNullable = true };
			Assert.True(property.IsNullable);
		}

		[Fact]
		public static void Property_Version_NullWhenNotSpecified()
		{
			var property = new EventProperty("name", "string");
			// before being added to a schema, the version is null
			IEventProperty iface = property;
			Assert.Null(iface.Version);
		}

		[Fact]
		public static void Property_VersionInherited_FromSchema()
		{
			var schema = new EventSchema("test", "3.0", "application/json");
			schema.Properties.Add(new EventProperty("name", "string"));

			IEventProperty iface = schema.Properties["name"]!;
			Assert.Equal("3.0", iface.Version);
		}

		[Fact]
		public static void Property_Constraints_ExposedAsReadOnlyList()
		{
			var property = new EventProperty("x", "int");
			property.Constraints.Add(new RangeConstraint<int>(0, 100));

			IEventProperty iface = property;
			Assert.IsAssignableFrom<IReadOnlyList<IEventPropertyConstraint>>(iface.Constraints);
			Assert.Single(iface.Constraints);
		}

		[Fact]
		public static void Property_Properties_ExposedAsReadOnlyList()
		{
			var parent = new EventProperty("address", "object");
			parent.Properties.Add(new EventProperty("city", "string"));

			IEventProperty iface = parent;
			Assert.IsAssignableFrom<IReadOnlyList<IEventProperty>>(iface.Properties);
			Assert.Single(iface.Properties);
		}

		// ─── IEventSchema interface contract ─────────────────────────────────

		[Fact]
		public static void Schema_Properties_ExposedAsReadOnlyList()
		{
			var schema = new EventSchema("test", "1.0", "application/json");
			schema.Properties.Add("name", "string");

			IEventSchema iface = schema;
			Assert.IsAssignableFrom<IReadOnlyList<IEventProperty>>(iface.Properties);
			Assert.Single(iface.Properties);
		}

		[Fact]
		public static void Schema_Version_ExposedAsStringViaInterface()
		{
			var schema = new EventSchema("test", "2.5", "application/json");
			IEventSchema iface = schema;
			Assert.Equal("2.5", iface.Version);
		}

		// ─── EventPropertyExtensions ──────────────────────────────────────────

		[Fact]
		public static void Extension_IsRequired_DelegatesToInterface()
		{
			IEventProperty property = new EventProperty("x", "string") {
				Constraints = { new PropertyRequiredConstraint() }
			};
			Assert.True(property.IsRequired());
		}

		[Fact]
		public static void Extension_IsNullable_DelegatesToInterface()
		{
			IEventProperty property = new EventProperty("x", "string") { IsNullable = true };
			Assert.True(property.IsNullable());
		}

		// ─── EventProperty invalid construction ───────────────────────────────

		[Fact]
		public static void Property_InvalidVersion_Throws()
		{
			Assert.Throws<ArgumentException>(() => new EventProperty("x", "string", "not-a-version"));
		}

		[Fact]
		public static void Property_NullName_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new EventProperty(null!, "string"));
		}

		[Fact]
		public static void Property_NullDataType_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new EventProperty("x", null!));
		}
    }
}
