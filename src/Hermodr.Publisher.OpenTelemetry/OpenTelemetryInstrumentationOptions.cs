//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// Options for configuring OpenTelemetry instrumentation of the event publisher.
    /// </summary>
    public class OpenTelemetryInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets the name of the <see cref="ActivitySource"/> used for instrumentation.
        /// Defaults to <c>"Hermodr"</c>.
        /// </summary>
        public string ActivitySourceName { get; set; } = TelemetryConstants.DefaultActivitySourceName;

        /// <summary>
        /// Gets or sets whether to enable publisher-side instrumentation (span creation + trace injection).
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool InstrumentPublisher { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable subscription-side instrumentation (trace extraction + consumer spans).
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool InstrumentSubscription { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to record exception details on spans when publish or handle operations fail.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool RecordException { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional callback to enrich span tags with data from the CloudEvent.
        /// Invoked after the span is created but before the operation executes.
        /// </summary>
        public Action<Activity, CloudEvent>? EnrichWithEvent { get; set; }

        /// <summary>
        /// Gets the options for configuring metrics collection.
        /// </summary>
        public MetricsOptions Metrics { get; set; } = new();
    }
}
