// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Deveel.Events
{
    public static class AnnotationAttributesTests
    {
        // ─── EventAttribute ─────────────────────────────────────────────────────

        [Fact]
        public static void EventAttribute_WithSchemaUri_SetsDataSchema()
        {
            var attr = new EventAttribute("order.created", "https://example.com/schema/order/1.0");
            Assert.Equal("order.created", attr.EventType);
            Assert.NotNull(attr.DataSchema);
            Assert.Equal("https://example.com/schema/order/1.0", attr.DataSchema!.ToString());
            Assert.Null(attr.DataVersion);
        }

        [Fact]
        public static void EventAttribute_WithVersionString_SetsDataVersion()
        {
            var attr = new EventAttribute("order.created", "1.0");
            Assert.Equal("order.created", attr.EventType);
            Assert.Null(attr.DataSchema);
            Assert.Equal("1.0", attr.DataVersion);
        }

        [Fact]
        public static void EventAttribute_Description_CanBeSet()
        {
            var attr = new EventAttribute("order.created", "https://example.com/schema/1.0")
            {
                Description = "An order was created"
            };
            Assert.Equal("An order was created", attr.Description);
        }

        [Fact]
        public static void EventAttribute_ContentType_CanBeSet()
        {
            var attr = new EventAttribute("order.created", "https://example.com/schema/1.0")
            {
                ContentType = "application/json"
            };
            Assert.Equal("application/json", attr.ContentType);
        }

        [Fact]
        public static void EventAttribute_NullEventType_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EventAttribute(null!, "https://example.com/schema/1.0"));
        }

        [Fact]
        public static void EventAttribute_NullDataSchemaOrVersion_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EventAttribute("order.created", null!));
        }

        // ─── EventAttributesAttribute ────────────────────────────────────────────

        [Fact]
        public static void EventAttributesAttribute_Constructor_SetsProperties()
        {
            var attr = new EventAttributesAttribute("streamtype", "order");
            Assert.Equal("streamtype", attr.AttributeName);
            Assert.Equal("order", attr.Value);
        }

        [Fact]
        public static void EventAttributesAttribute_NullValue_IsAllowed()
        {
            var attr = new EventAttributesAttribute("streamtype", null);
            Assert.Equal("streamtype", attr.AttributeName);
            Assert.Null(attr.Value);
        }

        [Fact]
        public static void EventAttributesAttribute_NullAttributeName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EventAttributesAttribute(null!, "order"));
        }

        [Fact]
        public static void EventAttributesAttribute_IntValue_IsSet()
        {
            var attr = new EventAttributesAttribute("priority", 1);
            Assert.Equal(1, attr.Value);
        }

        // ─── EventPropertyAttribute ──────────────────────────────────────────────

        [Fact]
        public static void EventPropertyAttribute_WithSchemaUri_SetsSchema()
        {
            var attr = new EventPropertyAttribute("orderId", "https://example.com/schema/props/orderId");
            Assert.Equal("orderId", attr.Name);
            Assert.NotNull(attr.Schema);
            Assert.Equal("https://example.com/schema/props/orderId", attr.Schema!.ToString());
        }

        [Fact]
        public static void EventPropertyAttribute_WithVersionString_SetsVersion()
        {
            var attr = new EventPropertyAttribute("orderId", "1.2.3");
            Assert.Equal("orderId", attr.Name);
            Assert.Equal("1.2.3", attr.Version);
        }

        [Fact]
        public static void EventPropertyAttribute_NullSchemaOrVersion_IsAllowed()
        {
            var attr = new EventPropertyAttribute("orderId");
            Assert.Equal("orderId", attr.Name);
            Assert.Null(attr.Schema);
            Assert.Null(attr.Version);
        }

        [Fact]
        public static void EventPropertyAttribute_InvalidSchemaOrVersion_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new EventPropertyAttribute("orderId", "not-a-uri-or-version"));
        }

        [Fact]
        public static void EventPropertyAttribute_Description_CanBeSet()
        {
            var attr = new EventPropertyAttribute("orderId")
            {
                Description = "The unique order identifier"
            };
            Assert.Equal("The unique order identifier", attr.Description);
        }

        [Fact]
        public static void EventPropertyAttribute_Schema_CanBeSetDirectly()
        {
            var attr = new EventPropertyAttribute("orderId")
            {
                Schema = new Uri("https://example.com/schema/props/orderId")
            };
            Assert.Equal("https://example.com/schema/props/orderId", attr.Schema!.ToString());
        }

        [Fact]
        public static void EventPropertyAttribute_Version_CanBeSetDirectly()
        {
            var attr = new EventPropertyAttribute("orderId")
            {
                Version = "2.0"
            };
            Assert.Equal("2.0", attr.Version);
        }
    }
}

