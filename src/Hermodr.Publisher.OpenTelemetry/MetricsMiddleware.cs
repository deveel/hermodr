//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// Middleware that collects OpenTelemetry metrics for the event publishing pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This middleware records the following metrics (when enabled via <see cref="MetricsOptions"/>):
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>hermodr.publish.duration</c> — Histogram of publish pipeline duration in seconds.</description></item>
    ///   <item><description><c>hermodr.publish.total</c> — Counter of total publish attempts.</description></item>
    ///   <item><description><c>hermodr.publish.errors</c> — Counter of publish failures.</description></item>
    /// </list>
    /// <para>
    /// Individual metrics can be toggled on/off via <see cref="OpenTelemetryInstrumentationOptions.Metrics"/>.
    /// Setting <see cref="MetricsOptions.Enabled"/> to <c>false</c> disables all metrics collection.
    /// </para>
    /// </remarks>
    public class MetricsMiddleware : IEventMiddleware
    {
        private readonly Meter _meter;
        private readonly OpenTelemetryInstrumentationOptions _options;
        private readonly ILogger _logger;

        private readonly Histogram<double>? _publishDuration;
        private readonly Counter<long>? _publishTotal;
        private readonly Counter<long>? _publishErrors;

        /// <summary>
        /// Creates a new instance of <see cref="MetricsMiddleware"/>.
        /// </summary>
        public MetricsMiddleware(
            Meter meter,
            IOptions<OpenTelemetryInstrumentationOptions>? options = null,
            ILogger<MetricsMiddleware>? logger = null)
        {
            _meter = meter;
            _options = options?.Value ?? new OpenTelemetryInstrumentationOptions();
            _logger = logger ?? NullLogger<MetricsMiddleware>.Instance;

            var metricsOpts = _options.Metrics;

            if (metricsOpts.Enabled)
            {
                if (metricsOpts.PublishDuration)
                {
                    _publishDuration = TelemetryMetrics.CreatePublishDurationHistogram(_meter);
                }

                if (metricsOpts.PublishTotal)
                {
                    _publishTotal = TelemetryMetrics.CreatePublishTotalCounter(_meter);
                }

                if (metricsOpts.PublishErrors)
                {
                    _publishErrors = TelemetryMetrics.CreatePublishErrorsCounter(_meter);
                }
            }
            else
            {
                _logger.MetricsNotEnabled("all");
            }
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var eventType = context.Event.Type ?? "unknown";

            if (_publishTotal != null)
            {
                var tags = new TagList { { "event.type", eventType } };
                _publishTotal.Add(1, tags);
            }

            var sw = Stopwatch.StartNew();
            try
            {
                await next(context);

                sw.Stop();

                if (_publishDuration != null)
                {
                    var tags = new TagList { { "event.type", eventType }, { "success", true } };
                    _publishDuration.Record(sw.Elapsed.TotalSeconds, tags);
                }
            }
            catch
            {
                sw.Stop();

                if (_publishErrors != null)
                {
                    var tags = new TagList { { "event.type", eventType } };
                    _publishErrors.Add(1, tags);
                }

                if (_publishDuration != null)
                {
                    var tags = new TagList { { "event.type", eventType }, { "success", false } };
                    _publishDuration.Record(sw.Elapsed.TotalSeconds, tags);
                }

                throw;
            }
        }
    }
}
