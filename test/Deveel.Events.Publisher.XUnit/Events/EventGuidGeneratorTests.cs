// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    public static class EventGuidGeneratorTests
    {
        [Fact]
        public static void GenerateId_DefaultFormat_ReturnsNoHyphens()
        {
            var options = Options.Create(new EventGuidGeneratorOptions { Format = "N" });
            var generator = new EventGuidGenerator(options);
            var id = generator.GenerateId();
            Assert.NotEmpty(id);
            Assert.DoesNotContain("-", id);
            Assert.Equal(32, id.Length);
        }

        [Fact]
        public static void GenerateId_DFormat_ContainsHyphens()
        {
            var options = Options.Create(new EventGuidGeneratorOptions { Format = "D" });
            var generator = new EventGuidGenerator(options);
            var id = generator.GenerateId();
            Assert.NotEmpty(id);
            Assert.Contains("-", id);
        }

        [Fact]
        public static void GenerateId_Default_IsUnique()
        {
            var id1 = EventGuidGenerator.Default.GenerateId();
            var id2 = EventGuidGenerator.Default.GenerateId();
            Assert.NotEqual(id1, id2);
        }

        [Fact]
        public static void GenerateId_NullFormatUsesDefault()
        {
            var options = Options.Create(new EventGuidGeneratorOptions { Format = null });
            var generator = new EventGuidGenerator(options);
            var id = generator.GenerateId();
            Assert.NotEmpty(id);
            // Default format is "N" (32 hex chars, no hyphens)
            Assert.Equal(32, id.Length);
        }

        [Fact]
        public static void GenerateId_NullOptions_UsesDefault()
        {
            // Passing null for options should still create a valid generator
            var generator = new EventGuidGenerator(null!);
            var id = generator.GenerateId();
            Assert.NotEmpty(id);
        }

        [Fact]
        public static void DefaultFormat_ConstantIsN()
        {
            Assert.Equal("N", EventGuidGenerator.DefaultFormat);
        }

        [Fact]
        public static void Default_StaticInstance_GeneratesIds()
        {
            Assert.NotNull(EventGuidGenerator.Default);
            var id = EventGuidGenerator.Default.GenerateId();
            Assert.NotEmpty(id);
        }
    }
}

