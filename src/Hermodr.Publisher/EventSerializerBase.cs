//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// Convenience base class for custom <see cref="IEventSerializer"/>
    /// implementations.
    /// </summary>
    /// <remarks>
    /// Override <see cref="Format"/>, <see cref="ContentType"/>, and
    /// <see cref="Serialize"/> at a minimum. Override <see cref="SerializeBatch"/>
    /// to enable batch serialization; the default implementation throws
    /// <see cref="NotSupportedException"/>.
    /// </remarks>
    public abstract class EventSerializerBase : IEventSerializer
    {
        /// <inheritdoc/>
        public abstract string Format { get; }

        /// <inheritdoc/>
        public abstract string ContentType { get; }

        /// <inheritdoc/>
        public virtual string BatchContentType => ContentType;

        /// <inheritdoc/>
        public abstract byte[] Serialize(CloudEvent @event);

        /// <inheritdoc/>
        public virtual byte[] SerializeBatch(IReadOnlyList<CloudEvent> events)
            => throw new NotSupportedException(
                $"Batch serialization is not supported by {GetType().Name}. " +
                $"Override {nameof(SerializeBatch)} to add support.");
    }
}

