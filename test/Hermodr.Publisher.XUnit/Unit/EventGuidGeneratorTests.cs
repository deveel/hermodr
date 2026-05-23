// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Microsoft.Extensions.Options;

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Application")]
    [Trait("Feature", "EventPublisher")]
    public static class EventGuidGeneratorTests
    {
        [Fact]
        public static void Should_ReturnIdWithoutHyphens_When_FormatIsN()
        {
            // Arrange
            var options = Options.Create(new EventGuidGeneratorOptions { Format = "N" });
            var generator = new EventGuidGenerator(options);

            // Act
            var id = generator.GenerateId();

            // Assert
            Assert.NotEmpty(id);
            Assert.DoesNotContain("-", id);
            Assert.Equal(32, id.Length);
        }

        [Fact]
        public static void Should_ReturnIdWithHyphens_When_FormatIsD()
        {
            // Arrange
            var options = Options.Create(new EventGuidGeneratorOptions { Format = "D" });
            var generator = new EventGuidGenerator(options);

            // Act
            var id = generator.GenerateId();

            // Assert
            Assert.NotEmpty(id);
            Assert.Contains("-", id);
        }

        [Fact]
        public static void Should_GenerateUniqueIds_When_CalledMultipleTimes()
        {
            // Arrange & Act
            var id1 = EventGuidGenerator.Default.GenerateId();
            var id2 = EventGuidGenerator.Default.GenerateId();

            // Assert
            Assert.NotEqual(id1, id2);
        }

        [Fact]
        public static void Should_UseDefaultFormat_When_FormatIsNull()
        {
            // Arrange
            var options = Options.Create(new EventGuidGeneratorOptions { Format = null });
            var generator = new EventGuidGenerator(options);

            // Act
            var id = generator.GenerateId();

            // Assert
            Assert.NotEmpty(id);
            Assert.Equal(32, id.Length); // Default format is "N" (32 hex chars, no hyphens)
        }

        [Fact]
        public static void Should_UseDefaultFormat_When_NullOptionsArePassed()
        {
            // Arrange
            var generator = new EventGuidGenerator(null!);

            // Act
            var id = generator.GenerateId();

            // Assert
            Assert.NotEmpty(id);
        }

        [Fact]
        public static void Should_HaveNAsDefaultFormatConstant()
        {
            // Assert
            Assert.Equal("N", EventGuidGenerator.DefaultFormat);
        }

        [Fact]
        public static void Should_GenerateId_When_DefaultStaticInstanceIsUsed()
        {
            // Arrange & Act
            Assert.NotNull(EventGuidGenerator.Default);
            var id = EventGuidGenerator.Default.GenerateId();

            // Assert
            Assert.NotEmpty(id);
        }
    }
}
