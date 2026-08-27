//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Grpc.Core;

namespace Hermodr
{
    public class GrpcPublishExceptionTests
    {
        [Fact]
        public void Constructor_MessageOnly_FailuresEmpty()
        {
            var ex = new GrpcPublishException("publish failed");

            Assert.Equal("publish failed", ex.Message);
            Assert.Empty(ex.Failures);
            Assert.Equal(StatusCode.OK, ex.StatusCode);
        }

        [Fact]
        public void Constructor_MessageAndInner_FailuresSingle()
        {
            var inner = new InvalidOperationException("boom");
            var ex = new GrpcPublishException("publish failed", inner);

            Assert.Equal("publish failed", ex.Message);
            Assert.Same(inner, ex.InnerException);
            Assert.Single(ex.Failures);
            Assert.Same(inner, ex.Failures[0]);
        }

        [Fact]
        public void Constructor_MessageAndNullFailures_FailuresEmpty()
        {
            var ex = new GrpcPublishException("publish failed", (IEnumerable<Exception>)null!);

            Assert.Equal("publish failed", ex.Message);
            Assert.Empty(ex.Failures);
        }

        [Fact]
        public void Constructor_MessageAndFailures_FailuresList()
        {
            var failures = new Exception[]
            {
                new InvalidOperationException("f1"),
                new IOException("f2"),
                new TimeoutException("f3"),
            };

            var ex = new GrpcPublishException("multiple failures", failures);

            Assert.Equal(3, ex.Failures.Count);
            Assert.Same(failures[0], ex.Failures[0]);
            Assert.Same(failures[1], ex.Failures[1]);
            Assert.Same(failures[2], ex.Failures[2]);
        }

        [Fact]
        public void StatusCode_DefaultsToOK()
        {
            var ex = new GrpcPublishException("test");

            Assert.Equal(StatusCode.OK, ex.StatusCode);
        }
    }
}
