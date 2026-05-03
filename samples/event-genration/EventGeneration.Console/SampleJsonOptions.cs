using System.Text.Json;

namespace EventGeneration.ConsoleSample;

/// <summary>
/// Provides the <see cref="JsonSerializerOptions"/> used by every generated
/// <c>ToCloudEvent()</c> implementation in this assembly.
///
/// Referenced by the assembly-level attribute in <c>Program.cs</c>:
/// <code>[assembly: EventJsonSerializationOptions(typeof(SampleJsonOptions))]</code>
///
/// The source generator emits a direct static call to <c>SampleJsonOptions.GetOptions()</c>
/// instead of reading <c>EventGeneratorContext.JsonSerializerOptions</c> at runtime.
/// </summary>
public static class SampleJsonOptions
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        WriteIndented               = false,
        DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Returns the shared <see cref="JsonSerializerOptions"/> instance.</summary>
    public static JsonSerializerOptions GetOptions() => _options;
}

