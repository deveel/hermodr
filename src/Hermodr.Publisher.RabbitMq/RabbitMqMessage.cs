//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// The obect that represents a message to be published 
    /// to a RabbitMQ server.
    /// </summary>
    public readonly struct RabbitMqMessage
    {
        /// <summary>
        /// Constructs the message with the given body and content type.
        /// </summary>
        /// <param name="body">
        /// The body of the message to be published.
        /// </param>
        /// <param name="contentType">
        /// The content type of the message.
        /// </param>
        /// <param name="contentEncoding">
        /// An optional content encoding of the message (eg. utf-8).
        /// </param>
        public RabbitMqMessage(ReadOnlyMemory<byte> body, string contentType, string? contentEncoding = null)
        {
            ArgumentNullException.ThrowIfNull(contentType, nameof(contentType));

            ContentType = contentType;
            ContentEncoding = contentEncoding;
            Body = body;
        }

        /// <summary>
        /// Gets the content type of the message.
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Gets the optional content encoding of the message.
        /// </summary>
        public string? ContentEncoding { get; }

        /// <summary>
        /// Gets the body of the message, as a memory of bytes.
        /// </summary>
        public ReadOnlyMemory<byte> Body { get; }
    }
}
