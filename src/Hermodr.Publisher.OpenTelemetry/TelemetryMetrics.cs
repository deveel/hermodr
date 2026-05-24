//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics.Metrics;

namespace Hermodr
{
    /// <summary>
    /// Provides shared <see cref="Meter"/> instance and instrument factory methods
    /// for Hermodr metrics instrumentation.
    /// </summary>
    public static class TelemetryMetrics
    {
        private static Meter? _meter;

        /// <summary>
        /// Gets or sets the shared <see cref="Meter"/> for Hermodr metrics.
        /// </summary>
        public static Meter Meter
        {
            get => _meter ??= new Meter("Hermodr");
            set => _meter = value;
        }

        /// <summary>
        /// Creates a histogram instrument for publish duration.
        /// </summary>
        public static Histogram<double> CreatePublishDurationHistogram(Meter meter) =>
            meter.CreateHistogram<double>(
                TelemetryConstants.MetricPublishDuration,
                unit: "s",
                description: "Time to publish an event through the full pipeline");

        /// <summary>
        /// Creates a counter instrument for total publish attempts.
        /// </summary>
        public static Counter<long> CreatePublishTotalCounter(Meter meter) =>
            meter.CreateCounter<long>(
                TelemetryConstants.MetricPublishTotal,
                unit: "{event}",
                description: "Total number of publish attempts");

        /// <summary>
        /// Creates a counter instrument for publish errors.
        /// </summary>
        public static Counter<long> CreatePublishErrorsCounter(Meter meter) =>
            meter.CreateCounter<long>(
                TelemetryConstants.MetricPublishErrors,
                unit: "{error}",
                description: "Number of publish failures");

        /// <summary>
        /// Creates a counter instrument for subscription dispatch total.
        /// </summary>
        public static Counter<long> CreateSubscriptionDispatchTotalCounter(Meter meter) =>
            meter.CreateCounter<long>(
                TelemetryConstants.MetricSubscriptionDispatchTotal,
                unit: "{event}",
                description: "Number of subscription handler invocations");

        /// <summary>
        /// Creates a histogram instrument for subscription handler duration.
        /// </summary>
        public static Histogram<double> CreateSubscriptionHandlerDurationHistogram(Meter meter) =>
            meter.CreateHistogram<double>(
                TelemetryConstants.MetricSubscriptionHandlerDuration,
                unit: "s",
                description: "Time per subscription handler");
    }
}
