//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hermodr
{
    /// <summary>
    /// Performs the
    /// <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/http-webhook.md">CloudEvents HTTP WebHooks</see>
    /// <c>OPTIONS</c> validation handshake against a delivery endpoint and
    /// caches the granted permission so that subsequent deliveries reuse it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The client is stateful: a single instance per channel owns a
    /// per-endpoint cache of <see cref="WebhookDiscoveryPermission"/>
    /// records. The cache is keyed by the normalized endpoint URL so that
    /// options-driven endpoint changes do not return stale permissions.
    /// </para>
    /// <para>
    /// All public members are safe for concurrent use: the cache uses a
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/> and a single
    /// sema­phore per endpoint gates the <c>OPTIONS</c> round-trip so that
    /// concurrent first deliveries to the same endpoint produce exactly one
    /// validation request.
    /// </para>
    /// </remarks>
    internal sealed class WebhookHandshakeClient
    {
        // Header constants from the CloudEvents HTTP WebHooks spec.
        internal const string RequestOriginHeader   = "WebHook-Request-Origin";
        internal const string RequestCallbackHeader = "WebHook-Request-Callback";
        internal const string RequestRateHeader     = "WebHook-Request-Rate";
        internal const string AllowedOriginHeader   = "WebHook-Allowed-Origin";
        internal const string AllowedRateHeader     = "WebHook-Allowed-Rate";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;

        // Per-endpoint permission cache: endpoint URL → permission.
        private readonly ConcurrentDictionary<string, WebhookDiscoveryPermission> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        // Per-endpoint in-flight handshake gates: ensures exactly one OPTIONS
        // request per endpoint even under concurrent first deliveries.
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a new handshake client.
        /// </summary>
        /// <param name="httpClientFactory">
        /// The <see cref="IHttpClientFactory"/> used to resolve the named
        /// <see cref="HttpClient"/> for the <c>OPTIONS</c> request.
        /// </param>
        /// <param name="logger">An optional logger.</param>
        public WebhookHandshakeClient(
            IHttpClientFactory httpClientFactory,
            ILogger? logger = null)
        {
            _httpClientFactory = httpClientFactory
                ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? NullLogger<WebhookHandshakeClient>.Instance;
        }

        /// <summary>
        /// Returns the cached permission for <paramref name="endpointUrl"/>,
        /// or <c>null</c> when no successful handshake has been recorded.
        /// </summary>
        public WebhookDiscoveryPermission? GetCached(string endpointUrl)
            => _cache.TryGetValue(Normalize(endpointUrl), out var p) ? p : null;

        /// <summary>
        /// Evicts the cached permission for <paramref name="endpointUrl"/>,
        /// forcing the next delivery to re-handshake. Safe to call when no
        /// permission is cached.
        /// </summary>
        public void Evict(string endpointUrl)
            => _cache.TryRemove(Normalize(endpointUrl), out _);

        /// <summary>
        /// Performs the <c>OPTIONS</c> validation handshake against
        /// <paramref name="endpointUrl"/> when no cached permission is
        /// available, and returns the granted permission. A cached permission
        /// is returned without issuing a request.
        /// </summary>
        /// <param name="endpointUrl">The exact delivery target URI to validate.</param>
        /// <param name="options">The discovery configuration to apply.</param>
        /// <param name="httpClientName">
        /// The named <see cref="HttpClient"/> to use (typically
        /// <see cref="WebhookDefaults.HttpClientName"/>).
        /// </param>
        /// <param name="cancellationToken">
        /// Token to cancel the handshake request.
        /// </param>
        /// <returns>
        /// The granted <see cref="WebhookDiscoveryPermission"/> when the
        /// target returns <c>WebHook-Allowed-Origin</c>; otherwise
        /// <c>null</c> (the target did not grant permission).
        /// </returns>
        /// <exception cref="WebhookHandshakeException">
        /// Thrown only when the caller requests a strict handshake
        /// (<see cref="WebhookDiscoveryOptions.RequireSuccessfulHandshake"/>)
        /// and the target refuses permission or the request faults.
        /// </exception>
        public async Task<WebhookDiscoveryPermission?> EnsureHandshakeAsync(
            string endpointUrl,
            WebhookDiscoveryOptions options,
            string? httpClientName,
            CancellationToken cancellationToken)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(endpointUrl))
                throw new ArgumentException("endpointUrl is required.", nameof(endpointUrl));

            var key = Normalize(endpointUrl);

            // Fast path: cached permission still valid.
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            // Gate per endpoint so concurrent first deliveries share one round-trip.
            var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Re-check inside the gate: another caller may have completed it.
                if (_cache.TryGetValue(key, out cached))
                    return cached;

                var permission = await SendOptionsAsync(endpointUrl, options, httpClientName, cancellationToken)
                    .ConfigureAwait(false);

                if (permission is not null)
                {
                    _cache[key] = permission;
                    _logger.LogHandshakeGranted(endpointUrl, permission.AllowedOrigin, permission.AllowedRate);
                }
                else
                {
                    _logger.LogHandshakeRefused(endpointUrl);

                    if (options.RequireSuccessfulHandshake)
                    {
                        throw new WebhookHandshakeException(
                            $"Webhook validation handshake was refused by '{endpointUrl}': " +
                            "the target did not return WebHook-Allowed-Origin.",
                            endpointUrl);
                    }
                }

                return permission;
            }
            catch (WebhookHandshakeException)
            {
                // Strict-mode refusal already logged above; rethrow.
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogHandshakeFailed(endpointUrl, ex.Message);

                if (options.RequireSuccessfulHandshake)
                {
                    throw new WebhookHandshakeException(
                        $"Webhook validation handshake to '{endpointUrl}' failed: {ex.Message}",
                        endpointUrl, innerException: ex);
                }

                return null;
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<WebhookDiscoveryPermission?> SendOptionsAsync(
            string endpointUrl,
            WebhookDiscoveryOptions options,
            string? httpClientName,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, endpointUrl);

            // Mandatory: WebHook-Request-Origin.
            request.Headers.TryAddWithoutValidation(RequestOriginHeader, options.RequestOrigin);

            // Optional: WebHook-Request-Callback (URL advertised for async grant).
            if (!string.IsNullOrWhiteSpace(options.RequestCallback))
                request.Headers.TryAddWithoutValidation(RequestCallbackHeader, options.RequestCallback);

            // Optional: WebHook-Request-Rate (requests per minute).
            if (options.RequestRate is int rate && rate > 0)
                request.Headers.TryAddWithoutValidation(RequestRateHeader, rate.ToString());

            var client = _httpClientFactory.CreateClient(
                string.IsNullOrEmpty(httpClientName) ? WebhookDefaults.HttpClientName : httpClientName!);

            var timeout = options.RequestTimeout ?? TimeSpan.FromSeconds(30);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            // The spec says the handshake cannot rely on status codes alone:
            // a target that does not handle OPTIONS returns 405, while a target
            // that does handle it but refuses may still return 200 without the
            // allowed-origin header. We therefore only consult the headers.
            var hasAllowedOrigin =
                response.Headers.TryGetValues(AllowedOriginHeader, out var originValues);

            if (!hasAllowedOrigin)
                return null;

            var allowedOrigin = originValues!.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(allowedOrigin))
                return null;

            int? allowedRate = null;
            if (response.Headers.TryGetValues(AllowedRateHeader, out var rateValues))
            {
                var rateStr = rateValues.FirstOrDefault();
                if (rateStr is not null && rateStr.Trim() != "*")
                {
                    rateStr = rateStr.Trim();
                    if (int.TryParse(rateStr, out var parsed) && parsed > 0)
                        allowedRate = parsed;
                }
            }

            return new WebhookDiscoveryPermission(allowedOrigin!, allowedRate);
        }

        private static string Normalize(string endpointUrl)
            => endpointUrl.Trim();
    }
}