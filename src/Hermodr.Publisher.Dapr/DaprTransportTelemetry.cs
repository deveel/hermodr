//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

namespace Hermodr;

internal sealed class DaprTransportTelemetry
{
    private readonly ActivitySource _activitySource;

    public DaprTransportTelemetry()
        : this(HermodrDiagnostics.ActivitySource)
    {
    }

    public DaprTransportTelemetry(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }

    public Activity? StartPublishActivity(string eventType, string? pubsubName, string? topic)
    {
        return _activitySource.StartActivity(
            ActivityKind.Producer,
            name: TelemetryConstants.SpanTransportDapr,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType },
                { TelemetryTags.MessagingSystem, "dapr" },
                { TelemetryTags.MessagingDestination, topic ?? string.Empty },
                { "messaging.dapr.pubsubname", pubsubName ?? string.Empty },
            });
    }
}