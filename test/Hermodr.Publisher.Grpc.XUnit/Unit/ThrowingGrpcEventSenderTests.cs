//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Grpc.Core;

namespace Hermodr
{
    public class ThrowingGrpcEventSenderTests
    {
        [Fact]
        public async Task SendAsync_ThrowsWithHelpfulMessage()
        {
            var sender = TestGrpc.CreateThrowingSender();
            var context = new GrpcCallContext(
                null!, new Metadata(), DateTime.UtcNow, CancellationToken.None);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sender.SendAsync(TestGrpc.MakeEvent(), context));

            Assert.Contains("IGrpcEventSender", ex.Message);
            Assert.Contains("AddGrpcEventSender", ex.Message);
        }

        [Fact]
        public async Task SendBatchAsync_ThrowsWithHelpfulMessage()
        {
            var sender = TestGrpc.CreateThrowingSender();
            var context = new GrpcCallContext(
                null!, new Metadata(), DateTime.UtcNow, CancellationToken.None);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sender.SendBatchAsync(new[] { TestGrpc.MakeEvent() }, context));

            Assert.Contains("IGrpcEventSender", ex.Message);
        }
    }
}
