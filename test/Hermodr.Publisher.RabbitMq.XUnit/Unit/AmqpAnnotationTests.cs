// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Hermodr
{
    /// <summary>
    /// Unit tests for the AMQP annotation attributes and constants 
    /// defined in <c>Hermodr.Amqp.Annotations</c>.
    /// </summary>
    [Trait("Package", "Amqp.Annotations")]
    public static class AmqpAnnotationTests
    {
        // ── AmqpCloudEventAttributes constants ────────────────────────────────

        [Fact]
        public static void AmqpCloudEventAttributes_ExchangeNameAttribute_HasExpectedValue()
        {
            Assert.Equal("amqpexchange", AmqpCloudEventAttributes.AmqpExchangeNameAttribute);
        }

        [Fact]
        public static void AmqpCloudEventAttributes_RoutingKeyAttribute_HasExpectedValue()
        {
            Assert.Equal("amqproutingkey", AmqpCloudEventAttributes.AmqpRoutingKeyAttribute);
        }

        // ── AmqpExchangeAttribute ─────────────────────────────────────────────

        [Fact]
        public static void AmqpExchangeAttribute_Constructor_SetsAttributeNameAndValue()
        {
            var attr = new AmqpExchangeAttribute("my-exchange");

            Assert.Equal(AmqpCloudEventAttributes.AmqpExchangeNameAttribute, attr.AttributeName);
            Assert.Equal("my-exchange", attr.Value);
        }

        [Fact]
        public static void AmqpExchangeAttribute_NullExchangeName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AmqpExchangeAttribute(null!));
        }

        [Fact]
        public static void AmqpExchangeAttribute_IsEventAttributesAttribute()
        {
            var attr = new AmqpExchangeAttribute("events");
            Assert.IsAssignableFrom<EventAttributesAttribute>(attr);
        }

        [Fact]
        public static void AmqpExchangeAttribute_EmptyExchangeName_IsAllowed()
        {
            // Empty string is not null, so no exception should be thrown
            var attr = new AmqpExchangeAttribute(string.Empty);
            Assert.Equal(string.Empty, attr.Value);
        }

        // ── AmqpRoutingKeyAttribute ───────────────────────────────────────────

        [Fact]
        public static void AmqpRoutingKeyAttribute_Constructor_SetsAttributeNameAndValue()
        {
            var attr = new AmqpRoutingKeyAttribute("order.created");

            Assert.Equal(AmqpCloudEventAttributes.AmqpRoutingKeyAttribute, attr.AttributeName);
            Assert.Equal("order.created", attr.Value);
        }

        [Fact]
        public static void AmqpRoutingKeyAttribute_NullRoutingKey_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AmqpRoutingKeyAttribute(null!));
        }

        [Fact]
        public static void AmqpRoutingKeyAttribute_IsEventAttributesAttribute()
        {
            var attr = new AmqpRoutingKeyAttribute("test.key");
            Assert.IsAssignableFrom<EventAttributesAttribute>(attr);
        }

        [Fact]
        public static void AmqpRoutingKeyAttribute_EmptyRoutingKey_IsAllowed()
        {
            var attr = new AmqpRoutingKeyAttribute(string.Empty);
            Assert.Equal(string.Empty, attr.Value);
        }

        // ── Applied to a class via reflection ────────────────────────────────

        [Fact]
        public static void AmqpExchangeAttribute_AppliedToClass_CanBeRetrievedViaReflection()
        {
            var attrs = typeof(AnnotatedEvent).GetCustomAttributes(typeof(AmqpExchangeAttribute), inherit: true);

            Assert.Single(attrs);
            var exchangeAttr = (AmqpExchangeAttribute)attrs[0];
            Assert.Equal("domain-events", exchangeAttr.Value);
        }

        [Fact]
        public static void AmqpRoutingKeyAttribute_AppliedToClass_CanBeRetrievedViaReflection()
        {
            var attrs = typeof(AnnotatedEvent).GetCustomAttributes(typeof(AmqpRoutingKeyAttribute), inherit: true);

            Assert.Single(attrs);
            var routingAttr = (AmqpRoutingKeyAttribute)attrs[0];
            Assert.Equal("order.placed", routingAttr.Value);
        }

        [Event("order.placed", "https://example.com/events/order.placed/1.0")]
        [AmqpExchange("domain-events")]
        [AmqpRoutingKey("order.placed")]
        private class AnnotatedEvent
        {
            public string OrderId { get; set; } = string.Empty;
        }
    }
}

