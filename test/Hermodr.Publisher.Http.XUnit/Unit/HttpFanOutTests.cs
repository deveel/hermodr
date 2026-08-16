//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

using CloudNative.CloudEvents;

namespace Hermodr
{
    public class HttpFanOutTests
    {
        private static CloudEvent MakeEvent() => new()
        {
            Type = "order.shipped",
            Source = new Uri("https://api.example.com/svc"),
            Id = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { id = 42 }),
        };

        private static HttpEndpoint MakeEndpoint(int index, HttpResilienceOptions? resilience = null) => new()
        {
            Url = $"https://svc{index}.example.com/events",
            HttpClientName = $"endpoint-{index}",
            Resilience = resilience
        };

        private static HttpResilienceOptions FastRetry(int maxAttempts) => new()
        {
            RetryMaxAttempts = maxAttempts,
            RetryDelay = TimeSpan.FromMilliseconds(10),
            RetryUseJitter = false
        };

        [Fact]
        public async Task PublishAsync_FansOutToAllEndpoints()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var requests = new ConcurrentQueue<HttpRequestMessage>();

            var channel = TestHttp.BuildChannel(
                [MakeEndpoint(0), MakeEndpoint(1)],
                new TestHttp.FakeHandler(req =>
                {
                    requests.Enqueue(req);
                    return TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.Equal(2, requests.Count);
            Assert.All(requests, r => Assert.Equal(HttpMethod.Post, r.Method));
        }

        [Fact]
        public async Task PublishAsync_SingleEndpointFailure_RethrowsEndpointException()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var channel = TestHttp.BuildChannel(
                [MakeEndpoint(0), MakeEndpoint(1, FastRetry(1))],
                new TestHttp.FakeHandler(req =>
                    req.RequestUri!.Host.StartsWith("svc1")
                        ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                        : TestHttp.OK()));

            // A single failed endpoint rethrows the individual endpoint exception
            // (the fan-out is not short-circuited: the healthy endpoint still receives
            // the event, but the failure surfaces to the caller).
            var ex = await Assert.ThrowsAsync<HttpStatusCodeException>(() =>
                channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken));

            Assert.Equal((int)HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        }

        [Fact]
        public async Task PublishAsync_MultipleEndpointFailures_AggregatesExceptions()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var channel = TestHttp.BuildChannel(
                [MakeEndpoint(0, FastRetry(1)), MakeEndpoint(1, FastRetry(1))],
                new TestHttp.FakeHandler(_ =>
                    new HttpResponseMessage(HttpStatusCode.BadGateway)));

            var ex = await Assert.ThrowsAsync<HttpPublishException>(() =>
                channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken));

            Assert.Equal(2, ex.Failures.Count);
            Assert.All(ex.Failures, f => Assert.IsType<HttpStatusCodeException>(f));
        }
    }
}