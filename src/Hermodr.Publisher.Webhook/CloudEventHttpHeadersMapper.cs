//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.ObjectModel;

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// Maps the context attributes of a <see cref="CloudEvent"/> onto the
    /// <c>ce-*</c> HTTP request headers mandated by the
    /// <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/http-protocol-binding.md#321-binary-content-mode">CloudEvents HTTP binary content mode</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per the HTTP binding specification, every CloudEvent context attribute
    /// — required or extension — is serialized as a single HTTP header named
    /// <c>ce-{attribute}</c>, using the ASCII-lowercased attribute name.
    /// Required attributes (<c>specversion</c>, <c>id</c>, <c>type</c>,
    /// <c>source</c>, <c>time</c>) and the commonly-populated
    /// <c>datacontenttype</c> / <c>subject</c> are always emitted when set;
    /// every other populated extension attribute is emitted with the same
    /// <c>ce-</c> prefix.
    /// </para>
    /// <para>
    /// This mapper is intentionally stateless and read-only with respect to
    /// the <see cref="CloudEvent"/>; it produces only the header dictionary,
    /// leaving the request body and <c>Content-Type</c> to the calling channel.
    /// </para>
    /// </remarks>
    internal static class CloudEventHttpHeadersMapper
    {
        // The core attribute names that are always handled explicitly (so they
        // can be skipped in the extension-attribute loop below).
        private static readonly HashSet<string> CoreAttributes =
            new(StringComparer.Ordinal)
            {
                "specversion", "id", "type", "source", "time",
                "datacontenttype", "subject"
            };

        /// <summary>
        /// Builds the <c>ce-*</c> HTTP header bag for <paramref name="event"/>.
        /// </summary>
        /// <param name="event">The event whose attributes are to be projected.</param>
        /// <returns>
        /// A read-only dictionary of <c>ce-{attribute}</c> header names to
        /// their string representations. Attributes whose value is <c>null</c>
        /// are omitted. Returns an empty dictionary when
        /// <paramref name="event"/> has no populated attributes.
        /// </returns>
        public static IReadOnlyDictionary<string, string> Map(CloudEvent @event)
        {
            if (@event is null)
                return ReadOnlyDictionary<string, string>.Empty;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (@event.SpecVersion is not null)
                headers["ce-specversion"] = @event.SpecVersion.VersionId;
            if (@event.Id is not null)
                headers["ce-id"] = @event.Id;
            if (@event.Type is not null)
                headers["ce-type"] = @event.Type;
            if (@event.Source is not null)
                headers["ce-source"] = @event.Source.ToString();
            if (@event.Time.HasValue)
                headers["ce-time"] = @event.Time.Value.ToString("O");
            if (@event.DataContentType is not null)
                headers["ce-datacontenttype"] = @event.DataContentType;
            if (@event.Subject is not null)
                headers["ce-subject"] = @event.Subject;

            // Every remaining populated extension attribute becomes ce-{name}.
            foreach (var (attr, value) in @event.GetPopulatedAttributes())
            {
                if (CoreAttributes.Contains(attr.Name))
                    continue;
                if (value is null)
                    continue;

                headers[$"ce-{attr.Name}"] = value.ToString() ?? string.Empty;
            }

            return headers;
        }
    }
}