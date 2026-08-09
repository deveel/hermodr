//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    public class WebhookBinaryContentModeTests
    {
        private static readonly DateTimeOffset FixedNow =
            new(2026, 01, 15, 12, 00, 00, TimeSpan.Zero);

        private const string SigningSecret = "test-secret";
        private const string EndpointUrl = "https://webhook.example.com/receive";

        private static CloudEvent MakeEvent(
            string type = "person.created",
            string? dataContentType = "application/json",
            string? data = """{"name":"John Doe"}""",
            string? subject = null) => new(CloudEventsSpecVersion.V1_0)
        {
            Type = type,
            Source = new Uri("https://api.example.com/svc"),
            Id = Guid.NewGuid().ToString("N"),
            DataContentType = dataContentType,
            Data = data,
            Time = FixedNow,
            Subject = subject
        };

        private static IBatchEventPublishChannel BuildChannel(
            HttpMessageHandler handler,
            Action<WebhookPublishOptions>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .UseSystemTime<MutableSystemTime>()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = EndpointUrl;
                    o.SigningSecret = SigningSecret;
                    o.MaxRetryCount = 1;
                    o.RetryDelay = TimeSpan.FromMilliseconds(10);
                    o.MessageFormat = EventMessageFormat.CloudEventsBinary;
                    o.SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha256;
                    configure?.Invoke(o);
                });

            MutableSystemTime.UtcNowValue = FixedNow;

            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            return services.BuildServiceProvider()
                .GetRequiredService<IBatchEventPublishChannel>();
        }

        // --- ce-* header emission ------------------------------------------------

        [Fact]
        public async Task PublishAsync_BinaryMode_EmitsCeHeaders()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            var evt = MakeEvent("user.signed.up");
            await BuildChannel(handler).PublishAsync(evt, cancellationToken: cancellationToken);

            Assert.NotNull(captured);
            var headers = captured!.Headers;

            Assert.Equal("1.0", headers.GetValues("ce-specversion").First());
            Assert.Equal(evt.Id, headers.GetValues("ce-id").First());
            Assert.Equal("user.signed.up", headers.GetValues("ce-type").First());
            Assert.Equal("https://api.example.com/svc", headers.GetValues("ce-source").First());
            Assert.Equal(FixedNow.ToString("O"), headers.GetValues("ce-time").First());
            Assert.Equal("application/json", headers.GetValues("ce-datacontenttype").First());
        }

        [Fact]
        public async Task PublishAsync_BinaryMode_EmitsSubjectHeaderWhenPresent()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler)
                .PublishAsync(MakeEvent(subject: "usr-123"), cancellationToken: cancellationToken);

            Assert.Equal("usr-123", captured!.Headers.GetValues("ce-subject").First());
        }

        [Fact]
        public async Task PublishAsync_BinaryMode_OmitsSubjectHeaderWhenAbsent()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler).PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.False(captured!.Headers.Contains("ce-subject"));
        }

        [Fact]
        public async Task PublishAsync_BinaryMode_ExtensionAttributesMappedToCeHeaders()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            var evt = MakeEvent();
            evt["traceparent"] = "00-abc-def-01";
            evt["dataschema"] = new Uri("https://schemas.example.com/person");

            await BuildChannel(handler).PublishAsync(evt, cancellationToken: cancellationToken);

            Assert.Equal("00-abc-def-01", captured!.Headers.GetValues("ce-traceparent").First());
            Assert.Equal("https://schemas.example.com/person",
                captured.Headers.GetValues("ce-dataschema").First());
        }

        // --- Content-Type / body -------------------------------------------------

        [Fact]
        public async Task PublishAsync_BinaryMode_ContentTypeIsDataContentType()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler).PublishAsync(
                MakeEvent(dataContentType: "application/vnd.custom+json"),
                cancellationToken: cancellationToken);

            Assert.Equal("application/vnd.custom+json",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishAsync_BinaryMode_DataContentTypeNull_FallsBackToJson()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler).PublishAsync(
                MakeEvent(dataContentType: null),
                cancellationToken: cancellationToken);

            Assert.Equal("application/json",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishAsync_BinaryMode_BodyIsDataOnly_NotEnvelope()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            byte[]? bytes = null;
            var handler = new FakeHandler(req =>
            {
                bytes = req.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return OK();
            });

            var data = """{"name":"John Doe"}""";
            await BuildChannel(handler).PublishAsync(MakeEvent(data: data), cancellationToken: cancellationToken);

            Assert.NotNull(bytes);
            var body = Encoding.UTF8.GetString(bytes!);

            // The CloudEvents SDK serializes a string-typed Data value as a JSON
            // string token (the spec's binary-mode data encoding for
            // application/json data): the body is a quoted JSON string whose
            // value is the original data. Parse and assert the round-trip.
            using var doc = JsonDocument.Parse(body);
            Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
            Assert.Equal(data, doc.RootElement.GetString());

            // The body must not embed the CloudEvents structured envelope: a
            // structured envelope would be a JSON object with a "specversion"
            // property. The binary-mode body here is a JSON string, never an
            // object — so specversion cannot be present.
            Assert.False(doc.RootElement.ValueKind == JsonValueKind.Object &&
                         doc.RootElement.TryGetProperty("specversion", out _),
                "Binary-mode body must not embed the CloudEvents envelope.");
        }

        [Fact]
        public async Task PublishAsync_BinaryMode_NullData_ProducesEmptyBody()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            byte[]? bytes = null;
            var handler = new FakeHandler(req =>
            {
                bytes = req.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return OK();
            });

            await BuildChannel(handler).PublishAsync(
                MakeEvent(data: null, dataContentType: null),
                cancellationToken: cancellationToken);

            Assert.NotNull(bytes);
            Assert.Empty(bytes!);
        }

        // --- Signing semantics ---------------------------------------------------

        [Fact]
        public async Task PublishAsync_BinaryMode_SignatureOverBodyBytes()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            byte[]? capturedBody = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                capturedBody = req.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return OK();
            });

            await BuildChannel(handler).PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.NotNull(captured);
            Assert.NotNull(capturedBody);

            var sigHeader = captured!.Headers.GetValues(WebhookDefaults.SignatureHeaderName).First();
            Assert.StartsWith("sha256=", sigHeader);

            // Recompute HMAC over "{timestamp}.{body}" using the same scheme as
            // HmacWebhookSignatureProvider and compare.
            var timestamp = FixedNow.ToUnixTimeSeconds();
            var message = Encoding.UTF8.GetBytes($"{timestamp}.")
                .Concat(capturedBody!).ToArray();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SigningSecret));
            var expected = "sha256=" +
                Convert.ToHexString(hmac.ComputeHash(message)).ToLowerInvariant();

            Assert.Equal(expected, sigHeader);
        }

        [Fact]
        public async Task PublishAsync_BinaryMode_LegacyHeadersStillPresent()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler).PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.True(captured!.Headers.Contains(WebhookDefaults.DeliveryIdHeaderName));
            Assert.True(captured.Headers.Contains(WebhookDefaults.TimestampHeaderName));
            Assert.True(captured.Headers.Contains(WebhookDefaults.SignatureAlgorithmHeaderName));
            Assert.Equal("person.created",
                captured.Headers.GetValues(WebhookDefaults.EventTypeHeaderName).First());
        }

        // --- Batch ----------------------------------------------------------------

        [Fact]
        public async Task PublishBatchAsync_BinaryMode_Throws()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var handler = new FakeHandler(_ => OK());

            var channel = BuildChannel(handler);
            var batch = new[] { MakeEvent(), MakeEvent() };

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                channel.PublishBatchAsync(batch, cancellationToken: cancellationToken));
        }

        // --- Per-call override ---------------------------------------------------

        [Fact]
        public async Task PublishAsync_BinaryMode_PerCallMessageFormatOverride()
        {
            // Channel configured for plain JSON; per-call override selects binary.
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .UseSystemTime<MutableSystemTime>()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = EndpointUrl;
                    o.SigningSecret = SigningSecret;
                    o.MaxRetryCount = 1;
                    o.MessageFormat = EventMessageFormat.Json;
                });
            MutableSystemTime.UtcNowValue = FixedNow;
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IBatchEventPublishChannel>();

            await channel.PublishAsync(MakeEvent(),
                new WebhookPublishOptions { MessageFormat = EventMessageFormat.CloudEventsBinary },
                cancellationToken);

            Assert.NotNull(captured);
            Assert.True(captured!.Headers.Contains("ce-type"));
            Assert.Equal("application/json",
                captured.Content!.Headers.ContentType!.MediaType);
        }

        // --- Regression: default behaviour unchanged -----------------------------

        [Fact]
        public async Task PublishAsync_DefaultFormat_DoesNotEmitCeHeaders()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            // Channel explicitly on the legacy default format.
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .UseSystemTime<MutableSystemTime>()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = EndpointUrl;
                    o.SigningSecret = SigningSecret;
                    o.MaxRetryCount = 1;
                    o.MessageFormat = EventMessageFormat.Json;
                });
            MutableSystemTime.UtcNowValue = FixedNow;
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IBatchEventPublishChannel>();

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.NotNull(captured);
            Assert.False(captured!.Headers.Contains("ce-type"),
                "Legacy default format must not emit ce-* headers.");
            Assert.Equal("application/json",
                captured.Content!.Headers.ContentType!.MediaType);
        }

        // --- Helpers -----------------------------------------------------------

        private static HttpResponseMessage OK() => new(HttpStatusCode.OK);

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> h) => _handler = h;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_handler(request));
        }

        private sealed class MutableSystemTime : IEventSystemTime
        {
            public static DateTimeOffset UtcNowValue { get; set; }
            public DateTimeOffset UtcNow => UtcNowValue;
        }
    }
}