// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Application")]
    [Trait("Feature", "EventAnnotations")]
    public static class AnnotationAttributesTests
    {
        // ─── EventAttribute ─────────────────────────────────────────────────────

        [Fact]
        public static void Should_SetDataSchema_When_SchemaUriIsProvided()
        {
            // Arrange & Act
            var attr = new EventAttribute("order.created", "https://example.com/schema/order/1.0");

            // Assert
            Assert.Equal("order.created", attr.EventType);
            Assert.NotNull(attr.DataSchema);
            Assert.Equal("https://example.com/schema/order/1.0", attr.DataSchema!.ToString());
            Assert.Null(attr.DataVersion);
        }

        [Fact]
        public static void Should_SetDataVersion_When_VersionStringIsProvided()
        {
            // Arrange & Act
            var attr = new EventAttribute("order.created", "1.0");

            // Assert
            Assert.Equal("order.created", attr.EventType);
            Assert.Null(attr.DataSchema);
            Assert.Equal("1.0", attr.DataVersion);
        }

        [Fact]
        public static void Should_SetDescription_When_DescriptionIsAssigned()
        {
            // Arrange
            var attr = new EventAttribute("order.created", "https://example.com/schema/1.0");

            // Act
            attr.Description = "An order was created";

            // Assert
            Assert.Equal("An order was created", attr.Description);
        }

        [Fact]
        public static void Should_SetContentType_When_ContentTypeIsAssigned()
        {
            // Arrange
            var attr = new EventAttribute("order.created", "https://example.com/schema/1.0");

            // Act
            attr.ContentType = "application/json";

            // Assert
            Assert.Equal("application/json", attr.ContentType);
        }

        [Fact]
        public static void Should_ThrowArgumentNullException_When_EventTypeIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EventAttribute(null!, "https://example.com/schema/1.0"));
        }

        [Fact]
        public static void Should_ThrowArgumentNullException_When_DataSchemaOrVersionIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EventAttribute("order.created", null!));
        }

        // ─── EventAttributesAttribute ────────────────────────────────────────────

        [Fact]
        public static void Should_SetAttributeNameAndValue_When_ConstructedWithBothArgs()
        {
            // Arrange & Act
            var attr = new EventAttributesAttribute("streamtype", "order");

            // Assert
            Assert.Equal("streamtype", attr.AttributeName);
            Assert.Equal("order", attr.Value);
        }

        [Fact]
        public static void Should_AllowNullValue_When_ValueIsNotProvided()
        {
            // Arrange & Act
            var attr = new EventAttributesAttribute("streamtype", null);

            // Assert
            Assert.Equal("streamtype", attr.AttributeName);
            Assert.Null(attr.Value);
        }

        [Fact]
        public static void Should_ThrowArgumentNullException_When_AttributeNameIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EventAttributesAttribute(null!, "order"));
        }

        [Fact]
        public static void Should_SetIntValue_When_IntegerValueIsProvided()
        {
            // Arrange & Act
            var attr = new EventAttributesAttribute("priority", 1);

            // Assert
            Assert.Equal(1, attr.Value);
        }

        // ─── EventPropertyAttribute ──────────────────────────────────────────────

        [Fact]
        public static void Should_SetSchema_When_SchemaUriIsProvided()
        {
            // Arrange & Act
            var attr = new EventPropertyAttribute("orderId", "https://example.com/schema/props/orderId");

            // Assert
            Assert.Equal("orderId", attr.Name);
            Assert.NotNull(attr.Schema);
            Assert.Equal("https://example.com/schema/props/orderId", attr.Schema!.ToString());
        }

        [Fact]
        public static void Should_SetVersion_When_VersionStringIsProvided()
        {
            // Arrange & Act
            var attr = new EventPropertyAttribute("orderId", "1.2.3");

            // Assert
            Assert.Equal("orderId", attr.Name);
            Assert.Equal("1.2.3", attr.Version);
        }

        [Fact]
        public static void Should_AllowNullSchemaAndVersion_When_ConstructedWithNameOnly()
        {
            // Arrange & Act
            var attr = new EventPropertyAttribute("orderId");

            // Assert
            Assert.Equal("orderId", attr.Name);
            Assert.Null(attr.Schema);
            Assert.Null(attr.Version);
        }

        [Fact]
        public static void Should_ThrowArgumentException_When_SchemaOrVersionIsInvalid()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                new EventPropertyAttribute("orderId", "not-a-uri-or-version"));
        }

        [Fact]
        public static void Should_SetDescription_When_DescriptionIsAssignedToPropertyAttribute()
        {
            // Arrange
            var attr = new EventPropertyAttribute("orderId");

            // Act
            attr.Description = "The unique order identifier";

            // Assert
            Assert.Equal("The unique order identifier", attr.Description);
        }

        [Fact]
        public static void Should_SetSchemaDirectly_When_SchemaUriIsAssigned()
        {
            // Arrange
            var attr = new EventPropertyAttribute("orderId");

            // Act
            attr.Schema = new Uri("https://example.com/schema/props/orderId");

            // Assert
            Assert.Equal("https://example.com/schema/props/orderId", attr.Schema!.ToString());
        }

        [Fact]
        public static void Should_SetVersionDirectly_When_VersionStringIsAssigned()
        {
            // Arrange
            var attr = new EventPropertyAttribute("orderId");

            // Act
            attr.Version = "2.0";

            // Assert
            Assert.Equal("2.0", attr.Version);
        }
    }
}
