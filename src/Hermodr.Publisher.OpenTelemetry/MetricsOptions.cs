//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Options for configuring metrics collection in Hermodr telemetry.
    /// </summary>
    public class MetricsOptions
    {
        /// <summary>
        /// Gets or sets whether to enable metrics collection.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the name of the <see cref="System.Diagnostics.Metrics.Meter"/> used for metrics.
        /// Defaults to <c>"Hermodr"</c>.
        /// </summary>
        public string MeterName { get; set; } = "Hermodr";

        /// <summary>
        /// Gets or sets whether to collect the publish duration histogram metric.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool PublishDuration { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to collect the publish total counter metric.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool PublishTotal { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to collect the publish errors counter metric.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool PublishErrors { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to collect the subscription dispatch total counter metric.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool SubscriptionDispatchTotal { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to collect the subscription handler duration histogram metric.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool SubscriptionHandlerDuration { get; set; } = true;
    }
}
