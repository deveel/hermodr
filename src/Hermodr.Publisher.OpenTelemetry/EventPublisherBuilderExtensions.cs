//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// Extension methods for <see cref="EventPublisherBuilder"/> that add OpenTelemetry
    /// instrumentation to the event publishing pipeline.
    /// </summary>
    public static class EventPublisherBuilderExtensions
    {
        /// <summary>
        /// Adds OpenTelemetry instrumentation to the publisher pipeline.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This registers four middleware components:
        /// <list type="bullet">
        ///   <item>
        ///     <see cref="OpenTelemetryPublishMiddleware"/> — creates a producer span for
        ///     each publish operation and injects W3C traceparent/tracestate as CloudEvent
        ///     extension attributes so that downstream services can continue the trace.
        ///   </item>
        ///   <item>
        ///     <see cref="OpenTelemetrySubscriptionMiddleware"/> — extracts trace context
        ///     from incoming CloudEvent extensions and creates a consumer span. This is
        ///     placed before the event dispatcher so subscription handlers
        ///     automatically participate in the distributed trace via <c>Activity.Current</c>.
        ///   </item>
        ///   <item>
        ///     <see cref="MetricsMiddleware"/> — collects OpenTelemetry metrics for publish
        ///     operations including duration, total count, and error count. Individual metrics
        ///     can be toggled via <see cref="OpenTelemetryInstrumentationOptions.Metrics"/>.
        ///   </item>
        ///   <item>
        ///     <see cref="PipelineDiagnosticsMiddleware"/> — tracks events flowing through
        ///     the full middleware pipeline (non-bypassed publishes).
        ///   </item>
        /// </list>
        /// </para>
        /// <para>
        /// All inline instrumentation in channel packages, outbox, dead letter, and audit
        /// trail packages relies on the shared <see cref="HermodrDiagnostics.ActivitySource"/>
        /// and <see cref="HermodrDiagnostics.Meter"/> instances.  The DI registration below
        /// replaces these with user-configured sources when
        /// <see cref="OpenTelemetryInstrumentationOptions.ActivitySourceName"/> or
        /// <see cref="MetricsOptions.MeterName"/> are customized.
        /// </para>
        /// </remarks>
        /// <param name="builder">The builder to configure.</param>
        /// <param name="configure">
        /// An optional action to customize <see cref="OpenTelemetryInstrumentationOptions"/>.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for chaining.</returns>
        /// <example>
        /// <code language="csharp">
        /// services.AddEventPublisher()
        ///     .UseOpenTelemetry(opts =>
        ///     {
        ///         opts.Metrics.PublishDuration = true;
        ///         opts.Metrics.PublishErrors = true;
        ///     })
        ///     .AddRabbitMq(opts => { ... });
        /// </code>
        /// </example>
        public static EventPublisherBuilder UseOpenTelemetry(
            this EventPublisherBuilder builder,
            Action<OpenTelemetryInstrumentationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.Configure<OpenTelemetryInstrumentationOptions>(
                opts => configure?.Invoke(opts));

            builder.Services.AddSingleton<ActivitySourceFactory>();
            builder.Services.AddSingleton<ActivitySource>(sp => sp.GetRequiredService<ActivitySourceFactory>().Create());

            builder.Services.AddSingleton<MeterFactory>();
            builder.Services.AddSingleton<Meter>(sp => sp.GetRequiredService<MeterFactory>().Create());

            builder.Services.TryAddSingleton<OpenTelemetryPublishMiddleware>();
            builder.Services.TryAddSingleton<OpenTelemetrySubscriptionMiddleware>();
            builder.Services.TryAddSingleton<MetricsMiddleware>();
            builder.Services.TryAddSingleton<PipelineDiagnosticsMiddleware>();

            builder.Use<OpenTelemetryPublishMiddleware>();
            builder.Use<OpenTelemetrySubscriptionMiddleware>();
            builder.Use<MetricsMiddleware>();
            builder.Use<PipelineDiagnosticsMiddleware>();

            return builder;
        }
    }

    /// <summary>
    /// Factory for creating the <see cref="ActivitySource"/> used by Hermodr OpenTelemetry instrumentation.
    /// Receives configuration via constructor injection and updates
    /// <see cref="HermodrDiagnostics.ActivitySource"/>.
    /// </summary>
    internal sealed class ActivitySourceFactory
    {
        private readonly OpenTelemetryInstrumentationOptions _options;

        public ActivitySourceFactory(IOptions<OpenTelemetryInstrumentationOptions> options)
        {
            _options = options.Value;
        }

        public ActivitySource Create()
        {
            var source = new ActivitySource(_options.ActivitySourceName);
            HermodrDiagnostics.ActivitySource = source;
            return source;
        }
    }

    /// <summary>
    /// Factory for creating the <see cref="Meter"/> used by Hermodr metrics instrumentation.
    /// Receives configuration via constructor injection and updates
    /// <see cref="HermodrDiagnostics.Meter"/>.
    /// </summary>
    internal sealed class MeterFactory
    {
        private readonly OpenTelemetryInstrumentationOptions _options;

        public MeterFactory(IOptions<OpenTelemetryInstrumentationOptions> options)
        {
            _options = options.Value;
        }

        public Meter Create()
        {
            var meter = new Meter(_options.Metrics.MeterName);
            HermodrDiagnostics.Meter = meter;
            return meter;
        }
    }
}
