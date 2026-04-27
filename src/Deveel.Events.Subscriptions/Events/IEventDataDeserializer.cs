//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Contract for deserializing the data payload of a <see cref="CloudEvent"/> into
/// a typed CLR object.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to support your wire format (Protobuf, MessagePack, Avro,
/// custom binary, etc.) and register the implementation with an
/// <see cref="EventDataDeserializerProvider"/> so that <see cref="TypedDataFilter{T}"/>
/// can dispatch to it based on the event's <c>datacontenttype</c> attribute.
/// </para>
/// <para>
/// Two built-in implementations are provided:
/// <list type="bullet">
///   <item><description>
///     <see cref="JsonEventDataDeserializer"/> — handles any content type containing
///     <c>"json"</c> (e.g. <c>application/json</c>).
///   </description></item>
/// </list>
/// </para>
/// </remarks>
public interface IEventDataDeserializer
{
    /// <summary>
    /// Returns <c>true</c> when this deserializer is capable of handling the given
    /// <paramref name="contentType"/>.
    /// </summary>
    /// <param name="contentType">
    /// The value of the CloudEvents <c>datacontenttype</c> attribute, or <c>null</c>
    /// when the attribute is absent.
    /// </param>
    bool CanDeserialize(string? contentType);

    /// <summary>
    /// Attempts to deserialize the data payload of <paramref name="event"/> to an
    /// instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Target CLR type (must be a reference type).</typeparam>
    /// <param name="event">The cloud event whose payload is to be deserialized.</param>
    /// <param name="result">
    /// When the method returns <c>true</c>, the deserialized object; otherwise <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> when deserialization succeeded and <paramref name="result"/> is not
    /// <c>null</c>; <c>false</c> otherwise.
    /// </returns>
    bool TryDeserialize<T>(CloudEvent @event, out T? result) where T : class;
}

