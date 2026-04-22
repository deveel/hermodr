//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Well-known format identifiers for <see cref="IEventSerializer"/>
    /// implementations.
    /// </summary>
    /// <remarks>
    /// Any string value can be used as a format key by registering a matching
    /// <see cref="IEventSerializer"/>; these constants cover the built-in
    /// implementations.  The default format used by most channels is <see cref="Json"/>.
    /// </remarks>
    public static class EventMessageFormat
    {
        /// <summary>
        /// Plain JSON envelope — a flat JSON object whose top-level properties
        /// mirror every CloudEvent context attribute, with the event data embedded
        /// under the <c>data</c> key.  Batch deliveries produce a JSON array.
        /// Content-Type: <c>application/json</c>.  <b>Default.</b>
        /// </summary>
        public const string Json = "json";

        /// <summary>
        /// Plain XML envelope.  Batch deliveries produce a root
        /// <c>&lt;events&gt;</c> element containing one <c>&lt;cloudevent&gt;</c>
        /// child per event.  Content-Type: <c>application/xml</c>.
        /// </summary>
        public const string Xml = "xml";

        /// <summary>
        /// CloudEvents structured JSON format
        /// (<c>application/cloudevents+json</c>).  Batch deliveries use the
        /// CloudEvents batch format (<c>application/cloudevents-batch+json</c>).
        /// </summary>
        public const string CloudEventsJson = "cloudevents+json";

        /// <summary>
        /// CloudEvents structured XML format
        /// (<c>application/cloudevents+xml</c>).
        /// </summary>
        public const string CloudEventsXml = "cloudevents+xml";
    }
}

