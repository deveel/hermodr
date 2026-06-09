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
[Trait("Feature", "SubscriptionTelemetry")]
public class SubscriptionMetricsTests
{
    [Fact]
    public async Task Subscribe_RecordsHandleDuration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new SubscriptionMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"), cancellationToken: cancellationToken);

        Assert.NotEmpty(fixture.HandleDurationValues);
        Assert.All(fixture.HandleDurationValues, v => Assert.True(v >= 0));
    }

    [Fact]
    public async Task Subscribe_RecordsHandleDurationWithEventTypeTag()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new SubscriptionMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"), cancellationToken: cancellationToken);

        Assert.Single(fixture.HandleDurationTags);
        var tags = fixture.HandleDurationTags[0];
        Assert.Contains(tags, kvp => kvp.Key == "event.type" && kvp.Value?.Equals("com.test.order") == true);
    }

    [Fact]
    public async Task Subscribe_RecordsHandleDurationWithSubscriptionNameTag()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new SubscriptionMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"), cancellationToken: cancellationToken);

        Assert.Single(fixture.HandleDurationTags);
        var tags = fixture.HandleDurationTags[0];
        Assert.Contains(tags, kvp => kvp.Key == "subscription.name" && kvp.Value?.Equals("test-subscription") == true);
    }

    [Fact]
    public async Task Subscribe_RecordsMatchedCount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new SubscriptionMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"), cancellationToken: cancellationToken);

        Assert.NotEmpty(fixture.MatchedValues);
        Assert.All(fixture.MatchedValues, v => Assert.True(v > 0));
    }

    [Fact]
    public async Task Subscribe_RecordsMatchedCountWithEventTypeTag()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new SubscriptionMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"), cancellationToken: cancellationToken);

        Assert.Single(fixture.MatchedTags);
        var tags = fixture.MatchedTags[0];
        Assert.Contains(tags, kvp => kvp.Key == "event.type" && kvp.Value?.Equals("com.test.order") == true);
    }

    [Fact]
    public async Task Subscribe_RecordsErrorCount_WhenHandlerThrows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new SubscriptionMetricsTestFixture(throwOnHandler: true);
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"), cancellationToken: cancellationToken);

        Assert.Equal(1, fixture.ErrorCount);
    }

    [Fact]
    public async Task Subscribe_RecordsErrorCountWithEventTypeAndSubscriptionNameTags()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new SubscriptionMetricsTestFixture(throwOnHandler: true);
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"), cancellationToken: cancellationToken);

        Assert.Single(fixture.ErrorTags);
        var tags = fixture.ErrorTags[0];
        Assert.Contains(tags, kvp => kvp.Key == "event.type" && kvp.Value?.Equals("com.test.order") == true);
        Assert.Contains(tags, kvp => kvp.Key == "subscription.name" && kvp.Value?.Equals("test-subscription") == true);
    }

    [Fact]
    public async Task Subscribe_DoesNotRecordErrorCount_WhenHandlerSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new SubscriptionMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken);

        Assert.Equal(0, fixture.ErrorCount);
    }

    [Fact]
    public async Task Subscribe_RecordsHandleDurationForEachMatchingSubscription()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new SubscriptionMetricsTestFixture(subscriptionCount: 3);
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"), cancellationToken: cancellationToken);

        Assert.Equal(3, fixture.HandleDurationValues.Count);
    }

    [Fact]
    public async Task Subscribe_NoMatchingSubscriptions_DoesNotRecordMetrics()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new SubscriptionMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.nonmatching.event"), cancellationToken: cancellationToken);

        Assert.Empty(fixture.HandleDurationValues);
        Assert.Empty(fixture.MatchedValues);
    }

    private static CloudEvent MakeEvent(string type = "com.test.event")
    {
        return new CloudEvent
        {
            Type = type,
            Source = new Uri("https://example.com"),
            Id = Guid.NewGuid().ToString("N"),
        };
    }

    private sealed class SubscriptionMetricsTestFixture : IAsyncDisposable
    {
        private readonly Meter _testMeter;
        private readonly MeterListener _listener;
        private readonly ServiceProvider _provider;
        private readonly Meter _originalMeter;

        public List<double> HandleDurationValues { get; } = new();
        public List<IReadOnlyList<KeyValuePair<string, object?>>> HandleDurationTags { get; } = new();
        public List<long> MatchedValues { get; } = new();
        public List<IReadOnlyList<KeyValuePair<string, object?>>> MatchedTags { get; } = new();
        public long ErrorCount { get; private set; }
        public List<IReadOnlyList<KeyValuePair<string, object?>>> ErrorTags { get; } = new();

        public SubscriptionMetricsTestFixture(bool throwOnHandler = false, int subscriptionCount = 1)
        {
            _originalMeter = HermodrDiagnostics.Meter;
            _testMeter = new Meter($"Hermodr.Subscription.Test.{Guid.NewGuid():N}");
            HermodrDiagnostics.Meter = _testMeter;

            _listener = new MeterListener();
            _listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name != _testMeter.Name) return;
                if (instrument.Name == TelemetryConstants.MetricSubscriptionHandleDuration)
                    l.EnableMeasurementEvents(instrument);
                else if (instrument.Name == TelemetryConstants.MetricSubscriptionMatched)
                    l.EnableMeasurementEvents(instrument);
                else if (instrument.Name == TelemetryConstants.MetricSubscriptionErrors)
                    l.EnableMeasurementEvents(instrument);
            };

            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                if (instrument.Name == TelemetryConstants.MetricSubscriptionHandleDuration)
                {
                    HandleDurationValues.Add(measurement);
                    HandleDurationTags.Add(tags.ToArray());
                }
            });

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                if (instrument.Name == TelemetryConstants.MetricSubscriptionMatched)
                {
                    MatchedValues.Add(measurement);
                    MatchedTags.Add(tags.ToArray());
                }
                else if (instrument.Name == TelemetryConstants.MetricSubscriptionErrors)
                {
                    ErrorCount += measurement;
                    ErrorTags.Add(tags.ToArray());
                }
            });

            _listener.Start();

            var sourceName = $"Hermodr.Subscription.Test.{Guid.NewGuid():N}";
            using var source = new System.Diagnostics.ActivitySource(sourceName);
            var activityListener = new System.Diagnostics.ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllData,
            };
            System.Diagnostics.ActivitySource.AddActivityListener(activityListener);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(source);
            var builder = services.AddEventPublisher(opts =>
            {
                opts.Source = new Uri("https://example.com");
                opts.ThrowOnErrors = false;
            });
            builder.UseOpenTelemetry(o =>
            {
                o.ActivitySourceName = sourceName;
                o.InstrumentPublisher = false;
                o.Metrics.MeterName = _testMeter.Name;
            });

            var subsBuilder = builder.AddSubscriptions();
            for (var i = 0; i < subscriptionCount; i++)
            {
                subsBuilder.Subscribe("com.test.*", async (evt, ct) =>
                {
                    if (throwOnHandler)
                        throw new InvalidOperationException("handler error");
                    await Task.CompletedTask;
                }, "test-subscription");
            }

            builder.AddTestChannel(_ => { });
            _provider = services.BuildServiceProvider();
        }

        public EventPublisher CreatePublisher() => _provider.GetRequiredService<EventPublisher>();

        public ValueTask DisposeAsync()
        {
            HermodrDiagnostics.Meter = _originalMeter;
            _testMeter.Dispose();
            _listener.Dispose();
            return _provider.DisposeAsync();
        }
    }
}
