using System.Text;

namespace Hermodr
{
    public static class XmlEventDataDeserializerTests
    {
        private static readonly XmlEventDataDeserializer Deserializer = new();

        [Fact]
        public static async Task Deserialize_SimpleElement_ReturnsDictionary()
        {
            var xml = """<root><name>John</name><age>30</age></root>""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            Assert.Equal("John", dict["name"]);
            Assert.Equal("30", dict["age"]);
        }

        [Fact]
        public static async Task Deserialize_NestedElements_ReturnsNestedDictionary()
        {
            var xml = """<root><user><name>Alice</name><email>alice@test.com</email></user></root>""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            var user = Assert.IsType<Dictionary<string, object?>>(dict["user"]);
            Assert.Equal("Alice", user["name"]);
            Assert.Equal("alice@test.com", user["email"]);
        }

        [Fact]
        public static async Task Deserialize_AttributesAsProperties_ReturnsDictionary()
        {
            var xml = """<order id="123" status="active"></order>""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            Assert.Equal("123", dict["id"]);
            Assert.Equal("active", dict["status"]);
        }

        [Fact]
        public static async Task Deserialize_AttributeAndElementConflict_AttributeWins()
        {
            var xml = """<order id="attr-value"><id>element-value</id></order>""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            Assert.Equal("attr-value", dict["id"]);
        }

        [Fact]
        public static async Task Deserialize_ElementWithAttributes_ReturnsDictionary()
        {
            var xml = """<person id="1"><name>Bob</name><age>25</age></person>""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            Assert.Equal("1", dict["id"]);
            Assert.Equal("Bob", dict["name"]);
            Assert.Equal("25", dict["age"]);
        }

        [Fact]
        public static async Task Deserialize_EmptyElement_ReturnsEmptyString()
        {
            var xml = """<root><value></value></root>""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            Assert.Equal("", dict["value"]);
        }

        [Fact]
        public static async Task Deserialize_ElementWithText_ReturnsString()
        {
            var xml = """<root><message>Hello World</message></root>""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var result = await Deserializer.DeserializeAsync(stream, CancellationToken.None);

            var dict = Assert.IsType<Dictionary<string, object?>>(result);
            Assert.Equal("Hello World", dict["message"]);
        }

        [Fact]
        public static async Task CanDeserialize_XmlContentType_ReturnsTrue()
        {
            Assert.True(Deserializer.CanDeserialize("application/xml"));
            Assert.True(Deserializer.CanDeserialize("APPLICATION/XML"));
        }

        [Fact]
        public static async Task CanDeserialize_NonXmlContentType_ReturnsFalse()
        {
            Assert.False(Deserializer.CanDeserialize("application/json"));
            Assert.False(Deserializer.CanDeserialize("text/plain"));
            Assert.False(Deserializer.CanDeserialize(""));
            Assert.False(Deserializer.CanDeserialize(null!));
        }

        [Fact]
        public static async Task Deserialize_EmptyStream_Throws()
        {
            using var stream = new MemoryStream(Array.Empty<byte>());
            await Assert.ThrowsAsync<System.Xml.XmlException>(() =>
                Deserializer.DeserializeAsync(stream, CancellationToken.None));
        }
    }
}
