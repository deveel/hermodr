//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    [Trait("Channel", "Webhook")]
    [Trait("Function", "TypedOptions")]
    public static class TypedChannelOptionsTests
    {
        // ── Unit tests on Merge() ──────────────────────────────────────────────

        [Fact]
        public static void Merge_BothSet_TypedWins()
        {
            var baseOpts = new WebhookPublishOptions
            {
                EndpointUrl       = "https://base.example.com/hooks",
                SigningSecret     = "base-secret",
                MaxRetryCount     = 3,
                RetryDelay        = TimeSpan.FromSeconds(1),
                MessageFormat     = EventMessageFormat.Json,
                SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha256,
                SignatureHeaderName = "X-Sig",
            };
            var typedOpts = new WebhookPublishOptions
            {
                EndpointUrl       = "https://typed.example.com/hooks",
                SigningSecret     = "typed-secret",
                MaxRetryCount     = 5,
                RetryDelay        = TimeSpan.FromSeconds(2),
                MessageFormat     = EventMessageFormat.Xml,
                SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha512,
                // Channel-structural left null — should be taken from base
            };

            var merged = WebhookPublishOptions.Merge(baseOpts, typedOpts);

            Assert.Equal("https://typed.example.com/hooks", merged.EndpointUrl);
            Assert.Equal("typed-secret",  merged.SigningSecret);
            Assert.Equal(5,               merged.MaxRetryCount);
            Assert.Equal(TimeSpan.FromSeconds(2), merged.RetryDelay);
            Assert.Equal(EventMessageFormat.Xml, merged.MessageFormat);
            Assert.Equal(WebhookSignatureAlgorithm.HmacSha512, merged.SignatureAlgorithm);
            // Channel-structural always from base
            Assert.Equal("X-Sig", merged.SignatureHeaderName);
        }

        [Fact]
        public static void Merge_OnlyBaseSet_BaseValuesUsed()
        {
            var baseOpts = new WebhookPublishOptions
            {
                EndpointUrl   = "https://base.example.com/hooks",
                SigningSecret = "base-secret",
                MaxRetryCount = 3,
            };
            var typedOpts = new WebhookPublishOptions(); // all nulls

            var merged = WebhookPublishOptions.Merge(baseOpts, typedOpts);

            Assert.Equal("https://base.example.com/hooks", merged.EndpointUrl);
            Assert.Equal("base-secret", merged.SigningSecret);
            Assert.Equal(3, merged.MaxRetryCount);
        }

        [Fact]
        public static void Merge_OnlyTypedSet_TypedValuesUsed()
        {
            var baseOpts  = new WebhookPublishOptions(); // all nulls
            var typedOpts = new WebhookPublishOptions
            {
                EndpointUrl   = "https://typed.example.com/hooks",
                SigningSecret = "typed-secret",
                MaxRetryCount = 5,
            };

            var merged = WebhookPublishOptions.Merge(baseOpts, typedOpts);

            Assert.Equal("https://typed.example.com/hooks", merged.EndpointUrl);
            Assert.Equal("typed-secret", merged.SigningSecret);
            Assert.Equal(5, merged.MaxRetryCount);
        }

        [Fact]
        public static void Merge_AdditionalHeaders_AreMerged_TypedWinsOnCollision()
        {
            var baseOpts = new WebhookPublishOptions
            {
                EndpointUrl       = "https://base.example.com/hooks",
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-Tenant-Id"] = "base-tenant",
                    ["X-Source"]    = "base-source",
                },
            };
            var typedOpts = new WebhookPublishOptions
            {
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-Tenant-Id"] = "typed-tenant", // collision — typed wins
                    ["X-Version"]   = "2",
                },
            };

            var merged = WebhookPublishOptions.Merge(baseOpts, typedOpts);

            Assert.Equal("typed-tenant", merged.AdditionalHeaders["X-Tenant-Id"]);
            Assert.Equal("base-source",  merged.AdditionalHeaders["X-Source"]);
            Assert.Equal("2",            merged.AdditionalHeaders["X-Version"]);
        }

        // ── DI / registration tests ────────────────────────────────────────────

        private class OrderPlaced { }

        [Fact]
        public static void AddWebhooks_Typed_RegistersTypedOptionsAndChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(opts => opts.EndpointUrl = "https://base.example.com/hooks")
                .AddWebhooks<OrderPlaced>(opts =>
                {
                    opts.EndpointUrl = "https://order.example.com/hooks";
                });

            // Typed options configure action registered
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IConfigureOptions<WebhookPublishOptions<OrderPlaced>>));

            // Typed channel registered as IEventPublishChannel<OrderPlaced>
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel<OrderPlaced>));
        }

        [Fact]
        public static void AddWebhooks_Typed_OptionsAreIndependentFromBase()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddWebhooks(opts =>
                {
                    opts.EndpointUrl   = "https://base.example.com/hooks";
                    opts.SigningSecret  = "base-secret";
                    opts.MaxRetryCount = 3;
                })
                .AddWebhooks<OrderPlaced>(opts =>
                {
                    opts.EndpointUrl = "https://order.example.com/hooks";
                });

            var provider = services.BuildServiceProvider();

            var baseOpts  = provider.GetRequiredService<IOptions<WebhookPublishOptions>>();
            var typedOpts = provider.GetRequiredService<IOptions<WebhookPublishOptions<OrderPlaced>>>();

            Assert.Equal("https://base.example.com/hooks",  baseOpts.Value.EndpointUrl);
            Assert.Equal("https://order.example.com/hooks", typedOpts.Value.EndpointUrl);

            // SigningSecret not set in typed opts — raw value should be null (inherit at construction)
            Assert.Null(typedOpts.Value.SigningSecret);
        }

        [Fact]
        public static void AddWebhooks_Typed_MergeAppliedAtConstruction()
        {
            var baseValue = new WebhookPublishOptions
            {
                EndpointUrl   = "https://base.example.com/hooks",
                SigningSecret = "base-secret",
                MaxRetryCount = 3,
                SignatureHeaderName = "X-Webhook-Signature",
            };
            var typedValue = new WebhookPublishOptions<OrderPlaced>
            {
                EndpointUrl = "https://order.example.com/hooks",
            };

            var merged = WebhookPublishOptions.Merge(baseValue, typedValue);

            Assert.Equal("https://order.example.com/hooks", merged.EndpointUrl);  // typed wins
            Assert.Equal("base-secret",  merged.SigningSecret);                    // falls back to base
            Assert.Equal(3,              merged.MaxRetryCount);                    // falls back to base
            // Channel-structural always from base
            Assert.Equal("X-Webhook-Signature", merged.SignatureHeaderName);
        }
    }
}

