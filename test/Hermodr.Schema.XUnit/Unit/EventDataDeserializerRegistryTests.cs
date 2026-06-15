namespace Hermodr
{
    public static class EventDataDeserializerRegistryTests
    {
        private static EventDataDeserializerRegistry CreateRegistry()
        {
            var registry = new EventDataDeserializerRegistry();
            registry.Register(new JsonEventDataDeserializer());
            registry.Register(new XmlEventDataDeserializer());
            return registry;
        }

        [Fact]
        public static void Register_And_Retrieve_ExactMatch()
        {
            var registry = CreateRegistry();

            var deserializer = registry.GetDeserializer("application/json");

            Assert.NotNull(deserializer);
            Assert.IsType<JsonEventDataDeserializer>(deserializer);
        }

        [Fact]
        public static void Register_And_Retrieve_CaseInsensitive()
        {
            var registry = CreateRegistry();

            var deserializer = registry.GetDeserializer("APPLICATION/JSON");

            Assert.NotNull(deserializer);
            Assert.IsType<JsonEventDataDeserializer>(deserializer);
        }

        [Fact]
        public static void GetDeserializer_WildcardPrefixMatch()
        {
            var registry = CreateRegistry();

            var deserializer = registry.GetDeserializer("application/vnd.custom+json");

            Assert.NotNull(deserializer);
            Assert.IsType<JsonEventDataDeserializer>(deserializer);
        }

        [Fact]
        public static void GetDeserializer_UnknownContentType_ReturnsNull()
        {
            var registry = CreateRegistry();

            var deserializer = registry.GetDeserializer("text/plain");

            Assert.Null(deserializer);
        }

        [Fact]
        public static void GetDeserializer_NullOrEmpty_ReturnsNull()
        {
            var registry = CreateRegistry();

            Assert.Null(registry.GetDeserializer(null!));
            Assert.Null(registry.GetDeserializer(""));
        }

        [Fact]
        public static void Register_CustomDeserializer_CanBeRetrieved()
        {
            var registry = CreateRegistry();
            var custom = new CustomTestDeserializer();
            registry.Register(custom);

            var deserializer = registry.GetDeserializer("application/x-protobuf");

            Assert.NotNull(deserializer);
            Assert.IsType<CustomTestDeserializer>(deserializer);
        }

        [Fact]
        public static void Register_OverwritesExisting()
        {
            var registry = CreateRegistry();
            var custom = new CustomTestDeserializer();
            registry.Register(custom);

            var deserializer = registry.GetDeserializer("application/json");

            Assert.NotNull(deserializer);
            Assert.IsType<CustomTestDeserializer>(deserializer);
        }

        private sealed class CustomTestDeserializer : IEventDataDeserializer
        {
            public string ContentType => "application/json";

            public bool CanDeserialize(string contentType)
                => contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);

            public Task<object?> DeserializeAsync(Stream data, CancellationToken cancellationToken)
                => Task.FromResult<object?>(new Dictionary<string, object?>());
        }
    }
}
