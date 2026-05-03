//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

namespace Deveel.Events
{
    /// <summary>
    /// Provides the ambient runtime context consumed by source-generated
    /// <see cref="IEventConvertible"/> implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Source-generated <c>ToCloudEvent()</c> methods read
    /// <see cref="DataSchemaBaseUri"/> and <see cref="JsonSerializerOptions"/> while
    /// converting. <see cref="EventPublisher"/> pushes publisher-specific values for the
    /// current async flow only, so multiple publisher instances can coexist safely.
    /// </para>
    /// <para>
    /// The two writable setters are <c>internal</c> so that only code inside the
    /// <c>Deveel.Events.Publisher</c> assembly can mutate them, while external code
    /// (including generated code in the consuming assembly) can read them freely.
    /// </para>
    /// </remarks>
    public static class EventGeneratorContext
    {
        private static readonly AsyncLocal<Frame?> CurrentFrame = new();

        // Optional global fallbacks used when conversion happens outside an active publisher scope.
        private static volatile Uri? _defaultDataSchemaBaseUri;
        private static volatile JsonSerializerOptions? _defaultJsonSerializerOptions;

        private sealed class Frame
        {
            public Frame(Uri? dataSchemaBaseUri, JsonSerializerOptions? jsonSerializerOptions)
            {
                ScopedDataSchemaBaseUri = dataSchemaBaseUri;
                ScopedJsonSerializerOptions = jsonSerializerOptions;
            }

            public Uri? ScopedDataSchemaBaseUri { get; }

            public JsonSerializerOptions? ScopedJsonSerializerOptions { get; }
        }

        private sealed class Scope(Frame? parent) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                    return;

                CurrentFrame.Value = parent;
                _disposed = true;
            }
        }

        /// <summary>
        /// Gets the base URI used by generated <c>ToCloudEvent()</c> implementations to
        /// construct a full <c>DataSchema</c> URI from a <c>DataVersion</c> string.
        /// </summary>
        /// <remarks>
        /// Resolved from the publisher scope currently active in this async flow.
        /// If no scope is active, falls back to a process-wide default value.
        /// </remarks>
        public static Uri? DataSchemaBaseUri
        {
            get => CurrentFrame.Value?.ScopedDataSchemaBaseUri ?? _defaultDataSchemaBaseUri;
            internal set => Interlocked.Exchange(ref _defaultDataSchemaBaseUri, value);
        }

        /// <summary>
        /// Gets the <see cref="System.Text.Json.JsonSerializerOptions"/> used by generated
        /// <c>ToCloudEvent()</c> implementations when serialising the event data object.
        /// </summary>
        /// <remarks>
        /// Resolved from the publisher scope currently active in this async flow.
        /// If no scope is active, falls back to a process-wide default value.
        /// When <c>null</c>, the default <see cref="System.Text.Json.JsonSerializer"/> options are used.
        /// </remarks>
        public static JsonSerializerOptions? JsonSerializerOptions
        {
            get => CurrentFrame.Value?.ScopedJsonSerializerOptions ?? _defaultJsonSerializerOptions;
            internal set => Interlocked.Exchange(ref _defaultJsonSerializerOptions, value);
        }

        internal static IDisposable Push(Uri? dataSchemaBaseUri, JsonSerializerOptions? jsonSerializerOptions)
        {
            var parent = CurrentFrame.Value;
            CurrentFrame.Value = new Frame(dataSchemaBaseUri, jsonSerializerOptions);
            return new Scope(parent);
        }
    }
}


