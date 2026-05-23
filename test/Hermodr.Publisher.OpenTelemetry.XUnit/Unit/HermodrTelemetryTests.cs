//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;

namespace Hermodr;

public class HermodrTelemetryTests
{
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
        var traceparent = cloudEvent[traceparentAttr] as string;

        Assert.NotNull(traceparent);
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

        Assert.Throws<FormatException>(() => HermodrTelemetry.TryExtractTraceContext(cloudEvent, out _));
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
        var traceparent = cloudEvent[traceparentAttr] as string;
        Assert.NotNull(traceparent);
        var parts = traceparent.Split('-');
        Assert.Equal(4, parts.Length);
        var expectedFlags = activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00";
        Assert.Equal(expectedFlags, parts[3]);
    }

    [Fact]
    public void ActivitySource_CanBeSetAndRetrieved()
    {
        var original = HermodrTelemetry.ActivitySource;
        try
        {
            var customSource = new ActivitySource("custom-test");
            HermodrTelemetry.ActivitySource = customSource;
            Assert.Same(customSource, HermodrTelemetry.ActivitySource);
        }
        finally
        {
            HermodrTelemetry.ActivitySource = original;
        }
    }

    [Fact]
    public void CreatePublisherActivity_SetsCorrectTags()
    {
        using var source = new ActivitySource("test-tags");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "test-tags",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var activity = HermodrTelemetry.CreatePublisherActivity(source, "com.test.order");

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
        using var source = new ActivitySource("test-tags");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "test-tags",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var activity = HermodrTelemetry.CreateConsumerActivity(source, "com.test.order");

        Assert.NotNull(activity);
        Assert.Equal("handle com.test.order", activity.DisplayName);
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal("com.test.order", activity.GetTagItem("event.type"));
        Assert.Equal("hermodr", activity.GetTagItem("messaging.system"));
        Assert.Equal("receive", activity.GetTagItem("messaging.operation"));
    }

    [Fact]
    public void CreateConsumerActivity_UsesParentContext_WhenProvided()
    {
        using var source = new ActivitySource("test-tags");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "test-tags",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);

        var activity = HermodrTelemetry.CreateConsumerActivity(source, "com.test.order", parentContext);

        Assert.NotNull(activity);
        Assert.Equal(parentContext.TraceId, activity.TraceId);
    }

    [Fact]
    public void CreatePublisherActivity_ReturnsNull_WhenNoListener()
    {
        using var source = new ActivitySource("no-listener-pub");

        var activity = HermodrTelemetry.CreatePublisherActivity(source, "com.test");

        Assert.Null(activity);
    }

    [Fact]
    public void CreateConsumerActivity_ReturnsNull_WhenNoListener()
    {
        using var source = new ActivitySource("no-listener-con");

        var activity = HermodrTelemetry.CreateConsumerActivity(source, "com.test");

        Assert.Null(activity);
    }
}
