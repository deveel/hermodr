//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    public class GrpcDefaultsTests
    {
        [Fact]
        public void HttpClientName_IsHermodrGrpc()
        {
            Assert.Equal("Hermodr.Grpc", GrpcDefaults.HttpClientName);
        }

        [Fact]
        public void DefaultDeadline_IsThirtySeconds()
        {
            Assert.Equal(TimeSpan.FromSeconds(30), GrpcDefaults.DefaultDeadline);
        }
    }
}
