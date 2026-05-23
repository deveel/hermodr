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
    /// Middleware that extracts W3C trace context from incoming CloudEvent extension attributes
    /// and creates a consumer span so that subscription handlers participate in the distributed trace.
    /// </summary>
    /// <remarks>
    /// This middleware should be placed in the pipeline <strong>before</strong> the
    /// event dispatcher so that the extracted <see cref="Activity"/> is available
    /// to all downstream subscription handlers via <c>Activity.Current</c>.
    /// </remarks>
    public class OpenTelemetrySubscriptionMiddleware : IEventMiddleware
    {
        private readonly ActivitySource _activitySource;
        private readonly OpenTelemetryInstrumentationOptions _options;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new instance of <see cref="OpenTelemetrySubscriptionMiddleware"/>.
        /// </summary>
        public OpenTelemetrySubscriptionMiddleware(
            ActivitySource activitySource,
            IOptions<OpenTelemetryInstrumentationOptions>? options = null,
            ILogger<OpenTelemetrySubscriptionMiddleware>? logger = null)
        {
            _activitySource = activitySource;
            _options = options?.Value ?? new OpenTelemetryInstrumentationOptions();
            _logger = logger ?? NullLogger<OpenTelemetrySubscriptionMiddleware>.Instance;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var eventType = context.Event.Type ?? "unknown";

            ActivityContext? parentContext = null;
            if (HermodrTelemetry.TryExtractTraceContext(context.Event, out var extracted))
            {
                parentContext = extracted;
                _logger.TraceExtracted(eventType, extracted.TraceId.ToString());
            }

            using var activity = _activitySource.StartActivity(
                TelemetryConstants.ConsumerSpanName(eventType),
                ActivityKind.Consumer,
                parentContext: parentContext ?? default,
                tags: new ActivityTagsCollection
                {
                    ["event.type"] = eventType,
                    ["messaging.system"] = TelemetryConstants.MessagingSystem,
                    ["messaging.operation"] = "receive",
                });

            if (activity == null)
            {
                _logger.TraceConsumeSpanNotEnabled(eventType);
                await next(context);
                return;
            }

            try
            {
                context.Items[TelemetryConstants.ActivityItemKey] = activity;

                _options.EnrichWithEvent?.Invoke(activity, context.Event);

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
        }
    }
}
