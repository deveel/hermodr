//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System.Net;
using System.Text.Json;
using System.Xml.Linq;

namespace Deveel.Events
{
    public class WebhookPublishChannelTests
    {
        private static CloudEvent MakeEvent(string type = "person.created") => new()
        {
            Type    = type,
            Source  = new Uri("https://api.example.com/svc"),
            Id      = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data    = JsonSerializer.Serialize(new { name = "John Doe" }),
            Time    = DateTimeOffset.UtcNow
        };

#pragma warning disable CS0618
        private static readonly IWebhookSignatureProvider[] AllProviders = new IWebhookSignatureProvider[]
        {
            HmacSha256SignatureProvider.Default,
            HmacSha384SignatureProvider.Default,
            HmacSha512SignatureProvider.Default,
            HmacSha1SignatureProvider.Default,
        };
#pragma warning restore CS0618

        private static WebhookEventPublishChannel BuildChannel(
            HttpMessageHandler handler,
            Action<WebhookEventPublishChannelOptions>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => handler);

            var options = new WebhookEventPublishChannelOptions
            {
                EndpointUrl            = "https://webhook.example.com/receive",
                SigningSecret          = "test-secret",
                MaxRetryCount          = 2,
                RetryDelay             = TimeSpan.FromMilliseconds(10),
                RetryBackoffMultiplier = 1.5,
                MessageFormat          = WebhookMessageFormat.Json,
                SignatureAlgorithm     = WebhookSignatureAlgorithm.HmacSha256,
            };
            configure?.Invoke(options);

            var sp      = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IHttpClientFactory>();

            return new WebhookEventPublishChannel(
                Options.Create(options),
                factory,
                AllProviders);
        }

        // --- Basic delivery --------------------------------------------------

        [Fact]
        public async Task PublishAsync_SuccessOnFirstAttempt_NoRetry()
        {
            var requests = new List<HttpRequestMessage>();
            var handler  = new FakeHandler(req => { requests.Add(req); return OK(); });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            Assert.Single(requests);
        }

        // --- Standard webhook headers ----------------------------------------

        [Fact]
        public async Task PublishAsync_SetsSignatureHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            var sig = captured!.Headers.GetValues(WebhookDefaults.SignatureHeaderName).First();
            Assert.StartsWith("sha256=", sig);
        }

        [Fact]
        public async Task PublishAsync_SetsAlgorithmHeader_DefaultSha256()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            Assert.True(captured!.Headers.Contains(WebhookDefaults.SignatureAlgorithmHeaderName));
            Assert.Equal("hmac-sha256",
                captured.Headers.GetValues(WebhookDefaults.SignatureAlgorithmHeaderName).First());
        }

        [Fact]
        public async Task PublishAsync_SetsDeliveryIdHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            var id = captured!.Headers.GetValues(WebhookDefaults.DeliveryIdHeaderName).First();
            Assert.NotEmpty(id);
        }

        [Fact]
        public async Task PublishAsync_SetsTimestampHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            var ts = captured!.Headers.GetValues(WebhookDefaults.TimestampHeaderName).First();
            Assert.True(long.TryParse(ts, out var v) && v > 0);
        }

        [Fact]
        public async Task PublishAsync_SetsEventTypeHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler).PublishAsync(MakeEvent("order.placed"));

            Assert.Equal("order.placed",
                captured!.Headers.GetValues(WebhookDefaults.EventTypeHeaderName).First());
        }

        [Fact]
        public async Task PublishAsync_NoSignatureOrAlgorithmHeaderWhenSecretNotConfigured()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler, o => o.SigningSecret = null).PublishAsync(MakeEvent());

            Assert.False(captured!.Headers.Contains(WebhookDefaults.SignatureHeaderName));
            Assert.False(captured.Headers.Contains(WebhookDefaults.SignatureAlgorithmHeaderName));
        }

        // --- Algorithm selection (channel-level) ------------------------------

        [Theory]
        [InlineData(WebhookSignatureAlgorithm.HmacSha256, "sha256=", "hmac-sha256")]
        [InlineData(WebhookSignatureAlgorithm.HmacSha384, "sha384=", "hmac-sha384")]
        [InlineData(WebhookSignatureAlgorithm.HmacSha512, "sha512=", "hmac-sha512")]
        public async Task PublishAsync_ChannelAlgorithm_SetsCorrectHeaders(
            WebhookSignatureAlgorithm algorithm,
            string expectedSigPrefix,
            string expectedAlgorithmHeaderValue)
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler, o => o.SignatureAlgorithm = algorithm)
                  .PublishAsync(MakeEvent());

            var sig = captured!.Headers.GetValues(WebhookDefaults.SignatureHeaderName).First();
            Assert.StartsWith(expectedSigPrefix, sig);

            Assert.Equal(expectedAlgorithmHeaderValue,
                captured.Headers.GetValues(WebhookDefaults.SignatureAlgorithmHeaderName).First());
        }

        // --- Algorithm override (per-call) ------------------------------------

        [Theory]
        [InlineData(WebhookSignatureAlgorithm.HmacSha256, "sha256=", "hmac-sha256")]
        [InlineData(WebhookSignatureAlgorithm.HmacSha384, "sha384=", "hmac-sha384")]
        [InlineData(WebhookSignatureAlgorithm.HmacSha512, "sha512=", "hmac-sha512")]
        public async Task PublishAsync_PerCallAlgorithmOverride(
            WebhookSignatureAlgorithm algorithm,
            string expectedSigPrefix,
            string expectedAlgorithmHeader)
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            // Channel default is SHA-256; override per-call.
            await BuildChannel(handler).PublishAsync(MakeEvent(), new WebhookPublishOptions
            {
                SignatureAlgorithm = algorithm
            });

            var sig = captured!.Headers.GetValues(WebhookDefaults.SignatureHeaderName).First();
            Assert.StartsWith(expectedSigPrefix, sig);
            Assert.Equal(expectedAlgorithmHeader,
                captured.Headers.GetValues(WebhookDefaults.SignatureAlgorithmHeaderName).First());
        }

        // --- Retry behaviour -------------------------------------------------

        [Fact]
        public async Task PublishAsync_RetriesOnTransientFailure()
        {
            var count   = 0;
            var handler = new FakeHandler(_ =>
            {
                count++;
                return count < 2
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK);
            });

            await BuildChannel(handler, o =>
            {
                o.MaxRetryCount = 3;
                o.RetryDelay    = TimeSpan.FromMilliseconds(10);
            }).PublishAsync(MakeEvent());

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task PublishAsync_ThrowsAfterExhaustedRetries()
        {
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            await Assert.ThrowsAsync<WebhookDeliveryException>(
                () => BuildChannel(handler, o =>
                {
                    o.MaxRetryCount = 2;
                    o.RetryDelay    = TimeSpan.FromMilliseconds(1);
                }).PublishAsync(MakeEvent()));
        }

        [Fact]
        public async Task PublishAsync_ThrowsImmediatelyOnNonRetryableStatus()
        {
            var count   = 0;
            var handler = new FakeHandler(_ => { count++; return new HttpResponseMessage(HttpStatusCode.Forbidden); });

            await Assert.ThrowsAsync<WebhookDeliveryException>(
                () => BuildChannel(handler).PublishAsync(MakeEvent()));

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task PublishAsync_ThrowsWhenEndpointUrlNotConfigured()
        {
            var handler = new FakeHandler(_ => OK());
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => BuildChannel(handler, o => o.EndpointUrl = "").PublishAsync(MakeEvent()));
        }

        // --- Additional headers and per-call overrides -----------------------

        [Fact]
        public async Task PublishAsync_AdditionalHeadersAreSent()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler, o => o.AdditionalHeaders["X-Custom"] = "my-value")
                  .PublishAsync(MakeEvent());

            Assert.Equal("my-value", captured!.Headers.GetValues("X-Custom").First());
        }

        [Fact]
        public async Task PublishAsync_PerCallOverride_ChangesEndpoint()
        {
            var urls    = new List<string>();
            var handler = new FakeHandler(req => { urls.Add(req.RequestUri!.ToString()); return OK(); });

            var channel = BuildChannel(handler, o => o.EndpointUrl = "https://default.example.com/");
            await channel.PublishAsync(MakeEvent());
            await channel.PublishAsync(MakeEvent(), new WebhookPublishOptions
            {
                EndpointUrl = "https://override.example.com/"
            });

            Assert.Equal("https://default.example.com/",  urls[0]);
            Assert.Equal("https://override.example.com/", urls[1]);
        }

        [Fact]
        public async Task PublishAsync_PerCallOverride_ChangesMessageFormat()
        {
            var contentTypes = new List<string>();
            var handler      = new FakeHandler(req =>
            {
                contentTypes.Add(req.Content!.Headers.ContentType!.MediaType!);
                return OK();
            });

            var channel = BuildChannel(handler, o => o.MessageFormat = WebhookMessageFormat.Json);
            await channel.PublishAsync(MakeEvent());
            await channel.PublishAsync(MakeEvent(), new WebhookPublishOptions
            {
                MessageFormat = WebhookMessageFormat.Xml
            });

            Assert.Equal("application/json", contentTypes[0]);
            Assert.Equal("application/xml",  contentTypes[1]);
        }

        [Fact]
        public async Task PublishAsync_PerCallOverride_ChangesRetryCount()
        {
            var count   = 0;
            var handler = new FakeHandler(_ =>
            {
                count++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            });

            var channel = BuildChannel(handler, o =>
            {
                o.MaxRetryCount = 5;
                o.RetryDelay    = TimeSpan.FromMilliseconds(1);
            });

            await Assert.ThrowsAsync<WebhookDeliveryException>(
                () => channel.PublishAsync(MakeEvent(), new WebhookPublishOptions
                {
                    MaxRetryCount = 1,
                    RetryDelay    = TimeSpan.FromMilliseconds(1),
                }));

            Assert.Equal(2, count);  // 1 initial + 1 retry
        }

        [Fact]
        public async Task PublishAsync_PerCallOverride_MergesAdditionalHeaders()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            var channel = BuildChannel(handler, o => o.AdditionalHeaders["X-Channel"] = "channel-value");

            await channel.PublishAsync(MakeEvent(), new WebhookPublishOptions
            {
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-Call"]    = "call-value",
                    ["X-Channel"] = "overridden",
                }
            });

            Assert.Equal("overridden", captured!.Headers.GetValues("X-Channel").First());
            Assert.Equal("call-value", captured.Headers.GetValues("X-Call").First());
        }

        [Fact]
        public async Task PublishAsync_PerCallOverride_SuppressesSignatureWhenSecretEmpty()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            var channel = BuildChannel(handler, o => o.SigningSecret = "channel-secret");

            await channel.PublishAsync(MakeEvent(), new WebhookPublishOptions
            {
                SigningSecret = string.Empty
            });

            Assert.False(captured!.Headers.Contains(WebhookDefaults.SignatureHeaderName));
            Assert.False(captured.Headers.Contains(WebhookDefaults.SignatureAlgorithmHeaderName));
        }

        // --- Message body format ---------------------------------------------

        [Fact]
        public async Task PublishAsync_DefaultFormat_IsJson()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            Assert.Equal("application/json",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishAsync_JsonBodyIsValidCloudEventsJson()
        {
            string? body = null;
            var handler  = new FakeHandler(req =>
            {
                body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return OK();
            });

            await BuildChannel(handler, o => o.MessageFormat = WebhookMessageFormat.Json)
                  .PublishAsync(MakeEvent("invoice.created"));

            Assert.NotNull(body);
            var doc = JsonDocument.Parse(body!);
            Assert.Equal("invoice.created", doc.RootElement.GetProperty("type").GetString());
        }

        [Fact]
        public async Task PublishAsync_XmlFormat_ContentTypeIsCloudEventsXml()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler, o => o.MessageFormat = WebhookMessageFormat.Xml)
                  .PublishAsync(MakeEvent());

            Assert.Equal("application/xml",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishAsync_XmlBodyIsValidCloudEventsXml()
        {
            byte[]? bytes = null;
            var handler   = new FakeHandler(req =>
            {
                bytes = req.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return OK();
            });

            await BuildChannel(handler, o => o.MessageFormat = WebhookMessageFormat.Xml)
                  .PublishAsync(MakeEvent("order.shipped"));

            Assert.NotNull(bytes);
            var xml = XDocument.Load(new MemoryStream(bytes!));

            XNamespace ns = "http://cloudevents.io/xmlformat/V1";
            Assert.Equal(ns + "cloudevent", xml.Root!.Name);
            Assert.Equal("order.shipped", xml.Root!.Element(ns + "type")!.Value);
        }

        // --- DI integration --------------------------------------------------

        [Fact]
        public void UseWebhook_RegistersAllProviders()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher().UseWebhook(o =>
            {
                o.EndpointUrl   = "https://webhook.example.com/";
                o.SigningSecret = "secret";
            });

            var sp = services.BuildServiceProvider();

            var channels = sp.GetServices<IEventPublishChannel>();
            Assert.Contains(channels, c => c is WebhookEventPublishChannel);

            var providers = sp.GetServices<IWebhookSignatureProvider>().ToList();
            Assert.Contains(providers, p => p.Algorithm == WebhookSignatureAlgorithm.HmacSha256);
            Assert.Contains(providers, p => p.Algorithm == WebhookSignatureAlgorithm.HmacSha384);
            Assert.Contains(providers, p => p.Algorithm == WebhookSignatureAlgorithm.HmacSha512);
            Assert.Contains(providers, p => p.Algorithm == WebhookSignatureAlgorithm.HmacSha1);

            var serializers = sp.GetServices<IEventSerializer>().ToList();
            Assert.Contains(serializers, s => s.Format == WebhookMessageFormat.Json);
            Assert.Contains(serializers, s => s.Format == WebhookMessageFormat.Xml);
        }

        // --- Helpers ---------------------------------------------------------

        private static HttpResponseMessage OK() => new(HttpStatusCode.OK);

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> h) => _handler = h;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_handler(request));
        }
    }
}
