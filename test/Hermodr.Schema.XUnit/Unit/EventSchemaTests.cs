// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Domain")]
    [Trait("Feature", "EventSchema")]
    public static class EventSchemaTests
    {
        // ── Versioned schema properties ───────────────────────────────────────

        [Fact]
        public static void Should_InheritSchemaVersion_When_PropertiesAddedWithoutVersion()
        {
            // Arrange
            var schema = new EventSchema("test", "1.0", "application/json");

            // Act
            schema.Properties.Add("name", "string");
            schema.Properties.Add(new EventProperty("age", "int"));

            // Assert
            var name = schema.Properties["name"];
            Assert.NotNull(name);
            Assert.Equal("1.0", name.Version.ToString());

            var age = schema.Properties["age"];
            Assert.NotNull(age);
            Assert.Equal("1.0", age.Version.ToString());
        }

        [Fact]
        public static void Should_PreservePropertyVersion_When_VersionIsLowerThanSchemaVersion()
        {
            // Arrange
            var schema = new EventSchema("test", "2.0", "application/json");

            // Act
            schema.Properties.Add("name", "string", "1.0");
            schema.Properties.Add(new EventProperty("age", "int", "1.2"));

            // Assert
            var name = schema.Properties["name"];
            Assert.NotNull(name);
            Assert.Equal("1.0", name.Version.ToString());

            var age = schema.Properties["age"];
            Assert.NotNull(age);
            Assert.Equal("1.2", age.Version.ToString());
        }

        [Fact]
        public static void Should_ThrowArgumentException_When_PropertyVersionIsHigherThanSchemaVersion()
        {
            // Arrange
            var schema = new EventSchema("test", "1.0", "application/json");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => schema.Properties.Add("name", "string", "2.0"));
            Assert.Throws<ArgumentException>(() => schema.Properties.Add(new EventProperty("age", "int", "2.0")));
        }

        [Fact]
        public static void Should_ThrowArgumentException_When_PropertyWithSameNameIsAdded()
        {
            // Arrange
            var schema = new EventSchema("test", "1.0", "application/json");
            schema.Properties.Add("name", "string");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => schema.Properties.Add("name", "int"));
        }

        [Fact]
        public static void Should_ThrowKeyNotFoundException_When_SettingNewPropertyViaIndexer()
        {
            // Arrange
            var schema = new EventSchema("test", "1.0", "application/json");
            schema.Properties.Add("name", "string");

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => schema.Properties["age"] = new EventProperty("age", "int", "1.0"));
            Assert.Null(schema.Properties["age"]);
        }

        [Fact]
        public static void Should_UpdateProperty_When_ExistingPropertyIsReplacedViaIndexer()
        {
            // Arrange
            var schema = new EventSchema("test", "2.0", "application/json");
            schema.Properties.Add("name", "string", "1.0");

            var newName = new EventProperty("name", "string", "1.1");
            newName.Constraints.Add(new EnumMemberConstraint<string>(new[] { "John", "Jane" }));

            // Act
            schema.Properties["name"] = newName;

            // Assert
            var name = schema.Properties["name"];
            Assert.NotNull(name);
            Assert.Equal("1.1", name.Version.ToString());
        }

        // ── IEventProperty interface contract ─────────────────────────────────

        [Fact]
        public static void Should_ReturnIsRequiredTrue_When_RequiredConstraintIsPresent()
        {
            // Arrange
            var property = new EventProperty("name", "string");
            property.Constraints.Add(new PropertyRequiredConstraint());

            // Act & Assert
            Assert.True(property.IsRequired);
        }

        [Fact]
        public static void Should_ReturnIsRequiredFalse_When_NoRequiredConstraintIsPresent()
        {
            // Arrange
            var property = new EventProperty("name", "string");

            // Assert
            Assert.False(property.IsRequired);
        }

        [Fact]
        public static void Should_ReturnIsRequiredTrue_When_AccessedViaInterface()
        {
            // Arrange
            IEventProperty property = new EventProperty("name", "string")
            {
                Constraints = { new PropertyRequiredConstraint() }
            };

            // Assert
            Assert.True(property.IsRequired);
        }

        [Fact]
        public static void Should_ReturnIsNullableFalse_When_PropertyIsNotMarkedNullable()
        {
            // Arrange
            var property = new EventProperty("name", "string");

            // Assert
            Assert.False(property.IsNullable);
        }

        [Fact]
        public static void Should_ReturnIsNullableTrue_When_PropertyIsMarkedNullable()
        {
            // Arrange & Act
            var property = new EventProperty("name", "string") { IsNullable = true };

            // Assert
            Assert.True(property.IsNullable);
        }

        [Fact]
        public static void Should_ReturnNullVersion_When_PropertyNotAddedToSchema()
        {
            // Arrange
            var property = new EventProperty("name", "string");

            // Assert (before being added to a schema the version is null via interface)
            IEventProperty iface = property;
            Assert.Null(iface.Version);
        }

        [Fact]
        public static void Should_InheritVersionFromSchema_When_PropertyIsAddedToSchema()
        {
            // Arrange
            var schema = new EventSchema("test", "3.0", "application/json");
            schema.Properties.Add(new EventProperty("name", "string"));

            // Act
            IEventProperty iface = schema.Properties["name"]!;

            // Assert
            Assert.Equal("3.0", iface.Version);
        }

        [Fact]
        public static void Should_ExposeConstraintsAsReadOnlyList_When_AccessedViaInterface()
        {
            // Arrange
            var property = new EventProperty("x", "int");
            property.Constraints.Add(new RangeConstraint<int>(0, 100));

            // Act
            IEventProperty iface = property;

            // Assert
            Assert.IsAssignableFrom<IReadOnlyList<IEventPropertyConstraint>>(iface.Constraints);
            Assert.Single(iface.Constraints);
        }

        [Fact]
        public static void Should_ExposeNestedPropertiesAsReadOnlyList_When_AccessedViaInterface()
        {
            // Arrange
            var parent = new EventProperty("address", "object");
            parent.Properties.Add(new EventProperty("city", "string"));

            // Act
            IEventProperty iface = parent;

            // Assert
            Assert.IsAssignableFrom<IReadOnlyList<IEventProperty>>(iface.Properties);
            Assert.Single(iface.Properties);
        }

        // ── IEventSchema interface contract ───────────────────────────────────

        [Fact]
        public static void Should_ExposePropertiesAsReadOnlyList_When_SchemaAccessedViaInterface()
        {
            // Arrange
            var schema = new EventSchema("test", "1.0", "application/json");
            schema.Properties.Add("name", "string");

            // Act
            IEventSchema iface = schema;

            // Assert
            Assert.IsAssignableFrom<IReadOnlyList<IEventProperty>>(iface.Properties);
            Assert.Single(iface.Properties);
        }

        [Fact]
        public static void Should_ExposeVersionAsString_When_SchemaAccessedViaInterface()
        {
            // Arrange
            var schema = new EventSchema("test", "2.5", "application/json");

            // Act
            IEventSchema iface = schema;

            // Assert
            Assert.Equal("2.5", iface.Version);
        }

        // ── EventPropertyExtensions ───────────────────────────────────────────

        [Fact]
        public static void Should_DelegateIsRequired_When_ExtensionMethodIsCalled()
        {
            // Arrange
            IEventProperty property = new EventProperty("x", "string")
            {
                Constraints = { new PropertyRequiredConstraint() }
            };

            // Assert
            Assert.True(property.IsRequired());
        }

        [Fact]
        public static void Should_DelegateIsNullable_When_ExtensionMethodIsCalled()
        {
            // Arrange
            IEventProperty property = new EventProperty("x", "string") { IsNullable = true };

            // Assert
            Assert.True(property.IsNullable());
        }

        // ── EventProperty invalid construction ────────────────────────────────

        [Fact]
        public static void Should_ThrowArgumentException_When_VersionIsInvalid()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new EventProperty("x", "string", "not-a-version"));
        }

        [Fact]
        public static void Should_ThrowArgumentNullException_When_NameIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EventProperty(null!, "string"));
        }

        [Fact]
        public static void Should_ThrowArgumentNullException_When_DataTypeIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EventProperty("x", null!));
        }
    }
}
