using System.Text;

namespace Hermodr
{
    public static class JsonEventDataDeserializerTests
    {
        private static readonly JsonEventDataDeserializer Deserializer = new();

        [Fact]
        public static async Task Deserialize_SimpleObject_ReturnsDictionary()
        {
            var json = """{"name": "John", "age": 30}""";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            Assert.Equal("John", dict["name"]);
            Assert.Equal(30.0, dict["age"]);
        }

        [Fact]
        public static async Task Deserialize_NestedObject_ReturnsNestedDictionary()
        {
            var json = """{"user": {"name": "Alice", "email": "alice@test.com"}}""";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            var user = Assert.IsType<Dictionary<string, object?>>(dict["user"]);
            Assert.Equal("Alice", user["name"]);
            Assert.Equal("alice@test.com", user["email"]);
        }

        [Fact]
        public static async Task Deserialize_Array_ReturnsList()
        {
            var json = """{"tags": ["a", "b", "c"]}""";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            var tags = Assert.IsType<List<object?>>(dict["tags"]);
            Assert.Equal(3, tags.Count);
            Assert.Equal("a", tags[0]);
            Assert.Equal("b", tags[1]);
            Assert.Equal("c", tags[2]);
        }

        [Fact]
        public static async Task Deserialize_NullValue_ReturnsNull()
        {
            var json = """{"value": null}""";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            Assert.Null(dict["value"]);
        }

        [Fact]
        public static async Task Deserialize_BooleanValues_ReturnsBool()
        {
            var json = """{"active": true, "verified": false}""";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            Assert.True((bool)dict["active"]!);
            Assert.False((bool)dict["verified"]!);
        }

        [Fact]
        public static async Task Deserialize_NumberValues_ReturnsDouble()
        {
            var json = """{"int_val": 42, "float_val": 3.14}""";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            Assert.Equal(42.0, dict["int_val"]);
            Assert.Equal(3.14, (double)dict["float_val"]!, 2);
        }

        [Fact]
        public static async Task CanDeserialize_JsonContentType_ReturnsTrue()
        {
            Assert.True(Deserializer.CanDeserialize("application/json"));
            Assert.True(Deserializer.CanDeserialize("application/cloudevents+json"));
            Assert.True(Deserializer.CanDeserialize("APPLICATION/JSON"));
        }

        [Fact]
        public static async Task CanDeserialize_NonJsonContentType_ReturnsFalse()
        {
            Assert.False(Deserializer.CanDeserialize("application/xml"));
            Assert.False(Deserializer.CanDeserialize("text/plain"));
            Assert.False(Deserializer.CanDeserialize(""));
            Assert.False(Deserializer.CanDeserialize(null!));
        }

        [Fact]
        public static async Task Deserialize_EmptyStream_Throws()
        {
            var stream = new MemoryStream(Array.Empty<byte>());
            var exception = await Record.ExceptionAsync(() =>
                Deserializer.DeserializeAsync(stream, CancellationToken.None));
            Assert.NotNull(exception);
        }
    }
}
