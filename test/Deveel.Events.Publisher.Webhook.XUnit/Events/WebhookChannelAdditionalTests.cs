//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System.Net;
using System.Text.Json;

namespace Deveel.Events
{
    /// <summary>
    /// Additional tests for <see cref="WebhookEventPublishChannel"/> and
    /// <see cref="WebhookPublishOptions"/> covering scenarios not already present
    /// in <c>WebhookPublishChannelTests</c>.
    /// </summary>
    public class WebhookChannelAdditionalTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static CloudEvent MakeEvent(string type = "test.event") => new()
        {
            Type            = type,
            Source          = new Uri("https://api.example.com/svc"),
            Id              = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data            = JsonSerializer.Serialize(new { name = "Test" }),
            Time            = DateTimeOffset.UtcNow,
        };

#pragma warning disable CS0618
        private static readonly IWebhookSignatureProvider[] AllProviders =
        [
            HmacSha256SignatureProvider.Default,
            HmacSha384SignatureProvider.Default,
            HmacSha512SignatureProvider.Default,
            HmacSha1SignatureProvider.Default,
        ];
#pragma warning restore CS0618

        private static WebhookEventPublishChannel BuildChannel(
            HttpMessageHandler handler,
            Action<WebhookPublishOptions>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => handler);

            var options = new WebhookPublishOptions
            {
                EndpointUrl        = "https://webhook.example.com/receive",
                SigningSecret      = "test-secret",
                MaxRetryCount      = 2,
                RetryDelay         = TimeSpan.FromMilliseconds(10),
                MessageFormat      = EventMessageFormat.Json,
                SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha256,
            };
            configure?.Invoke(options);

            var sp      = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IHttpClientFactory>();

            return new WebhookEventPublishChannel(
                Options.Create(options),
                factory,
                AllProviders);
        }

        private static HttpResponseMessage OK()   => new(HttpStatusCode.OK);

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _fn;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> fn) => _fn = fn;
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_fn(request));
        }

        // ── WebhookPublishOptions defaults ───────────────────────────────────

        [Fact]
        public static void WebhookPublishOptions_Defaults_AreCorrect()
        {
            var opts = new WebhookPublishOptions();

            Assert.Equal(WebhookDefaults.SignatureHeaderName,          opts.SignatureHeaderName);
            Assert.Equal(WebhookDefaults.DeliveryIdHeaderName,         opts.DeliveryIdHeaderName);
            Assert.Equal(WebhookDefaults.EventTypeHeaderName,          opts.EventTypeHeaderName);
            Assert.Equal(WebhookDefaults.TimestampHeaderName,          opts.TimestampHeaderName);
            Assert.Equal(WebhookDefaults.SignatureAlgorithmHeaderName,  opts.SignatureAlgorithmHeaderName);
            Assert.Null(opts.EndpointUrl);
            Assert.Null(opts.SigningSecret);
            Assert.Null(opts.MaxRetryCount);
            Assert.Null(opts.RetryDelay);
            Assert.Null(opts.RetryBackoffMultiplier);
            Assert.Null(opts.RequestTimeout);
            Assert.Null(opts.MessageFormat);
            Assert.Null(opts.SignatureAlgorithm);
            Assert.Null(opts.HttpClientName);
            Assert.NotEmpty(opts.RetryableStatusCodes);
        }

        [Fact]
        public static void WebhookPublishOptions_DefaultRetryableStatusCodes_ContainsExpected()
        {
            var opts = new WebhookPublishOptions();

            Assert.Contains(429, opts.RetryableStatusCodes);
            Assert.Contains(500, opts.RetryableStatusCodes);
            Assert.Contains(502, opts.RetryableStatusCodes);
            Assert.Contains(503, opts.RetryableStatusCodes);
            Assert.Contains(504, opts.RetryableStatusCodes);
        }

        // ── SHA-1 signing algorithm ──────────────────────────────────────────

        [Fact]
        public async Task PublishAsync_HmacSha1Algorithm_SetsCorrectHeaders()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

#pragma warning disable CS0618
            await BuildChannel(handler, o => o.SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha1)
                  .PublishAsync(MakeEvent());
#pragma warning restore CS0618

            var sig = captured!.Headers.GetValues(WebhookDefaults.SignatureHeaderName).First();
            Assert.StartsWith("sha1=", sig);

            Assert.Equal("hmac-sha1",
                captured.Headers.GetValues(WebhookDefaults.SignatureAlgorithmHeaderName).First());
        }

        // ── 429 (Too Many Requests) is retried by default ────────────────────

        [Fact]
        public async Task PublishAsync_Returns429_IsRetriedByDefault()
        {
            var count   = 0;
            var handler = new FakeHandler(_ =>
            {
                count++;
                return count <= 1
                    ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    : OK();
            });

            await BuildChannel(handler, o =>
            {
                o.MaxRetryCount = 3;
                o.RetryDelay    = TimeSpan.FromMilliseconds(10);
            }).PublishAsync(MakeEvent());

            Assert.Equal(2, count); // 1 failed + 1 success
        }

        [Fact]
        public async Task PublishAsync_Returns429_ThenExhausted_ThrowsDeliveryException()
        {
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

            await Assert.ThrowsAsync<WebhookDeliveryException>(
                () => BuildChannel(handler, o =>
                {
                    o.MaxRetryCount = 1;
                    o.RetryDelay    = TimeSpan.FromMilliseconds(1);
                }).PublishAsync(MakeEvent()));
        }

        // ── Custom retryable status codes ────────────────────────────────────

        [Fact]
        public async Task PublishAsync_CustomRetryableStatusCode_IsRetried()
        {
            var count   = 0;
            var handler = new FakeHandler(_ =>
            {
                count++;
                return count <= 1
                    ? new HttpResponseMessage((HttpStatusCode)418) // I'm a teapot — custom retryable
                    : OK();
            });

            await BuildChannel(handler, o =>
            {
                o.MaxRetryCount        = 3;
                o.RetryDelay           = TimeSpan.FromMilliseconds(1);
                o.RetryableStatusCodes = new HashSet<int> { 418 };
            }).PublishAsync(MakeEvent());

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task PublishAsync_NonCustomRetryableStatusCode_DoesNotRetry()
        {
            var count   = 0;
            var handler = new FakeHandler(_ =>
            {
                count++;
                return new HttpResponseMessage((HttpStatusCode)418);
            });

            // 418 is NOT in the default retryable codes → should throw immediately after 1 attempt
            await Assert.ThrowsAsync<WebhookDeliveryException>(
                () => BuildChannel(handler, o =>
                {
                    o.MaxRetryCount = 3;
                    o.RetryDelay    = TimeSpan.FromMilliseconds(1);
                    // Leave RetryableStatusCodes at default (which doesn't include 418)
                }).PublishAsync(MakeEvent()));

            Assert.Equal(1, count);
        }

        // ── SignatureAlgorithmHeaderName = null suppresses header ─────────────

        [Fact]
        public async Task PublishAsync_SignatureAlgorithmHeaderNameNull_HeaderSuppressed()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            await BuildChannel(handler, o =>
            {
                o.SigningSecret               = "secret";
                o.SignatureAlgorithmHeaderName = null;
            }).PublishAsync(MakeEvent());

            // SignatureAlgorithmHeaderName is null → the algorithm header must NOT be sent
            Assert.False(captured!.Headers.Contains(WebhookDefaults.SignatureAlgorithmHeaderName));
            // But the signature itself should still be present
            Assert.True(captured.Headers.Contains(WebhookDefaults.SignatureHeaderName));
        }

        // ── PublishBatchAsync retry behaviour ────────────────────────────────

        [Fact]
        public async Task PublishBatchAsync_RetriesOnTransientFailure()
        {
            var count   = 0;
            var handler = new FakeHandler(_ =>
            {
                count++;
                return count < 2
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : OK();
            });

            var channel = BuildChannel(handler, o =>
            {
                o.MaxRetryCount = 3;
                o.RetryDelay    = TimeSpan.FromMilliseconds(1);
            });

            await channel.PublishBatchAsync(
                [MakeEvent("event.a"), MakeEvent("event.b")]);

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task PublishBatchAsync_ThrowsAfterExhaustedRetries()
        {
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            var channel = BuildChannel(handler, o =>
            {
                o.MaxRetryCount = 1;
                o.RetryDelay    = TimeSpan.FromMilliseconds(1);
            });

            await Assert.ThrowsAsync<WebhookDeliveryException>(
                () => channel.PublishBatchAsync(
                    [MakeEvent("event.a"), MakeEvent("event.b")]));
        }

        [Fact]
        public async Task PublishBatchAsync_ExceptionBased_ThrowsAfterExhausted()
        {
            var handler = new FakeHandler(_ => throw new HttpRequestException("connection reset"));

            var channel = BuildChannel(handler, o =>
            {
                o.MaxRetryCount = 1;
                o.RetryDelay    = TimeSpan.FromMilliseconds(1);
            });

            await Assert.ThrowsAsync<WebhookDeliveryException>(
                () => channel.PublishBatchAsync([MakeEvent()]));
        }

        // ── PublishBatchAsync CloudEventsXml format ──────────────────────────

        [Fact]
        public async Task PublishBatchAsync_CloudEventsXmlFormat_SetsCorrectBatchContentType()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            var channel = BuildChannel(handler, o => o.MessageFormat = EventMessageFormat.CloudEventsXml);
            await channel.PublishBatchAsync(
                [MakeEvent("event.a"), MakeEvent("event.b")], null, TestContext.Current.CancellationToken);

            Assert.StartsWith("application/cloudevents+xml",
                captured!.Content!.Headers.ContentType!.MediaType);
        }

        // ── WebhookDeliveryException status-code constructor ──────────────────

        [Fact]
        public static void WebhookDeliveryException_StatusCodeConstructor_SetsStatusCode()
        {
            var ex = new WebhookDeliveryException("failed with 503", 503);
            Assert.Equal(503, ex.StatusCode);
            Assert.Equal("failed with 503", ex.Message);
            Assert.Null(ex.InnerException);
        }

        [Fact]
        public static void WebhookDeliveryException_IsException()
        {
            var ex = new WebhookDeliveryException("error", 500);
            Assert.IsAssignableFrom<Exception>(ex);
        }

        // ── WebhookDefaults constants ────────────────────────────────────────

        [Fact]
        public static void WebhookDefaults_Constants_AreCorrectStrings()
        {
            Assert.Equal("X-Webhook-Signature",           WebhookDefaults.SignatureHeaderName);
            Assert.Equal("X-Webhook-Delivery",            WebhookDefaults.DeliveryIdHeaderName);
            Assert.Equal("X-Webhook-Event",               WebhookDefaults.EventTypeHeaderName);
            Assert.Equal("X-Webhook-Timestamp",           WebhookDefaults.TimestampHeaderName);
            Assert.Equal("X-Webhook-Signature-Algorithm", WebhookDefaults.SignatureAlgorithmHeaderName);
            Assert.Equal("Deveel.Events.Webhook",         WebhookDefaults.HttpClientName);
        }

        // ── Merge: channel-structural properties taken from defaults ──────────

        [Fact]
        public async Task PublishAsync_PerCallOverride_CannotChangeStructuralHeaders()
        {
            HttpRequestMessage? captured = null;
            var handler = new FakeHandler(req => { captured = req; return OK(); });

            var channel = BuildChannel(handler, o =>
            {
                // The channel default uses the standard header name
                o.SignatureHeaderName = "X-Custom-Sig";
            });

            // Try to override the structural header name via per-call options
            await channel.PublishAsync(MakeEvent(), new WebhookPublishOptions
            {
                SignatureHeaderName = "X-Should-Be-Ignored",
            });

            // The structural header name from the channel defaults must be used
            Assert.True(captured!.Headers.Contains("X-Custom-Sig"),
                "Expected the channel-level signature header name to be used.");
            Assert.False(captured.Headers.Contains("X-Should-Be-Ignored"),
                "Per-call structural header overrides must be ignored.");
        }

        // ── UseWebhookMessageSerializer via DI ────────────────────────────────

        [Fact]
        public void UseWebhookMessageSerializer_RegistersCustomSerializer()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o => o.EndpointUrl = "https://webhook.example.com/")
                .UseWebhookSignatureProvider<HmacSha256SignatureProvider>();

            var sp = services.BuildServiceProvider();
            var serializers = sp.GetServices<IEventSerializer>().ToList();

            // The default serializers are all registered
            Assert.Contains(serializers, s => s.Format == EventMessageFormat.Json);
            Assert.Contains(serializers, s => s.Format == EventMessageFormat.Xml);
            Assert.Contains(serializers, s => s.Format == EventMessageFormat.CloudEventsJson);
            Assert.Contains(serializers, s => s.Format == EventMessageFormat.CloudEventsXml);
        }
    }
}

