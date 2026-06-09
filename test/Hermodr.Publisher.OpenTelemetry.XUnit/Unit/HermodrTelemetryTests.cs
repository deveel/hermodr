//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;
using System.Diagnostics.Metrics;

using CloudNative.CloudEvents;

namespace Hermodr;

public class HermodrTelemetryTests : IDisposable
{
    private readonly ActivitySource _originalSource;
    private readonly Meter _originalMeter;

    public HermodrTelemetryTests()
    {
        _originalSource = HermodrDiagnostics.ActivitySource;
        _originalMeter = HermodrDiagnostics.Meter;
        HermodrDiagnostics.ActivitySource = new ActivitySource("Hermodr");
        HermodrDiagnostics.Meter = new Meter("Hermodr");
    }

    public void Dispose()
    {
        HermodrDiagnostics.ActivitySource = _originalSource;
        HermodrDiagnostics.Meter = _originalMeter;
    }

    private const string TraceParentKey = "traceparent";
    private const string TraceStateKey = "tracestate";

    [Fact]
    public void InjectTraceContext_SetsTraceParentExtension()
    {
        using var source = new ActivitySource("test");
        using var listener = new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test", ActivityKind.Internal)!;
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };

        HermodrTelemetry.InjectTraceContext(cloudEvent, activity);

        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        var traceparent = Assert.IsType<string>(cloudEvent[traceparentAttr]);

        Assert.StartsWith("00-", traceparent);
        var parts = traceparent.Split('-');
        Assert.Equal(4, parts.Length);
        Assert.Equal(activity.TraceId.ToHexString(), parts[1]);
        Assert.Equal(activity.SpanId.ToHexString(), parts[2]);
    }

    [Fact]
    public void InjectTraceContext_SetsTraceStateExtension_WhenPresent()
    {
        using var source = new ActivitySource("test");
        using var listener = new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData };
        ActivitySource.AddActivityListener(listener);

        var parentCtx = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, "key=value");
        using var activity = source.StartActivity(
            ActivityKind.Internal,
            parentCtx,
            name: "test")!;

        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };

        HermodrTelemetry.InjectTraceContext(cloudEvent, activity);

        var traceStateAttr = CloudEventAttribute.CreateExtension(TraceStateKey, CloudEventAttributeType.String);
        var traceState = cloudEvent[traceStateAttr] as string;

        Assert.Equal("key=value", traceState);
    }

    [Fact]
    public void InjectTraceContext_DoesNotSetTraceState_WhenNull()
    {
        using var source = new ActivitySource("test");
        using var listener = new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test", ActivityKind.Internal)!;
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };

        HermodrTelemetry.InjectTraceContext(cloudEvent, activity);

        var traceStateAttr = CloudEventAttribute.CreateExtension(TraceStateKey, CloudEventAttributeType.String);
        Assert.Null(cloudEvent[traceStateAttr]);
    }

    [Fact]
    public void TryExtractTraceContext_ReturnsTrue_WhenTraceParentPresent()
    {
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        var result = HermodrTelemetry.TryExtractTraceContext(cloudEvent, out var parentContext);

        Assert.True(result);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", parentContext.TraceId.ToHexString());
        Assert.Equal("00f067aa0ba902b7", parentContext.SpanId.ToHexString());
        Assert.True(parentContext.IsRemote);
    }

    [Fact]
    public void TryExtractTraceContext_ReturnsFalse_WhenTraceParentMissing()
    {
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };

        var result = HermodrTelemetry.TryExtractTraceContext(cloudEvent, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryExtractTraceContext_ReturnsFalse_WhenTraceParentInvalid()
    {
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "invalid-traceparent";

        var result = HermodrTelemetry.TryExtractTraceContext(cloudEvent, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryExtractTraceContext_ExtractsTraceState_WhenPresent()
    {
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        var traceStateAttr = CloudEventAttribute.CreateExtension(TraceStateKey, CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        cloudEvent[traceStateAttr] = "key=value";

        var result = HermodrTelemetry.TryExtractTraceContext(cloudEvent, out var parentContext);

        Assert.True(result);
        Assert.Equal("key=value", parentContext.TraceState);
    }

    [Fact]
    public void RoundTrip_InjectThenExtract_ProducesMatchingContext()
    {
        using var source = new ActivitySource("test");
        using var listener = new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData };
        ActivitySource.AddActivityListener(listener);

        var originalContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            "tenant=abc",
            isRemote: false);

        using var activity = source.StartActivity(
            ActivityKind.Producer,
            originalContext,
            name: "test")!;
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };

        HermodrTelemetry.InjectTraceContext(cloudEvent, activity);

        var extracted = HermodrTelemetry.TryExtractTraceContext(cloudEvent, out var extractedContext);

        Assert.True(extracted);
        Assert.Equal(originalContext.TraceId, extractedContext.TraceId);
        Assert.Equal(activity.SpanId, extractedContext.SpanId);
        Assert.Equal(activity.ActivityTraceFlags, extractedContext.TraceFlags);
        Assert.Equal(originalContext.TraceState, extractedContext.TraceState);
        Assert.True(extractedContext.IsRemote);
    }

    [Fact]
    public void InjectTraceContext_Throws_WhenEventIsNull()
    {
        using var source = new ActivitySource("test");
        using var listener = new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData };
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("test", ActivityKind.Internal)!;

        Assert.Throws<ArgumentNullException>(() => HermodrTelemetry.InjectTraceContext(null!, activity));
    }

    [Fact]
    public void InjectTraceContext_Throws_WhenActivityIsNull()
    {
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };
        Assert.Throws<ArgumentNullException>(() => HermodrTelemetry.InjectTraceContext(cloudEvent, null!));
    }

    [Fact]
    public void TryExtractTraceContext_Throws_WhenEventIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => HermodrTelemetry.TryExtractTraceContext(null!, out _));
    }

    [Fact]
    public void TryExtractTraceContext_ReturnsFalse_WhenTraceParentEmpty()
    {
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "";

        var result = HermodrTelemetry.TryExtractTraceContext(cloudEvent, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryExtractTraceContext_ReturnsFalse_WhenTraceParentTooShort()
    {
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "00-abc";

        var result = HermodrTelemetry.TryExtractTraceContext(cloudEvent, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryExtractTraceContext_ReturnsFalse_WhenTraceIdInvalidHex()
    {
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "00-INVALID_HEX_HERE_4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        var result = HermodrTelemetry.TryExtractTraceContext(cloudEvent, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryExtractTraceContext_ReturnsFalse_WhenSpanIdInvalidHex()
    {
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "00-4bf92f3577b34da6a3ce929d0e0e4736-INVALID_HEX-01";

        var result = HermodrTelemetry.TryExtractTraceContext(cloudEvent, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryExtractTraceContext_ReturnsFalse_WhenTraceFlagsInvalid()
    {
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-XX";

        var result = HermodrTelemetry.TryExtractTraceContext(cloudEvent, out _);
        Assert.False(result);
    }

    [Fact]
    public void InjectTraceContext_SetsTraceFlags_Correctly()
    {
        using var source = new ActivitySource("test");
        using var listener = new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test", ActivityKind.Internal)!;
        var cloudEvent = new CloudEvent { Type = "com.test", Source = new Uri("https://example.com") };

        HermodrTelemetry.InjectTraceContext(cloudEvent, activity);

        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        var traceparent = Assert.IsType<string>(cloudEvent[traceparentAttr]);
        var parts = traceparent.Split('-');
        Assert.Equal(4, parts.Length);
        var expectedFlags = activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00";
        Assert.Equal(expectedFlags, parts[3]);
    }

    [Fact]
    public void CreatePublisherActivity_SetsCorrectTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Hermodr",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var activity = HermodrTelemetry.CreatePublisherActivity("com.test.order");

        Assert.NotNull(activity);
        Assert.Equal("publish com.test.order", activity.DisplayName);
        Assert.Equal(ActivityKind.Producer, activity.Kind);
        Assert.Equal("com.test.order", activity.GetTagItem("event.type"));
        Assert.Equal("hermodr", activity.GetTagItem("messaging.system"));
        Assert.Equal("publish", activity.GetTagItem("messaging.operation"));
    }

    [Fact]
    public void CreateConsumerActivity_SetsCorrectTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Hermodr",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var activity = HermodrTelemetry.CreateConsumerActivity("com.test.order");

        Assert.NotNull(activity);
        Assert.Equal("handle com.test.order", activity.DisplayName);
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal("com.test.order", activity.GetTagItem("event.type"));
        Assert.Equal("hermodr", activity.GetTagItem("messaging.system"));
        Assert.Equal("receive", activity.GetTagItem("messaging.operation"));
    }

    [Fact]
    public void CreatePublisherActivity_ReturnsNull_WhenNoListener()
    {
        var activity = HermodrTelemetry.CreatePublisherActivity("com.test.no-listener");
        Assert.Null(activity);
    }

    [Fact]
    public void CreateConsumerActivity_ReturnsNull_WhenNoListener()
    {
        var activity = HermodrTelemetry.CreateConsumerActivity("com.test.no-listener");
        Assert.Null(activity);
    }

    [Fact]
    public void CreatePublisherActivity_HandlesNullEventType()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Hermodr",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var activity = HermodrTelemetry.CreatePublisherActivity(null!);
        Assert.NotNull(activity);
        Assert.Equal("publish ", activity.DisplayName);
        Assert.Null(activity.GetTagItem("event.type"));
    }

    [Fact]
    public void CreateConsumerActivity_HandlesNullEventType()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Hermodr",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var activity = HermodrTelemetry.CreateConsumerActivity(null!);
        Assert.NotNull(activity);
        Assert.Equal("handle ", activity.DisplayName);
        Assert.Null(activity.GetTagItem("event.type"));
    }

    [Fact]
    public void CreatePublisherActivity_HasNoParent_WhenNoCurrentActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Hermodr",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var activity = HermodrTelemetry.CreatePublisherActivity("com.test.noparent");
        Assert.NotNull(activity);
        Assert.Equal(default, activity.Parent);
    }

    [Fact]
    public void CreateConsumerActivity_HasNoParent_WhenNoCurrentActivityAndNoParentContext()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Hermodr",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var activity = HermodrTelemetry.CreateConsumerActivity("com.test.noparent");
        Assert.NotNull(activity);
        Assert.Equal(default, activity.Parent);
    }

    [Fact]
    public void CreatePublisherActivity_LinksToCurrentActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Hermodr",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var parent = new Activity("parent").SetStartTime(DateTime.UtcNow).Start();
        var activity = HermodrTelemetry.CreatePublisherActivity("com.test.child");
        Assert.NotNull(activity);
        Assert.Equal(parent.TraceId, activity.TraceId);
        Assert.Equal(parent.SpanId, activity.ParentSpanId);
        parent.Stop();
    }

    [Fact]
    public void CreateConsumerActivity_NoParentContext_FallsBackToCurrentActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Hermodr",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var parent = new Activity("parent").SetStartTime(DateTime.UtcNow).Start();
        var activity = HermodrTelemetry.CreateConsumerActivity("com.test.child");
        Assert.NotNull(activity);
        Assert.Equal(parent.TraceId, activity.TraceId);
        parent.Stop();
    }

    [Fact]
    public void CreateConsumerActivity_Precedence_ParentContextOverCurrentActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Hermodr",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var explicitParent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);

        using var current = new Activity("current").SetStartTime(DateTime.UtcNow).Start();

        var activity = HermodrTelemetry.CreateConsumerActivity("com.test.precedence", explicitParent);
        Assert.NotNull(activity);
        Assert.Equal(explicitParent.TraceId, activity.TraceId);
        Assert.NotEqual(current.TraceId, activity.TraceId);
        current.Stop();
    }

    [Fact]
    public void CreateConsumerActivity_UsesParentContext_WhenProvided()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Hermodr",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);

        var activity = HermodrTelemetry.CreateConsumerActivity("com.test.order", parentContext);

        Assert.NotNull(activity);
        Assert.Equal(parentContext.TraceId, activity.TraceId);
    }
}
