//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Serializable model for a single CloudEvents envelope-attribute filter (type, source,
    /// subject, or an extension attribute), suitable for storage in any database or as part
    /// of a JSON document.
    /// </summary>
    /// <remarks>
    /// Convert to/from the runtime <see cref="EventAttributeFilter"/> via
    /// <see cref="ToAttributeFilter"/> and <see cref="From"/>.
    /// </remarks>
    public sealed class AttributeFilterModel
    {
        /// <summary>
        /// The filter value.
        /// For <see cref="FilterMatchMode.Prefix"/> and <see cref="FilterMatchMode.Suffix"/>
        /// this is the raw prefix/suffix string (wildcards already stripped).
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// How <see cref="Value"/> is compared to the incoming event attribute.
        /// </summary>
        public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.Exact;

        // ── Conversion ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts this model into the runtime <see cref="EventAttributeFilter"/>.
        /// </summary>
        public EventAttributeFilter ToAttributeFilter() => MatchMode switch
        {
            FilterMatchMode.Prefix => EventAttributeFilter.Prefix(Value),
            FilterMatchMode.Suffix => EventAttributeFilter.Suffix(Value),
            _                      => EventAttributeFilter.Exact(Value)
        };

        /// <summary>
        /// Creates an <see cref="AttributeFilterModel"/> from the runtime
        /// <paramref name="filter"/>.
        /// </summary>
        public static AttributeFilterModel From(EventAttributeFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);
            return new AttributeFilterModel { Value = filter.Value, MatchMode = filter.MatchMode };
        }

        // ── Convenience factory methods ─────────────────────────────────────────────

        /// <summary>Creates an exact-match model.</summary>
        public static AttributeFilterModel Exact(string value)
            => new() { Value = value, MatchMode = FilterMatchMode.Exact };

        /// <summary>
        /// Creates a prefix model.  Any trailing <c>*</c> is stripped from
        /// <paramref name="prefix"/> before storing.
        /// </summary>
        public static AttributeFilterModel Prefix(string prefix)
            => new() { Value = prefix.TrimEnd('*'), MatchMode = FilterMatchMode.Prefix };

        /// <summary>
        /// Creates a suffix model.  Any leading <c>*</c> is stripped from
        /// <paramref name="suffix"/> before storing.
        /// </summary>
        public static AttributeFilterModel Suffix(string suffix)
            => new() { Value = suffix.TrimStart('*'), MatchMode = FilterMatchMode.Suffix };

        /// <summary>
        /// Parses a wildcard pattern into the appropriate model:
        /// trailing <c>*</c> → prefix, leading <c>*</c> → suffix, otherwise exact.
        /// </summary>
        public static AttributeFilterModel Parse(string pattern)
        {
            if (pattern.EndsWith('*')) return Prefix(pattern);
            if (pattern.StartsWith('*')) return Suffix(pattern);
            return Exact(pattern);
        }
    }
}

