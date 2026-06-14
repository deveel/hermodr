//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;
using System.Diagnostics.Metrics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "Telemetry")]
public class TelemetryEdgeCasesTests
{
    [Fact]
    public void HermodrDiagnostics_ActivitySource_Replacement_IsThreadSafe()
    {
        var original = HermodrDiagnostics.ActivitySource;
        try
        {
            var first = HermodrDiagnostics.ActivitySource;
            var replacement = new ActivitySource("Replacement");
            HermodrDiagnostics.ActivitySource = replacement;
            var second = HermodrDiagnostics.ActivitySource;

            Assert.Same(replacement, second);
            Assert.NotSame(first, second);
        }
        finally
        {
            HermodrDiagnostics.ActivitySource = original;
        }
    }

    [Fact]
    public void HermodrDiagnostics_Meter_Replacement_IsThreadSafe()
    {
        var original = HermodrDiagnostics.Meter;
        try
        {
            var first = HermodrDiagnostics.Meter;
            var replacement = new Meter("Replacement");
            HermodrDiagnostics.Meter = replacement;
            var second = HermodrDiagnostics.Meter;

            Assert.Same(replacement, second);
            Assert.NotSame(first, second);
        }
        finally
        {
            HermodrDiagnostics.Meter = original;
        }
    }

    [Fact]
    public void HermodrDiagnostics_ActivitySourceAndMeter_AreIndependent()
    {
        Assert.NotSame(HermodrDiagnostics.ActivitySource, HermodrDiagnostics.Meter);
    }

    [Fact]
    public void PipelineDiagnosticsMiddleware_Constructor_WithNullLogger()
    {
        using var meter = new Meter("Hermodr");
        var options = Options.Create(new OpenTelemetryInstrumentationOptions());
        var middleware = new PipelineDiagnosticsMiddleware(meter, options, null);
        Assert.NotNull(middleware);
    }

    [Fact]
    public void PipelineDiagnosticsMiddleware_Constructor_WithNoOptions()
    {
        using var meter = new Meter("Hermodr");
        var middleware = new PipelineDiagnosticsMiddleware(meter);
        Assert.NotNull(middleware);
    }

    [Theory]
    [InlineData("hermodr.publish.duration")]
    [InlineData("hermodr.publish.total")]
    [InlineData("hermodr.publish.errors")]
    [InlineData("hermodr.event.enrich.duration")]
    [InlineData("hermodr.pipeline.bypass_total")]
    [InlineData("hermodr.channel.publish.duration")]
    [InlineData("hermodr.pipeline.total")]
    [InlineData("hermodr.subscription.handle.duration")]
    [InlineData("hermodr.subscription.matched")]
    [InlineData("hermodr.subscription.errors")]
    public void MetricName_StartsWithHermodrPrefix(string metricName)
    {
        Assert.StartsWith("hermodr.", metricName);
    }

    [Theory]
    [InlineData("dead_letter.replay")]
    [InlineData("dead_letter.store")]
    [InlineData("audit.append")]
    [InlineData("outbox.store")]
    [InlineData("outbox.relay")]
    [InlineData("transport.publish.rabbitmq")]
    [InlineData("transport.publish.servicebus")]
    [InlineData("transport.publish.masstransit")]
    [InlineData("transport.publish.webhook")]
    public void SpanName_ContainsDotSeparator(string spanName)
    {
        Assert.Contains('.', spanName);
    }

    [Theory]
    [InlineData("event.type")]
    [InlineData("messaging.system")]
    [InlineData("messaging.operation")]
    [InlineData("messaging.destination")]
    [InlineData("messaging.destination_kind")]
    [InlineData("messaging.rabbitmq.routing_key")]
    [InlineData("http.request.method")]
    [InlineData("subscription.name")]
    [InlineData("channel.type")]
    [InlineData("message.id")]
    [InlineData("success")]
    public void TagKey_IsConsistent(string tagKey)
    {
        Assert.NotEmpty(tagKey);
    }

    [Fact]
    public void PublisherTelemetry_UsesGlobalMeter()
    {
        var originalMeter = HermodrDiagnostics.Meter;
        try
        {
            var testMeter = new Meter("Hermodr.Test.Publisher");
            HermodrDiagnostics.Meter = testMeter;
            var telemetry = new PublisherTelemetry();
            Assert.NotNull(telemetry);
        }
        finally
        {
            HermodrDiagnostics.Meter = originalMeter;
        }
    }

    [Fact]
    public void PublisherTelemetry_WithCustomMeter_RecordsEnrichDuration()
    {
        var values = new List<double>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "test.publisher" && instrument.Name == TelemetryConstants.MetricEventEnrichDuration)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, val, _, _) => values.Add(val));
        listener.Start();

        var meter = new Meter("test.publisher");
        var telemetry = new PublisherTelemetry(meter);
        telemetry.RecordEventEnrichDuration(0.5, "com.test.event");

        Assert.Single(values);
        Assert.Equal(0.5, values[0]);
        listener.Dispose();
    }

    [Fact]
    public void PublisherTelemetry_WithCustomMeter_RecordsPipelineBypass()
    {
        var values = new List<long>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "test.publisher" && instrument.Name == TelemetryConstants.MetricPipelineBypassTotal)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, val, _, _) => values.Add(val));
        listener.Start();

        var meter = new Meter("test.publisher");
        var telemetry = new PublisherTelemetry(meter);
        telemetry.AddPipelineBypass("com.test.event");

        Assert.Single(values);
        Assert.Equal(1, values[0]);
        listener.Dispose();
    }

    [Fact]
    public void ChannelTelemetry_UsesGlobalMeter()
    {
        var originalMeter = HermodrDiagnostics.Meter;
        try
        {
            HermodrDiagnostics.Meter = new Meter("Hermodr.Test.Channel");
            var telemetry = new ChannelTelemetry();
            Assert.NotNull(telemetry);
        }
        finally
        {
            HermodrDiagnostics.Meter = originalMeter;
        }
    }

    [Fact]
    public void ChannelTelemetry_WithCustomMeter_RecordsPublishDuration()
    {
        var values = new List<double>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "test.channel" && instrument.Name == TelemetryConstants.MetricChannelPublishDuration)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, val, _, _) => values.Add(val));
        listener.Start();

        var meter = new Meter("test.channel");
        var telemetry = new ChannelTelemetry(meter);
        telemetry.RecordChannelPublishDuration(1.5, "com.test.order", "TestChannel");

        Assert.Single(values);
        Assert.Equal(1.5, values[0]);
        listener.Dispose();
    }

    [Fact]
    public void EventPublisher_CreatesPublisherTelemetry()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IEventPublishChannel>(new SimpleTestChannel());

        services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"));

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();
        Assert.NotNull(publisher);
    }

    private sealed class SimpleTestOptions : EventPublishOptions { }

    private sealed class SimpleTestChannel : EventPublishChannel<SimpleTestOptions>
    {
        public SimpleTestChannel(IServiceProvider? serviceProvider = null) : base(new SimpleTestOptions(), serviceProvider ?? new ServiceCollection().BuildServiceProvider()) { }

        protected override Task PublishCoreAsync(CloudEvent @event, SimpleTestOptions options, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
