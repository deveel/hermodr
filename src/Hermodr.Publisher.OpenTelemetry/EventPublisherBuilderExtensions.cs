//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
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
        /// This registers two middleware components:
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
        /// </list>
        /// </para>
        /// <para>
        /// The shared <see cref="HermodrTelemetry.ActivitySource"/> is configured with the
        /// name from <see cref="OpenTelemetryInstrumentationOptions.ActivitySourceName"/>
        /// (default: <c>"Hermodr"</c>). Configure your OpenTelemetry SDK to listen to this
        /// source to collect spans.
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
        ///     .AddOpenTelemetry()
        ///     .AddRabbitMq(opts => { ... });
        /// </code>
        /// </example>
        public static EventPublisherBuilder AddOpenTelemetry(
            this EventPublisherBuilder builder,
            Action<OpenTelemetryInstrumentationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.Configure<OpenTelemetryInstrumentationOptions>(
                opts => configure?.Invoke(opts));

            builder.Services.AddSingleton<ActivitySourceFactory>();
            builder.Services.AddSingleton<ActivitySource>(sp => sp.GetRequiredService<ActivitySourceFactory>().Create());

            builder.Use<OpenTelemetryPublishMiddleware>();
            builder.Use<OpenTelemetrySubscriptionMiddleware>();

            return builder;
        }

        /// <summary>
        /// Adds only the publisher-side OpenTelemetry instrumentation (producer span + trace injection).
        /// Does not add the subscription-side middleware.
        /// </summary>
        /// <param name="builder">The builder to configure.</param>
        /// <param name="configure">
        /// An optional action to customize <see cref="OpenTelemetryInstrumentationOptions"/>.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for chaining.</returns>
        public static EventPublisherBuilder AddOpenTelemetryPublisherInstrumentation(
            this EventPublisherBuilder builder,
            Action<OpenTelemetryInstrumentationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.Configure<OpenTelemetryInstrumentationOptions>(
                opts =>
                {
                    opts.InstrumentSubscription = false;
                    configure?.Invoke(opts);
                });

            builder.Services.AddSingleton<ActivitySourceFactory>();
            builder.Services.AddSingleton<ActivitySource>(sp => sp.GetRequiredService<ActivitySourceFactory>().Create());

            builder.Use<OpenTelemetryPublishMiddleware>();

            return builder;
        }

        /// <summary>
        /// Adds only the subscription-side OpenTelemetry instrumentation (trace extraction + consumer span).
        /// Does not add the publisher-side middleware.
        /// </summary>
        /// <param name="builder">The builder to configure.</param>
        /// <param name="configure">
        /// An optional action to customize <see cref="OpenTelemetryInstrumentationOptions"/>.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for chaining.</returns>
        public static EventPublisherBuilder AddOpenTelemetrySubscriptionInstrumentation(
            this EventPublisherBuilder builder,
            Action<OpenTelemetryInstrumentationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.Configure<OpenTelemetryInstrumentationOptions>(
                opts =>
                {
                    opts.InstrumentPublisher = false;
                    configure?.Invoke(opts);
                });

            builder.Services.AddSingleton<ActivitySourceFactory>();
            builder.Services.AddSingleton<ActivitySource>(sp => sp.GetRequiredService<ActivitySourceFactory>().Create());

            builder.Use<OpenTelemetrySubscriptionMiddleware>();

            return builder;
        }
    }

    /// <summary>
    /// Factory for creating the <see cref="ActivitySource"/> used by Hermodr OpenTelemetry instrumentation.
    /// Receives configuration via constructor injection and sets the shared <see cref="HermodrTelemetry.ActivitySource"/>.
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
            HermodrTelemetry.ActivitySource = source;
            return source;
        }
    }
}
