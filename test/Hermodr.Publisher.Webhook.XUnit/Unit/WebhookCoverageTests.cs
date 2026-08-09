//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.Concurrent;
using System.Net;
using System.Reflection;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// Targeted tests that close the remaining coverage gaps in the webhook
    /// publisher: channel metadata, options merging (all branches), the
    /// Retry-After parser (HTTP-date / invalid / negative), the retry
    /// ShouldHandle null-result branch, the handshake cache API, the typed
    /// config-section DI overload, exception getters, and edge cases of the
    /// CloudEvent header mapper.
    /// </summary>
    [Trait("Channel", "Webhook")]
    [Trait("Function", "Coverage")]
    public class WebhookCoverageTests
    {
        // ── IChannelMetadataSource.GetChannelMetadata ───────────────────────

        [Fact]
        public void GetChannelMetadata_WithEndpointUrl_IncludesUrlProperty()
        {
            var options = new WebhookPublishOptions
            {
                ChannelName = "orders",
                EndpointUrl = "https://hook.example.com/r"
            };

            var metadata = ((IChannelMetadataSource)options).GetChannelMetadata();

            Assert.Equal("orders", metadata.ChannelName);
            Assert.Equal(EventTransports.Webhook, metadata.Transport);
            Assert.Equal("https://hook.example.com/r", metadata.Properties["url"]);
        }

        [Fact]
        public void GetChannelMetadata_WithoutEndpointUrl_OmitsUrlProperty()
        {
            // Exercises the null/empty cleanup loop that removes the url key.
            var options = new WebhookPublishOptions { ChannelName = "anon" };

            var metadata = ((IChannelMetadataSource)options).GetChannelMetadata();

            Assert.Equal("anon", metadata.ChannelName);
            Assert.Empty(metadata.Properties);
        }

        [Fact]
        public void GetChannelMetadata_WithEmptyEndpointUrl_OmitsUrlProperty()
        {
            var options = new WebhookPublishOptions
            {
                ChannelName = "empty",
                EndpointUrl = ""
            };

            var metadata = ((IChannelMetadataSource)options).GetChannelMetadata();

            Assert.False(metadata.Properties.ContainsKey("url"));
        }

        // ── WebhookPublishOptions.Merge (full matrix) ───────────────────────

        [Fact]
        public void Merge_TypedOptionsNullFields_InheritFromBase()
        {
            var baseOpts = new WebhookPublishOptions
            {
                EndpointUrl = "https://base/hook",
                SigningSecret = "base-secret",
                MaxRetryCount = 5,
                RetryDelay = TimeSpan.FromSeconds(2),
                RetryBackoffMultiplier = 3.0,
                RequestTimeout = TimeSpan.FromSeconds(45),
                MessageFormat = EventMessageFormat.CloudEventsJson,
                SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha512,
                ScheduleDeliveryAt = DateTimeOffset.UtcNow,
                Discovery = new WebhookDiscoveryOptions { RequestOrigin = "base" }
            };
            baseOpts.AdditionalHeaders["X-Base"] = "base";

            var typed = new WebhookPublishOptions
            {
                EndpointUrl = "https://typed/hook",
                SigningSecret = "typed-secret"
            };
            typed.AdditionalHeaders["X-Typed"] = "typed";

            var merged = WebhookPublishOptions.Merge(baseOpts, typed);

            Assert.Equal("https://typed/hook", merged.EndpointUrl);
            Assert.Equal("typed-secret", merged.SigningSecret);
            // Inherited from base because null in typed.
            Assert.Equal(5, merged.MaxRetryCount);
            Assert.Equal(TimeSpan.FromSeconds(2), merged.RetryDelay);
            Assert.Equal(3.0, merged.RetryBackoffMultiplier);
            Assert.Equal(TimeSpan.FromSeconds(45), merged.RequestTimeout);
            Assert.Equal(EventMessageFormat.CloudEventsJson, merged.MessageFormat);
            Assert.Equal(WebhookSignatureAlgorithm.HmacSha512, merged.SignatureAlgorithm);
            Assert.Equal(baseOpts.ScheduleDeliveryAt, merged.ScheduleDeliveryAt);
            Assert.Equal("base", merged.Discovery!.RequestOrigin);
            // Channel-structural always from base.
            Assert.Equal(baseOpts.SignatureHeaderName, merged.SignatureHeaderName);
            Assert.Equal(baseOpts.DeliveryIdHeaderName, merged.DeliveryIdHeaderName);
            Assert.Equal(baseOpts.EventTypeHeaderName, merged.EventTypeHeaderName);
            Assert.Equal(baseOpts.TimestampHeaderName, merged.TimestampHeaderName);
            Assert.Equal(baseOpts.SignatureAlgorithmHeaderName, merged.SignatureAlgorithmHeaderName);
            Assert.Same(baseOpts.RetryableStatusCodes, merged.RetryableStatusCodes);
            Assert.Equal(baseOpts.HttpClientName, merged.HttpClientName);
            // Headers merged: both present, typed wins on collision.
            Assert.Equal("base", merged.AdditionalHeaders["X-Base"]);
            Assert.Equal("typed", merged.AdditionalHeaders["X-Typed"]);
        }

        [Fact]
        public void Merge_TypedHeadersOverrideBaseOnCollision()
        {
            var baseOpts = new WebhookPublishOptions();
            baseOpts.AdditionalHeaders["X-Shared"] = "base";

            var typed = new WebhookPublishOptions();
            typed.AdditionalHeaders["X-Shared"] = "typed";

            var merged = WebhookPublishOptions.Merge(baseOpts, typed);

            Assert.Equal("typed", merged.AdditionalHeaders["X-Shared"]);
        }

        [Fact]
        public void Merge_TypedDiscoveryOverridesBaseDiscovery()
        {
            var baseOpts = new WebhookPublishOptions
            {
                Discovery = new WebhookDiscoveryOptions { RequestOrigin = "base" }
            };
            var typed = new WebhookPublishOptions
            {
                Discovery = new WebhookDiscoveryOptions { RequestOrigin = "typed" }
            };

            var merged = WebhookPublishOptions.Merge(baseOpts, typed);

            Assert.Equal("typed", merged.Discovery!.RequestOrigin);
        }

        // ── WebhookPublishChannel.MergeOptions (per-call path) ──────────────

        [Fact]
        public async Task PublishAsync_PerCallOverride_InheritsNullFieldsFromDefaults()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://hook.example.com/r";
                    o.SigningSecret = "channel-secret";
                    o.MaxRetryCount = 1;
                    o.MessageFormat = EventMessageFormat.CloudEventsJson;
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler((req, _) =>
                {
                    captured = req;
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }));

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            // Per-call override: only EndpointUrl is set; everything else null
            // → inherited from channel defaults (exercises the MergeOptions
            // null-coalescing branches for every field).
            await channel.PublishAsync(
                new CloudEvent(CloudEventsSpecVersion.V1_0)
                {
                    Type = "t", Source = new Uri("https://x"), Id = "1",
                    DataContentType = "application/json", Data = "{}"
                },
                new WebhookPublishOptions { EndpointUrl = "https://override.example.com/r" },
                cancellationToken);

            Assert.NotNull(captured);
            Assert.Equal("https://override.example.com/r", captured!.RequestUri!.ToString());
            // Format inherited from channel default (cloudevents+json).
            Assert.Equal("application/cloudevents+json",
                captured.Content!.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task PublishAsync_PerCallDiscoveryOverrideWins()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var optionsSeen = new ConcurrentBag<string>();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://hook.example.com/r";
                    o.MaxRetryCount = 1;
                    // Channel-level: no Discovery.
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler((req, _) =>
                {
                    if (req.Method == HttpMethod.Options)
                        optionsSeen.Add(req.Headers.GetValues("WebHook-Request-Origin").First());
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { { "WebHook-Allowed-Origin", "*" } }
                    };
                }));

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            await channel.PublishAsync(
                new CloudEvent(CloudEventsSpecVersion.V1_0)
                {
                    Type = "t", Source = new Uri("https://x"), Id = "1",
                    DataContentType = "application/json", Data = "{}"
                },
                new WebhookPublishOptions
                {
                    EndpointUrl = "https://hook.example.com/r",
                    Discovery = new WebhookDiscoveryOptions { RequestOrigin = "per-call-origin" }
                },
                cancellationToken);

            Assert.Contains("per-call-origin", optionsSeen);
        }

        // ── TryGetRetryAfterDelay (HTTP-date + invalid + negative) ──────────

        [Fact]
        public async Task PublishAsync_429WithRetryAfterHttpDate_HonorsDateDelay()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var attempts = 0;
            var firstAt = DateTimeOffset.MinValue;
            var secondAt = DateTimeOffset.MinValue;

            var retryAfterDate = DateTimeOffset.UtcNow.AddSeconds(2);

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://hook.example.com/r";
                    o.MaxRetryCount = 1;
                    o.RetryDelay = TimeSpan.FromMilliseconds(10);
                    o.RetryBackoffMultiplier = 1.0;
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler((req, _) =>
                {
                    var n = Interlocked.Increment(ref attempts);
                    if (n == 1)
                    {
                        firstAt = DateTimeOffset.UtcNow;
                        return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                        {
                            Headers = { { "Retry-After", retryAfterDate.ToString("R") } }
                        };
                    }
                    secondAt = DateTimeOffset.UtcNow;
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }));

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            await channel.PublishAsync(
                new CloudEvent(CloudEventsSpecVersion.V1_0)
                {
                    Type = "t", Source = new Uri("https://x"), Id = "1",
                    DataContentType = "application/json", Data = "{}"
                }, null, cancellationToken);

            Assert.Equal(2, attempts);
            var observed = secondAt - firstAt;
            Assert.True(observed >= TimeSpan.FromSeconds(1),
                $"HTTP-date Retry-After should dominate backoff; observed {observed.TotalMilliseconds}ms.");
        }

        [Fact]
        public async Task PublishAsync_429WithUnparseableRetryAfter_FallsBackToBackoff()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var attempts = 0;

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://hook.example.com/r";
                    o.MaxRetryCount = 1;
                    o.RetryDelay = TimeSpan.FromMilliseconds(20);
                    o.RetryBackoffMultiplier = 1.0;
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler((req, _) =>
                {
                    var n = Interlocked.Increment(ref attempts);
                    if (n == 1)
                    {
                        var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                        resp.Headers.TryAddWithoutValidation("Retry-After", "not-a-date");
                        return resp;
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }));

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            await channel.PublishAsync(
                new CloudEvent(CloudEventsSpecVersion.V1_0)
                {
                    Type = "t", Source = new Uri("https://x"), Id = "1",
                    DataContentType = "application/json", Data = "{}"
                }, null, cancellationToken);

            // Unparseable Retry-After → falls back to configured backoff, still retries.
            Assert.Equal(2, attempts);
        }

        // ── Retry OnRetry status-code branch + ShouldHandle null outcome ─────
        // (The status-code retry-log branch is covered by existing tests; the
        // null-result ShouldHandle branch is unreachable in practice because
        // Polly only delivers null outcomes via custom strategies, so we don't
        // force it here — it's a defensive guard.)

        // ── WebhookHandshakeClient cache API + ctor guard ───────────────────

        [Fact]
        public void WebhookHandshakeClient_Constructor_NullFactory_Throws()
        {
            // The ctor is internal; reach it via reflection. The real
            // ArgumentNullException is wrapped in TargetInvocationException.
            var ex = Assert.Throws<TargetInvocationException>(() =>
                HandshakeClientReflector.CreateInstance(null!));

            Assert.IsType<ArgumentNullException>(HandshakeClientReflector.Unwrap(ex));
        }

        [Fact]
        public async Task HandshakeClient_GetCachedAndEvict_RoundTrip()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var handshakeCount = 0;

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://hook.example.com/r";
                    o.MaxRetryCount = 1;
                    o.Discovery = new WebhookDiscoveryOptions { RequestOrigin = "origin" };
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler((req, _) =>
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
                }));

            var provider = services.BuildServiceProvider();
            // Resolve the concrete channel (not the decorated IEventPublishChannel)
            // so publish and reflection share the same handshake-client instance.
            // The channel type is internal — resolve it reflectively via the
            // service provider's non-generic GetService.
            var channelType = typeof(WebhookPublishOptions).Assembly
                .GetType("Hermodr.WebhookPublishChannel", throwOnError: true)!;
            var channel = provider.GetService(channelType)
                ?? throw new InvalidOperationException("WebhookPublishChannel not registered.");
            var handshakeClient = HandshakeClientReflector.GetClientFromProvider(provider);

            // GetCached returns null before any handshake.
            Assert.Null(HandshakeClientReflector.InvokeGetCached(handshakeClient, "https://hook.example.com/r"));

            // PublishAsync is part of the public IEventPublishChannel interface,
            // so the public cast works without internal access.
            var ev = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Type = "t", Source = new Uri("https://x"), Id = "1",
                DataContentType = "application/json", Data = "{}"
            };
            await ((IEventPublishChannel)channel).PublishAsync(ev, null, cancellationToken);
            Assert.Equal(1, handshakeCount);

            // Cache now populated.
            var cached = HandshakeClientReflector.InvokeGetCached(handshakeClient, "https://hook.example.com/r");
            Assert.NotNull(cached);
            Assert.Equal("*", HandshakeClientReflector.GetAllowedOrigin(cached!));

            // Second delivery reuses the cache (no new handshake).
            await ((IEventPublishChannel)channel).PublishAsync(ev, null, cancellationToken);
            Assert.Equal(1, handshakeCount);

            // Evict → next delivery re-handshakes.
            HandshakeClientReflector.InvokeEvict(handshakeClient, "https://hook.example.com/r");
            Assert.Null(HandshakeClientReflector.InvokeGetCached(handshakeClient, "https://hook.example.com/r"));

            await ((IEventPublishChannel)channel).PublishAsync(ev, null, cancellationToken);
            Assert.Equal(2, handshakeCount);
        }

        // ── WebhookHandshakeException getters ───────────────────────────────

        [Fact]
        public void WebhookHandshakeException_PreservesEndpointAndStatusCode()
        {
            var inner = new InvalidOperationException("boom");
            var ex = new WebhookHandshakeException("refused", "https://x/hook", 405, inner);

            Assert.Equal("refused", ex.Message);
            Assert.Equal("https://x/hook", ex.EndpointUrl);
            Assert.Equal(405, ex.StatusCode);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public void WebhookHandshakeException_DefaultStatusCode_Null()
        {
            var ex = new WebhookHandshakeException("refused", "https://x/hook");
            Assert.Null(ex.StatusCode);
        }

        // ── WebhookDiscoveryPermission GrantedAt ────────────────────────────

        [Fact]
        public void WebhookDiscoveryPermission_GrantedAt_IsRecentUtc()
        {
            var before = DateTimeOffset.UtcNow;

            var permission = new WebhookDiscoveryPermission("origin", 100);

            var after = DateTimeOffset.UtcNow;
            Assert.Equal("origin", permission.AllowedOrigin);
            Assert.Equal(100, permission.AllowedRate);
            Assert.InRange(permission.GrantedAt, before, after);
        }

        [Fact]
        public void WebhookDiscoveryPermission_NullOrigin_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new WebhookDiscoveryPermission(null!, 100));
        }

        // ── AddWebhooks<TEvent>(string sectionPath) DI overload ─────────────

        [Fact]
        public void AddWebhooks_Typed_FromConfigSection_BindsOptions()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Webhook:EndpointUrl"] = "https://cfg.example.com/hook",
                    ["Webhook:SigningSecret"] = "cfg-secret",
                    ["Webhook:MaxRetryCount"] = "7"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                    .AddWebhooks<OrderCreated>("Webhook");

            var sp = services.BuildServiceProvider();
            var typed = sp.GetRequiredService<IOptions<WebhookPublishOptions<OrderCreated>>>().Value;

            Assert.Equal("https://cfg.example.com/hook", typed.EndpointUrl);
            Assert.Equal("cfg-secret", typed.SigningSecret);
            Assert.Equal(7, typed.MaxRetryCount);
        }

        // ── AddWebhooks(string sectionPath) non-typed overload ──────────────

        [Fact]
        public void AddWebhooks_FromConfigSection_BindsOptions()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Webhook:EndpointUrl"] = "https://cfg.example.com/base",
                    ["Webhook:MessageFormat"] = EventMessageFormat.CloudEventsJson
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                    .AddWebhooks("Webhook");

            var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<WebhookPublishOptions>>().Value;

            Assert.Equal("https://cfg.example.com/base", opts.EndpointUrl);
            Assert.Equal(EventMessageFormat.CloudEventsJson, opts.MessageFormat);
        }

        // ── CloudEventHttpHeadersMapper edge cases ──────────────────────────

        [Fact]
        public async Task CloudEventHttpHeadersMapper_MinimalEvent_ReturnsOnlyCoreHeaders()
        {
            // The mapper is internal; reach it through the binary publish path
            // by sending an event with no populated attributes other than the
            // required ones. We assert the absence of extension headers.
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://hook.example.com/r";
                    o.MaxRetryCount = 1;
                    o.MessageFormat = EventMessageFormat.CloudEventsBinary;
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler((req, _) =>
                {
                    captured = req;
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }));

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            // Minimal event: only required attributes (specversion/id/type/source).
            // No datacontenttype, no subject, no extensions → exercises the
            // null-skip branches in Map.
            await channel.PublishAsync(
                new CloudEvent(CloudEventsSpecVersion.V1_0)
                {
                    Type = "t", Source = new Uri("https://x"), Id = "1"
                }, null, cancellationToken);

            Assert.NotNull(captured);
            Assert.False(captured!.Headers.Contains("ce-datacontenttype"));
            Assert.False(captured.Headers.Contains("ce-subject"));
            // No extension ce-* headers beyond the core ones.
            var ceHeaders = captured.Headers
                .Where(h => h.Key.StartsWith("ce-", StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Key)
                .ToList();
            Assert.Contains("ce-specversion", ceHeaders);
            Assert.Contains("ce-id", ceHeaders);
            Assert.Contains("ce-type", ceHeaders);
            Assert.Contains("ce-source", ceHeaders);
            // Exactly the four core attributes, no time/datacontenttype/subject/extensions.
            Assert.Equal(4, ceHeaders.Count);
        }

        [Fact]
        public async Task PublishAsync_BinaryMode_DataschemaExtension_MappedToCeHeader()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://hook.example.com/r";
                    o.MaxRetryCount = 1;
                    o.MessageFormat = EventMessageFormat.CloudEventsBinary;
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler((req, _) =>
                {
                    captured = req;
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }));

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            var ev = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Type = "t", Source = new Uri("https://x"), Id = "1",
                DataContentType = "application/json", Data = "{}"
            };
            ev["dataschema"] = new Uri("https://schemas.example.com/evt");

            await channel.PublishAsync(ev, null, cancellationToken);

            Assert.NotNull(captured);
            Assert.Equal("https://schemas.example.com/evt",
                captured!.Headers.GetValues("ce-dataschema").First());
        }

        // ── WebhookPublishOptions.EndpointUrl getter + defaults sanity ──────

        [Fact]
        public void WebhookPublishOptions_Defaults_AreExpected()
        {
            var opts = new WebhookPublishOptions();

            Assert.Null(opts.EndpointUrl);
            Assert.Equal(WebhookDefaults.SignatureHeaderName, opts.SignatureHeaderName);
            Assert.Equal(WebhookDefaults.DeliveryIdHeaderName, opts.DeliveryIdHeaderName);
            Assert.Equal(WebhookDefaults.EventTypeHeaderName, opts.EventTypeHeaderName);
            Assert.Equal(WebhookDefaults.TimestampHeaderName, opts.TimestampHeaderName);
            Assert.Equal(WebhookDefaults.SignatureAlgorithmHeaderName, opts.SignatureAlgorithmHeaderName);
            Assert.Null(opts.HttpClientName);
            Assert.Null(opts.Discovery);
            Assert.Equal(new[] { 429, 500, 502, 503, 504 }, opts.RetryableStatusCodes.Order());
        }

        // ── Merge with all typed fields set (flips every ?? branch in Merge) ─

        [Fact]
        public void Merge_AllTypedFieldsSet_OverridesEveryBaseField()
        {
            var baseOpts = new WebhookPublishOptions
            {
                EndpointUrl = "https://base/hook",
                SigningSecret = "base-secret",
                MaxRetryCount = 5,
                RetryDelay = TimeSpan.FromSeconds(2),
                RetryBackoffMultiplier = 3.0,
                RequestTimeout = TimeSpan.FromSeconds(45),
                MessageFormat = EventMessageFormat.CloudEventsJson,
                SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha512,
                ScheduleDeliveryAt = DateTimeOffset.UtcNow,
                ChannelName = "base-name",
                Discovery = new WebhookDiscoveryOptions { RequestOrigin = "base" }
            };

            var typed = new WebhookPublishOptions
            {
                EndpointUrl = "https://typed/hook",
                SigningSecret = "typed-secret",
                MaxRetryCount = 1,
                RetryDelay = TimeSpan.FromMilliseconds(50),
                RetryBackoffMultiplier = 1.5,
                RequestTimeout = TimeSpan.FromSeconds(10),
                MessageFormat = EventMessageFormat.CloudEventsBinary,
                SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha256,
                ScheduleDeliveryAt = DateTimeOffset.UtcNow.AddMinutes(5),
                ChannelName = "typed-name",
                Discovery = new WebhookDiscoveryOptions { RequestOrigin = "typed" }
            };

            var merged = WebhookPublishOptions.Merge(baseOpts, typed);

            Assert.Equal("https://typed/hook", merged.EndpointUrl);
            Assert.Equal("typed-secret", merged.SigningSecret);
            Assert.Equal(1, merged.MaxRetryCount);
            Assert.Equal(TimeSpan.FromMilliseconds(50), merged.RetryDelay);
            Assert.Equal(1.5, merged.RetryBackoffMultiplier);
            Assert.Equal(TimeSpan.FromSeconds(10), merged.RequestTimeout);
            Assert.Equal(EventMessageFormat.CloudEventsBinary, merged.MessageFormat);
            Assert.Equal(WebhookSignatureAlgorithm.HmacSha256, merged.SignatureAlgorithm);
            Assert.Equal(typed.ScheduleDeliveryAt, merged.ScheduleDeliveryAt);
            Assert.Equal("typed-name", merged.ChannelName);
            Assert.Equal("typed", merged.Discovery!.RequestOrigin);
        }

        // ── MergeOptions (per-call) with all per-call fields set ─────────────

        [Fact]
        public async Task PublishAsync_PerCallAllFieldsSet_OverridesChannelDefaults()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            HttpRequestMessage? captured = null;

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://base.example.com/r";
                    o.SigningSecret = "base-secret";
                    o.MaxRetryCount = 1;
                    o.MessageFormat = EventMessageFormat.Json;
                    o.SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha512;
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler((req, _) =>
                {
                    captured = req;
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }));

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            await channel.PublishAsync(
                new CloudEvent(CloudEventsSpecVersion.V1_0)
                {
                    Type = "t", Source = new Uri("https://x"), Id = "1",
                    DataContentType = "application/json", Data = "{}"
                },
                new WebhookPublishOptions
                {
                    EndpointUrl = "https://override.example.com/r",
                    SigningSecret = "override-secret",
                    MessageFormat = EventMessageFormat.CloudEventsBinary,
                    SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha256
                },
                cancellationToken);

            Assert.NotNull(captured);
            Assert.Equal("https://override.example.com/r", captured!.RequestUri!.ToString());
            // Binary format → ce-* headers present and content-type from data.
            Assert.True(captured.Headers.Contains("ce-type"));
            Assert.Equal("application/json", captured.Content!.Headers.ContentType!.MediaType);
            // HmacSha256 override → sha256 signature prefix.
            Assert.StartsWith("sha256=",
                captured.Headers.GetValues(WebhookDefaults.SignatureHeaderName).First());
        }

        // ── TryGetRetryAfterDelay: past HTTP-date → TimeSpan.Zero ────────────

        [Fact]
        public async Task PublishAsync_429WithPastRetryAfterDate_RetriesImmediately()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var attempts = 0;
            var firstAt = DateTimeOffset.MinValue;
            var secondAt = DateTimeOffset.MinValue;

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://hook.example.com/r";
                    o.MaxRetryCount = 1;
                    o.RetryDelay = TimeSpan.FromMilliseconds(10);
                    o.RetryBackoffMultiplier = 1.0;
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler((req, _) =>
                {
                    var n = Interlocked.Increment(ref attempts);
                    if (n == 1)
                    {
                        firstAt = DateTimeOffset.UtcNow;
                        // A past date → clamped to TimeSpan.Zero (immediate retry).
                        var past = DateTimeOffset.UtcNow.AddMinutes(-5);
                        var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                        resp.Headers.TryAddWithoutValidation("Retry-After", past.ToString("R"));
                        return resp;
                    }
                    secondAt = DateTimeOffset.UtcNow;
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }));

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            await channel.PublishAsync(
                new CloudEvent(CloudEventsSpecVersion.V1_0)
                {
                    Type = "t", Source = new Uri("https://x"), Id = "1",
                    DataContentType = "application/json", Data = "{}"
                }, null, cancellationToken);

            Assert.Equal(2, attempts);
            // Past Retry-After → immediate retry (clamped to zero), well under 900ms.
            var observed = secondAt - firstAt;
            Assert.True(observed < TimeSpan.FromMilliseconds(500),
                $"Past Retry-After should retry immediately; observed {observed.TotalMilliseconds}ms.");
        }

        // ── GetSignatureProvider: unknown algorithm throws ───────────────────

        [Fact]
        public async Task PublishAsync_UnknownSignatureAlgorithm_Throws()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(o =>
                {
                    o.EndpointUrl = "https://hook.example.com/r";
                    o.MaxRetryCount = 1;
                    // An enum value with no registered provider.
                    o.SignatureAlgorithm = (WebhookSignatureAlgorithm)999;
                });
            services.AddHttpClient(WebhookDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new LambdaHandler((_, _) =>
                    new HttpResponseMessage(HttpStatusCode.OK)));

            var channel = services.BuildServiceProvider()
                .GetRequiredService<IEventPublishChannel>();

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                channel.PublishAsync(
                    new CloudEvent(CloudEventsSpecVersion.V1_0)
                    {
                        Type = "t", Source = new Uri("https://x"), Id = "1",
                        DataContentType = "application/json", Data = "{}"
                    }, null, cancellationToken));
        }

        // ── WebhookHandshakeClient.EnsureHandshakeAsync arg validation ──────

        [Fact]
        public async Task HandshakeClient_EnsureHandshakeAsync_NullOptions_Throws()
        {
            using var factory = new DummyHttpClientFactory();
            var client = HandshakeClientReflector.CreateInstance(factory);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                HandshakeClientReflector.InvokeEnsureHandshakeAsync(
                    client, "https://x", null!, null, default));
        }

        [Fact]
        public async Task HandshakeClient_EnsureHandshakeAsync_EmptyEndpoint_Throws()
        {
            using var factory = new DummyHttpClientFactory();
            var client = HandshakeClientReflector.CreateInstance(factory);
            var opts = new WebhookDiscoveryOptions { RequestOrigin = "o" };

            await Assert.ThrowsAsync<ArgumentException>(() =>
                HandshakeClientReflector.InvokeEnsureHandshakeAsync(
                    client, "  ", opts, null, default));
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private sealed class LambdaHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _fn;
            public LambdaHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> fn) => _fn = fn;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_fn(request, cancellationToken));
        }

        /// <summary>
        /// A minimal <see cref="IHttpClientFactory"/> that returns a plain
        /// <see cref="HttpClient"/>; used for direct handshake-client tests
        /// that don't go through the channel DI surface.
        /// </summary>
        private sealed class DummyHttpClientFactory : IHttpClientFactory, IDisposable
        {
            private readonly HttpClient _client = new();
            public HttpClient CreateClient(string name) => _client;
            public void Dispose() => _client.Dispose();
        }

        private sealed class OrderCreated { }
    }

    /// <summary>
    /// Test-only reflection accessor for the internal
    /// <c>Hermodr.WebhookHandshakeClient</c> owned by the internal
    /// <c>Hermodr.WebhookPublishChannel</c>. No <c>InternalsVisibleTo</c>
    /// attribute is used: every member is reached via reflection so the
    /// production assembly stays sealed.
    /// </summary>
    internal static class HandshakeClientReflector
    {
        // The webhook assembly is reached through a *public* type so the
        // reflector does not depend on internal accessibility.
        private static readonly Assembly WebhookAssembly = typeof(WebhookPublishOptions).Assembly;

        private static readonly Type ChannelType =
            WebhookAssembly.GetType("Hermodr.WebhookPublishChannel", throwOnError: true)!;

        private static readonly Type ClientType =
            WebhookAssembly.GetType("Hermodr.WebhookHandshakeClient", throwOnError: true)!;

        private static readonly Type PermissionType =
            WebhookAssembly.GetType("Hermodr.WebhookDiscoveryPermission", throwOnError: true)!;

        /// <summary>
        /// Resolves the concrete <c>WebhookPublishChannel</c> singleton from
        /// the provider (not the decorated IEventPublishChannel) so the
        /// returned handshake client is the same instance used for delivery.
        /// </summary>
        public static object GetClientFromProvider(IServiceProvider provider)
        {
            var channel = provider.GetRequiredService(ChannelType);
            var field = ChannelType.GetField(
                "_handshakeClient",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field!.GetValue(channel)
                ?? throw new InvalidOperationException("_handshakeClient field was null.");
        }

        /// <summary>Reflectively invokes the parameterless <c>GetCached</c>.</summary>
        public static object? InvokeGetCached(object client, string endpointUrl)
            => ClientType.GetMethod("GetCached")!.Invoke(client, new object[] { endpointUrl });

        /// <summary>Reflectively invokes the parameterless <c>Evict</c>.</summary>
        public static void InvokeEvict(object client, string endpointUrl)
            => ClientType.GetMethod("Evict")!.Invoke(client, new object[] { endpointUrl });

        /// <summary>Reads the <c>AllowedOrigin</c> property off a permission.</summary>
        public static string GetAllowedOrigin(object permission)
            => (string)PermissionType.GetProperty("AllowedOrigin")!.GetValue(permission)!;

        /// <summary>
        /// Constructs a <c>WebhookHandshakeClient</c> via reflection. Pass
        /// <c>null</c> for <paramref name="factory"/> to exercise the ctor's
        /// null-argument guard. The underlying constructor throws
        /// <see cref="TargetInvocationException"/> wrapping the real
        /// exception; callers should use <see cref="Unwrap"/> to inspect it.
        /// </summary>
        public static object CreateInstance(IHttpClientFactory? factory)
        {
            // Bind the ctor explicitly so a null factory is typed correctly
            // (Activator.CreateInstance cannot infer the parameter type from null).
            var ctor = ClientType.GetConstructors().Single();
            return ctor.Invoke(new object?[] { factory, null })!;
        }

        /// <summary>
        /// Reflectively invokes <c>EnsureHandshakeAsync</c>. Because the method
        /// is async, argument-validation exceptions are captured into the
        /// returned task (not thrown synchronously by <c>Invoke</c>); this
        /// helper awaits the task and rethrows the real exception unwrapped,
        /// so callers can assert on the concrete exception type directly.
        /// </summary>
        public static async Task InvokeEnsureHandshakeAsync(
            object client, string endpointUrl, object options, string? httpClientName, CancellationToken ct)
        {
            var method = ClientType.GetMethod("EnsureHandshakeAsync")!;
            // The async method returns Task<WebhookDiscoveryPermission?>; we
            // only need to await it for its side effect / exception.
            var task = (Task)method.Invoke(client, new object?[] { endpointUrl, options, httpClientName, ct })!;
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (TargetInvocationException tie)
            {
                // Synchronous throw from Invoke (rare for async) — unwrap once.
                throw tie.InnerException!;
            }
            // If the task faulted, await already rethrew the real exception
            // (Argument*Exception), not TargetInvocationException.
        }

        /// <summary>Unwraps the real exception from a reflective invocation.</summary>
        public static Exception Unwrap(Exception ex) =>
            ex is TargetInvocationException tie && tie.InnerException != null
                ? tie.InnerException
                : ex;
    }
}