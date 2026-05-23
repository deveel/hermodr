//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;
using System.Text.RegularExpressions;

using Deveel.Filters;

namespace Hermodr
{
    /// <summary>
    /// Provides static factory methods for building <see cref="FilterExpression"/> instances
    /// that target standard CloudEvents envelope attributes and JSON data payload fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each method returns a <see cref="FilterExpression"/> that can be used directly as a
    /// subscription filter, or combined with <see cref="FilterExpression.And"/>,
    /// <see cref="FilterExpression.Or"/>, and <see cref="FilterExpression.Not"/> to form
    /// composite conditions.
    /// </para>
    /// <para>
    /// Variable naming conventions used internally:
    /// <list type="bullet">
    ///   <item><term><c>type</c></term><description>CloudEvent <c>type</c> attribute.</description></item>
    ///   <item><term><c>source</c></term><description>CloudEvent <c>source</c> attribute.</description></item>
    ///   <item><term><c>subject</c></term><description>CloudEvent <c>subject</c> attribute.</description></item>
    ///   <item><term><c>extension.&lt;name&gt;</c></term><description>CloudEvent extension attribute.</description></item>
    ///   <item><term><c>data.&lt;path&gt;</c></term><description>Dot-separated path inside the JSON data payload.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static class EventFilter
    {
        // ── Shared JSON options ────────────────────────────────────────────────────────

        // Default shared options — used when no options are supplied by the caller.
        // Kept here so EventFilter does not depend on FilterExpressionExtensions at runtime.

        // ── Fluent builder entry point ─────────────────────────────────────────────────

        /// <summary>
        /// Creates a new, empty <see cref="EventFilterBuilder"/> that can be used
        /// to compose a <see cref="FilterExpression"/> with a fluent API.
        /// </summary>
        public static EventFilterBuilder New() => new();

        // ── JSON round-trip ────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses <paramref name="json"/> and returns the <see cref="FilterExpression"/>
        /// it represents, using <see cref="JsonFilterConverter"/>.
        /// </summary>
        /// <param name="json">
        /// A JSON string that was previously produced by
        /// <see cref="FilterExpressionExtensions.ToJson(FilterExpression, JsonSerializerOptions?)"/>
        /// or by any other serializer that registered <see cref="JsonFilterConverter"/>.
        /// </param>
        /// <param name="options">
        /// Optional <see cref="JsonSerializerOptions"/> to control deserialization behaviour.
        /// When <c>null</c>, a default set of options with <see cref="JsonFilterConverter"/>
        /// already registered is used.
        /// <para>
        /// When a non-<c>null</c> value is provided and it does not already contain a
        /// <see cref="JsonFilterConverter"/>, the converter is added automatically to a
        /// <em>copy</em> of the supplied options so that the original object is never
        /// mutated.
        /// </para>
        /// </param>
        /// <returns>
        /// The deserialized <see cref="FilterExpression"/>, or <c>null</c> when
        /// <paramref name="json"/> represents the JSON literal <c>null</c>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="json"/> is <c>null</c>, empty, or whitespace-only.
        /// </exception>
        /// <exception cref="JsonException">
        /// Thrown when <paramref name="json"/> is not valid JSON, or when the JSON object
        /// does not conform to the <see cref="FilterExpression"/> schema understood by
        /// <see cref="JsonFilterConverter"/>.
        /// </exception>
        /// <example>
        /// <code language="csharp">
        /// // Simplest form — uses default options
        /// FilterExpression? filter = EventFilter.FromJson(json);
        ///
        /// // Custom options — converter is injected automatically into a copy
        /// var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        /// FilterExpression? filter2 = EventFilter.FromJson(json, opts);
        ///
        /// // Pre-configured options that already include the converter
        /// var opts2 = new JsonSerializerOptions();
        /// opts2.Converters.Add(new JsonFilterConverter());
        /// FilterExpression? filter3 = EventFilter.FromJson(json, opts2);
        /// </code>
        /// </example>
        public static FilterExpression? FromJson(string json, JsonSerializerOptions? options = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json, nameof(json));
            return JsonSerializer.Deserialize<FilterExpression>(
                json,
                FilterExpressionExtensions.EnsureFilterConverter(options));
        }

        // ── JSON path validation ────────────────────────────────────────────────────
        
        /// <summary>
        /// Validates <paramref name="path"/> and throws an <see cref="ArgumentException"/>
        /// when the path is null, empty, or contains characters outside the allowed set.
        /// </summary>
        /// <param name="path">The dot-separated JSON path to validate.</param>
        /// <param name="paramName">The caller parameter name for the exception message.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="path"/> is null, empty, whitespace-only, or contains
        /// characters other than letters, digits, underscores, hyphens, and dot separators,
        /// or when any segment in the path is empty (e.g. <c>"a..b"</c>, <c>".a"</c>).
        /// </exception>
        private static void ValidateJsonPath(string path, string paramName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path, paramName);

            if (!JsonPath.Pattern().IsMatch(path))
                throw new ArgumentException(
                    $"The path '{path}' is invalid. A JSON path must consist of one or more " +
                    "dot-separated segments where each segment contains only letters, digits, " +
                    "or underscores ('_').",
                    paramName);
        }
        // ── Envelope-attribute filters ──────────────────────────────────────────────

        /// <summary>
        /// Returns a filter that matches events whose <c>type</c> attribute exactly equals
        /// <paramref name="type"/>.
        /// </summary>
        public static FilterExpression ByType(string type)
            => FilterExpression.Equal(
                FilterExpression.Variable("type"),
                FilterExpression.Constant(type));

        /// <summary>
        /// Returns a filter that matches events whose <c>type</c> attribute satisfies
        /// <paramref name="pattern"/>.
        /// A trailing <c>*</c> (e.g. <c>"com.example.*"</c>) performs a prefix match;
        /// a leading <c>*</c> (e.g. <c>"*.placed"</c>) performs a suffix match;
        /// otherwise an exact match is used.
        /// </summary>
        public static FilterExpression ByTypePattern(string pattern)
            => BuildAttributePattern("type", pattern);

        /// <summary>
        /// Returns a filter that matches events whose <c>source</c> attribute exactly equals
        /// <paramref name="source"/>.
        /// </summary>
        public static FilterExpression BySource(string source)
            => FilterExpression.Equal(
                FilterExpression.Variable("source"),
                FilterExpression.Constant(source));

        /// <summary>
        /// Returns a filter that matches events whose <c>source</c> attribute satisfies
        /// <paramref name="pattern"/> (same wildcard rules as <see cref="ByTypePattern"/>).
        /// </summary>
        public static FilterExpression BySourcePattern(string pattern)
            => BuildAttributePattern("source", pattern);

        /// <summary>
        /// Returns a filter that matches events whose <c>subject</c> attribute exactly equals
        /// <paramref name="subject"/>.
        /// </summary>
        public static FilterExpression BySubject(string subject)
            => FilterExpression.Equal(
                FilterExpression.Variable("subject"),
                FilterExpression.Constant(subject));

        /// <summary>
        /// Returns a filter that matches events whose <c>subject</c> attribute satisfies
        /// <paramref name="pattern"/> (same wildcard rules as <see cref="ByTypePattern"/>).
        /// </summary>
        public static FilterExpression BySubjectPattern(string pattern)
            => BuildAttributePattern("subject", pattern);

        /// <summary>
        /// Returns a filter that matches events whose extension attribute named
        /// <paramref name="extensionName"/> exactly equals <paramref name="value"/>.
        /// </summary>
        public static FilterExpression ByExtension(string extensionName, string value)
            => FilterExpression.Equal(
                FilterExpression.Variable("extension." + extensionName),
                FilterExpression.Constant(value));

        // ── Data-payload field filters ───────────────────────────────────────────────

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// exactly equals <paramref name="value"/>.
        /// </summary>
        public static FilterExpression ByField(string path, string value)
        {
            ValidateJsonPath(path, nameof(path));
            return FilterExpression.Equal(
                FilterExpression.Variable("data." + path),
                FilterExpression.Constant(value));
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="operator"/> compared to <paramref name="value"/>.
        /// </summary>
        public static FilterExpression ByField(string path, FilterExpressionType @operator, string value)
        {
            ValidateJsonPath(path, nameof(path));
            return BuildFieldFilter("data." + path, @operator, FilterExpression.Constant(value));
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="operator"/> compared to <paramref name="value"/>.
        /// </summary>
        public static FilterExpression ByField(string path, FilterExpressionType @operator, bool value)
        {
            ValidateJsonPath(path, nameof(path));
            return BuildFieldFilter("data." + path, @operator, FilterExpression.Constant(value));
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="operator"/> compared to <paramref name="value"/>.
        /// </summary>
        public static FilterExpression ByField(string path, FilterExpressionType @operator, int value)
        {
            ValidateJsonPath(path, nameof(path));
            return BuildFieldFilter("data." + path, @operator, FilterExpression.Constant(value));
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="operator"/> compared to <paramref name="value"/>.
        /// </summary>
        public static FilterExpression ByField(string path, FilterExpressionType @operator, long value)
        {
            ValidateJsonPath(path, nameof(path));
            return BuildFieldFilter("data." + path, @operator, FilterExpression.Constant(value));
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="operator"/> compared to <paramref name="value"/>.
        /// </summary>
        public static FilterExpression ByField(string path, FilterExpressionType @operator, double value)
        {
            ValidateJsonPath(path, nameof(path));
            return BuildFieldFilter("data." + path, @operator, FilterExpression.Constant(value));
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="operator"/> compared to <paramref name="value"/>.
        /// </summary>
        public static FilterExpression ByField(string path, FilterExpressionType @operator, DateTime value)
        {
            ValidateJsonPath(path, nameof(path));
            return BuildFieldFilter("data." + path, @operator, FilterExpression.Constant(value.ToString("O")));
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="operator"/> compared to <paramref name="value"/>.
        /// </summary>
        public static FilterExpression ByField(string path, FilterExpressionType @operator, DateTimeOffset value)
        {
            ValidateJsonPath(path, nameof(path));
            return BuildFieldFilter("data." + path, @operator, FilterExpression.Constant(value.ToString("O")));
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// starts with <paramref name="value"/>.
        /// </summary>
        public static FilterExpression FieldStartsWith(string path, string value)
        {
            ValidateJsonPath(path, nameof(path));
            var variable = FilterExpression.Variable("data." + path);
            return FilterExpression.Function(variable, "startsWith", new[] { FilterExpression.Constant(value) });
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// ends with <paramref name="value"/>.
        /// </summary>
        public static FilterExpression FieldEndsWith(string path, string value)
        {
            ValidateJsonPath(path, nameof(path));
            var variable = FilterExpression.Variable("data." + path);
            return FilterExpression.Function(variable, "endsWith", new[] { FilterExpression.Constant(value) });
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// contains <paramref name="value"/>.
        /// </summary>
        public static FilterExpression FieldContains(string path, string value)
        {
            ValidateJsonPath(path, nameof(path));
            var variable = FilterExpression.Variable("data." + path);
            return FilterExpression.Function(variable, "contains", new[] { FilterExpression.Constant(value) });
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// is present in the payload.
        /// </summary>
        public static FilterExpression FieldExists(string path)
        {
            ValidateJsonPath(path, nameof(path));
            return FilterExpression.Function(FilterExpression.Variable("data." + path), "exists");
        }

        /// <summary>
        /// Returns a filter that matches events where the JSON data field at <paramref name="path"/>
        /// is absent from the payload.
        /// </summary>
        public static FilterExpression FieldNotExists(string path)
        {
            ValidateJsonPath(path, nameof(path));
            return FilterExpression.Not(FilterExpression.Function(FilterExpression.Variable("data." + path), "exists"));
        }

        // ── Combining helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Combines two or more <see cref="FilterExpression"/> instances with a logical AND.
        /// </summary>
        /// <param name="filters">At least two filter expressions to combine.</param>
        /// <exception cref="ArgumentException">Thrown when fewer than two expressions are supplied.</exception>
        public static FilterExpression All(params FilterExpression[] filters)
        {
            if (filters.Length < 2)
                throw new ArgumentException("At least two filter expressions are required.", nameof(filters));

            return filters.Aggregate(FilterExpression.And);
        }

        /// <summary>
        /// Combines two or more <see cref="FilterExpression"/> instances with a logical OR.
        /// </summary>
        /// <param name="filters">At least two filter expressions to combine.</param>
        /// <exception cref="ArgumentException">Thrown when fewer than two expressions are supplied.</exception>
        public static FilterExpression Any(params FilterExpression[] filters)
        {
            if (filters.Length < 2)
                throw new ArgumentException("At least two filter expressions are required.", nameof(filters));

            return filters.Aggregate(FilterExpression.Or);
        }

        // ── Private helpers ─────────────────────────────────────────────────────────

        private static FilterExpression BuildAttributePattern(string variableName, string pattern)
        {
            var variable = FilterExpression.Variable(variableName);

            if (pattern.EndsWith('*'))
                return FilterExpression.Function(
                    variable, "startsWith",
                    new[] { FilterExpression.Constant(pattern.TrimEnd('*')) });

            if (pattern.StartsWith('*'))
                return FilterExpression.Function(
                    variable, "endsWith",
                    new[] { FilterExpression.Constant(pattern.TrimStart('*')) });

            return FilterExpression.Equal(variable, FilterExpression.Constant(pattern));
        }

        private static FilterExpression BuildFieldFilter(
            string variableName,
            FilterExpressionType @operator,
            FilterExpression valueExpr)
        {
            var variable = FilterExpression.Variable(variableName);

            return @operator switch
            {
                FilterExpressionType.Equal              => FilterExpression.Equal(variable, valueExpr),
                FilterExpressionType.NotEqual           => FilterExpression.NotEquals(variable, valueExpr),
                FilterExpressionType.GreaterThan        => FilterExpression.GreaterThan(variable, valueExpr),
                FilterExpressionType.GreaterThanOrEqual => FilterExpression.GreaterThanOrEqual(variable, valueExpr),
                FilterExpressionType.LessThan           => FilterExpression.LessThan(variable, valueExpr),
                FilterExpressionType.LessThanOrEqual    => FilterExpression.LessThanOrEqual(variable, valueExpr),
                _                                       => throw new ArgumentOutOfRangeException(nameof(@operator), @operator, null)
            };
        }
    }
    
    static partial class JsonPath
    {
        /// <summary>
        /// Pattern that matches a valid dot-notation JSON path.
        /// Each segment may contain letters, digits, and underscores.
        /// Segments are separated by dots; no empty segments are allowed.
        /// </summary>
        [GeneratedRegex(@"^[a-zA-Z0-9_]+(\.[a-zA-Z0-9_]+)*$",
            RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        public static partial Regex Pattern();
    }
}

