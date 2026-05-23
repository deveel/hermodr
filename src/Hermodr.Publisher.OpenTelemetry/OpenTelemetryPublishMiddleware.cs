//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// Middleware that creates an OpenTelemetry span for each event publish operation
    /// and injects W3C trace context (traceparent/tracestate) as CloudEvent extension attributes.
    /// </summary>
    public class OpenTelemetryPublishMiddleware : IEventMiddleware
    {
        private readonly ActivitySource _activitySource;
        private readonly OpenTelemetryInstrumentationOptions _options;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new instance of <see cref="OpenTelemetryPublishMiddleware"/>.
        /// </summary>
        public OpenTelemetryPublishMiddleware(
            ActivitySource activitySource,
            IOptions<OpenTelemetryInstrumentationOptions>? options = null,
            ILogger<OpenTelemetryPublishMiddleware>? logger = null)
        {
            _activitySource = activitySource;
            _options = options?.Value ?? new OpenTelemetryInstrumentationOptions();
            _logger = logger ?? NullLogger<OpenTelemetryPublishMiddleware>.Instance;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var eventType = context.Event.Type ?? "unknown";

            var activity = _activitySource.StartActivity(
                TelemetryConstants.PublisherSpanName(eventType),
                ActivityKind.Producer,
                parentContext: default,
                tags: new ActivityTagsCollection
                {
                    ["event.type"] = eventType,
                    ["messaging.system"] = TelemetryConstants.MessagingSystem,
                    ["messaging.operation"] = "publish",
                });

            if (activity == null)
            {
                _logger.TracePublishSpanNotEnabled(eventType);
                await next(context);
                return;
            }

            try
            {
                _options.EnrichWithEvent?.Invoke(activity, context.Event);

                HermodrTelemetry.InjectTraceContext(context.Event, activity);

                if (context.Event.Id != null)
                {
                    activity.SetTag("event.id", context.Event.Id);
                }

                await next(context);

                activity.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                if (_options.RecordException)
                {
                    activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity.AddException(ex);
                }
                else
                {
                    activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                }

                throw;
            }
            finally
            {
                activity.Dispose();
            }
        }
    }
}
