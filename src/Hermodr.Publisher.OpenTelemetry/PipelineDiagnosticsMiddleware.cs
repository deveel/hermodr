//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// Middleware that tracks events flowing through the full middleware pipeline.
    /// Pipeline bypass counting is handled directly in <see cref="EventPublisher"/>.
    /// </summary>
    public sealed class PipelineDiagnosticsMiddleware : IEventMiddleware
    {
        private readonly PipelineDiagnosticsTelemetry _telemetry;

    /// <summary>
    /// Creates a new instance of <see cref="PipelineDiagnosticsMiddleware"/>.
    /// </summary>
    /// <param name="meter">The Hermodr <see cref="Meter"/>. Injected by DI to
    /// trigger the factory that updates <see cref="HermodrDiagnostics.Meter"/>.</param>
    /// <param name="options">Optional instrumentation options.</param>
    /// <param name="logger">Optional logger.</param>
    public PipelineDiagnosticsMiddleware(
        Meter meter,
        IOptions<OpenTelemetryInstrumentationOptions>? options = null,
        ILogger<PipelineDiagnosticsMiddleware>? logger = null)
    {
        var metricsOpts = options?.Value?.Metrics ?? new MetricsOptions();
        _telemetry = new PipelineDiagnosticsTelemetry(meter, metricsOpts.Enabled);
    }

        /// <inheritdoc/>
        public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            await next(context);

            if (_telemetry.IsEnabled)
            {
                _telemetry.AddPipelineTotal(context.Event.Type ?? "unknown");
            }
        }
    }
}
