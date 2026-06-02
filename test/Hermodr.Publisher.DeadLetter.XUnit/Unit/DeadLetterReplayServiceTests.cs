//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
[Trait("Feature", "DeadLetterReplay")]
public class DeadLetterReplayServiceTests
{
    private static CloudEvent MakeEvent(string type = "test.event") => new()
    {
        Type = type,
        Source = new Uri("https://example.com"),
        Id = Guid.NewGuid().ToString("N"),
    };

    [Fact]
    public async Task WithReplayWorker_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                .AddDeadLetter()
                .WithReplayWorker();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        var hosted = sp.GetServices<IHostedService>().ToList();

        Assert.Contains(hosted, h => h.GetType().Name == "DeadLetterReplayService");
    }

    [Fact]
    public async Task WithReplayWorker_ProcessorInvokedAtLeastOnce_When_StoreHasMessages()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services2 = new ServiceCollection();
        services2.AddLogging();
        services2.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                 .AddDeadLetter()
                 .WithReplayWorker(o => o.Interval = TimeSpan.FromMilliseconds(50));

        var sp = services2.BuildServiceProvider();

        var store = sp.GetRequiredService<IDeadLetterMessageStore>();
        await store.AddAsync(new DeadLetterMessage
        {
            Event = MakeEvent("first"),
            PublisherName = "test-publisher",
            Status = DeadLetterMessageStatus.Pending,
        }, cancellationToken);
        await store.AddAsync(new DeadLetterMessage
        {
            Event = MakeEvent("second"),
            PublisherName = "test-publisher",
            Status = DeadLetterMessageStatus.Pending,
        }, cancellationToken);

        var hosted = sp.GetServices<IHostedService>().First(h => h.GetType().Name == "DeadLetterReplayService");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await hosted.StartAsync(cts.Token);
        try
        {
            await Task.Delay(400, cts.Token);
        }
        catch (OperationCanceledException) { }
        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WithReplayWorker_StopAsyncCompletesCleanly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                .AddDeadLetter()
                .WithReplayWorker(o => o.Interval = TimeSpan.FromMilliseconds(20));

        var sp = services.BuildServiceProvider();
        var hosted = sp.GetServices<IHostedService>().First(h => h.GetType().Name == "DeadLetterReplayService");

        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void WithReplay_DoesNotRegisterHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                .AddDeadLetter()
                .WithReplay();

        var sp = services.BuildServiceProvider();
        var hosted = sp.GetServices<IHostedService>().ToList();

        Assert.DoesNotContain(hosted, h => h.GetType().Name == "DeadLetterReplayService");
    }

    [Fact]
    public void WithReplayWorker_RegistersProcessor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                .AddDeadLetter()
                .WithReplayWorker();

        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<IDeadLetterReplayProcessor>());
    }

    [Fact]
    public void UseHandler_WithUnsupportedLifetime_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                              .AddDeadLetter();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => builder.UseHandler<FakeHandler>((ServiceLifetime)999));
    }

    [Fact]
    public void UseHandler_WithSingletonLifetime_Registers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                .AddDeadLetter()
                .UseHandler<FakeHandler>(ServiceLifetime.Singleton);

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEventPublishErrorHandler));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void UseHandler_WithScopedLifetime_Registers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                .AddDeadLetter()
                .UseHandler<FakeHandler>(ServiceLifetime.Scoped);

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IEventPublishErrorHandler));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void UseHandler_WithTransientLifetime_Registers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                .AddDeadLetter()
                .UseHandler<FakeHandler>(ServiceLifetime.Transient);

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IEventPublishErrorHandler));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void UseHandler_WithInstance_Registers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var handler = new FakeHandler();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                .AddDeadLetter()
                .UseHandler(handler);

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IEventPublishErrorHandler));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void UseHandler_WithNullInstance_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                              .AddDeadLetter();

        Assert.Throws<ArgumentNullException>(() => builder.UseHandler((IEventDeadLetterHandler)null!));
    }

    [Fact]
    public void UseHandler_WithNullCallback_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                              .AddDeadLetter();

        Assert.Throws<ArgumentNullException>(() => builder.UseHandler((Func<DeadLetterContext, Task>)null!));
    }

    [Fact]
    public void UseHandler_WithSyncCallback_Registers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                .AddDeadLetter()
                .UseHandler(ctx => { /* no-op */ });

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IEventPublishErrorHandler));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void UseHandler_WithAsyncCallback_Registers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com/publisher"))
                .AddDeadLetter()
                .UseHandler(ctx => Task.CompletedTask);

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IEventPublishErrorHandler));
        Assert.NotNull(descriptor);
    }

    private sealed class FailingChannel(List<CloudEvent> received) : IEventPublishChannel
    {
        private readonly List<CloudEvent> _received = received;
        private int _attempts;

        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
        {
            var attempt = Interlocked.Increment(ref _attempts);
            if (attempt == 1)
                throw new InvalidOperationException("first failure");
            _received.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHandler : IEventDeadLetterHandler
    {
        public Task HandleAsync(DeadLetterContext context)
            => Task.CompletedTask;
    }
}

