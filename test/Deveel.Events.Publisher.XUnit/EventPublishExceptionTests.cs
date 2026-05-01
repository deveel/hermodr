// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Deveel.Events
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Application")]
    [Trait("Feature", "EventPublisher")]
    public static class EventPublishExceptionTests
    {
        [Fact]
        public static void Should_CreateException_When_DefaultConstructorIsUsed()
        {
            // Arrange & Act
            var ex = new EventPublishException();

            // Assert
            Assert.NotNull(ex);
            Assert.Null(ex.InnerException);
        }

        [Fact]
        public static void Should_SetMessage_When_MessageConstructorIsUsed()
        {
            // Arrange & Act
            var ex = new EventPublishException("Test message");

            // Assert
            Assert.Equal("Test message", ex.Message);
            Assert.Null(ex.InnerException);
        }

        [Fact]
        public static void Should_SetMessageAndInnerException_When_BothAreProvided()
        {
            // Arrange
            var inner = new InvalidOperationException("inner error");

            // Act
            var ex = new EventPublishException("Test message", inner);

            // Assert
            Assert.Equal("Test message", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public static void Should_InheritFromException_When_ExceptionIsCreated()
        {
            // Arrange & Act
            var ex = new EventPublishException("test");

            // Assert
            Assert.IsAssignableFrom<Exception>(ex);
        }

        [Fact]
        public static void Should_AllowNullMessage_When_NullIsPassedToConstructor()
        {
            // Arrange & Act
            var ex = new EventPublishException(null);

            // Assert
            Assert.Null(ex.InnerException);
        }
    }
}
