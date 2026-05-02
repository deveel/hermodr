// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Drawing;

namespace Deveel.Events
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Domain")]
    [Trait("Feature", "EventSchema")]
    public static class ConstraintTests
    {
        // ── PropertyRequiredConstraint ────────────────────────────────────────

        [Theory]
        [InlineData("name", true)]
        [InlineData(null, false)]
        public static void Should_ValidateRequired_When_ValueIsProvided(object? value, bool expected)
        {
            // Arrange
            IEventPropertyConstraint constraint = new PropertyRequiredConstraint();

            // Act & Assert
            Assert.Equal(expected, constraint.IsValid(value));
        }

        [Fact]
        public static void Should_HaveRequiredConstraintType_When_NewConstraintIsCreated()
        {
            // Arrange
            IEventPropertyConstraint constraint = new PropertyRequiredConstraint();

            // Assert
            Assert.Equal("required", constraint.ConstraintType);
        }

		// ─── RangeConstraint ─────────────────────────────────────────────────

        // ── RangeConstraint ───────────────────────────────────────────────────

        // Inclusive-bounds cases (the old code used exclusive bounds — this verifies the fix)
        [Theory]
        [InlineData(22, 34, 22, true)]    // exactly at min (inclusive)
        [InlineData(22, 34, 34, true)]    // exactly at max (inclusive)
        [InlineData(22, 34, 23, true)]    // inside range
        [InlineData(22, 34, 21, false)]   // below min
        [InlineData(22, 34, 35, false)]   // above max
        [InlineData(null, 34, 0, true)]   // no min — any value ≤ 34
        [InlineData(null, 34, 34, true)]  // exactly at max with no min
        [InlineData(null, 34, 35, false)] // above max with no min
        [InlineData(22, null, 22, true)]  // exactly at min with no max
        [InlineData(22, null, 100, true)] // above min with no max
        [InlineData(22, null, 21, false)] // below min with no max
        public static void Should_ValidateRange_When_ValueIsCheckedAgainstBounds(int? min, int? max, int value, bool expected)
        {
            // Arrange
            IEventPropertyConstraint constraint = new RangeConstraint<int>(min, max);

            // Act & Assert
            Assert.Equal(expected, constraint.IsValid(value));
        }

        [Fact]
        public static void Should_ReturnFalse_When_NullValueIsCheckedAgainstRange()
        {
            // Arrange
            IEventPropertyConstraint constraint = new RangeConstraint<int>(1, 10);

            // Act & Assert
            Assert.False(constraint.IsValid(null));
        }

        [Fact]
        public static void Should_ThrowArgumentException_When_BothMinAndMaxAreNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new RangeConstraint<int>(null, null));
        }

        [Fact]
        public static void Should_HaveRangeConstraintType_When_RangeConstraintIsCreated()
        {
            // Arrange
            IEventPropertyConstraint constraint = new RangeConstraint<int>(0, 100);

            // Assert
            Assert.Equal("range", constraint.ConstraintType);
        }

        [Fact]
        public static void Should_ImplementIRangeConstraint_When_RangeConstraintIsCreated()
        {
            // Arrange & Act
            var constraint = new RangeConstraint<int>(5, 15);
            var rangeConstraint = Assert.IsAssignableFrom<IRangeConstraint>(constraint);

            // Assert
            Assert.Equal(typeof(int), rangeConstraint.ValueType);
            Assert.Equal(5, rangeConstraint.Min);
            Assert.Equal(15, rangeConstraint.Max);
        }

        [Fact]
        public static void Should_HaveNullMax_When_OnlyMinIsSpecified()
        {
            // Arrange
            var constraint = new RangeConstraint<int>(5, null);
            var rangeConstraint = (IRangeConstraint)constraint;

            // Assert
            Assert.Equal(5, rangeConstraint.Min);
            Assert.Null(rangeConstraint.Max);
        }

        // ── EnumMemberConstraint ──────────────────────────────────────────────

        [Theory]
        [InlineData(typeof(KnownColor), nameof(KnownColor.LawnGreen), true)]
        [InlineData(typeof(KnownColor), "fooBar", false)]
        public static void Should_ValidateEnumMember_When_ValueIsCheckedAgainstAllowedValues(Type enumType, string value, bool expected)
        {
            // Arrange
            var names = Enum.GetNames(enumType);
            IEventPropertyConstraint constraint = new EnumMemberConstraint<string>(names);

            // Act & Assert
            Assert.Equal(expected, constraint.IsValid(value));
        }

        [Fact]
        public static void Should_ReturnFalse_When_NullValueIsCheckedAgainstAllowedValues()
        {
            // Arrange
            IEventPropertyConstraint constraint = new EnumMemberConstraint<string>(new[] { "A", "B" });

            // Act & Assert
            Assert.False(constraint.IsValid(null));
        }

        [Fact]
        public static void Should_ReturnFalse_When_WrongTypeValueIsCheckedAgainstAllowedValues()
        {
            // Arrange
            IEventPropertyConstraint constraint = new EnumMemberConstraint<string>(new[] { "A", "B" });

            // Act & Assert
            Assert.False(constraint.IsValid(42)); // int instead of string
        }

        [Fact]
        public static void Should_HaveAllowedValuesConstraintType_When_EnumMemberConstraintIsCreated()
        {
            // Arrange
            IEventPropertyConstraint constraint = new EnumMemberConstraint<string>(new[] { "X" });

            // Assert
            Assert.Equal("allowedValues", constraint.ConstraintType);
        }

        [Fact]
        public static void Should_ImplementIEnumMemberConstraint_When_EnumMemberConstraintIsCreated()
        {
            // Arrange
            var values = new[] { "Alpha", "Beta", "Gamma" };

            // Act
            var constraint = new EnumMemberConstraint<string>(values);
            var enumConstraint = Assert.IsAssignableFrom<IEnumMemberConstraint>(constraint);

            // Assert
            Assert.Equal(3, enumConstraint.AllowedValueObjects.Count);
            Assert.Equal("Alpha", enumConstraint.AllowedValueObjects[0]?.ToString());
        }

        // ── EventPropertyConstraintCollection uniqueness ──────────────────────

        [Fact]
        public static void Should_ThrowArgumentException_When_DuplicateConstraintTypeIsAdded()
        {
            // Arrange
            var property = new EventProperty("x", "string");
            property.Constraints.Add(new PropertyRequiredConstraint());

            // Act & Assert
            Assert.Throws<ArgumentException>(() => property.Constraints.Add(new PropertyRequiredConstraint()));
        }

        [Fact]
        public static void Should_AllowMultipleConstraints_When_ConstraintTypesAreDifferent()
        {
            // Arrange
            var property = new EventProperty("x", "int");

            // Act
            property.Constraints.Add(new PropertyRequiredConstraint());
            property.Constraints.Add(new RangeConstraint<int>(0, 100));

            // Assert
            Assert.Equal(2, property.Constraints.Count);
        }
    }
}
