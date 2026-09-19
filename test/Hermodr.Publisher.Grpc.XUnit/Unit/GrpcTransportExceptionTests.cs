//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Grpc.Core;

namespace Hermodr
{
    public class GrpcTransportExceptionTests
    {
        [Fact]
        public void Constructor_MessageOnly_Succeeds()
        {
            var ex = new GrpcTransportException("transport error");

            Assert.Equal("transport error", ex.Message);
            Assert.Empty(ex.Failures);
        }

        [Fact]
        public void Constructor_WithRpcException_ExtractsStatusCode()
        {
            var rpcEx = new RpcException(new Status(StatusCode.DeadlineExceeded, "timeout"));

            var ex = new GrpcTransportException("delivery failed", rpcEx);

            Assert.Equal(StatusCode.DeadlineExceeded, ex.StatusCode);
        }

        [Fact]
        public void Constructor_WithNonRpcException_KeepsDefaultStatusCode()
        {
            var inner = new InvalidOperationException("non-grpc");

            var ex = new GrpcTransportException("delivery failed", inner);

            Assert.Equal(StatusCode.OK, ex.StatusCode);
        }
    }
}
