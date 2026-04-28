//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

using Deveel.Filters;

namespace Deveel.Events
{
    /// <summary>
    /// Extension methods for <see cref="FilterExpression"/> that add CloudEvent-specific
    /// matching and JSON serialization capabilities.
    /// </summary>
    public static class FilterExpressionExtensions
    {
        // Default shared options — used when the caller passes no options at all.
        // JsonFilterConverter is stateless, so a single instance is fine.
        private static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            Converters = { new JsonFilterConverter() }
        };

        // ── CloudEvent matching ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> when this <see cref="FilterExpression"/> matches
        /// <paramref name="event"/> within the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="filter">The filter to evaluate.</param>
        /// <param name="event">The incoming <see cref="CloudEvent"/> to test.</param>
        /// <param name="context">
        /// The subscription context that provides runtime services such as data
        /// deserialization.  Pass <see cref="EventSubscriptionContext.Empty"/> when no
        /// context is available.
        /// </param>
        public static bool Matches(
            this FilterExpression filter,
            CloudEvent @event,
            EventSubscriptionContext context)
            => EventFilterEvaluator.Matches(filter, @event, context);

        // ── JSON serialization ───────────────────────────────────────────────────────

        /// <summary>
        /// Serializes this <see cref="FilterExpression"/> to a JSON string using
        /// <see cref="JsonFilterConverter"/>.
        /// </summary>
        /// <param name="filter">The filter expression to serialize.</param>
        /// <param name="options">
        /// Optional <see cref="JsonSerializerOptions"/> to control serialization behaviour
        /// (e.g. indentation, naming policy).  When <c>null</c>, a default set of options
        /// with <see cref="JsonFilterConverter"/> already registered is used.
        /// <para>
        /// When a non-<c>null</c> value is provided and it does not already contain a
        /// <see cref="JsonFilterConverter"/>, the converter is added automatically to a
        /// <em>copy</em> of the supplied options so that the original object is never
        /// mutated.
        /// </para>
        /// </param>
        /// <returns>
        /// A JSON string representing the filter expression, or the JSON literal
        /// <c>null</c> when <paramref name="filter"/> is <c>null</c>.
        /// </returns>
        /// <example>
        /// <code language="csharp">
        /// // Default options (compact JSON)
        /// string json = filter.ToJson();
        ///
        /// // Custom options — indented output; converter is injected automatically
        /// var opts = new JsonSerializerOptions { WriteIndented = true };
        /// string pretty = filter.ToJson(opts);
        ///
        /// // Pre-configured options that already include the converter
        /// var opts2 = new JsonSerializerOptions();
        /// opts2.Converters.Add(new JsonFilterConverter());
        /// string json2 = filter.ToJson(opts2);   // converter is not duplicated
        /// </code>
        /// </example>
        public static string ToJson(this FilterExpression filter, JsonSerializerOptions? options = null)
            => JsonSerializer.Serialize(filter, EnsureFilterConverter(options));

        // ── Private helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns <paramref name="options"/> guaranteed to contain a
        /// <see cref="JsonFilterConverter"/>.
        /// <list type="bullet">
        ///   <item><c>null</c> → returns the shared <see cref="DefaultJsonOptions"/>.</item>
        ///   <item>Options that already have the converter → returned as-is.</item>
        ///   <item>Options without the converter → a shallow copy is made via the
        ///   <see cref="JsonSerializerOptions(JsonSerializerOptions)"/> copy constructor
        ///   and the converter is appended; the original is never touched.</item>
        /// </list>
        /// </summary>
        internal static JsonSerializerOptions EnsureFilterConverter(JsonSerializerOptions? options)
        {
            if (options is null)
                return DefaultJsonOptions;

            if (options.Converters.OfType<JsonFilterConverter>().Any())
                return options;

            var copy = new JsonSerializerOptions(options);
            copy.Converters.Add(new JsonFilterConverter());
            return copy;
        }
    }
}

