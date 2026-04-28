//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// A filter that tests a named CloudEvents envelope attribute against a value using a
    /// <see cref="FilterMatchMode"/> strategy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standard attribute names are: <c>type</c>, <c>source</c>, <c>subject</c>, <c>id</c>,
    /// <c>time</c>, <c>datacontenttype</c>, and <c>dataschema</c>.
    /// </para>
    /// <para>
    /// Extension attributes must be prefixed with <c>extension.</c>
    /// (e.g. <c>"extension.tenantid"</c>) — the part after the dot is used as the
    /// CloudEvents extension attribute name.
    /// </para>
    /// </remarks>
    public sealed class EventAttributeFilter : EventFilter
    {
        private const string ExtensionPrefix = "extension.";

        // ── Constructors ────────────────────────────────────────────────────────────


        /// <summary>
        /// Creates a filter that matches the CloudEvents attribute named
        /// <paramref name="attributeName"/> using the supplied <paramref name="matchMode"/>.
        /// </summary>
        /// <param name="attributeName">
        /// A standard attribute name (<c>type</c>, <c>source</c>, <c>subject</c>, <c>id</c>,
        /// <c>time</c>, <c>datacontenttype</c>, <c>dataschema</c>) or an extension attribute
        /// prefixed with <c>extension.</c> (e.g. <c>"extension.tenantid"</c>).
        /// </param>
        /// <param name="value">The value to match against.</param>
        /// <param name="matchMode">The matching strategy.  Defaults to <see cref="FilterMatchMode.Exact"/>.</param>
        public EventAttributeFilter(
            string attributeName,
            string value,
            FilterMatchMode matchMode = FilterMatchMode.Exact)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(attributeName, nameof(attributeName));
            ArgumentNullException.ThrowIfNull(value, nameof(value));
            AttributeName = attributeName;
            Value = value;
            MatchMode = matchMode;
        }

        // ── Properties ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the wildcard character used to indicate prefix or suffix matching in filter patterns.
        /// </summary>
        public const char Wildcard = '*';

        /// <summary>
        /// Gets the CloudEvents attribute name this filter targets, or <c>null</c> when the
        /// instance was created in value-only mode.
        /// </summary>
        public string? AttributeName { get; }

        /// <summary>
        /// Gets the filter value used in the comparison.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the <see cref="FilterMatchMode"/> that governs how <see cref="Value"/>
        /// is compared against the attribute value.
        /// </summary>
        public FilterMatchMode MatchMode { get; }


        // ── Attribute-aware factory methods ──────────────────────────────────────────
        // (factory methods have been moved to EventFilter)

        // ── Value-only matching ───────────────────────────────────────────────────

        /// <summary>
        /// Tests whether <paramref name="input"/> matches this filter using <see cref="MatchMode"/>.
        /// Returns <c>false</c> when <paramref name="input"/> is <c>null</c>.
        /// </summary>
        public bool Matches(string? input)
        {
            if (input is null)
                return false;

            return MatchMode switch
            {
                FilterMatchMode.Exact  => string.Equals(input, Value, StringComparison.Ordinal),
                FilterMatchMode.Prefix => input.StartsWith(Value, StringComparison.Ordinal),
                FilterMatchMode.Suffix => input.EndsWith(Value, StringComparison.Ordinal),
                _ => false
            };
        }

        // ── EventFilter ───────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public override TResult Accept<TResult>(IEventFilterVisitor<TResult> visitor)
            => visitor.VisitAttribute(this);

        /// <summary>
        /// Returns <c>true</c> when the named CloudEvents attribute of <paramref name="event"/>
        /// satisfies this filter.
        /// </summary>
        /// <remarks>
        /// Returns <c>false</c> when this instance was created in value-only mode (i.e.
        /// <see cref="AttributeName"/> is <c>null</c>).
        /// </remarks>
        public override bool Matches(CloudEvent @event, EventSubscriptionContext context)
        {
            if (@event is null || AttributeName is null)
                return false;

            string? attributeValue;

            if (AttributeName.StartsWith(ExtensionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var extName = AttributeName[ExtensionPrefix.Length..];
                var attribute = CloudEventAttribute.CreateExtension(extName, CloudEventAttributeType.String);
                attributeValue = @event[attribute]?.ToString();
            }
            else
            {
                attributeValue = AttributeName.ToLowerInvariant() switch
                {
                    "type"            => @event.Type,
                    "source"          => @event.Source?.ToString(),
                    "subject"         => @event.Subject,
                    "id"              => @event.Id,
                    "time"            => @event.Time?.ToString(),
                    "datacontenttype" => @event.DataContentType,
                    "dataschema"      => @event.DataSchema?.ToString(),
                    _                 => null
                };
            }

            return Matches(attributeValue);
        }

        // ── Formatting ───────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public override string ToString()
        {
            var valueStr = MatchMode switch
            {
                FilterMatchMode.Prefix => $"{Value}{Wildcard}",
                FilterMatchMode.Suffix => $"{Wildcard}{Value}",
                _                      => Value
            };

            return AttributeName is not null
                ? $"{AttributeName}:{valueStr}"
                : valueStr;
        }
    }
}

