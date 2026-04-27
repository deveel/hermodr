//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Encapsulates a single attribute filter: a value to match and the strategy to compare it
    /// against an incoming event attribute.
    /// </summary>
    public sealed class EventAttributeFilter
    {
        private EventAttributeFilter(string value, FilterMatchMode matchMode)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            MatchMode = matchMode;
        }
        
        /// <summary>
        /// Gets the wildcard character used to indicate prefix or suffix matching in filter patterns.
        /// </summary>
        public const char Wildcard = '*';

        /// <summary>
        /// Gets the filter value used in the comparison.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the <see cref="FilterMatchMode"/> that governs how <see cref="Value"/>
        /// is compared against the event attribute.
        /// </summary>
        public FilterMatchMode MatchMode { get; }

        /// <summary>
        /// Creates a filter that requires an exact, case-sensitive match.
        /// </summary>
        public static EventAttributeFilter Exact(string value)
            => new(value, FilterMatchMode.Exact);

        /// <summary>
        /// Creates a filter that requires the attribute value to start with <paramref name="prefix"/>.
        /// Any trailing <c>*</c> characters are stripped from <paramref name="prefix"/> before storing.
        /// </summary>
        public static EventAttributeFilter Prefix(string prefix)
            => new(prefix.TrimEnd('*'), FilterMatchMode.Prefix);

        /// <summary>
        /// Creates a filter that requires the attribute value to end with <paramref name="suffix"/>.
        /// Any leading <c>*</c> characters are stripped from <paramref name="suffix"/> before storing.
        /// </summary>
        public static EventAttributeFilter Suffix(string suffix)
            => new(suffix.TrimStart('*'), FilterMatchMode.Suffix);

        /// <summary>
        /// Parses a pattern string into an <see cref="EventAttributeFilter"/>.
        /// <list type="bullet">
        ///   <item><description>A trailing <c>*</c> (e.g. <c>"com.example.*"</c>) produces a <see cref="FilterMatchMode.Prefix"/> filter.</description></item>
        ///   <item><description>A leading <c>*</c> (e.g. <c>"*.placed"</c>) produces a <see cref="FilterMatchMode.Suffix"/> filter.</description></item>
        ///   <item><description>Any other string produces a <see cref="FilterMatchMode.Exact"/> filter.</description></item>
        /// </list>
        /// </summary>
        public static EventAttributeFilter Parse(string pattern)
        {
            if (pattern.EndsWith(Wildcard))
                return Prefix(pattern);
            if (pattern.StartsWith(Wildcard))
                return Suffix(pattern);
            return Exact(pattern);
        }

        /// <summary>
        /// Tests whether <paramref name="input"/> matches this filter.
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

        /// <inheritdoc/>
        public override string ToString() => MatchMode switch
        {
            FilterMatchMode.Prefix => $"{Value}*",
            FilterMatchMode.Suffix => $"*{Value}",
            _ => Value
        };
    }
}

