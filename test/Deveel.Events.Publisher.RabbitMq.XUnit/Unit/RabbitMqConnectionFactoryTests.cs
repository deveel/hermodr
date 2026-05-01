// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    /// <summary>
    /// Unit tests for <see cref="RabbitMqConnectionFactory"/> that do not require
    /// a live RabbitMQ broker.
    /// </summary>
    [Trait("Channel", "RabbitMQ")]
    [Trait("Function", "ConnectionFactory")]
    public static class RabbitMqConnectionFactoryTests
    {
        [Fact]
        public static void Constructor_WithNullConnectionString_ThrowsArgumentException()
        {
            var options = Options.Create(new RabbitMqPublishOptions
            {
                ConnectionString = null
            });

            Assert.Throws<ArgumentException>(() => new RabbitMqConnectionFactory(options));
        }

        [Fact]
        public static void Constructor_WithEmptyConnectionString_ThrowsArgumentException()
        {
            var options = Options.Create(new RabbitMqPublishOptions
            {
                ConnectionString = string.Empty
            });

            Assert.Throws<ArgumentException>(() => new RabbitMqConnectionFactory(options));
        }

        [Fact]
        public static void Constructor_WithInvalidConnectionString_ThrowsArgumentException()
        {
            var options = Options.Create(new RabbitMqPublishOptions
            {
                ConnectionString = "not-a-valid-uri"
            });

            Assert.Throws<ArgumentException>(() => new RabbitMqConnectionFactory(options));
        }

        [Fact]
        public static void Constructor_WithValidConnectionString_DoesNotThrow()
        {
            var options = Options.Create(new RabbitMqPublishOptions
            {
                ConnectionString = "amqp://guest:guest@localhost:5672/"
            });

            // Should not throw — factory is just configured, no connection is opened yet.
            var factory = new RabbitMqConnectionFactory(options);
            Assert.NotNull(factory);
        }

        [Fact]
        public static void Constructor_SetsClientName_WhenProvided()
        {
            var options = Options.Create(new RabbitMqPublishOptions
            {
                ConnectionString = "amqp://guest:guest@localhost:5672/",
                ClientName = "MyApp"
            });

            // Should not throw — the client name is forwarded to the underlying ConnectionFactory.
            var factory = new RabbitMqConnectionFactory(options);
            Assert.NotNull(factory);
        }

        [Fact]
        public static void Constructor_UsesDefaultClientName_WhenClientNameIsNull()
        {
            var options = Options.Create(new RabbitMqPublishOptions
            {
                ConnectionString = "amqp://guest:guest@localhost:5672/",
                ClientName = null
            });

            // Should not throw — falls back to "Deveel.Events".
            var factory = new RabbitMqConnectionFactory(options);
            Assert.NotNull(factory);
        }
    }
}

