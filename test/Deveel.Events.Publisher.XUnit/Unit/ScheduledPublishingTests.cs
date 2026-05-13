// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
namespace Deveel.Events;

[Trait("Category", "Unit")]
[Trait("Feature", "Scheduling")]
public static class ScheduledPublishingTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 01, 15, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public static async Task NonSchedulingChannel_IsNotDelayedByPublisher()
    {
        var now = FixedNow;
        MutableSystemTime.UtcNowValue = now;

        var services = new ServiceCollection();
        services.AddEventPublisher().UseSystemTime<MutableSystemTime>().AddChannel<NonSchedulingTestChannel>();

        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();
        var channel = provider.GetRequiredService<NonSchedulingTestChannel>();

        var @event = new CloudEvent
        {
            Type = "test.event",
            Source = new Uri("https://example.com"),
            Id = Guid.NewGuid().ToString("N"),
        };

        var scheduledDelay = TimeSpan.FromMilliseconds(120);
        var options = new TestPublishOptions
        {
            ScheduleDeliveryAt = now + scheduledDelay,
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await publisher.PublishEventAsync(@event, options, TestContext.Current.CancellationToken);
        sw.Stop();

        Assert.Equal(1, channel.PublishCount);
        // Must complete well before the scheduled delay (proves no waiting)
        Assert.True(sw.Elapsed < scheduledDelay - TimeSpan.FromMilliseconds(20),
            $"Publisher took {sw.Elapsed.TotalMilliseconds}ms (expected < {scheduledDelay.TotalMilliseconds - 20}ms)");
    }

    [Fact]
    public static async Task SchedulingCapableChannel_ReceivesSchedulingOptions()
    {
        var now = FixedNow;
        MutableSystemTime.UtcNowValue = now;

        var services = new ServiceCollection();
        services.AddEventPublisher().UseSystemTime<MutableSystemTime>().AddChannel<NativeSchedulingTestChannel>();

        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();
        var channel = provider.GetRequiredService<NativeSchedulingTestChannel>();

        var @event = new CloudEvent
        {
            Type = "test.event",
            Source = new Uri("https://example.com"),
            Id = Guid.NewGuid().ToString("N"),
        };

        var scheduledAt = now.AddSeconds(10);
        var options = new TestPublishOptions
        {
            ScheduleDeliveryAt = scheduledAt,
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await publisher.PublishEventAsync(@event, options, TestContext.Current.CancellationToken);
        sw.Stop();

        Assert.Equal(1, channel.PublishCount);
        Assert.Equal(scheduledAt, channel.LastScheduleDeliveryAt);
        // 10s scheduled delay; any reasonable in-memory publish is << 1s
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1),
            $"Publisher took {sw.Elapsed.TotalMilliseconds}ms (expected < 1000ms)");
    }

    private sealed class TestPublishOptions : EventPublishOptions
    {
    }

    private sealed class NonSchedulingTestChannel : EventPublishChannel<TestPublishOptions>
    {
        public NonSchedulingTestChannel()
            : base(new TestPublishOptions())
        {
        }

        public int PublishCount { get; private set; }

        protected override Task PublishCoreAsync(CloudEvent @event, TestPublishOptions options, CancellationToken cancellationToken)
        {
            PublishCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NativeSchedulingTestChannel : EventPublishChannel<TestPublishOptions>, IScheduledEventPublishChannel
    {
        public NativeSchedulingTestChannel()
            : base(new TestPublishOptions())
        {
        }

        public int PublishCount { get; private set; }

        public DateTimeOffset? LastScheduleDeliveryAt { get; private set; }

        protected override Task PublishCoreAsync(CloudEvent @event, TestPublishOptions options, CancellationToken cancellationToken)
        {
            PublishCount++;
            LastScheduleDeliveryAt = options.ScheduleDeliveryAt;
            return Task.CompletedTask;
        }
    }

    private sealed class MutableSystemTime : IEventSystemTime
    {
        public static DateTimeOffset UtcNowValue { get; set; }

        public DateTimeOffset UtcNow => UtcNowValue;
    }
}


