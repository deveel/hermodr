using System.Reflection;

namespace Hermodr
{
    public static class EventSchemaRegistryTests
    {
        private static EventSchemaRegistry CreateRegistry()
        {
            var factory = new EventSchemaFactory();
            return new EventSchemaRegistry(factory);
        }

        [Fact]
        public static void Register_SchemaInstance_CanBeRetrieved()
        {
            var registry = CreateRegistry();
            var schema = new EventSchema("test.event", "1.0", "application/json");
            registry.Register(schema);

            var retrieved = registry.GetSchema("test.event", "1.0");

            Assert.NotNull(retrieved);
            Assert.Equal("test.event", retrieved.EventType);
            Assert.Equal("1.0", retrieved.Version.ToString());
        }

        [Fact]
        public static void Register_FromClrType_CanBeRetrieved()
        {
            var registry = CreateRegistry();
            registry.Register<TestEvent>();

            var schema = registry.GetSchema("test.event", "1.0");

            Assert.NotNull(schema);
            Assert.Equal("test.event", schema.EventType);
            Assert.Equal("1.0", schema.Version.ToString());
            Assert.True(schema.Properties.Any(p => p.Name == "name"));
        }

        [Fact]
        public static void GetSchema_LatestVersion_WhenNoVersionSpecified()
        {
            var registry = CreateRegistry();
            registry.Register(new EventSchema("test.event", "1.0", "application/json"));
            registry.Register(new EventSchema("test.event", "2.0", "application/json"));

            var schema = registry.GetSchema("test.event");

            Assert.NotNull(schema);
            Assert.Equal("2.0", schema.Version.ToString());
        }

        [Fact]
        public static void GetSchema_ExactVersion_ReturnsCorrectVersion()
        {
            var registry = CreateRegistry();
            registry.Register(new EventSchema("test.event", "1.0", "application/json"));
            registry.Register(new EventSchema("test.event", "2.0", "application/json"));

            var schema = registry.GetSchema("test.event", "1.0");

            Assert.NotNull(schema);
            Assert.Equal("1.0", schema.Version.ToString());
        }

        [Fact]
        public static void GetSchema_UnknownEventType_ReturnsNull()
        {
            var registry = CreateRegistry();
            registry.Register(new EventSchema("test.event", "1.0", "application/json"));

            var schema = registry.GetSchema("unknown.event");

            Assert.Null(schema);
        }

        [Fact]
        public static void GetSchema_UnknownVersion_ReturnsNull()
        {
            var registry = CreateRegistry();
            registry.Register(new EventSchema("test.event", "1.0", "application/json"));

            var schema = registry.GetSchema("test.event", "99.0");

            Assert.Null(schema);
        }

        [Fact]
        public static void GetAllVersions_ReturnsAllVersionsOrdered()
        {
            var registry = CreateRegistry();
            registry.Register(new EventSchema("test.event", "1.0", "application/json"));
            registry.Register(new EventSchema("test.event", "3.0", "application/json"));
            registry.Register(new EventSchema("test.event", "2.0", "application/json"));

            var versions = registry.GetAllVersions("test.event");

            Assert.Equal(3, versions.Count);
            Assert.Equal("3.0", versions[0].Version.ToString());
            Assert.Equal("2.0", versions[1].Version.ToString());
            Assert.Equal("1.0", versions[2].Version.ToString());
        }

        [Fact]
        public static void GetAllVersions_UnknownEventType_ReturnsEmpty()
        {
            var registry = CreateRegistry();

            var versions = registry.GetAllVersions("unknown.event");

            Assert.Empty(versions);
        }

        [Fact]
        public static void ScanAssembly_RegistersAllEventTypes()
        {
            var registry = CreateRegistry();
            var assembly = typeof(EventSchemaRegistryTests).Assembly;

            registry.ScanAssembly(assembly);

            // TestEvent is defined in this file
            var schema = registry.GetSchema("test.event", "1.0");
            Assert.NotNull(schema);
        }

        [Fact]
        public static void Register_MultipleEventTypes_AllRetrievable()
        {
            var registry = CreateRegistry();
            registry.Register(new EventSchema("order.placed", "1.0", "application/json"));
            registry.Register(new EventSchema("user.registered", "1.0", "application/json"));

            Assert.NotNull(registry.GetSchema("order.placed", "1.0"));
            Assert.NotNull(registry.GetSchema("user.registered", "1.0"));
        }

        [Fact]
        public static void GetSchema_NullEventType_ReturnsNull()
        {
            var registry = CreateRegistry();

            Assert.Null(registry.GetSchema(null!));
            Assert.Null(registry.GetSchema(""));
        }

        [Event("test.event", "1.0")]
        private sealed class TestEvent
        {
            [EventProperty("name")]
            public string Name { get; set; } = "";
        }
    }
}
