//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// The default implementation of <see cref="ICloudEventMessage"/> that
    /// carries the structured-mode JSON body of a CloudEvent.
    /// </summary>
    internal sealed class CloudEventMessage : ICloudEventMessage
    {
        public CloudEventMessage(byte[] body, string contentType)
        {
            Body = body;
            ContentType = contentType;
        }

        /// <inheritdoc/>
        public byte[] Body { get; }

        /// <inheritdoc/>
        public string ContentType { get; }
    }
}

