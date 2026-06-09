//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics.Metrics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "ChannelTelemetry")]
public class ChannelMetricsTests
{
    [Fact]
    public void ChannelTelemetry_Constructor_CreatesHistogram()
    {
        using var meter = new Meter("test.channel");
        var telemetry = new ChannelTelemetry(meter);
        Assert.NotNull(telemetry);
    }

    [Fact]
    public void ChannelTelemetry_RecordChannelPublishDuration_RecordsValue()
    {
        var values = new List<double>();
        var tags = new List<IReadOnlyList<KeyValuePair<string, object?>>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "test.channel" && instrument.Name == TelemetryConstants.MetricChannelPublishDuration)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, val, t, _) =>
        {
            values.Add(val);
            tags.Add(t.ToArray());
        });
        listener.Start();

        using var meter = new Meter("test.channel");
        var telemetry = new ChannelTelemetry(meter);
        telemetry.RecordChannelPublishDuration(1.5, "com.test.order", "TestChannel");

        Assert.Single(values);
        Assert.Equal(1.5, values[0]);
        Assert.Contains(tags[0], kvp => kvp.Key == "event.type" && kvp.Value?.Equals("com.test.order") == true);
        Assert.Contains(tags[0], kvp => kvp.Key == "channel.type" && kvp.Value?.Equals("TestChannel") == true);
    }

    [Fact]
    public void ChannelTelemetry_Constructor_UsesGlobalMeter()
    {
        var originalMeter = HermodrDiagnostics.Meter;
        using var testMeter = new Meter("Hermodr");
        try
        {
            HermodrDiagnostics.Meter = testMeter;
            var telemetry = new ChannelTelemetry();
            Assert.NotNull(telemetry);
        }
        finally
        {
            HermodrDiagnostics.Meter = originalMeter;
        }
    }
}
