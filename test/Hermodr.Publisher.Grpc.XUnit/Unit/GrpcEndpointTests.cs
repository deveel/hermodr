//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    public class GrpcEndpointTests
    {
        [Fact]
        public void Defaults_AllPropertiesNull_ExceptAddress()
        {
            var endpoint = new GrpcEndpoint();

            Assert.Equal(string.Empty, endpoint.Address);
            Assert.Null(endpoint.HttpClientName);
            Assert.Null(endpoint.SenderName);
            Assert.Null(endpoint.Deadline);
            Assert.Empty(endpoint.Headers);
        }

        [Fact]
        public void Deadline_CanBeSetAndRetrieved()
        {
            var deadline = TimeSpan.FromMinutes(2);

            var endpoint = new GrpcEndpoint { Deadline = deadline };

            Assert.Equal(deadline, endpoint.Deadline);
        }

        [Fact]
        public void Headers_CanAddAndRetrieve()
        {
            var endpoint = new GrpcEndpoint();

            endpoint.Headers["x-custom"] = "value";
            endpoint.Headers["authorization"] = "Bearer token";

            Assert.Equal(2, endpoint.Headers.Count);
            Assert.Equal("value", endpoint.Headers["x-custom"]);
            Assert.Equal("Bearer token", endpoint.Headers["authorization"]);
        }

        [Fact]
        public void AllProperties_CanBeSet()
        {
            var deadline = TimeSpan.FromSeconds(45);

            var endpoint = new GrpcEndpoint
            {
                Address = "https://svc.example.com:5001",
                HttpClientName = "custom-client",
                SenderName = "orders",
                Deadline = deadline,
            };
            endpoint.Headers["x-trace"] = "abc123";

            Assert.Equal("https://svc.example.com:5001", endpoint.Address);
            Assert.Equal("custom-client", endpoint.HttpClientName);
            Assert.Equal("orders", endpoint.SenderName);
            Assert.Equal(deadline, endpoint.Deadline);
            Assert.Equal("abc123", endpoint.Headers["x-trace"]);
        }
    }
}
