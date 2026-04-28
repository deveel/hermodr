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
    /// Evaluates a <see cref="FilterExpression"/> against a <see cref="CloudEvent"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Variable names map to CloudEvents envelope attributes (<c>type</c>, <c>source</c>,
    /// <c>subject</c>, <c>id</c>, <c>time</c>, <c>datacontenttype</c>, <c>dataschema</c>)
    /// or to extension attributes using the <c>extension.</c> prefix.
    /// </para>
    /// <para>
    /// To target a JSON field inside the event data payload, prefix the dot-separated
    /// path with <c>data.</c> — e.g. <c>data.customer.tier</c>.
    /// </para>
    /// </remarks>
    internal static class EventFilterEvaluator
    {
        /// <summary>
        /// Returns <c>true</c> when <paramref name="filter"/> matches <paramref name="event"/>.
        /// A <c>null</c> filter is treated as "match all".
        /// </summary>
        public static bool Matches(
            FilterExpression? filter,
            CloudEvent @event,
            EventSubscriptionContext context)
        {
            if (filter is null || filter.IsEmpty)
                return true;
            if (@event is null)
                return false;

            var visitor = new EvaluatorVisitor(@event, context);
            var result  = visitor.Visit(filter);

            return result is ConstantFilterExpression { Value: true };
        }

        // ── Variable resolution ──────────────────────────────────────────────────────

        private static object? ResolveVariable(
            string name,
            CloudEvent @event,
            EventSubscriptionContext context)
        {
            // Data-field navigation: "data.<dot.separated.path>"
            if (name.StartsWith("data.", StringComparison.OrdinalIgnoreCase))
                return ResolveDataPath(name[5..], @event, context);

            // Extension attribute: "extension.<name>"
            if (name.StartsWith("extension.", StringComparison.OrdinalIgnoreCase))
            {
                var extName = name[10..];
                var attr    = CloudEventAttribute.CreateExtension(extName, CloudEventAttributeType.String);
                return @event[attr]?.ToString();
            }

            // Standard CloudEvents envelope attributes.
            return name.ToLowerInvariant() switch
            {
                "type"            => @event.Type,
                "source"          => @event.Source?.ToString(),
                "subject"         => @event.Subject,
                "id"              => @event.Id,
                "time"            => @event.Time?.ToString("O"),
                "datacontenttype" => @event.DataContentType,
                "dataschema"      => @event.DataSchema?.ToString(),
                _                 => null
            };
        }

        private static object? ResolveDataPath(
            string path,
            CloudEvent @event,
            EventSubscriptionContext context)
        {
            var jsonData = context.GetJsonData(@event);
            if (jsonData is null)
                return null;

            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var current  = jsonData.Value;

            foreach (var seg in segments)
            {
                if (current.ValueKind != JsonValueKind.Object)
                    return null;
                if (!current.TryGetProperty(seg, out current))
                    return null;
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString(),
                JsonValueKind.Number => current.GetDouble(),
                JsonValueKind.True   => true,
                JsonValueKind.False  => false,
                JsonValueKind.Null   => null,
                _                   => current.GetRawText()
            };
        }

        // ── Value comparison helpers ─────────────────────────────────────────────────

        private static bool EqualValues(object? left, object? right)
        {
            if (left is null && right is null) return true;
            if (left is null || right is null) return false;

            if (left is string ls && right is string rs)
                return string.Equals(ls, rs, StringComparison.Ordinal);

            var ld = ToDouble(left);
            var rd = ToDouble(right);
            if (ld.HasValue && rd.HasValue)
                return ld.Value == rd.Value;

            if (left is bool lb && right is bool rb)
                return lb == rb;

            return left.Equals(right);
        }

        private static int CompareValues(object? left, object? right)
        {
            if (left is null && right is null) return 0;
            if (left is null) return -1;
            if (right is null) return 1;

            var ld = ToDouble(left);
            var rd = ToDouble(right);
            if (ld.HasValue && rd.HasValue)
                return ld.Value.CompareTo(rd.Value);

            return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal);
        }

        private static double? ToDouble(object? value) => value switch
        {
            double d                                    => d,
            int i                                       => (double)i,
            long l                                      => (double)l,
            float f                                     => (double)f,
            string s when double.TryParse(s, out var d) => d,
            _                                           => null
        };

        // ── Visitor ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// A <see cref="FilterExpressionVisitor"/> that evaluates each node against a
        /// specific <see cref="CloudEvent"/> and folds the result back into a
        /// <see cref="ConstantFilterExpression"/> so parent nodes can read it.
        /// </summary>
        private sealed class EvaluatorVisitor : FilterExpressionVisitor
        {
            private readonly CloudEvent _event;
            private readonly EventSubscriptionContext _context;

            public EvaluatorVisitor(CloudEvent @event, EventSubscriptionContext context)
            {
                _event   = @event;
                _context = context;
            }

            // Constants are already fully evaluated values.
            public override FilterExpression VisitConstant(ConstantFilterExpression constant)
                => constant;

            // Resolve the variable name to its runtime value and wrap it.
            public override FilterExpression VisitVariable(VariableFilterExpression variable)
            {
                var value = ResolveVariable(variable.VariableName, _event, _context);
                return FilterExpression.Constant(value);
            }

            // Evaluate binary expressions, short-circuiting AND / OR.
            public override FilterExpression VisitBinary(BinaryFilterExpression binary)
            {
                if (binary.ExpressionType == FilterExpressionType.And)
                {
                    var left = ((ConstantFilterExpression)Visit(binary.Left)).Value;
                    if (left is false) return FilterExpression.Constant(false);
                    return Visit(binary.Right);
                }

                if (binary.ExpressionType == FilterExpressionType.Or)
                {
                    var left = ((ConstantFilterExpression)Visit(binary.Left)).Value;
                    if (left is true) return FilterExpression.Constant(true);
                    return Visit(binary.Right);
                }

                var lv = ((ConstantFilterExpression)Visit(binary.Left)).Value;
                var rv = ((ConstantFilterExpression)Visit(binary.Right)).Value;

                object? result = binary.ExpressionType switch
                {
                    FilterExpressionType.Equal              => EqualValues(lv, rv),
                    FilterExpressionType.NotEqual           => !EqualValues(lv, rv),
                    FilterExpressionType.GreaterThan        => CompareValues(lv, rv) > 0,
                    FilterExpressionType.GreaterThanOrEqual => CompareValues(lv, rv) >= 0,
                    FilterExpressionType.LessThan           => CompareValues(lv, rv) < 0,
                    FilterExpressionType.LessThanOrEqual    => CompareValues(lv, rv) <= 0,
                    _                                       => null
                };

                return FilterExpression.Constant(result);
            }

            // Evaluate unary expressions (currently only NOT).
            public override FilterExpression VisitUnary(UnaryFilterExpression unary)
            {
                var operand = ((ConstantFilterExpression)Visit(unary.Operand)).Value;

                object? result = unary.ExpressionType switch
                {
                    FilterExpressionType.Not => operand is bool b ? (object)!b : null,
                    _                        => null
                };

                return FilterExpression.Constant(result);
            }

            // Evaluate built-in string/existence functions.
            public override FilterExpression VisitFunction(FunctionFilterExpression function)
            {
                var varValue = ResolveVariable(function.Variable.VariableName, _event, _context)?.ToString();
                var args     = (function.Arguments ?? [])
                    .Select(a => ((ConstantFilterExpression)Visit(a)).Value)
                    .ToArray();

                object? result = function.FunctionName.ToLowerInvariant() switch
                {
                    "startswith" => varValue is not null
                        && args is [string prefix, ..]
                        && varValue.StartsWith(prefix, StringComparison.Ordinal),

                    "endswith"   => varValue is not null
                        && args is [string suffix, ..]
                        && varValue.EndsWith(suffix, StringComparison.Ordinal),

                    "contains"   => varValue is not null
                        && args is [string sub, ..]
                        && varValue.Contains(sub, StringComparison.Ordinal),

                    "exists"     => varValue is not null,

                    _            => null
                };

                return FilterExpression.Constant(result);
            }
        }
    }
}

