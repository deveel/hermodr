//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Security.Cryptography;
using System.Text;

namespace Deveel.Events
{
    public class WebhookSignatureProviderTests
    {
        // ── Algorithm metadata ────────────────────────────────────────────────

        [Theory]
        [InlineData(typeof(HmacSha256SignatureProvider), WebhookSignatureAlgorithm.HmacSha256, "hmac-sha256", "sha256")]
        [InlineData(typeof(HmacSha384SignatureProvider), WebhookSignatureAlgorithm.HmacSha384, "hmac-sha384", "sha384")]
        [InlineData(typeof(HmacSha512SignatureProvider), WebhookSignatureAlgorithm.HmacSha512, "hmac-sha512", "sha512")]
        public void Provider_AlgorithmMetadata_IsCorrect(
            Type providerType,
            WebhookSignatureAlgorithm expectedAlgorithm,
            string expectedAlgorithmName,
            string expectedPrefix)
        {
            var provider = (IWebhookSignatureProvider)Activator.CreateInstance(providerType)!;

            Assert.Equal(expectedAlgorithm,    provider.Algorithm);
            Assert.Equal(expectedAlgorithmName, provider.AlgorithmName);

            var sig = provider.ComputeSignature(new byte[] { 1, 2, 3 }, 1_000_000L, "s");
            Assert.StartsWith($"{expectedPrefix}=", sig);
        }

        // ── Signature correctness ─────────────────────────────────────────────

        [Theory]
        [InlineData(typeof(HmacSha256SignatureProvider))]
        [InlineData(typeof(HmacSha384SignatureProvider))]
        [InlineData(typeof(HmacSha512SignatureProvider))]
        public void ComputeSignature_IsDeterministic(Type providerType)
        {
            var p       = (IWebhookSignatureProvider)Activator.CreateInstance(providerType)!;
            var payload = Encoding.UTF8.GetBytes("{\"type\":\"test\"}");
            var s1      = p.ComputeSignature(payload, 1_000_000L, "mysecret");
            var s2      = p.ComputeSignature(payload, 1_000_000L, "mysecret");
            Assert.Equal(s1, s2);
        }

        [Theory]
        [InlineData(typeof(HmacSha256SignatureProvider))]
        [InlineData(typeof(HmacSha384SignatureProvider))]
        [InlineData(typeof(HmacSha512SignatureProvider))]
        public void ComputeSignature_DiffersForDifferentSecrets(Type providerType)
        {
            var p       = (IWebhookSignatureProvider)Activator.CreateInstance(providerType)!;
            var payload = Encoding.UTF8.GetBytes("body");
            Assert.NotEqual(
                p.ComputeSignature(payload, 1_000_000L, "secret1"),
                p.ComputeSignature(payload, 1_000_000L, "secret2"));
        }

        [Theory]
        [InlineData(typeof(HmacSha256SignatureProvider))]
        [InlineData(typeof(HmacSha384SignatureProvider))]
        [InlineData(typeof(HmacSha512SignatureProvider))]
        public void ComputeSignature_DiffersForDifferentTimestamps(Type providerType)
        {
            var p       = (IWebhookSignatureProvider)Activator.CreateInstance(providerType)!;
            var payload = Encoding.UTF8.GetBytes("body");
            Assert.NotEqual(
                p.ComputeSignature(payload, 1_000_000L, "secret"),
                p.ComputeSignature(payload, 1_000_001L, "secret"));
        }

        // ── Manual round-trip verification ────────────────────────────────────

        [Fact]
        public void Sha256_MatchesManualHmac()
            => VerifyAgainstManualHmac(
                HmacSha256SignatureProvider.Default, "sha256",
                (msg, key) => { using var h = new HMACSHA256(key); return h.ComputeHash(msg); });

        [Fact]
        public void Sha384_MatchesManualHmac()
            => VerifyAgainstManualHmac(
                HmacSha384SignatureProvider.Default, "sha384",
                (msg, key) => { using var h = new HMACSHA384(key); return h.ComputeHash(msg); });

        [Fact]
        public void Sha512_MatchesManualHmac()
            => VerifyAgainstManualHmac(
                HmacSha512SignatureProvider.Default, "sha512",
                (msg, key) => { using var h = new HMACSHA512(key); return h.ComputeHash(msg); });

        private static void VerifyAgainstManualHmac(
            IWebhookSignatureProvider provider,
            string prefix,
            Func<byte[], byte[], byte[]> computeHash)
        {
            var payload   = Encoding.UTF8.GetBytes("hello");
            var timestamp = 1_700_000_000L;
            var secret    = "mysecret";

            var prefixBytes = Encoding.UTF8.GetBytes($"{timestamp}.");
            var message     = prefixBytes.Concat(payload).ToArray();
            var key         = Encoding.UTF8.GetBytes(secret);
            var expected    = $"{prefix}=" + Convert.ToHexString(computeHash(message, key)).ToLowerInvariant();

            Assert.Equal(expected, provider.ComputeSignature(payload, timestamp, secret));
        }

        // ── SHA-1 legacy provider ─────────────────────────────────────────────

        [Fact]
        public void Sha1_HasCorrectAlgorithmMetadata()
        {
#pragma warning disable CS0618
            var p = HmacSha1SignatureProvider.Default;
#pragma warning restore CS0618
            Assert.Equal(WebhookSignatureAlgorithm.HmacSha1, p.Algorithm);
            Assert.Equal("hmac-sha1", p.AlgorithmName);
            var sig = p.ComputeSignature(new byte[] { 1 }, 1L, "s");
            Assert.StartsWith("sha1=", sig);
        }

        // ── Backward-compat alias ─────────────────────────────────────────────

        [Fact]
        [Obsolete]
        public void LegacyWebhookSignatureProvider_IsSha256Underneath()
        {
            var legacy = WebhookSignatureProvider.Default;
            Assert.Equal(WebhookSignatureAlgorithm.HmacSha256, legacy.Algorithm);
            Assert.Equal("hmac-sha256", legacy.AlgorithmName);
        }
    }
}
