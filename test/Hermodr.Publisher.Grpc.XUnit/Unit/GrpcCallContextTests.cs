//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Grpc.Core;

namespace Hermodr
{
    public class GrpcCallContextTests
    {
        [Fact]
        public void Constructor_SetsAllProperties()
        {
            var callInvoker = new DummyInvoker();
            var headers = new Metadata { { "x-key", "val" } };
            var deadline = DateTime.UtcNow.Add(TimeSpan.FromSeconds(10));
            var ct = new CancellationToken(canceled: true);

            var context = new GrpcCallContext(callInvoker, headers, deadline, ct);

            Assert.Same(callInvoker, context.CallInvoker);
            Assert.Same(headers, context.Headers);
            Assert.Equal(deadline, context.Deadline);
            Assert.Equal(ct, context.CancellationToken);
        }

        [Fact]
        public void Equality_SameValues_AreEqual()
        {
            var invoker = new DummyInvoker();
            var headers = new Metadata { { "x-key", "val" } };
            var deadline = DateTime.UtcNow;
            var ct = CancellationToken.None;

            var a = new GrpcCallContext(invoker, headers, deadline, ct);
            var b = new GrpcCallContext(invoker, headers, deadline, ct);

            Assert.Equal(a, b);
            Assert.True(a == b);
        }

        [Fact]
        public void Equality_DifferentValues_AreNotEqual()
        {
            var invoker = new DummyInvoker();
            var headers = new Metadata { { "x-key", "val" } };
            var deadline = DateTime.UtcNow;

            var a = new GrpcCallContext(invoker, headers, deadline, CancellationToken.None);
            var b = new GrpcCallContext(invoker, headers, deadline, new CancellationToken(canceled: true));

            Assert.NotEqual(a, b);
            Assert.True(a != b);
        }

        private sealed class DummyInvoker : CallInvoker
        {
            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
                => throw new NotSupportedException();
            public override TResponse BlockingUnaryCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
                => throw new NotSupportedException();
            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string? host, CallOptions options)
                => throw new NotSupportedException();
            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
                => throw new NotSupportedException();
            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string? host, CallOptions options)
                => throw new NotSupportedException();
        }
    }
}
