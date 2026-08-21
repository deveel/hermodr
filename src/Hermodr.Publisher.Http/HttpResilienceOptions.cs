//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Http.Resilience;

namespace Hermodr
{
    /// <summary>
    /// Bindable configuration subset for the standard resilience policies
    /// (retry and circuit-breaker) applied to an <see cref="HttpEndpoint"/>'s
    /// named <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The HTTP channel configures every endpoint's named client through
    /// <c>AddStandardResilienceHandler()</c>, which builds a
    /// <see cref="Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions"/>
    /// pipeline (retry, circuit-breaker, total-request timeout and an optional
    /// rate limiter). Instances of this type carry the subset of those settings
    /// that supports configuration binding; properties left <c>null</c> keep the
    /// library-provided defaults.
    /// </para>
    /// <para>
    /// For settings not exposed here (such as the rate limiter) configure the
    /// client's pipeline directly through the <see cref="Microsoft.Extensions.Http.Resilience.IHttpStandardResiliencePipelineBuilder"/>
    /// surfaced by <c>ConfigureHttpClient</c>.
    /// </para>
    /// </remarks>
    public class HttpResilienceOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of retry attempts after the initial
        /// attempt. When <c>null</c> the library default (3) is used.
        /// </summary>
        public int? RetryMaxAttempts { get; set; }

        /// <summary>
        /// Gets or sets the base delay between retry attempts (grown
        /// exponentially). When <c>null</c> the library default is used.
        /// </summary>
        public TimeSpan? RetryDelay { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether retry delays are jittered to
        /// avoid retry storms. When <c>null</c> the library default
        /// (<c>true</c>) is used.
        /// </summary>
        public bool? RetryUseJitter { get; set; }

        /// <summary>
        /// Gets or sets the duration over which failures are sampled for the
        /// circuit breaker. When <c>null</c> the library default is used.
        /// </summary>
        public TimeSpan? CircuitBreakerSamplingDuration { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of requests required before the
        /// circuit breaker evaluates the failure ratio. When <c>null</c> the
        /// library default is used.
        /// </summary>
        public int? CircuitBreakerMinimumThroughput { get; set; }

        /// <summary>
        /// Gets or sets the failure ratio (0.0 to 1.0) of a sampling window that
        /// trips the circuit breaker. When <c>null</c> the library default is used.
        /// </summary>
        public double? CircuitBreakerFailureRatio { get; set; }

        /// <summary>
        /// Gets or sets the duration the circuit stays open before allowing
        /// requests through again. When <c>null</c> the library default is used.
        /// </summary>
        public TimeSpan? CircuitBreakerBreakDuration { get; set; }

        /// <summary>
        /// Applies the non-<c>null</c> settings of this instance to
        /// <paramref name="target"/>, leaving all other settings untouched.
        /// </summary>
        /// <param name="target">The standard resilience options to configure.</param>
        public void Apply(HttpStandardResilienceOptions target)
        {
            ArgumentNullException.ThrowIfNull(target);

            if (RetryMaxAttempts.HasValue)
                target.Retry.MaxRetryAttempts = RetryMaxAttempts.Value;
            if (RetryDelay.HasValue)
                target.Retry.Delay = RetryDelay.Value;
            if (RetryUseJitter.HasValue)
                target.Retry.UseJitter = RetryUseJitter.Value;

            if (CircuitBreakerSamplingDuration.HasValue)
                target.CircuitBreaker.SamplingDuration = CircuitBreakerSamplingDuration.Value;
            if (CircuitBreakerMinimumThroughput.HasValue)
                target.CircuitBreaker.MinimumThroughput = CircuitBreakerMinimumThroughput.Value;
            if (CircuitBreakerFailureRatio.HasValue)
                target.CircuitBreaker.FailureRatio = CircuitBreakerFailureRatio.Value;
            if (CircuitBreakerBreakDuration.HasValue)
                target.CircuitBreaker.BreakDuration = CircuitBreakerBreakDuration.Value;
        }
    }
}