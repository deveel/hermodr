using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Hermodr;

/// <summary>
/// A custom JSON converter for serializing and deserializing <see cref="CloudEvent"/> instances
/// using the CloudEvents structured-mode JSON format.
/// </summary>
public class CloudEventJsonConverter : JsonConverter<CloudEvent>
{
    private static readonly JsonEventFormatter Formatter = new();
    private static readonly ContentType CloudEventsContentType = new("application/cloudevents+json");

    public override CloudEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var bytes = System.Text.Encoding.UTF8.GetBytes(doc.RootElement.GetRawText());
        return Formatter.DecodeStructuredModeMessage(bytes, CloudEventsContentType, null);
    }

    public override void Write(Utf8JsonWriter writer, CloudEvent value, JsonSerializerOptions options)
    {
        var bytes = Formatter.EncodeStructuredModeMessage(value, out _);
        using var doc = JsonDocument.Parse(bytes);
        doc.RootElement.WriteTo(writer);
    }
}
