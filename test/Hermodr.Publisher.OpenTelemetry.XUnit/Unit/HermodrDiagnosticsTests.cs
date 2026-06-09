using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "Diagnostics")]
public class HermodrDiagnosticsTests : IDisposable
{
    private readonly ActivitySource _originalSource;
    private readonly Meter _originalMeter;

    public HermodrDiagnosticsTests()
    {
        _originalSource = HermodrDiagnostics.ActivitySource;
        _originalMeter = HermodrDiagnostics.Meter;
        // Reset to default for isolation
        HermodrDiagnostics.ActivitySource = new ActivitySource("Hermodr");
        HermodrDiagnostics.Meter = new Meter("Hermodr");
    }

    public void Dispose()
    {
        HermodrDiagnostics.ActivitySource = _originalSource;
        HermodrDiagnostics.Meter = _originalMeter;
    }

    [Fact]
    public void ActivitySource_HasCorrectDefaultName()
    {
        Assert.Equal("Hermodr", HermodrDiagnostics.ActivitySource.Name);
    }

    [Fact]
    public void ActivitySource_Get_ReturnsSameInstanceOnMultipleCalls()
    {
        var first = HermodrDiagnostics.ActivitySource;
        var second = HermodrDiagnostics.ActivitySource;
        Assert.Same(first, second);
    }

    [Fact]
    public void Meter_Default_HasCorrectName()
    {
        Assert.Equal("Hermodr", HermodrDiagnostics.Meter.Name);
    }

    [Fact]
    public void Meter_Get_ReturnsSameInstanceOnMultipleCalls()
    {
        var first = HermodrDiagnostics.Meter;
        var second = HermodrDiagnostics.Meter;
        Assert.Same(first, second);
    }

    [Fact]
    public void ActivitySourceAndMeter_AreIndependent()
    {
        Assert.NotNull(HermodrDiagnostics.ActivitySource);
        Assert.NotNull(HermodrDiagnostics.Meter);
        Assert.NotSame(HermodrDiagnostics.ActivitySource, HermodrDiagnostics.Meter);
    }

    [Fact]
    public void TelemetryTags_MessagingSystem_IsMessagingSystem()
    {
        Assert.Equal("messaging.system", TelemetryTags.MessagingSystem);
    }

    [Fact]
    public void TelemetryTags_EventType_IsCorrect()
    {
        Assert.Equal("event.type", TelemetryTags.EventType);
    }
}
