//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Assembly-level attribute that designates a static provider type whose
    /// <c>GetOptions()</c> method supplies the <see cref="System.Text.Json.JsonSerializerOptions"/>
    /// used by source-generated <c>ToCloudEvent()</c> implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Place this attribute in any source file of the consuming assembly:
    /// <code>
    /// [assembly: EventJsonSerializationOptions(typeof(MyApp.MyJsonOptions))]
    /// </code>
    /// </para>
    /// <para>
    /// The referenced type must expose a public static method with the following signature:
    /// <code>
    /// public static System.Text.Json.JsonSerializerOptions GetOptions();
    /// </code>
    /// Example:
    /// <code>
    /// public static class MyJsonOptions
    /// {
    ///     public static JsonSerializerOptions GetOptions() =>
    ///         new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// When this attribute is present the generator emits a direct static call to
    /// <c>ProviderType.GetOptions()</c> instead of reading
    /// <see cref="EventGeneratorContext.JsonSerializerOptions"/> at runtime.
    /// When the attribute is absent, the runtime context value is used, which is
    /// seeded by the active <c>EventPublisher</c> instance.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class EventJsonSerializationOptionsAttribute : Attribute
    {
        /// <summary>
        /// Initialises a new instance of <see cref="EventJsonSerializationOptionsAttribute"/>.
        /// </summary>
        /// <param name="providerType">
        /// The type that exposes a <c>public static JsonSerializerOptions GetOptions()</c> method.
        /// </param>
        public EventJsonSerializationOptionsAttribute(Type providerType)
        {
            ProviderType = providerType;
        }

        /// <summary>
        /// Gets the type whose static <c>GetOptions()</c> method provides the
        /// <see cref="System.Text.Json.JsonSerializerOptions"/> for serialisation.
        /// </summary>
        public Type ProviderType { get; }
    }
}

