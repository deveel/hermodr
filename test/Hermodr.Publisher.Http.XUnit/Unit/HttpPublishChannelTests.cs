//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    public class HttpPublishChannelTests
    {
        private static CloudEvent MakeEvent(string type = "person.created") => new()
        {
            Type = type,
            Source = new Uri("https://api.example.com/svc"),
            Id = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { name = "John Doe" }),
        };

        private static HttpEndpoint MakeEndpoint(string url = "https://svc.example.com/events")
            => new()
            {
                Url = url,
                HttpClientName = "test-client",
                Format = EventMessageFormat.CloudEventsJson
            };

        // --- Basic delivery --------------------------------------------------

        [Fact]
        public async Task PublishAsync_Success_SendsSinglePost()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var requests = new List<HttpRequestMessage>();
            var channel = TestHttp.BuildChannel(
                [MakeEndpoint()],
                new TestHttp.FakeHandler(req =>
                {
                    requests.Add(req);
                    return TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.Single(requests);
            Assert.Equal(HttpMethod.Post, requests[0].Method);
            Assert.Equal("https://svc.example.com/events",
                requests[0].RequestUri!.ToString());
        }

        // --- Structured content mode -----------------------------------------

        [Fact]
        public async Task PublishAsync_StructuredJson_SetsCloudEventsContentType()
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
            Assert.Equal("application/cloudevents+json",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishAsync_StructuredJson_BodyIsEnvelope()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            string? body = null;
            var channel = TestHttp.BuildChannel(
                [MakeEndpoint()],
                new TestHttp.FakeHandler(req =>
                {
                    body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return TestHttp.OK();
                }));

            var @event = MakeEvent();

            await channel.PublishAsync(@event, cancellationToken: cancellationToken);

            Assert.NotNull(body);
            Assert.Contains("\"specversion\"", body);
            Assert.Contains($"\"{@event.Type}\"", body);
            Assert.Contains("John Doe", body);
        }

        [Fact]
        public async Task PublishAsync_StructuredXml_SetsContentTypeAndEnvelope()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            string? body = null;

            var endpoint = MakeEndpoint();
            endpoint.Format = EventMessageFormat.CloudEventsXml;

            var channel = TestHttp.BuildChannel(
                [endpoint],
                new TestHttp.FakeHandler(req =>
                {
                    captured = req;
                    body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.NotNull(captured);
            Assert.Equal("application/cloudevents+xml",
                captured!.Content!.Headers.ContentType!.MediaType);

            Assert.NotNull(body);
            var xml = XDocument.Parse(body!);

            XNamespace ns = "http://cloudevents.io/xmlformat/V1";
            Assert.Equal(ns + "cloudevent", xml.Root!.Name);
            Assert.Equal("1.0", xml.Root.Attribute("specversion")!.Value);
            Assert.Equal("person.created", xml.Root.Element(ns + "type")!.Value);
        }

        // --- Additional headers ----------------------------------------------

        [Fact]
        public async Task PublishAsync_AdditionalHeadersAreSent()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;

            var endpoint = MakeEndpoint();
            endpoint.AdditionalHeaders["X-Custom"] = "custom-value";
            endpoint.AdditionalHeaders["X-Tenant"] = "tenant-42";

            var channel = TestHttp.BuildChannel(
                [endpoint],
                new TestHttp.FakeHandler(req =>
                {
                    captured = req;
                    return TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.Equal("custom-value", captured!.Headers.GetValues("X-Custom").First());
            Assert.Equal("tenant-42", captured.Headers.GetValues("X-Tenant").First());
        }

        // --- Validation ------------------------------------------------------

        [Fact]
        public async Task PublishAsync_NoEndpoints_ThrowsValidationException()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var channel = TestHttp.BuildChannel(
                [MakeEndpoint()],
                new TestHttp.FakeHandler(_ => TestHttp.OK()));

            await Assert.ThrowsAsync<ValidationException>(() =>
                channel.PublishAsync(MakeEvent(),
                    new HttpPublishOptions { Endpoints = Array.Empty<HttpEndpoint>() },
                    cancellationToken));
        }

        [Fact]
        public async Task PublishAsync_InvalidEndpointUrl_ThrowsValidationException()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var channel = TestHttp.BuildChannel(
                [MakeEndpoint()],
                new TestHttp.FakeHandler(_ => TestHttp.OK()));

            await Assert.ThrowsAsync<ValidationException>(() =>
                channel.PublishAsync(MakeEvent(),
                    new HttpPublishOptions
                    {
                        Endpoints = [new HttpEndpoint { Url = "not-a-url" }]
                    },
                    cancellationToken));
        }

        // --- Non-success status ----------------------------------------------

        [Fact]
        public async Task PublishAsync_NonSuccessStatus_ThrowsHttpStatusCodeException()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var channel = TestHttp.BuildChannel(
                [MakeEndpoint()],
                new TestHttp.FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)));

            var ex = await Assert.ThrowsAsync<HttpStatusCodeException>(() =>
                channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken));

            Assert.Equal((int)HttpStatusCode.BadRequest, ex.StatusCode);
        }

        // --- Per-call overrides ----------------------------------------------

        [Fact]
        public async Task PublishAsync_PerCallEndpoints_OverrideChannelDefaults()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var urls = new List<string>();
            var channel = TestHttp.BuildChannel(
                [MakeEndpoint("https://default.example.com/events")],
                new TestHttp.FakeHandler(req =>
                {
                    lock (urls)
                        urls.Add(req.RequestUri!.ToString());
                    return TestHttp.OK();
                }));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);
            await channel.PublishAsync(MakeEvent(),
                new HttpPublishOptions
                {
                    Endpoints = [MakeEndpoint("https://override.example.com/events")]
                },
                cancellationToken);

            lock (urls)
            {
                Assert.Equal(2, urls.Count);
                Assert.Equal("https://default.example.com/events", urls[0]);
                Assert.Equal("https://override.example.com/events", urls[1]);
            }
        }
    }
}