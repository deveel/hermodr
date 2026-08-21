//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

namespace Hermodr
{
    public class HttpAuthenticationTests
    {
        private static CloudEvent MakeEvent() => new()
        {
            Type = "person.deleted",
            Source = new Uri("https://api.example.com/svc"),
            Id = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { id = 42 }),
        };

        private static HttpEndpoint MakeEndpoint() => new()
        {
            Url = "https://svc.example.com/events",
            HttpClientName = "test-client"
        };

        [Fact]
        public async Task PublishAsync_BearerAuth_SetsAuthorizationHeader()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;

            var endpoint = MakeEndpoint();
            endpoint.Authentication = new HttpEndpointAuthentication
            {
                AuthenticationMode = HttpAuthenticationMode.Bearer,
                BearerToken = "secret-token"
            };

            var channel = TestHttp.BuildChannel(
                [endpoint],
                new TestHttp.FakeHandler(req =>
                {
                    captured = req;
                    return TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.NotNull(captured);
            Assert.Equal("Bearer", captured!.Headers.Authorization!.Scheme);
            Assert.Equal("secret-token", captured.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task PublishAsync_ApiKeyAuth_SetsDefaultHeader()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;

            var endpoint = MakeEndpoint();
            endpoint.Authentication = new HttpEndpointAuthentication
            {
                AuthenticationMode = HttpAuthenticationMode.ApiKey,
                ApiKey = "api-key-123"
            };

            var channel = TestHttp.BuildChannel(
                [endpoint],
                new TestHttp.FakeHandler(req =>
                {
                    captured = req;
                    return TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.NotNull(captured);
            Assert.Equal("api-key-123",
                captured!.Headers.GetValues("X-Api-Key").First());
        }

        [Fact]
        public async Task PublishAsync_ApiKeyAuth_CustomHeaderName()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;

            var endpoint = MakeEndpoint();
            endpoint.Authentication = new HttpEndpointAuthentication
            {
                AuthenticationMode = HttpAuthenticationMode.ApiKey,
                ApiKey = "api-key-456",
                ApiKeyHeaderName = "X-Custom-Api-Key"
            };

            var channel = TestHttp.BuildChannel(
                [endpoint],
                new TestHttp.FakeHandler(req =>
                {
                    captured = req;
                    return TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.NotNull(captured);
            Assert.Equal("api-key-456",
                captured!.Headers.GetValues("X-Custom-Api-Key").First());
        }

        [Fact]
        public async Task PublishAsync_NoAuth_SendsNoAuthorizationHeader()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;

            var channel = TestHttp.BuildChannel(
                [MakeEndpoint()],
                new TestHttp.FakeHandler(req =>
                {
                    captured = req;
                    return TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.NotNull(captured);
            Assert.Null(captured!.Headers.Authorization);
        }
    }
}