//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deveel.Events
{
    /// <summary>
    /// Root of the serializable, data-driven filter expression tree used by
    /// <see cref="EventSubscriptionFilterModel"/> to express body-matching logic
    /// without code (delegates or lambdas).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hierarchy is decorated with <see cref="JsonPolymorphicAttribute"/> so that the
    /// entire tree round-trips through <c>System.Text.Json</c> without extra configuration —
    /// suitable for storage in document databases (MongoDB, Cosmos DB, DynamoDB, etc.) or
    /// in a <c>JSON</c>/<c>JSONB</c> column in any relational database.
    /// </para>
    /// <para>
    /// The <c>"$kind"</c> discriminator property identifies the concrete node type:
    /// <list type="table">
    ///   <listheader><term>$kind</term><description>Type</description></listheader>
    ///   <item><term><c>"jsonPath"</c></term><description><see cref="JsonPathComparisonExpression"/></description></item>
    ///   <item><term><c>"and"</c></term><description><see cref="AndFilterExpression"/></description></item>
    ///   <item><term><c>"or"</c></term><description><see cref="OrFilterExpression"/></description></item>
    ///   <item><term><c>"not"</c></term><description><see cref="NotFilterExpression"/></description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Code-based predicates (<see cref="IEventSubscription.Filter"/>'s
    /// <c>Predicate</c> field and <see cref="JsonPredicateDataFilter"/>) have no serializable
    /// representation and remain runtime-only.
    /// </para>
    /// </remarks>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
    [JsonDerivedType(typeof(JsonPathComparisonExpression), "jsonPath")]
    [JsonDerivedType(typeof(AndFilterExpression), "and")]
    [JsonDerivedType(typeof(OrFilterExpression), "or")]
    [JsonDerivedType(typeof(NotFilterExpression), "not")]
    public abstract class FilterExpression
    {
        /// <summary>
        /// Evaluates the expression against the given JSON <paramref name="root"/> element.
        /// </summary>
        public abstract bool Evaluate(JsonElement root);

        // ── Static factory helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Creates a <see cref="JsonPathComparisonExpression"/> that compares the value at
        /// <paramref name="path"/> with <paramref name="value"/> using
        /// <paramref name="op"/>.
        /// </summary>
        public static FilterExpression JsonPath(string path, FilterOperator op, string? value = null)
            => new JsonPathComparisonExpression { Path = path, Operator = op, Value = value };

        /// <summary>Shorthand — creates an <see cref="FilterOperator.Equals"/> path comparison.</summary>
        public static FilterExpression JsonPath(string path, string value)
            => JsonPath(path, FilterOperator.Equals, value);

        /// <summary>
        /// Creates an <see cref="AndFilterExpression"/> that requires all
        /// <paramref name="operands"/> to evaluate to <c>true</c>.
        /// </summary>
        public static FilterExpression And(params FilterExpression[] operands)
            => new AndFilterExpression { Operands = [.. operands] };

        /// <summary>
        /// Creates an <see cref="OrFilterExpression"/> that requires at least one
        /// <paramref name="operands"/> to evaluate to <c>true</c>.
        /// </summary>
        public static FilterExpression Or(params FilterExpression[] operands)
            => new OrFilterExpression { Operands = [.. operands] };

        /// <summary>
        /// Creates a <see cref="NotFilterExpression"/> that negates <paramref name="operand"/>.
        /// </summary>
        public static FilterExpression Not(FilterExpression operand)
            => new NotFilterExpression { Operand = operand };
    }

    // ────────────────────────────────────────────────────────────────────────────────
    // Leaf node
    // ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A leaf expression that navigates a dot-separated <see cref="Path"/> inside a JSON
    /// object and applies a <see cref="FilterOperator"/> comparison.
    /// </summary>
    /// <example>
    /// <code language="json">
    /// { "$kind": "jsonPath", "path": "order.customer.tier", "operator": "Equals", "value": "gold" }
    /// </code>
    /// </example>
    public sealed class JsonPathComparisonExpression : FilterExpression
    {
        /// <summary>Dot-separated property path, e.g. <c>"order.customer.tier"</c>.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>The comparison operator to apply to the resolved property value.</summary>
        public FilterOperator Operator { get; set; } = FilterOperator.Equals;

        /// <summary>
        /// The reference value for the comparison, stored as a string.
        /// For numeric operators parse it as <see cref="double"/>.
        /// For <see cref="FilterOperator.Exists"/> / <see cref="FilterOperator.NotExists"/>
        /// this property is ignored.
        /// </summary>
        public string? Value { get; set; }

        /// <inheritdoc/>
        public override bool Evaluate(JsonElement root)
        {
            var segments = Path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var current = root;

            foreach (var seg in segments)
            {
                if (current.ValueKind != JsonValueKind.Object)
                    return Operator == FilterOperator.NotExists;

                if (!current.TryGetProperty(seg, out current))
                    return Operator == FilterOperator.NotExists;
            }

            return Operator switch
            {
                FilterOperator.Exists    => true,
                FilterOperator.NotExists => false,

                FilterOperator.Equals    => StringValue(current) == Value,
                FilterOperator.NotEquals => StringValue(current) != Value,

                FilterOperator.StartsWith => StringValue(current)
                    ?.StartsWith(Value ?? string.Empty, StringComparison.Ordinal) == true,

                FilterOperator.EndsWith => StringValue(current)
                    ?.EndsWith(Value ?? string.Empty, StringComparison.Ordinal) == true,

                FilterOperator.Contains => StringValue(current)
                    ?.Contains(Value ?? string.Empty, StringComparison.Ordinal) == true,

                FilterOperator.GreaterThan         => NumericCompare(current, Value) > 0,
                FilterOperator.LessThan            => NumericCompare(current, Value) < 0,
                FilterOperator.GreaterThanOrEqual  => NumericCompare(current, Value) >= 0,
                FilterOperator.LessThanOrEqual     => NumericCompare(current, Value) <= 0,

                _ => false
            };
        }

        /// <summary>
        /// Returns the string representation of a <see cref="JsonElement"/> consistent with
        /// <see cref="JsonElement.ToString()"/>: booleans use <c>bool.TrueString</c> /
        /// <c>bool.FalseString</c> (<c>"True"</c> / <c>"False"</c>), numbers use
        /// their raw JSON text, strings return their unquoted value.
        /// </summary>
        private static string? StringValue(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.True   => bool.TrueString,   // "True"
            JsonValueKind.False  => bool.FalseString,  // "False"
            JsonValueKind.Null   => null,
            _                    => el.ToString()
        };

        private static int NumericCompare(JsonElement el, string? reference)
        {
            if (!double.TryParse(reference, NumberStyles.Any, CultureInfo.InvariantCulture, out var refNum))
                return 0;

            double elNum;
            if (el.ValueKind == JsonValueKind.Number)
                elNum = el.GetDouble();
            else if (!double.TryParse(el.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out elNum))
                return 0;

            return elNum.CompareTo(refNum);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────
    // Logical combinators
    // ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A composite expression that evaluates to <c>true</c> only when
    /// <em>all</em> <see cref="Operands"/> evaluate to <c>true</c> (logical AND).
    /// An empty operand list evaluates to <c>true</c>.
    /// </summary>
    public sealed class AndFilterExpression : FilterExpression
    {
        /// <summary>The child expressions that must all pass.</summary>
        public List<FilterExpression> Operands { get; set; } = [];

        /// <inheritdoc/>
        public override bool Evaluate(JsonElement root)
            => Operands.All(o => o.Evaluate(root));
    }

    /// <summary>
    /// A composite expression that evaluates to <c>true</c> when
    /// <em>at least one</em> of the <see cref="Operands"/> evaluates to <c>true</c> (logical OR).
    /// An empty operand list evaluates to <c>false</c>.
    /// </summary>
    public sealed class OrFilterExpression : FilterExpression
    {
        /// <summary>The child expressions of which at least one must pass.</summary>
        public List<FilterExpression> Operands { get; set; } = [];

        /// <inheritdoc/>
        public override bool Evaluate(JsonElement root)
            => Operands.Any(o => o.Evaluate(root));
    }

    /// <summary>
    /// A unary expression that negates the result of <see cref="Operand"/> (logical NOT).
    /// </summary>
    public sealed class NotFilterExpression : FilterExpression
    {
        /// <summary>The expression whose result is negated.</summary>
        public FilterExpression Operand { get; set; } = null!;

        /// <inheritdoc/>
        public override bool Evaluate(JsonElement root)
            => !Operand.Evaluate(root);
    }
}

