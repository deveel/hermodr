// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Domain")]
    [Trait("Feature", "EventSchema")]
    public static class EventSchemaBuilderTests
    {
        // ── Basic fluent build ────────────────────────────────────────────────

        [Fact]
        public static void Should_BuildSimpleSchema_When_FluentBuilderIsUsed()
        {
            // Arrange & Act
            var schema = EventSchema.Build("order.placed")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .WithDescription("Raised when an order is placed")
                .AddProperty("order_id", "guid")
                .AddProperty("amount", "money")
                .Build();

            // Assert
            Assert.Equal("order.placed", schema.EventType);
            Assert.Equal("1.0", schema.Version.ToString());
            Assert.Equal("application/json", schema.ContentType);
            Assert.Equal("Raised when an order is placed", schema.Description);
            Assert.Equal(2, schema.Properties.Count);
            Assert.True(schema.Properties.Contains("order_id"));
            Assert.True(schema.Properties.Contains("amount"));
        }

        [Fact]
        public static void Should_BuildSchemaWithConfiguredProperties_When_PropertyBuildersAreUsed()
        {
            // Arrange & Act
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

            // Assert
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
        public static void Should_DefaultTo1_0Version_When_NoVersionIsSpecified()
        {
            // Arrange & Act
            var schema = EventSchema.Build("test.event").Build();

            // Assert
            Assert.Equal("1.0", schema.Version.ToString());
        }

        [Fact]
        public static void Should_DefaultToApplicationJson_When_NoContentTypeIsSpecified()
        {
            // Arrange & Act
            var schema = EventSchema.Build("test.event").Build();

            // Assert
            Assert.Equal("application/json", schema.ContentType);
        }

        [Fact]
        public static void Should_AddPrebuiltProperty_When_PropertyObjectIsProvided()
        {
            // Arrange
            var property = new EventPropertyBuilder("status")
                .OfType("string")
                .WithAllowedValues(new[] { "active", "inactive", "pending" })
                .Build();

            // Act
            var schema = EventSchema.Build("user.status.changed")
                .AddProperty(property)
                .Build();

            // Assert
            Assert.Single(schema.Properties);
            var p = schema.Properties["status"]!;
            Assert.Single(p.Constraints);
            Assert.Equal("allowedValues", p.Constraints[0].ConstraintType);
        }

        [Fact]
        public static void Should_BuildNestedProperties_When_NestedPropertyBuildersAreUsed()
        {
            // Arrange & Act
            var schema = EventSchema.Build("order.shipped")
                .WithVersion("1.0")
                .AddProperty("address", p => p
                    .OfType("object")
                    .AddProperty("street", nested => nested.OfType("string").Required())
                    .AddProperty("city",   nested => nested.OfType("string").Required())
                    .AddProperty("zip",    nested => nested.OfType("string")))
                .Build();

            // Assert
            var address = schema.Properties["address"]!;
            Assert.Equal(3, address.Properties.Count);
            Assert.True(address.Properties[0].IsRequired);
            Assert.Equal("street", address.Properties[0].Name);
        }

        // ── EventPropertyBuilder standalone ───────────────────────────────────

        [Fact]
        public static void Should_ThrowArgumentNullException_When_PropertyBuilderNameIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EventPropertyBuilder(null!));
        }

        [Fact]
        public static void Should_AddConstraint_When_WithConstraintIsCalled()
        {
            // Arrange & Act
            var property = new EventPropertyBuilder("score")
                .OfType("int")
                .WithConstraint(new RangeConstraint<int>(0, 10))
                .Build();

            // Assert
            Assert.Single(property.Constraints);
            Assert.Equal("range", property.Constraints[0].ConstraintType);
        }

        [Fact]
        public static void Should_AddRequiredConstraint_When_RequiredIsCalled()
        {
            // Arrange & Act
            var property = new EventPropertyBuilder("name")
                .OfType("string")
                .Required()
                .Build();

            // Assert
            Assert.True(property.IsRequired);
        }

        [Fact]
        public static void Should_AddOnlyOneConstraint_When_RequiredIsCalledTwice()
        {
            // Arrange & Act — calling Required() twice should not duplicate the constraint
            var property = new EventPropertyBuilder("name")
                .OfType("string")
                .Required()
                .Required()
                .Build();

            // Assert
            Assert.Single(property.Constraints);
        }

        [Fact]
        public static void Should_SetPropertyVersion_When_WithVersionIsCalledOnPropertyBuilder()
        {
            // Arrange & Act
            var schema = EventSchema.Build("test")
                .WithVersion("2.0")
                .AddProperty("legacy_field", p => p.OfType("string").WithVersion("1.0"))
                .Build();

            // Assert
            var prop = schema.Properties["legacy_field"]!;
            Assert.Equal("1.0", prop.Version!.ToString());
        }

        [Fact]
        public static void Should_SetDescription_When_WithDescriptionIsCalledOnPropertyBuilder()
        {
            // Arrange & Act
            var property = new EventPropertyBuilder("notes")
                .OfType("string")
                .WithDescription("Optional free-text notes")
                .Build();

            // Assert
            Assert.Equal("Optional free-text notes", property.Description);
        }

        [Fact]
        public static void Should_DefaultDataTypeToString_When_NoTypeIsSpecified()
        {
            // Arrange & Act
            var property = new EventPropertyBuilder("x").Build();

            // Assert
            Assert.Equal("string", property.DataType);
        }
    }
}
