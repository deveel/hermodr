//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Filters;

namespace Deveel.Events
{
    /// <summary>
    /// Verifies that <see cref="EventFilter"/> rejects invalid JSON data-field paths
    /// (null, empty, whitespace, leading/trailing dots, consecutive dots, and paths that
    /// contain characters outside the allowed set) with an <see cref="ArgumentException"/>,
    /// and accepts all well-formed paths.
    /// </summary>
    [Trait("Feature", "Subscriptions")]
    [Trait("Subject", "PathValidation")]
    public static class CloudEventFilterPathValidationTests
    {
        // ── Valid path data ────────────────────────────────────────────────────────────

        public static TheoryData<string> ValidPaths => new()
        {
            "field",
            "Field",
            "FIELD",
            "field123",
            "field_name",
            "customer.tier",
            "order.items.price",
            "a.b.c.d.e",
            "Level1.Level2.Level3",
            "snake_case.child_prop",
            "mixed_Case_123.deep.path",
            "_internal.value",
            "prop0.prop1",
        };

        // ── Invalid path data ──────────────────────────────────────────────────────────

        public static TheoryData<string?> InvalidPaths => new()
        {
            (string?)null,   // null
            "",              // empty string
            "   ",           // whitespace only
            ".",             // dot only
            ".field",        // leading dot
            "field.",        // trailing dot
            "a..b",          // consecutive dots
            "field-name",    // hyphen inside segment
            "field name",    // space inside segment
            "field\tname",   // tab inside segment
            "field[0]",      // square brackets
            "field(0)",      // parentheses
            "field$child",   // dollar sign
            "field@child",   // at sign
            "field#child",   // hash
            "field*",        // asterisk
            "field/child",   // forward slash
            "field\\child",  // backslash
            "field<child",   // less-than
            "field>child",   // greater-than
            "field;child",   // semicolon
            "field:child",   // colon
            "field'child",   // single quote
            "field\"child",  // double quote
            "field+child",   // plus
            "field=child",   // equals
            "field,child",   // comma
        };

        // ── ByField (string value) ─────────────────────────────────────────────────────

        [Theory, MemberData(nameof(ValidPaths))]
        public static void ByField_String_ValidPath_DoesNotThrow(string path)
        {
            var ex = Record.Exception(() => EventFilter.ByField(path, "value"));
            Assert.Null(ex);
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void ByField_String_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() => EventFilter.ByField(path!, "value"));
        }

        // ── ByField with operator overloads ───────────────────────────────────────────

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void ByField_OperatorString_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                EventFilter.ByField(path!, FilterExpressionType.Equal, "value"));
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void ByField_OperatorBool_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                EventFilter.ByField(path!, FilterExpressionType.Equal, true));
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void ByField_OperatorInt_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                EventFilter.ByField(path!, FilterExpressionType.Equal, 42));
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void ByField_OperatorLong_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                EventFilter.ByField(path!, FilterExpressionType.Equal, 42L));
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void ByField_OperatorDouble_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                EventFilter.ByField(path!, FilterExpressionType.Equal, 3.14));
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void ByField_OperatorDateTime_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                EventFilter.ByField(path!, FilterExpressionType.Equal, DateTime.UtcNow));
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void ByField_OperatorDateTimeOffset_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                EventFilter.ByField(path!, FilterExpressionType.Equal, DateTimeOffset.UtcNow));
        }

        // ── FieldStartsWith ────────────────────────────────────────────────────────────

        [Theory, MemberData(nameof(ValidPaths))]
        public static void FieldStartsWith_ValidPath_DoesNotThrow(string path)
        {
            var ex = Record.Exception(() => EventFilter.FieldStartsWith(path, "prefix"));
            Assert.Null(ex);
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void FieldStartsWith_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() => EventFilter.FieldStartsWith(path!, "prefix"));
        }

        // ── FieldEndsWith ──────────────────────────────────────────────────────────────

        [Theory, MemberData(nameof(ValidPaths))]
        public static void FieldEndsWith_ValidPath_DoesNotThrow(string path)
        {
            var ex = Record.Exception(() => EventFilter.FieldEndsWith(path, "suffix"));
            Assert.Null(ex);
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void FieldEndsWith_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() => EventFilter.FieldEndsWith(path!, "suffix"));
        }

        // ── FieldContains ──────────────────────────────────────────────────────────────

        [Theory, MemberData(nameof(ValidPaths))]
        public static void FieldContains_ValidPath_DoesNotThrow(string path)
        {
            var ex = Record.Exception(() => EventFilter.FieldContains(path, "substring"));
            Assert.Null(ex);
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void FieldContains_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() => EventFilter.FieldContains(path!, "substring"));
        }

        // ── FieldExists ────────────────────────────────────────────────────────────────

        [Theory, MemberData(nameof(ValidPaths))]
        public static void FieldExists_ValidPath_DoesNotThrow(string path)
        {
            var ex = Record.Exception(() => EventFilter.FieldExists(path));
            Assert.Null(ex);
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void FieldExists_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() => EventFilter.FieldExists(path!));
        }

        // ── FieldNotExists ─────────────────────────────────────────────────────────────

        [Theory, MemberData(nameof(ValidPaths))]
        public static void FieldNotExists_ValidPath_DoesNotThrow(string path)
        {
            var ex = Record.Exception(() => EventFilter.FieldNotExists(path));
            Assert.Null(ex);
        }

        [Theory, MemberData(nameof(InvalidPaths))]
        public static void FieldNotExists_InvalidPath_ThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() => EventFilter.FieldNotExists(path!));
        }

        // ── Error message quality ──────────────────────────────────────────────────────

        [Fact]
        public static void InvalidPath_ExceptionMessage_ContainsInvalidPath()
        {
            const string badPath = "field[0]";
            var ex = Assert.ThrowsAny<ArgumentException>(() => EventFilter.ByField(badPath, "x"));
            Assert.Contains(badPath, ex.Message);
        }

        [Fact]
        public static void InvalidPath_ExceptionMessage_MentionsParamName()
        {
            var ex = Assert.ThrowsAny<ArgumentException>(() => EventFilter.ByField("bad path", "x"));
            Assert.Equal("path", ex.ParamName);
        }

        [Fact]
        public static void NullPath_ExceptionMessage_MentionsParamName()
        {
            var ex = Assert.ThrowsAny<ArgumentException>(() => EventFilter.FieldExists(null!));
            Assert.Equal("path", ex.ParamName);
        }
    }
}



