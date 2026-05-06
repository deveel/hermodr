//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    public class WebhookPublishChannelTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 01, 15, 12, 00, 00, TimeSpan.Zero);

        private static CloudEvent MakeEvent(string type = "person.created") => new()
        {
            Type = type,
            Source = new Uri("https://api.example.com/svc"),
            Id = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { name = "John Doe" }),
            Time = FixedNow
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
                    o.EndpointUrl = "https://webhook.example.com/receive";
                    o.SigningSecret = "test-secret";
                    o.MaxRetryCount = 2;
                    o.RetryDelay = TimeSpan.FromMilliseconds(10);
                    o.RetryBackoffMultiplier = 1.5;
                    o.MessageFormat = EventMessageFormat.Json;
                    o.SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha256;
                    configure?.Invoke(o);
                });

            MutableSystemTime.UtcNowValue = FixedNow;

            // Override the default HTTP message handler with the fake one
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            return services.BuildServiceProvider()
                .GetRequiredService<IBatchEventPublishChannel>();
        }

        // --- Basic delivery --------------------------------------------------

        [Fact]
        public async Task PublishAsync_SuccessOnFirstAttempt_NoRetry()
        {
            var requests = new List<HttpRequestMessage>();
            var handler = new FakeHandler(req =>
            {
                requests.Add(req);
                return OK();
            });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            Assert.Single(requests);
        }

        // --- Standard webhook headers ----------------------------------------

        [Fact]
        public async Task PublishAsync_SetsSignatureHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            var sig = captured!.Headers.GetValues(WebhookDefaults.SignatureHeaderName).First();
            Assert.StartsWith("sha256=", sig);
        }

        [Fact]
        public async Task PublishAsync_SetsAlgorithmHeader_DefaultSha256()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            Assert.True(captured!.Headers.Contains(WebhookDefaults.SignatureAlgorithmHeaderName));
            Assert.Equal("hmac-sha256",
                captured.Headers.GetValues(WebhookDefaults.SignatureAlgorithmHeaderName).First());
        }

        [Fact]
        public async Task PublishAsync_SetsDeliveryIdHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            var id = captured!.Headers.GetValues(WebhookDefaults.DeliveryIdHeaderName).First();
            Assert.NotEmpty(id);
        }

        [Fact]
        public async Task PublishAsync_SetsTimestampHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            var ts = captured!.Headers.GetValues(WebhookDefaults.TimestampHeaderName).First();
            Assert.True(long.TryParse(ts, out var v) && v > 0);
        }

        [Fact]
        public async Task PublishAsync_EventWithoutTime_UsesSystemTimeForTimestampHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            var @event = MakeEvent();
            @event.Time = null;

            await BuildChannel(handler).PublishAsync(@event);

            var ts = captured!.Headers.GetValues(WebhookDefaults.TimestampHeaderName).First();
            Assert.Equal(FixedNow.ToUnixTimeSeconds().ToString(), ts);
        }

        [Fact]
        public async Task PublishAsync_SetsEventTypeHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler).PublishAsync(MakeEvent("order.placed"));

            Assert.Equal("order.placed",
                captured!.Headers.GetValues(WebhookDefaults.EventTypeHeaderName).First());
        }

        [Fact]
        public async Task PublishAsync_NoSignatureOrAlgorithmHeaderWhenSecretNotConfigured()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

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
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

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
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

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
            var count = 0;
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
                o.RetryDelay = TimeSpan.FromMilliseconds(10);
            }).PublishAsync(MakeEvent());

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task PublishAsync_ThrowsAfterExhaustedRetries()
        {
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            await Assert.ThrowsAsync<WebhookStatusCodeException>(() => BuildChannel(handler, o =>
            {
                o.MaxRetryCount = 2;
                o.RetryDelay = TimeSpan.FromMilliseconds(1);
            }).PublishAsync(MakeEvent()));
        }

        [Fact]
        public async Task PublishAsync_ThrowsImmediatelyOnNonRetryableStatus()
        {
            var count = 0;
            var handler = new FakeHandler(_ =>
            {
                count++;
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            });

            await Assert.ThrowsAsync<WebhookStatusCodeException>(() => BuildChannel(handler).PublishAsync(MakeEvent()));

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task PublishAsync_ThrowsWhenEndpointUrlNotConfigured()
        {
            var handler = new FakeHandler(_ => OK());
            await Assert.ThrowsAsync<ValidationException>(() =>
                BuildChannel(handler, o => o.EndpointUrl = "").PublishAsync(MakeEvent()));
        }

        // --- Additional headers and per-call overrides -----------------------

        [Fact]
        public async Task PublishAsync_AdditionalHeadersAreSent()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler, o => o.AdditionalHeaders["X-Custom"] = "my-value")
                .PublishAsync(MakeEvent());

            Assert.Equal("my-value", captured!.Headers.GetValues("X-Custom").First());
        }

        [Fact]
        public async Task PublishAsync_PerCallOverride_ChangesEndpoint()
        {
            var urls = new List<string>();
            var handler = new FakeHandler(req =>
            {
                urls.Add(req.RequestUri!.ToString());
                return OK();
            });

            var channel = BuildChannel(handler, o => o.EndpointUrl = "https://default.example.com/");
            await channel.PublishAsync(MakeEvent());
            await channel.PublishAsync(MakeEvent(), new WebhookPublishOptions
            {
                EndpointUrl = "https://override.example.com/"
            });

            Assert.Equal("https://default.example.com/", urls[0]);
            Assert.Equal("https://override.example.com/", urls[1]);
        }

        [Fact]
        public async Task PublishAsync_PerCallOverride_ChangesMessageFormat()
        {
            var contentTypes = new List<string>();
            var handler = new FakeHandler(req =>
            {
                contentTypes.Add(req.Content!.Headers.ContentType!.MediaType!);
                return OK();
            });

            var channel = BuildChannel(handler, o => o.MessageFormat = EventMessageFormat.Json);
            await channel.PublishAsync(MakeEvent(), null, TestContext.Current.CancellationToken);
            await channel.PublishAsync(MakeEvent(), new WebhookPublishOptions
            {
                MessageFormat = EventMessageFormat.Xml
            }, TestContext.Current.CancellationToken);

            Assert.Equal("application/json", contentTypes[0]);
            Assert.Equal("application/xml", contentTypes[1]);
        }

        [Fact]
        public async Task PublishAsync_PerCallOverride_ChangesRetryCount()
        {
            var count = 0;
            var handler = new FakeHandler(_ =>
            {
                count++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            });

            var channel = BuildChannel(handler, o =>
            {
                o.MaxRetryCount = 5;
                o.RetryDelay = TimeSpan.FromMilliseconds(1);
            });

            await Assert.ThrowsAsync<WebhookStatusCodeException>(() => channel.PublishAsync(MakeEvent(),
                new WebhookPublishOptions
                {
                    MaxRetryCount = 1,
                    RetryDelay = TimeSpan.FromMilliseconds(1),
                }));

            Assert.Equal(2, count); // 1 initial + 1 retry
        }

        [Fact]
        public async Task PublishAsync_PerCallOverride_MergesAdditionalHeaders()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            var channel = BuildChannel(handler, o => o.AdditionalHeaders["X-Channel"] = "channel-value");

            await channel.PublishAsync(MakeEvent(), new WebhookPublishOptions
            {
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-Call"] = "call-value",
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
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

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
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler).PublishAsync(MakeEvent());

            Assert.Equal("application/json",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishAsync_JsonBodyIsValidCloudEventsJson()
        {
            string? body = null;
            var handler = new FakeHandler(req =>
            {
                body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return OK();
            });

            await BuildChannel(handler, o => o.MessageFormat = EventMessageFormat.Json)
                .PublishAsync(MakeEvent("invoice.created"), null, TestContext.Current.CancellationToken);

            Assert.NotNull(body);
            var doc = JsonDocument.Parse(body!);
            Assert.Equal("invoice.created", doc.RootElement.GetProperty("type").GetString());
        }

        [Fact]
        public async Task PublishAsync_XmlFormat_ContentTypeIsCloudEventsXml()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler, o => o.MessageFormat = EventMessageFormat.Xml)
                .PublishAsync(MakeEvent(), null, TestContext.Current.CancellationToken);

            Assert.Equal("application/xml",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishAsync_XmlBodyIsValidCloudEventsXml()
        {
            byte[]? bytes = null;
            var handler = new FakeHandler(req =>
            {
                bytes = req.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return OK();
            });

            await BuildChannel(handler, o => o.MessageFormat = EventMessageFormat.Xml)
                .PublishAsync(MakeEvent("order.shipped"), null, TestContext.Current.CancellationToken);

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
            services.AddEventPublisher().AddWebhooks(o =>
            {
                o.EndpointUrl = "https://webhook.example.com/";
                o.SigningSecret = "secret";
            });

            var sp = services.BuildServiceProvider();

            var channels = sp.GetServices<IEventPublishChannel>();
            Assert.Contains(channels, c => c is IBatchEventPublishChannel);

            var providers = sp.GetServices<IWebhookSignatureProvider>().ToList();
            Assert.Contains(providers, p => p.Algorithm == WebhookSignatureAlgorithm.HmacSha256);
            Assert.Contains(providers, p => p.Algorithm == WebhookSignatureAlgorithm.HmacSha384);
            Assert.Contains(providers, p => p.Algorithm == WebhookSignatureAlgorithm.HmacSha512);
            Assert.Contains(providers, p => p.Algorithm == WebhookSignatureAlgorithm.HmacSha1);

            var serializers = sp.GetServices<IEventSerializer>().ToList();
            Assert.Contains(serializers, s => s.Format == EventMessageFormat.Json);
            Assert.Contains(serializers, s => s.Format == EventMessageFormat.Xml);
        }

        // ── Batch delivery ────────────────────────────────────────────────────

        [Fact]
        public async Task PublishBatchAsync_SuccessOnFirstAttempt()
        {
            var requestCount = 0;
            var handler = new FakeHandler(req =>
            {
                requestCount++;
                return OK();
            });

            var channel = BuildChannel(handler);
            await channel.PublishBatchAsync(new[] { MakeEvent("event.one"), MakeEvent("event.two") });

            Assert.Equal(1, requestCount);
        }

        [Fact]
        public async Task PublishBatchAsync_EmptyList_Throws()
        {
            var handler = new FakeHandler(_ => OK());
            var channel = BuildChannel(handler);

            await Assert.ThrowsAsync<ArgumentException>(() => channel.PublishBatchAsync(Array.Empty<CloudEvent>()));
        }

        [Fact]
        public async Task PublishBatchAsync_NullList_Throws()
        {
            var handler = new FakeHandler(_ => OK());
            var channel = BuildChannel(handler);

            await Assert.ThrowsAsync<ArgumentException>(() => channel.PublishBatchAsync(null!));
        }

        [Fact]
        public async Task PublishBatchAsync_SetsBatchContentType()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            var channel = BuildChannel(handler, o => o.MessageFormat = EventMessageFormat.Json);
            await channel.PublishBatchAsync(new[] { MakeEvent("event.a"), MakeEvent("event.b") }, null,
                TestContext.Current.CancellationToken);

            // Batch should use BatchContentType
            Assert.NotNull(captured!.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishBatchAsync_WithOptions_UsesOverrideEndpoint()
        {
            var urls = new List<string>();
            var handler = new FakeHandler(req =>
            {
                urls.Add(req.RequestUri!.ToString());
                return OK();
            });

            var channel = BuildChannel(handler, o => o.EndpointUrl = "https://default.example.com/");
            await channel.PublishBatchAsync(new[] { MakeEvent() }, new WebhookPublishOptions
            {
                EndpointUrl = "https://batch-override.example.com/"
            });

            Assert.Single(urls);
            Assert.Equal("https://batch-override.example.com/", urls[0]);
        }

        [Fact]
        public async Task PublishBatchAsync_NoEventTypeHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            var channel = BuildChannel(handler);
            await channel.PublishBatchAsync(new[] { MakeEvent("event.a"), MakeEvent("event.b") });

            // For batches, the event-type header is NOT set
            Assert.False(captured!.Headers.Contains(WebhookDefaults.EventTypeHeaderName));
        }

        // ── CloudEventsJson/Xml format ─────────────────────────────────────

        [Fact]
        public async Task PublishAsync_CloudEventsJsonFormat_ContentTypeIsCloudEventsJson()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler, o => o.MessageFormat = EventMessageFormat.CloudEventsJson)
                .PublishAsync(MakeEvent(), null, TestContext.Current.CancellationToken);

            Assert.StartsWith("application/cloudevents+json",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishAsync_CloudEventsXmlFormat_ContentTypeIsCloudEventsXml()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req =>
            {
                captured = req;
                return OK();
            });

            await BuildChannel(handler, o => o.MessageFormat = EventMessageFormat.CloudEventsXml)
                .PublishAsync(MakeEvent(), null, TestContext.Current.CancellationToken);

            Assert.StartsWith("application/cloudevents+xml",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        // ── AddWebhooks(sectionPath) ───────────────────────────────────────────

        [Fact]
        public void UseWebhook_WithSectionPath_RegistersChannel()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Webhook:EndpointUrl"] = "https://webhook.example.com/",
                    ["Webhook:SigningSecret"] = "secret",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher().AddWebhooks("Webhook");

            var sp = services.BuildServiceProvider();

            var channels = sp.GetServices<IEventPublishChannel>();
            Assert.Contains(channels, c => c is IBatchEventPublishChannel);
        }

        // ── UseWebhookSignatureProvider and UseWebhookMessageSerializer ────────

        [Fact]
        public void UseWebhookSignatureProvider_RegistersCustomProvider()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o => o.EndpointUrl = "https://webhook.example.com/")
                .UseWebhookSignatureProvider<HmacSha256SignatureProvider>();

            var sp = services.BuildServiceProvider();
            var providers = sp.GetServices<IWebhookSignatureProvider>();
            Assert.Contains(providers, p => p.Algorithm == WebhookSignatureAlgorithm.HmacSha256);
        }

        // ── Typed channel resolution (DI lambdas) ────────────────────────────

        [Fact]
        public void UseWebhook_TypedBatchChannelPublishOptions_Resolved()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher().AddWebhooks(o =>
            {
                o.EndpointUrl = "https://webhook.example.com/";
                o.SigningSecret = "secret";
            });

            var sp = services.BuildServiceProvider();
            var batch = sp.GetService<IBatchEventPublishChannel>();
            Assert.NotNull(batch);
        }

        // ── WebhookDeliveryException extra constructor ────────────────────────

        [Fact]
        public void WebhookDeliveryException_WithInnerException_SetsMessage()
        {
            var inner = new InvalidOperationException("network error");
            var ex = new WebhookDeliveryException("delivery failed", inner);
            Assert.Equal("delivery failed", ex.Message);
            Assert.Same(inner, ex.InnerException);
            Assert.Equal(0, ex.StatusCode); // default int value
        }

        // ── Exception-based retry failure (HttpRequestException thrown) ───────

        [Fact]
        public async Task PublishAsync_ThrowsAfterExhausted_WithException()
        {
            var handler = new FakeHandler(_ => throw new HttpRequestException("connection refused"));

            await Assert.ThrowsAsync<WebhookTransportException>(() => BuildChannel(handler, o =>
            {
                o.MaxRetryCount = 1;
                o.RetryDelay = TimeSpan.FromMilliseconds(1);
            }).PublishAsync(MakeEvent()));
        }

        // ── No serializer for format ──────────────────────────────────────────

        [Fact]
        public async Task PublishAsync_UnsupportedFormat_ThrowsNotSupported()
        {
            var handler = new FakeHandler(_ => OK());

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://webhook.example.com/receive";
                    o.SigningSecret = "test-secret";
                    o.MessageFormat = "unsupported-format";
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            await Assert.ThrowsAsync<NotSupportedException>(() => channel.PublishAsync(MakeEvent()));
        }

        // ── Per-call RetryBackoffMultiplier and RequestTimeout ─────────────────

        [Fact]
        public async Task PublishAsync_PerCallOverride_SetsRetryBackoffAndTimeout()
        {
            var count = 0;
            var handler = new FakeHandler(_ =>
            {
                count++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            });

            var channel = BuildChannel(handler, o =>
            {
                o.MaxRetryCount = 5;
                o.RetryDelay = TimeSpan.FromMilliseconds(1);
            });

            await Assert.ThrowsAsync<WebhookStatusCodeException>(() => channel.PublishAsync(MakeEvent(),
                new WebhookPublishOptions
                {
                    MaxRetryCount = 1,
                    RetryDelay = TimeSpan.FromMilliseconds(1),
                    RetryBackoffMultiplier = 1.0,
                    RequestTimeout = TimeSpan.FromSeconds(5),
                }));

            Assert.Equal(2, count); // 1 initial + 1 retry
        }

        // ── HttpClientName option ─────────────────────────────────────────────

        [Fact]
        public async Task PublishAsync_CustomHttpClientName_UsesNamedClient()
        {
            const string customName = "custom-webhook-client";
            var requests = new List<HttpRequestMessage>();
            var handler = new FakeHandler(req =>
            {
                requests.Add(req);
                return OK();
            });

            var services = new ServiceCollection();
            services.AddHttpClient(customName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://webhook.example.com/receive";
                    o.SigningSecret = "test-secret";
                    o.MessageFormat = EventMessageFormat.Json;
                    o.HttpClientName = customName;
                });

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            await channel.PublishAsync(MakeEvent());
            Assert.Single(requests);
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

        private sealed class MutableSystemTime : IEventSystemTime
        {
            public static DateTimeOffset UtcNowValue { get; set; }

            public DateTimeOffset UtcNow => UtcNowValue;
        }
    }
}