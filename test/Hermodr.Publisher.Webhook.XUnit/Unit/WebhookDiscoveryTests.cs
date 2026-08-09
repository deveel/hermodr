//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Net;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    public class WebhookDiscoveryTests
    {
        private static readonly DateTimeOffset FixedNow =
            new(2026, 01, 15, 12, 00, 00, TimeSpan.Zero);

        private const string SigningSecret = "test-secret";
        private const string EndpointUrl = "https://webhook.example.com/receive";
        private const string RequestOrigin = "eventemitter.example.com";

        private static CloudEvent MakeEvent(string type = "person.created") => new(CloudEventsSpecVersion.V1_0)
        {
            Type = type,
            Source = new Uri("https://api.example.com/svc"),
            Id = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data = """{"name":"John Doe"}""",
            Time = FixedNow
        };

        private static (IBatchEventPublishChannel channel, List<HttpRequestMessage> requests) BuildChannel(
            Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send,
            Action<WebhookPublishOptions>? configure = null)
        {
            var requests = new List<HttpRequestMessage>();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .UseSystemTime<MutableSystemTime>()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = EndpointUrl;
                    o.SigningSecret = SigningSecret;
                    o.MaxRetryCount = 1;
                    o.RetryDelay = TimeSpan.FromMilliseconds(10);
                    o.MessageFormat = EventMessageFormat.Json;
                    o.SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha256;
                    configure?.Invoke(o);
                });

            MutableSystemTime.UtcNowValue = FixedNow;

            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler(send, requests));

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IBatchEventPublishChannel>();
            return (channel, requests);
        }

        // --- OPTIONS handshake --------------------------------------------------

        [Fact]
        public async Task PublishAsync_WithDiscovery_IssuesOptionsHandshakeBeforePost()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var (channel, requests) = BuildChannel((req, _) =>
            {
                if (req.Method == HttpMethod.Options)
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers =
                        {
                            { "WebHook-Allowed-Origin", RequestOrigin },
                            { "WebHook-Allowed-Rate", "100" }
                        }
                    };
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, o => o.Discovery = new WebhookDiscoveryOptions { RequestOrigin = RequestOrigin });

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            // The OPTIONS pre-flight must precede the POST.
            Assert.True(requests.Count >= 2);
            Assert.Equal(HttpMethod.Options, requests[0].Method);
            Assert.Equal(HttpMethod.Post, requests[1].Method);
        }

        [Fact]
        public async Task PublishAsync_WithDiscovery_SendsRequestOriginAndRateInHandshake()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? optionsRequest = null;

            var (channel, _) = BuildChannel((req, _) =>
            {
                if (req.Method == HttpMethod.Options)
                {
                    optionsRequest = req;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { { "WebHook-Allowed-Origin", "*" } }
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, o => o.Discovery = new WebhookDiscoveryOptions
            {
                RequestOrigin = RequestOrigin,
                RequestRate = 120,
                RequestCallback = "https://callback.example.com/grant?id=1"
            });

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.NotNull(optionsRequest);
            Assert.Equal(RequestOrigin,
                optionsRequest!.Headers.GetValues("WebHook-Request-Origin").First());
            Assert.Equal("120",
                optionsRequest.Headers.GetValues("WebHook-Request-Rate").First());
            Assert.Equal("https://callback.example.com/grant?id=1",
                optionsRequest.Headers.GetValues("WebHook-Request-Callback").First());
        }

        [Fact]
        public async Task PublishAsync_WithDiscovery_AttachesRequestOriginToDeliveryPost()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? postRequest = null;

            var (channel, _) = BuildChannel((req, _) =>
            {
                if (req.Method == HttpMethod.Options)
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { { "WebHook-Allowed-Origin", RequestOrigin } }
                    };
                postRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, o => o.Discovery = new WebhookDiscoveryOptions { RequestOrigin = RequestOrigin });

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.NotNull(postRequest);
            Assert.Equal(RequestOrigin,
                postRequest!.Headers.GetValues("WebHook-Request-Origin").First());
        }

        // --- Cache: handshake issued once per endpoint --------------------------

        [Fact]
        public async Task PublishAsync_TwiceToSameEndpoint_HandshakeIssuedOnce()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var handshakeCount = 0;

            var (channel, _) = BuildChannel((req, _) =>
            {
                if (req.Method == HttpMethod.Options)
                {
                    Interlocked.Increment(ref handshakeCount);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { { "WebHook-Allowed-Origin", "*" } }
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, o => o.Discovery = new WebhookDiscoveryOptions { RequestOrigin = RequestOrigin });

            await channel.PublishAsync(MakeEvent("a"), cancellationToken: cancellationToken);
            await channel.PublishAsync(MakeEvent("b"), cancellationToken: cancellationToken);

            Assert.Equal(1, handshakeCount);
        }

        // --- Refused handshake --------------------------------------------------

        [Fact]
        public async Task PublishAsync_HandshakeRefused_BestEffort_StillDelivers()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var postSeen = false;

            var (channel, _) = BuildChannel((req, _) =>
            {
                if (req.Method == HttpMethod.Options)
                    return new HttpResponseMessage(HttpStatusCode.OK); // no Allowed-Origin
                postSeen = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, o => o.Discovery = new WebhookDiscoveryOptions
            {
                RequestOrigin = RequestOrigin,
                RequireSuccessfulHandshake = false
            });

            // Best-effort: delivery proceeds despite refusal.
            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);
            Assert.True(postSeen);
        }

        [Fact]
        public async Task PublishAsync_HandshakeRefused_Strict_ThrowsAndDoesNotDeliver()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var postSeen = false;

            var (channel, _) = BuildChannel((req, _) =>
            {
                if (req.Method == HttpMethod.Options)
                    return new HttpResponseMessage(HttpStatusCode.OK); // no Allowed-Origin
                postSeen = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, o => o.Discovery = new WebhookDiscoveryOptions
            {
                RequestOrigin = RequestOrigin,
                RequireSuccessfulHandshake = true
            });

            await Assert.ThrowsAsync<WebhookHandshakeException>(() =>
                channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken));

            Assert.False(postSeen, "Delivery POST must not be sent when strict handshake fails.");
        }

        [Fact]
        public async Task PublishAsync_HandshakeRequestFaults_Strict_Throws()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var (channel, _) = BuildChannel((req, _) =>
            {
                if (req.Method == HttpMethod.Options)
                    throw new HttpRequestException("connection reset");
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, o => o.Discovery = new WebhookDiscoveryOptions
            {
                RequestOrigin = RequestOrigin,
                RequireSuccessfulHandshake = true
            });

            await Assert.ThrowsAsync<WebhookHandshakeException>(() =>
                channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken));
        }

        [Fact]
        public async Task PublishAsync_HandshakeRequestFaults_BestEffort_Proceeds()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var postSeen = false;

            var (channel, _) = BuildChannel((req, _) =>
            {
                if (req.Method == HttpMethod.Options)
                    throw new HttpRequestException("dns failure");
                postSeen = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, o => o.Discovery = new WebhookDiscoveryOptions { RequestOrigin = RequestOrigin });

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);
            Assert.True(postSeen);
        }

        // --- WebHook-Allowed-Rate parsing ---------------------------------------

        [Fact]
        public async Task PublishAsync_AllowedRateAsterisk_ParsedAsNull()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var (channel, _) = BuildChannel((req, _) =>
            {
                if (req.Method == HttpMethod.Options)
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers =
                        {
                            { "WebHook-Allowed-Origin", "*" },
                            { "WebHook-Allowed-Rate", "*" }
                        }
                    };
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, o => o.Discovery = new WebhookDiscoveryOptions { RequestOrigin = RequestOrigin });

            // Guards that an asterisk allowed-rate doesn't throw and delivery
            // completes normally.
            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);
        }

        // --- No Discovery → no handshake, no origin header ----------------------

        [Fact]
        public async Task PublishAsync_NoDiscovery_NoOptionsRequest()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var (channel, requests) = BuildChannel((_, _) => new HttpResponseMessage(HttpStatusCode.OK));

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.All(requests, r => Assert.Equal(HttpMethod.Post, r.Method));
            Assert.DoesNotContain(requests,
                r => r.Headers.Contains("WebHook-Request-Origin"));
        }

        // --- Retry-After on 429 -------------------------------------------------

        [Fact]
        public async Task PublishAsync_429WithRetryAfter_RetriesAfterSpecifiedDelay()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var attempts = 0;
            var firstAttemptAt = DateTimeOffset.MinValue;
            var secondAttemptAt = DateTimeOffset.MinValue;

            var (channel, _) = BuildChannel((req, _) =>
            {
                var n = Interlocked.Increment(ref attempts);
                if (n == 1)
                {
                    firstAttemptAt = DateTimeOffset.UtcNow;
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    {
                        Headers = { { "Retry-After", "1" } }
                    };
                }

                secondAttemptAt = DateTimeOffset.UtcNow;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, o =>
            {
                o.MaxRetryCount = 1;
                o.RetryDelay = TimeSpan.FromMilliseconds(10);
                o.RetryBackoffMultiplier = 1.0;
            });

            await channel.PublishAsync(MakeEvent(), cancellationToken: cancellationToken);

            Assert.Equal(2, attempts);
            var observed = secondAttemptAt - firstAttemptAt;
            // Retry-After=1s must dominate the configured 10ms backoff.
            Assert.True(observed >= TimeSpan.FromMilliseconds(900),
                $"Expected ≥900ms between attempts due to Retry-After, observed {observed.TotalMilliseconds}ms.");
        }

        // --- Handlers -----------------------------------------------------------

        private sealed class LambdaHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _send;
            private readonly List<HttpRequestMessage> _requests;

            public LambdaHandler(
                Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send,
                List<HttpRequestMessage> requests)
            {
                _send = send;
                _requests = requests;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _requests.Add(request);
                return Task.FromResult(_send(request, cancellationToken));
            }
        }

        private sealed class MutableSystemTime : IEventSystemTime
        {
            public static DateTimeOffset UtcNowValue { get; set; }
            public DateTimeOffset UtcNow => UtcNowValue;
        }
    }
}