using System.Text.Json;
using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Navigates a dot-separated property path inside a JSON body and compares the leaf value
/// with an <see cref="EventAttributeFilter"/>.
/// </summary>
/// <remarks>
/// Example path: <c>"order.customer.tier"</c> drills into
/// <c>{ "order": { "customer": { "tier": "gold" } } }</c>.
/// Array indices are not supported; each segment must be an object property name.
/// </remarks>
public sealed class JsonPathDataFilter : EventDataFilter
{
    private readonly string[] _segments;
    private readonly EventAttributeFilter _valueFilter;

    /// <summary>
    /// Initialises the filter with a dot-separated <paramref name="path"/> and a
    /// <paramref name="valueFilter"/> applied to the leaf element.
    /// </summary>
    public JsonPathDataFilter(string path, EventAttributeFilter valueFilter)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));
        _segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        _valueFilter = valueFilter ?? throw new ArgumentNullException(nameof(valueFilter));
    }

    /// <summary>Gets the dotted property path used to locate the leaf element.</summary>
    public string Path => string.Join('.', _segments);

    /// <summary>Gets the filter applied to the leaf element's string representation.</summary>
    public EventAttributeFilter ValueFilter => _valueFilter;

    /// <inheritdoc/>
    public override bool Matches(CloudEvent @event)
    {
        if (!JsonEventDataDeserializer.TryGetJsonElement(@event, out var root))
            return false;

        var current = root;
        foreach (var seg in _segments)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return false;
            if (!current.TryGetProperty(seg, out current))
                return false;
        }

        var value = current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : current.ToString();

        return _valueFilter.Matches(value);
    }
}