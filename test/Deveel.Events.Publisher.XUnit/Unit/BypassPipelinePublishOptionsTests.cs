// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Bogus;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events;

/// <summary>
/// Unit and integration tests for <see cref="BypassPipelinePublishOptions"/> and the
/// <see cref="EventPublishOptions.BypassPipeline"/> factory method.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
[Trait("Feature", "EventPublisher")]
public class BypassPipelinePublishOptionsTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly Faker Faker = new("en");

    private static CloudEvent MakeEvent() => new()
    {
        Type   = "test.bypass",
        Source = new Uri("https://example.com"),
        Id     = Faker.Random.Guid().ToString("N"),
        Time   = DateTimeOffset.UtcNow,
    };

    // Minimal concrete options for testing inner options forwarding
    private sealed class ConcreteOptions : EventPublishOptions
    {
        public string Tag { get; set; } = string.Empty;
    }

    // ── BypassPipeline factory ────────────────────────────────────────────────

    #region BypassPipeline factory

    [Fact]
    public void Should_ReturnBypassPipelinePublishOptions_When_BypassPipelineIsCalled()
    {
        // Act
        var opts = EventPublishOptions.BypassPipeline();

        // Assert
        Assert.IsType<BypassPipelinePublishOptions>(opts);
    }

    [Fact]
    public void Should_HaveNullInnerOptions_When_BypassPipelineIsCalledWithNoArgument()
    {
        // Act
        var opts = (BypassPipelinePublishOptions)EventPublishOptions.BypassPipeline();

        // Assert
        Assert.Null(opts.InnerOptions);
    }

    [Fact]
    public void Should_HaveInnerOptions_When_BypassPipelineIsCalledWithOptions()
    {
        // Arrange
        var inner = new ConcreteOptions { Tag = "test-tag" };

        // Act
        var opts = (BypassPipelinePublishOptions)EventPublishOptions.BypassPipeline(inner);

        // Assert
        Assert.Same(inner, opts.InnerOptions);
    }

    #endregion

    // ── Unwrap ────────────────────────────────────────────────────────────────

    #region Unwrap

    [Fact]
    public void Should_ReturnNull_When_UnwrapIsCalledAndNoInnerOptions()
    {
        // Act
        var unwrapped = EventPublishOptions.BypassPipeline().Unwrap();

        // Assert
        Assert.Null(unwrapped);
    }

    [Fact]
    public void Should_ReturnInnerOptions_When_UnwrapIsCalledWithInnerOptions()
    {
        // Arrange
        var inner = new ConcreteOptions { Tag = "inner" };

        // Act
        var unwrapped = EventPublishOptions.BypassPipeline(inner).Unwrap();

        // Assert
        Assert.Same(inner, unwrapped);
    }

    [Fact]
    public void Should_RecurseUnwrap_When_InnerOptionsAreAlsoBypassPipeline()
    {
        // Arrange – double-nested bypass
        var innermost = new ConcreteOptions { Tag = "innermost" };
        var innerBypass  = EventPublishOptions.BypassPipeline(innermost);
        var outerBypass  = EventPublishOptions.BypassPipeline(innerBypass);

        // Act
        var unwrapped = outerBypass.Unwrap();

        // Assert – Unwrap recurses so the innermost options are returned
        Assert.Same(innermost, unwrapped);
    }

    [Fact]
    public void Should_ReturnSelf_When_BaseEventPublishOptionsUnwrapIsCalled()
    {
        // Arrange
        var opts = new ConcreteOptions { Tag = "base" };

        // Act
        var unwrapped = opts.Unwrap();

        // Assert – base implementation returns `this`
        Assert.Same(opts, unwrapped);
    }

    #endregion

    // ── Middleware bypass integration ─────────────────────────────────────────

    #region Middleware bypass integration

    /// <summary>
    /// A spy middleware that records how many times it was invoked.
    /// </summary>
    private sealed class CountingMiddleware : IEventMiddleware
    {
        private int _count;
        public int Count => _count;

        public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
        {
            Interlocked.Increment(ref _count);
            await next(context);
        }
    }

    [Fact]
    public async Task Should_NotInvokeMiddleware_When_BypassPipelineOptionsIsUsed()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var received  = new List<CloudEvent>();
        var middleware = new CountingMiddleware();

        var services = new ServiceCollection();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddTestChannel(e => received.Add(e))
                .Use<CountingMiddleware>();
        services.AddSingleton(middleware);

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        // Act – publish with bypass options
        await publisher.PublishEventAsync(
            MakeEvent(),
            EventPublishOptions.BypassPipeline(),
            cancellationToken);

        // Assert – event was delivered but the middleware was never called
        Assert.Single(received);
        Assert.Equal(0, middleware.Count);
    }

    [Fact]
    public async Task Should_InvokeMiddleware_When_NormalPublishIsUsed()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var received   = new List<CloudEvent>();
        var middleware = new CountingMiddleware();

        var services = new ServiceCollection();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddTestChannel(e => received.Add(e))
                .Use<CountingMiddleware>();
        services.AddSingleton(middleware);

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        // Act – publish without bypass options
        await publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken);

        // Assert – middleware WAS called
        Assert.Single(received);
        Assert.Equal(1, middleware.Count);
    }

    [Fact]
    public async Task Should_DeliverToChannel_When_BypassPipelineOptionsIsUsed()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var received = new List<CloudEvent>();

        var services = new ServiceCollection();
        services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddTestChannel(e => received.Add(e));

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<EventPublisher>();

        // Act
        await publisher.PublishEventAsync(
            MakeEvent(),
            EventPublishOptions.BypassPipeline(),
            cancellationToken);

        // Assert – delivery still happened
        Assert.Single(received);
    }

    #endregion

    // ── EventPublishOptions.Combine ───────────────────────────────────────────

    #region EventPublishOptions.Combine

    private sealed class AlphaOptions : EventPublishOptions
    {
        public string Alpha { get; set; } = string.Empty;
    }

    private sealed class BetaOptions : EventPublishOptions
    {
        public string Beta { get; set; } = string.Empty;
    }

    [Fact]
    public void Should_ReturnCombinedPublishOptions_When_CombineIsCalled()
    {
        // Act
        var combined = EventPublishOptions.Combine(
            new AlphaOptions { Alpha = "a" },
            new BetaOptions  { Beta  = "b" });

        // Assert
        Assert.IsType<CombinedPublishOptions>(combined);
    }

    [Fact]
    public void Should_ContainAllEntries_When_CombineIsCalledWithMultipleOptions()
    {
        // Arrange
        var alpha = new AlphaOptions { Alpha = "a" };
        var beta  = new BetaOptions  { Beta  = "b" };

        // Act
        var combined = (CombinedPublishOptions)EventPublishOptions.Combine(alpha, beta);

        // Assert
        Assert.Equal(2, combined.Options.Count);
        Assert.Contains(alpha, combined.Options);
        Assert.Contains(beta,  combined.Options);
    }

    [Fact]
    public void Should_ReturnNull_When_GetOptionsIsCalledWithMissingType()
    {
        // Arrange
        var combined = (CombinedPublishOptions)EventPublishOptions.Combine(
            new AlphaOptions { Alpha = "a" });

        // Act
        var result = combined.GetOptions<BetaOptions>();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_GetOptionsIsCalledWithNullType()
    {
        // Arrange
        var combined = (CombinedPublishOptions)EventPublishOptions.Combine(
            new AlphaOptions { Alpha = "a" });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => combined.GetOptions(null!));
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_CombinedPublishOptionsConstructedWithNullEnumerable()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CombinedPublishOptions((IEnumerable<EventPublishOptions>)null!));
    }

    [Fact]
    public void Should_ReturnMatchingEntry_When_GetOptionsIsCalledWithType()
    {
        // Arrange
        var alpha    = new AlphaOptions { Alpha = "a" };
        var combined = (CombinedPublishOptions)EventPublishOptions.Combine(alpha, new BetaOptions());

        // Act
        var result = combined.GetOptions(typeof(AlphaOptions));

        // Assert
        Assert.Same(alpha, result);
    }

    #endregion
}
