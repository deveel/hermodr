//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Default implementation of <see cref="IEventSchemaCompatibilityChecker"/>.
    /// Compares two event schemas property-by-property and reports backward/forward compatibility.
    /// </summary>
    public sealed class EventSchemaCompatibilityChecker : IEventSchemaCompatibilityChecker
    {
        /// <inheritdoc/>
        public SchemaCompatibilityResult Check(IEventSchema older, IEventSchema newer)
        {
            ArgumentNullException.ThrowIfNull(older);
            ArgumentNullException.ThrowIfNull(newer);

            if (older.EventType != newer.EventType)
            {
                return new SchemaCompatibilityResult(
                    SchemaCompatibilityLevel.None,
                    new[]
                    {
                        new SchemaChange(
                            SchemaChangeType.PropertyTypeChanged,
                            "(event type)",
                            $"Event type changed from '{older.EventType}' to '{newer.EventType}'",
                            isBackwardBreaking: true,
                            isForwardBreaking: true)
                    });
            }

            var changes = ComparePropertyCollections(older.Properties, newer.Properties, pathPrefix: "").ToList();
            var level = ComputeLevel(changes);

            return new SchemaCompatibilityResult(level, changes);
        }

        private static SchemaCompatibilityLevel ComputeLevel(List<SchemaChange> changes)
        {
            var backwardBreaking = changes.Any(c => c.IsBackwardBreaking);
            var forwardBreaking = changes.Any(c => c.IsForwardBreaking);

            return (backwardBreaking, forwardBreaking) switch
            {
                (false, false) => SchemaCompatibilityLevel.Full,
                (false, true)  => SchemaCompatibilityLevel.Forward,
                (true, false)  => SchemaCompatibilityLevel.Backward,
                _              => SchemaCompatibilityLevel.None
            };
        }

        private static IEnumerable<SchemaChange> ComparePropertyCollections(
            IReadOnlyList<IEventProperty> olderProperties,
            IReadOnlyList<IEventProperty> newerProperties,
            string pathPrefix)
        {
            var olderProps = olderProperties.ToDictionary(p => p.Name);
            var newerProps = newerProperties.ToDictionary(p => p.Name);

            foreach (var (name, newerProp) in newerProps)
            {
                var propertyPath = string.IsNullOrEmpty(pathPrefix) ? name : $"{pathPrefix}.{name}";

                if (!olderProps.TryGetValue(name, out var olderProp))
                {
                    var isRequired = newerProp.IsRequired;
                    yield return new SchemaChange(
                        SchemaChangeType.PropertyAdded,
                        propertyPath,
                        isRequired
                            ? $"Required property '{name}' was added"
                            : $"Optional property '{name}' was added",
                        isBackwardBreaking: isRequired,
                        isForwardBreaking: false);
                    continue;
                }

                foreach (var change in CompareProperties(olderProp, newerProp, propertyPath))
                    yield return change;
            }

            foreach (var (name, _) in olderProps)
            {
                if (!newerProps.ContainsKey(name))
                {
                    var propertyPath = string.IsNullOrEmpty(pathPrefix) ? name : $"{pathPrefix}.{name}";
                    yield return new SchemaChange(
                        SchemaChangeType.PropertyRemoved,
                        propertyPath,
                        $"Property '{name}' was removed",
                        isBackwardBreaking: false,
                        isForwardBreaking: true);
                }
            }
        }

        private static IEnumerable<SchemaChange> CompareProperties(IEventProperty older, IEventProperty newer, string propertyPath)
        {
            if (older.DataType != newer.DataType)
            {
                yield return new SchemaChange(
                    SchemaChangeType.PropertyTypeChanged,
                    propertyPath,
                    $"Property type changed from '{older.DataType}' to '{newer.DataType}'",
                    isBackwardBreaking: true,
                    isForwardBreaking: true);
                yield break;
            }

            if (older.IsRequired != newer.IsRequired)
            {
                var (backwardBreaking, forwardBreaking, message) = newer.IsRequired
                    ? (true, false, $"Property '{newer.Name}' became required")
                    : (false, true, $"Property '{newer.Name}' became optional");

                yield return new SchemaChange(
                    SchemaChangeType.PropertyRequirementChanged,
                    propertyPath,
                    message,
                    backwardBreaking,
                    forwardBreaking);
            }

            foreach (var change in CompareConstraints(older, newer, propertyPath))
                yield return change;

            if (older.Properties.Count > 0 || newer.Properties.Count > 0)
            {
                foreach (var change in ComparePropertyCollections(older.Properties, newer.Properties, propertyPath))
                    yield return change;
            }
        }

        private static IEnumerable<SchemaChange> CompareConstraints(IEventProperty older, IEventProperty newer, string propertyPath)
        {
            foreach (var change in CompareRangeConstraints(older, newer, propertyPath))
                yield return change;

            foreach (var change in CompareEnumConstraints(older, newer, propertyPath))
                yield return change;
        }

        private static IEnumerable<SchemaChange> CompareRangeConstraints(IEventProperty older, IEventProperty newer, string propertyPath)
        {
            var olderRanges = older.Constraints.OfType<IRangeConstraint>().ToList();
            var newerRanges = newer.Constraints.OfType<IRangeConstraint>().ToList();

            if (olderRanges.Count == 0 && newerRanges.Count == 0)
                yield break;

            var olderRange = olderRanges.FirstOrDefault();
            var newerRange = newerRanges.FirstOrDefault();

            if (olderRange != null && newerRange != null)
            {
                var minChanged = !EqualNullable(olderRange.Min, newerRange.Min);
                var maxChanged = !EqualNullable(olderRange.Max, newerRange.Max);

                if (minChanged || maxChanged)
                {
                    var backwardBreaking = MinNarrowed(olderRange.Min, newerRange.Min) || MaxNarrowed(olderRange.Max, newerRange.Max);
                    var forwardBreaking = MinWidened(olderRange.Min, newerRange.Min) || MaxWidened(olderRange.Max, newerRange.Max);

                    if (backwardBreaking || forwardBreaking)
                    {
                        yield return new SchemaChange(
                            SchemaChangeType.ConstraintChanged,
                            propertyPath,
                            $"Range constraint changed from [{olderRange.Min}, {olderRange.Max}] to [{newerRange.Min}, {newerRange.Max}]",
                            backwardBreaking,
                            forwardBreaking);
                    }
                }
            }
            else if (olderRange != null)
            {
                yield return new SchemaChange(
                    SchemaChangeType.ConstraintChanged,
                    propertyPath,
                    "Range constraint was removed",
                    isBackwardBreaking: false,
                    isForwardBreaking: true);
            }
            else
            {
                yield return new SchemaChange(
                    SchemaChangeType.ConstraintChanged,
                    propertyPath,
                    "Range constraint was added",
                    isBackwardBreaking: true,
                    isForwardBreaking: false);
            }
        }

        private static IEnumerable<SchemaChange> CompareEnumConstraints(IEventProperty older, IEventProperty newer, string propertyPath)
        {
            var olderEnums = older.Constraints.OfType<IEnumMemberConstraint>().ToList();
            var newerEnums = newer.Constraints.OfType<IEnumMemberConstraint>().ToList();

            if (olderEnums.Count == 0 && newerEnums.Count == 0)
                yield break;

            var olderEnum = olderEnums.FirstOrDefault();
            var newerEnum = newerEnums.FirstOrDefault();

            if (olderEnum != null && newerEnum != null)
            {
                var olderValues = olderEnum.AllowedValueObjects.Select(v => v?.ToString()).ToHashSet();
                var newerValues = newerEnum.AllowedValueObjects.Select(v => v?.ToString()).ToHashSet();

                var removedValues = olderValues.Except(newerValues).ToList();
                var addedValues = newerValues.Except(olderValues).ToList();

                if (removedValues.Count > 0 || addedValues.Count > 0)
                {
                    var backwardBreaking = removedValues.Count > 0;
                    var forwardBreaking = addedValues.Count > 0;

                    yield return new SchemaChange(
                        SchemaChangeType.ConstraintChanged,
                        propertyPath,
                        $"Enum constraint changed: removed [{string.Join(", ", removedValues)}], added [{string.Join(", ", addedValues)}]",
                        backwardBreaking,
                        forwardBreaking);
                }
            }
            else if (olderEnum != null)
            {
                yield return new SchemaChange(
                    SchemaChangeType.ConstraintChanged,
                    propertyPath,
                    "Enum constraint was removed",
                    isBackwardBreaking: false,
                    isForwardBreaking: true);
            }
            else
            {
                yield return new SchemaChange(
                    SchemaChangeType.ConstraintChanged,
                    propertyPath,
                    "Enum constraint was added",
                    isBackwardBreaking: true,
                    isForwardBreaking: false);
            }
        }

        private static bool EqualNullable(object? a, object? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            return a.Equals(b);
        }

        private static bool MinNarrowed(object? olderMin, object? newerMin)
        {
            if (olderMin is null && newerMin is null) return false;
            if (olderMin is null) return true;
            if (newerMin is null) return false;
            return Comparer<object>.Default.Compare(newerMin, olderMin) > 0;
        }

        private static bool MinWidened(object? olderMin, object? newerMin)
        {
            if (olderMin is null && newerMin is null) return false;
            if (olderMin is null) return false;
            if (newerMin is null) return true;
            return Comparer<object>.Default.Compare(newerMin, olderMin) < 0;
        }

        private static bool MaxNarrowed(object? olderMax, object? newerMax)
        {
            if (olderMax is null && newerMax is null) return false;
            if (olderMax is null) return true;
            if (newerMax is null) return false;
            return Comparer<object>.Default.Compare(newerMax, olderMax) < 0;
        }

        private static bool MaxWidened(object? olderMax, object? newerMax)
        {
            if (olderMax is null && newerMax is null) return false;
            if (olderMax is null) return false;
            if (newerMax is null) return true;
            return Comparer<object>.Default.Compare(newerMax, olderMax) > 0;
        }
    }
}
