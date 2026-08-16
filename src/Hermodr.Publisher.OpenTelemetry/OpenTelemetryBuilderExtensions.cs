//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Hermodr
{
    /// <summary>
    /// Extension methods for <see cref="TracerProviderBuilder"/> and <see cref="MeterProviderBuilder"/>
    /// to add Hermodr OpenTelemetry instrumentation.
    /// </summary>
    public static class OpenTelemetryBuilderExtensions
    {
        /// <summary>
        /// Adds the Hermodr <see cref="System.Diagnostics.ActivitySource"/> to the tracer provider.
        /// </summary>
        /// <param name="builder">The tracer provider builder.</param>
        /// <param name="activitySourceName">
        /// The activity source name to subscribe to. Defaults to <c>"Hermodr"</c>.
        /// Must match the name configured in <see cref="OpenTelemetryInstrumentationOptions.ActivitySourceName"/>.
        /// </param>
        /// <returns>The same builder instance for chaining.</returns>
        public static TracerProviderBuilder AddHermodrInstrumentation(
            this TracerProviderBuilder builder,
            string? activitySourceName = null)
        {
            return builder.AddSource(activitySourceName ?? TelemetryConstants.DefaultActivitySourceName);
        }

        /// <summary>
        /// Adds the Hermodr <see cref="System.Diagnostics.Metrics.Meter"/> to the meter provider.
        /// </summary>
        /// <param name="builder">The meter provider builder.</param>
        /// <param name="meterName">
        /// The meter name to subscribe to. Defaults to <c>"Hermodr"</c>.
        /// Must match the name configured in <see cref="MetricsOptions.MeterName"/>.
        /// </param>
        /// <returns>The same builder instance for chaining.</returns>
        public static MeterProviderBuilder AddHermodrInstrumentation(
            this MeterProviderBuilder builder,
            string? meterName = null)
        {
            return builder.AddMeter(meterName ?? TelemetryConstants.DefaultMeterName);
        }
    }
}
