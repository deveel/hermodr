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
    public sealed class EventAttributeFilter : IEventFilter
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

        /// <summary>
        /// Creates a filter that matches the CloudEvents attribute <paramref name="attributeName"/>
        /// using the supplied <paramref name="value"/> and <paramref name="matchMode"/>.
        /// </summary>
        public static EventAttributeFilter For(
            string attributeName,
            string value,
            FilterMatchMode matchMode = FilterMatchMode.Exact)
            => new(attributeName, value, matchMode);

        // ── Type-attribute factory methods ────────────────────────────────────────────

        /// <summary>
        /// Creates a filter that matches the CloudEvents <c>type</c> attribute with an exact,
        /// case-sensitive comparison against <paramref name="value"/>.
        /// </summary>
        public static EventAttributeFilter Type(string value)
            => new("type", value, FilterMatchMode.Exact);

        /// <summary>
        /// Creates a filter that matches the CloudEvents <c>type</c> attribute using the
        /// specified <paramref name="matchMode"/> (<see cref="FilterMatchMode.Exact"/>,
        /// <see cref="FilterMatchMode.Prefix"/>, or <see cref="FilterMatchMode.Suffix"/>).
        /// </summary>
        public static EventAttributeFilter Type(string value, FilterMatchMode matchMode)
            => new("type", value, matchMode);

        /// <summary>
        /// Parses a wildcard pattern and creates a filter targeting the CloudEvents <c>type</c>
        /// attribute.  When <paramref name="parseWildcard"/> is <c>true</c>, a trailing <c>*</c>
        /// produces <see cref="FilterMatchMode.Prefix"/>, a leading <c>*</c> produces
        /// <see cref="FilterMatchMode.Suffix"/>, and no wildcard produces
        /// <see cref="FilterMatchMode.Exact"/>.
        /// </summary>
        public static EventAttributeFilter Type(string pattern, bool parseWildcard)
            => For("type", pattern, parseWildcard);

        /// <summary>
        /// Parses a wildcard <paramref name="pattern"/> and creates a filter that targets
        /// <paramref name="attributeName"/>.
        /// A trailing <c>*</c> produces <see cref="FilterMatchMode.Prefix"/>;
        /// a leading <c>*</c> produces <see cref="FilterMatchMode.Suffix"/>;
        /// anything else produces <see cref="FilterMatchMode.Exact"/>.
        /// </summary>
        public static EventAttributeFilter For(string attributeName, string pattern, bool parseWildcard)
        {
            if (!parseWildcard)
                return new(attributeName, pattern);

            if (pattern.EndsWith(Wildcard))
                return new(attributeName, pattern.TrimEnd(Wildcard), FilterMatchMode.Prefix);
            if (pattern.StartsWith(Wildcard))
                return new(attributeName, pattern.TrimStart(Wildcard), FilterMatchMode.Suffix);
            return new(attributeName, pattern);
        }

        /// <summary>
        /// Creates a filter that tests the CloudEvents extension attribute
        /// <paramref name="extensionName"/> (the <c>extension.</c> prefix is added automatically).
        /// </summary>
        public static EventAttributeFilter ForExtension(
            string extensionName,
            string value,
            FilterMatchMode matchMode = FilterMatchMode.Exact)
            => new(ExtensionPrefix + extensionName, value, matchMode);

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

        // ── IEventFilter ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> when the named CloudEvents attribute of <paramref name="event"/>
        /// satisfies this filter.
        /// </summary>
        /// <remarks>
        /// Returns <c>false</c> when this instance was created in value-only mode (i.e.
        /// <see cref="AttributeName"/> is <c>null</c>).
        /// </remarks>
        public bool Matches(CloudEvent @event, EventSubscriptionContext context)
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

