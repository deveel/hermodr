//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Assembly-level attribute that supplies a base URI describing where event data
    /// schemas are rooted for the current assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Place this attribute in any source file of the consuming assembly:
    /// <code>
    /// [assembly: EventDataSchemaUri("https://schemas.example.com/events")]
    /// </code>
    /// </para>
    /// <para>
    /// This metadata can be consumed by generators, runtime components, or other
    /// tooling to resolve or compose schema URIs consistently.
    /// </para>
    /// <para>
    /// The interpretation of the value is determined by the consuming component.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class EventDataSchemaUriAttribute : Attribute
    {
        /// <summary>
        /// Initialises a new instance of <see cref="EventDataSchemaUriAttribute"/>.
        /// </summary>
        /// <param name="baseUri">
        /// The absolute base URI (e.g. <c>https://schemas.example.com/events</c>).
        /// Must be a valid absolute URI string.
        /// </param>
        public EventDataSchemaUriAttribute(string baseUri)
        {
            if (string.IsNullOrWhiteSpace(baseUri))
                throw new ArgumentException("The value cannot be null, empty, or whitespace.", nameof(baseUri));

            if (!Uri.TryCreate(baseUri, UriKind.Absolute, out _))
                throw new ArgumentException("The value must be a valid absolute URI.", nameof(baseUri));

            BaseUri = baseUri;
        }

        /// <summary>
        /// Gets the absolute base URI value declared by this attribute.
        /// </summary>
        public string BaseUri { get; }
    }
}


