//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

using Deveel.Filters;

namespace Hermodr
{
    /// <summary>
    /// Defines a service that can deserialize the data payload of a <see cref="CloudEvent"/>
    /// into a <see cref="JsonElement"/>, enabling JSON-path–based filtering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations are resolved from the DI container by
    /// <see cref="EventSubscriptionContext.GetJsonData"/> when a <see cref="CloudEvent"/> is
    /// being evaluated by a <see cref="FilterExpression"/>.  The first registered implementation
    /// whose <see cref="CanDeserialize"/> method returns <c>true</c> for the event's
    /// <c>datacontenttype</c> is used.
    /// </para>
    /// <para>
    /// When no DI-registered deserializer matches, the context falls back to its built-in
    /// JSON deserializer which handles plain JSON strings, already-parsed
    /// <see cref="JsonElement"/> objects, and CLR objects serializable with
    /// <see cref="System.Text.Json.JsonSerializer"/>.
    /// </para>
    /// </remarks>
    public interface IEventDataDeserializer
    {
        /// <summary>
        /// Returns <c>true</c> when this deserializer is capable of handling the given
        /// <paramref name="contentType"/> (e.g. <c>"application/json"</c>).
        /// </summary>
        /// <param name="contentType">
        /// The value of the CloudEvent <c>datacontenttype</c> attribute, or <c>null</c> when the
        /// attribute is absent.
        /// </param>
        bool CanDeserialize(string? contentType);

        /// <summary>
        /// Attempts to deserialize the data payload of <paramref name="event"/> into a
        /// <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="event">The <see cref="CloudEvent"/> whose data should be deserialized.</param>
        /// <param name="element">
        /// When this method returns <c>true</c>, contains the deserialized root element;
        /// otherwise the value is undefined.
        /// </param>
        /// <returns>
        /// <c>true</c> when deserialization succeeded; <c>false</c> when the payload cannot be
        /// represented as a <see cref="JsonElement"/> (e.g. binary data, missing content, parse
        /// errors).
        /// </returns>
        bool TryDeserialize(CloudEvent @event, out JsonElement element);
    }
}

