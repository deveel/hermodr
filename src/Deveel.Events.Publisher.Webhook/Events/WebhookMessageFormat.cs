//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Well-known format identifiers for webhook message serialization.
    /// </summary>
    /// <remarks>
    /// This class is preserved for backward compatibility. Prefer
    /// <see cref="EventMessageFormat"/> which defines the same constants in a
    /// channel-agnostic way.
    /// <br/>
    /// Any string value can be used as a format key by registering a matching
    /// <see cref="IEventSerializer"/>; these constants cover the built-in
    /// implementations. The default channel format is <see cref="Json"/>.
    /// </remarks>
    public static class WebhookMessageFormat
    {
        /// <summary>
        /// Plain JSON envelope.
        /// Content-Type: <c>application/json</c>. <b>Default.</b>
        /// </summary>
        /// <seealso cref="EventMessageFormat.Json"/>
        public const string Json = EventMessageFormat.Json;

        /// <summary>
        /// Plain XML envelope.
        /// Content-Type: <c>application/xml</c>.
        /// </summary>
        /// <seealso cref="EventMessageFormat.Xml"/>
        public const string Xml = EventMessageFormat.Xml;

        /// <summary>
        /// CloudEvents structured JSON format (<c>application/cloudevents+json</c>).
        /// </summary>
        /// <seealso cref="EventMessageFormat.CloudEventsJson"/>
        public const string CloudEventsJson = EventMessageFormat.CloudEventsJson;

        /// <summary>
        /// CloudEvents structured XML format (<c>application/cloudevents+xml</c>).
        /// </summary>
        /// <seealso cref="EventMessageFormat.CloudEventsXml"/>
        public const string CloudEventsXml = EventMessageFormat.CloudEventsXml;
    }
}
