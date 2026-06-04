namespace Hermodr;

/// <summary>
/// A set of standard CloudEvents attribute names that are not considered extension attributes.
/// </summary>
internal static class StandardCloudEventAttributes
{
    private static readonly HashSet<string> Names =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "specversion", "id", "type", "source",
            "time", "subject", "datacontenttype", "dataschema"
        };

    /// <summary>
    /// Determines whether the given attribute name is a standard CloudEvents attribute.
    /// </summary>
    public static bool IsStandard(string name) => Names.Contains(name);
}