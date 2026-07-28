//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Reflection;

namespace Hermodr;

/// <summary>
/// Resolves channel metadata by invoking a static method on a type loaded
/// from a referenced assembly. The entry-point string has the form
/// <c>&lt;assembly-qualified-type-name&gt;::&lt;method-name&gt;</c>, where the
/// method is expected to return <see cref="IReadOnlyList{T}"/> of
/// <see cref="IEventPublishChannelMetadata"/>.
/// </summary>
public static class EntryChannelResolver
{
    /// <summary>
    /// Parses <paramref name="entry"/> and invokes the referenced static
    /// method, returning the channel metadata it produces.
    /// </summary>
    /// <param name="entry">
    /// The entry specifier, <c>AssemblyQualifiedName::MethodName</c>.
    /// </param>
    /// <returns>
    /// The list of channel metadata returned by the invoked method.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entry"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="entry"/> is not of the form
    /// <c>type::method</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type or method cannot be resolved, or when the method
    /// does not return <see cref="IReadOnlyList{T}"/> of
    /// <see cref="IEventPublishChannelMetadata"/>.
    /// </exception>
    public static IReadOnlyList<IEventPublishChannelMetadata> Resolve(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var parts = entry.Split("::", 2);
        if (parts.Length != 2)
            throw new ArgumentException("Entry must be of the form 'AssemblyQualifiedName::MethodName'.", nameof(entry));

        var typeName = parts[0];
        var methodName = parts[1];

        var type = Type.GetType(typeName, throwOnError: false);
        if (type == null)
            throw new InvalidOperationException($"Could not resolve type '{typeName}'. Ensure the assembly is loadable from the tool's runtime context.");

        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null)
            throw new InvalidOperationException($"Could not find a public static method '{methodName}' on type '{typeName}'.");

        var result = method.Invoke(null, null);
        if (result is IReadOnlyList<IEventPublishChannelMetadata> list)
            return list;
        if (result is IEnumerable<IEventPublishChannelMetadata> enumerable)
            return enumerable.ToList();

        throw new InvalidOperationException($"Method '{methodName}' on type '{typeName}' did not return IReadOnlyList<IEventPublishChannelMetadata>.");
    }
}