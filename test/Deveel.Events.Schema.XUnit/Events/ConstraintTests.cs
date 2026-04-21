using System.Drawing;

namespace Deveel.Events {
	public static class ConstraintTests {

		// ─── PropertyRequiredConstraint ───────────────────────────────────────

		[Theory]
		[InlineData("name", true)]
		[InlineData(null, false)]
		public static void Required_IsValid(object? value, bool expected) {
			IEventPropertyConstraint constraint = new PropertyRequiredConstraint();
			Assert.Equal(expected, constraint.IsValid(value));
		}

		[Fact]
		public static void Required_ConstraintType_IsRequired() {
			IEventPropertyConstraint constraint = new PropertyRequiredConstraint();
			Assert.Equal("required", constraint.ConstraintType);
		}

		// ─── RangeConstraint ─────────────────────────────────────────────────

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
		public static void Range_IsValid(int? min, int? max, int value, bool expected) {
			IEventPropertyConstraint constraint = new RangeConstraint<int>(min, max);
			Assert.Equal(expected, constraint.IsValid(value));
		}

		[Fact]
		public static void Range_NullValue_ReturnsFalse() {
			IEventPropertyConstraint constraint = new RangeConstraint<int>(1, 10);
			Assert.False(constraint.IsValid(null));
		}

		[Fact]
		public static void Range_BothNullThrows() {
			Assert.Throws<ArgumentException>(() => new RangeConstraint<int>(null, null));
		}

		[Fact]
		public static void Range_ConstraintType_IsRange() {
			IEventPropertyConstraint constraint = new RangeConstraint<int>(0, 100);
			Assert.Equal("range", constraint.ConstraintType);
		}

		[Fact]
		public static void Range_ImplementsIRangeConstraint() {
			var constraint = new RangeConstraint<int>(5, 15);
			var rangeConstraint = Assert.IsAssignableFrom<IRangeConstraint>(constraint);
			Assert.Equal(typeof(int), rangeConstraint.ValueType);
			Assert.Equal(5, rangeConstraint.Min);
			Assert.Equal(15, rangeConstraint.Max);
		}

		[Fact]
		public static void Range_MinOnly_IRangeConstraint_MaxIsNull() {
			var constraint = new RangeConstraint<int>(5, null);
			var rangeConstraint = (IRangeConstraint) constraint;
			Assert.Equal(5, rangeConstraint.Min);
			Assert.Null(rangeConstraint.Max);
		}

		// ─── EnumMemberConstraint ─────────────────────────────────────────────

		[Theory]
		[InlineData(typeof(KnownColor), nameof(KnownColor.LawnGreen), true)]
		[InlineData(typeof(KnownColor), "fooBar", false)]
		public static void EnumMember_IsValid(Type enumType, string value, bool expected) {
			var names = Enum.GetNames(enumType);
			IEventPropertyConstraint constraint = new EnumMemberConstraint<string>(names);
			Assert.Equal(expected, constraint.IsValid(value));
		}

		[Fact]
		public static void EnumMember_NullValue_ReturnsFalse() {
			IEventPropertyConstraint constraint = new EnumMemberConstraint<string>(new[] { "A", "B" });
			Assert.False(constraint.IsValid(null));
		}

		[Fact]
		public static void EnumMember_WrongType_ReturnsFalse() {
			IEventPropertyConstraint constraint = new EnumMemberConstraint<string>(new[] { "A", "B" });
			Assert.False(constraint.IsValid(42)); // int instead of string
		}

		[Fact]
		public static void EnumMember_ConstraintType_IsAllowedValues() {
			IEventPropertyConstraint constraint = new EnumMemberConstraint<string>(new[] { "X" });
			Assert.Equal("allowedValues", constraint.ConstraintType);
		}

		[Fact]
		public static void EnumMember_ImplementsIEnumMemberConstraint() {
			var values = new[] { "Alpha", "Beta", "Gamma" };
			var constraint = new EnumMemberConstraint<string>(values);
			var enumConstraint = Assert.IsAssignableFrom<IEnumMemberConstraint>(constraint);
			Assert.Equal(3, enumConstraint.AllowedValueObjects.Count);
			Assert.Equal("Alpha", enumConstraint.AllowedValueObjects[0]?.ToString());
		}

		// ─── EventPropertyConstraintCollection uniqueness ─────────────────────

		[Fact]
		public static void ConstraintCollection_DuplicateConstraintType_Throws() {
			var property = new EventProperty("x", "string");
			property.Constraints.Add(new PropertyRequiredConstraint());
			Assert.Throws<ArgumentException>(() => property.Constraints.Add(new PropertyRequiredConstraint()));
		}

		[Fact]
		public static void ConstraintCollection_DifferentConstraintTypes_Allowed() {
			var property = new EventProperty("x", "int");
			property.Constraints.Add(new PropertyRequiredConstraint());
			property.Constraints.Add(new RangeConstraint<int>(0, 100));
			Assert.Equal(2, property.Constraints.Count);
		}

		// Keep original method names for backward compat
		[Theory]
		[InlineData("name", true)]
		[InlineData(null, false)]
		public static void TestIsRequired(object? value, bool valid) {
			IEventPropertyConstraint constraint = new PropertyRequiredConstraint();
			Assert.Equal(valid, constraint.IsValid(value));
		}

		[Theory]
		[InlineData(22, 34, 11, false)]
		[InlineData(22, 34, 35, false)]
		[InlineData(22, 34, 25, true)]
		[InlineData(null, 34, 25, true)]
		[InlineData(22, null, 25, true)]
		public static void TestRange(int? min, int? max, int value, bool valid) {
			IEventPropertyConstraint constraint = new RangeConstraint<int>(min, max);
			Assert.Equal(valid, constraint.IsValid(value));
		}

		[Theory]
		[InlineData(typeof(KnownColor), nameof(KnownColor.LawnGreen), true)]
		[InlineData(typeof(KnownColor), "fooBar", false)]
		public static void TestEnumMember(Type enumType, string value, bool valid) {
			var names = Enum.GetNames(enumType);
			IEventPropertyConstraint constraint = new EnumMemberConstraint<string>(names);
			Assert.Equal(valid, constraint.IsValid(value));
		}
	}
}
