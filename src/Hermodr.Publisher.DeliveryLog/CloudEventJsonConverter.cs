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

    /// <summary>Reads a structured-mode CloudEvents JSON payload into a <see cref="CloudEvent"/> instance.</summary>
    /// <param name="reader">The UTF-8 JSON reader positioned at the CloudEvent value.</param>
    /// <param name="typeToConvert">The target type to convert to (always <see cref="CloudEvent"/>).</param>
    /// <param name="options">The JSON serialiser options in effect.</param>
    /// <returns>A <see cref="CloudEvent"/> decoded from the JSON payload, or <c>null</c> when the token is JSON null.</returns>
    public override CloudEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var bytes = System.Text.Encoding.UTF8.GetBytes(doc.RootElement.GetRawText());
        return Formatter.DecodeStructuredModeMessage(bytes, CloudEventsContentType, null);
    }

    /// <summary>Writes a <see cref="CloudEvent"/> as a structured-mode CloudEvents JSON payload.</summary>
    /// <param name="writer">The UTF-8 JSON writer to write to.</param>
    /// <param name="value">The CloudEvent instance to serialise.</param>
    /// <param name="options">The JSON serialiser options in effect.</param>
    public override void Write(Utf8JsonWriter writer, CloudEvent value, JsonSerializerOptions options)
    {
        var bytes = Formatter.EncodeStructuredModeMessage(value, out _);
        using var doc = JsonDocument.Parse(bytes);
        doc.RootElement.WriteTo(writer);
    }
}
