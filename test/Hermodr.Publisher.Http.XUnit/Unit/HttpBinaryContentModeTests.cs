//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

namespace Hermodr
{
    public class HttpBinaryContentModeTests
    {
        private static CloudEvent MakeEvent() => new()
        {
            Type = "person.updated",
            Source = new Uri("https://api.example.com/svc"),
            Id = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { name = "John Doe" }),
        };

        private static HttpEndpoint MakeEndpoint() => new()
        {
            Url = "https://svc.example.com/events",
            HttpClientName = "test-client",
            Format = EventMessageFormat.CloudEventsBinary
        };

        [Fact]
        public async Task PublishAsync_BinaryMode_ContentTypeIsDataContentType()
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
            Assert.Equal("application/json",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishAsync_BinaryMode_SetsCloudEventsHeaders()
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

            var @event = MakeEvent();

            await channel.PublishAsync(@event, cancellationToken: cancellationToken);

            Assert.NotNull(captured);
            Assert.Contains("ce-type", captured!.Headers.Select(h => h.Key));
            Assert.Equal("person.updated", captured.Headers.GetValues("ce-type").First());
            Assert.Equal(@event.Id, captured.Headers.GetValues("ce-id").First());
            Assert.Equal("application/json", captured.Headers.GetValues("ce-datacontenttype").First());
        }

        [Fact]
        public async Task PublishAsync_BinaryMode_BodyIsRawDataNotEnvelope()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            string? body = null;
            var channel = TestHttp.BuildChannel(
                [MakeEndpoint()],
                new TestHttp.FakeHandler(req =>
                {
                    captured = req;
                    body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.NotNull(body);

            // The body must be the raw event data, not the CloudEvents envelope.
            Assert.Contains("John Doe", body);
            Assert.DoesNotContain("specversion", body);
        }
    }
}