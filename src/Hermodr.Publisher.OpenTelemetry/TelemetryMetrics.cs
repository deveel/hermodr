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
    /// Delegates to <see cref="HermodrDiagnostics.Meter"/> as the default instance.
    /// </summary>
    public static class TelemetryMetrics
    {
        /// <summary>
        /// Gets the default <see cref="Meter"/> for Hermodr metrics.
        /// Delegates to <see cref="HermodrDiagnostics.Meter"/>.
        /// </summary>
        public static Meter Meter => HermodrDiagnostics.Meter;

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
    }
}
