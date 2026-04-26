// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Deveel.Events
{
    public static class EventPublishExceptionTests
    {
        [Fact]
        public static void DefaultConstructor_CreatesException()
        {
            var ex = new EventPublishException();
            Assert.NotNull(ex);
            Assert.Null(ex.InnerException);
        }

        [Fact]
        public static void MessageConstructor_SetsMessage()
        {
            var ex = new EventPublishException("Test message");
            Assert.Equal("Test message", ex.Message);
            Assert.Null(ex.InnerException);
        }

        [Fact]
        public static void MessageAndInnerExceptionConstructor_SetsBoth()
        {
            var inner = new InvalidOperationException("inner error");
            var ex = new EventPublishException("Test message", inner);
            Assert.Equal("Test message", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public static void EventPublishException_IsException()
        {
            var ex = new EventPublishException("test");
            Assert.IsAssignableFrom<Exception>(ex);
        }

        [Fact]
        public static void NullMessage_IsAllowed()
        {
            var ex = new EventPublishException(null);
            Assert.Null(ex.InnerException);
        }
    }
}

