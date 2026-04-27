using System.Text.Json;
using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Applies a caller-supplied <c>Func&lt;JsonElement, bool&gt;</c> to the root of the JSON
/// event body.  Returns <c>false</c> when the body cannot be represented as JSON.
/// </summary>
public sealed class JsonPredicateDataFilter : EventDataFilter
{
    private readonly Func<JsonElement, bool> _predicate;

    /// <summary>Initialises the filter with the given <paramref name="predicate"/>.</summary>
    public JsonPredicateDataFilter(Func<JsonElement, bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <inheritdoc/>
    public override bool Matches(CloudEvent @event)
    {
        if (!JsonEventDataDeserializer.TryGetJsonElement(@event, out var element))
            return false;

        try
        {
            return _predicate(element);
        }
        catch
        {
            return false;
        }
    }
}