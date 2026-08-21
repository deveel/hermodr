//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Net;
using System.Text.Json;

using CloudNative.CloudEvents;

namespace Hermodr
{
    public class HttpResilienceTests
    {
        private static CloudEvent MakeEvent() => new()
        {
            Type = "order.placed",
            Source = new Uri("https://api.example.com/svc"),
            Id = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { id = 42 }),
        };

        private static HttpEndpoint MakeEndpoint(HttpResilienceOptions? resilience = null) => new()
        {
            Url = "https://svc.example.com/events",
            HttpClientName = "test-client",
            Resilience = resilience
        };

        private static HttpResilienceOptions FastRetry(int maxAttempts) => new()
        {
            RetryMaxAttempts = maxAttempts,
            RetryDelay = TimeSpan.FromMilliseconds(10),
            RetryUseJitter = false
        };

        [Fact]
        public async Task PublishAsync_RetriesOnTransientStatus()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var count = 0;

            var channel = TestHttp.BuildChannel(
                [MakeEndpoint(FastRetry(3))],
                new TestHttp.FakeHandler(_ =>
                {
                    count++;
                    return count < 3
                        ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                        : TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.Equal(3, count);
        }

        [Fact]
        public async Task PublishAsync_ThrowsAfterExhaustedRetries()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var count = 0;

            var channel = TestHttp.BuildChannel(
                [MakeEndpoint(FastRetry(2))],
                new TestHttp.FakeHandler(_ =>
                {
                    count++;
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }));

            var ex = await Assert.ThrowsAsync<HttpStatusCodeException>(() =>
                channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken));

            Assert.Equal((int)HttpStatusCode.ServiceUnavailable, ex.StatusCode);
            Assert.Equal(3, count); // 1 initial + 2 retries
        }

        [Fact]
        public async Task PublishAsync_NonRetryableStatus_ThrowsImmediately()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var count = 0;

            var channel = TestHttp.BuildChannel(
                [MakeEndpoint(FastRetry(5))],
                new TestHttp.FakeHandler(_ =>
                {
                    count++;
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);
                }));

            var ex = await Assert.ThrowsAsync<HttpStatusCodeException>(() =>
                channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken));

            Assert.Equal(403, ex.StatusCode);
            Assert.Equal(1, count); // 403 is not retryable
        }

        [Fact]
        public async Task PublishAsync_TransportFailure_ThrowsHttpTransportException()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var count = 0;

            var channel = TestHttp.BuildChannel(
                [MakeEndpoint(FastRetry(1))],
                new TestHttp.FakeHandler(_ =>
                {
                    count++;
                    throw new HttpRequestException("connection refused");
                }));

            var ex = await Assert.ThrowsAsync<HttpTransportException>(() =>
                channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken));

            Assert.NotNull(ex.InnerException);
            Assert.Equal(2, count); // 1 initial + 1 retry
        }
    }
}