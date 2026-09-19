//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Grpc.Core;

using Microsoft.AspNetCore.TestHost;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr;

/// <summary>
/// Integration tests that drive the gRPC publish channel against a real
/// Grpc.AspNetCore server hosted in <see cref="Microsoft.AspNetCore.TestHost.TestServer"/>:
/// the publish goes through the full pipeline (EventPublisher, channel,
/// GrpcChannel over HTTP/2, proto-generated client) and is received by an
/// actual gRPC service implementation.
/// </summary>
/// <remarks>
/// These tests do not require Docker: the server runs in-memory on the
/// standard <c>TestServer</c> transport.
/// </remarks>
[Trait("Channel", "Grpc")]
[Trait("Feature", "Grpc")]
[Trait("Category", "Integration")]
[Trait("Kind", "Integration")]
public class GrpcServerIntegrationTests : IClassFixture<GrpcServerFixture>
{
    private const string HttpClientName = "test-grpc-client";

    private readonly GrpcServerFixture _fixture;

    public GrpcServerIntegrationTests(GrpcServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PublishAsync_UnaryEvent_IsReceivedByGrpcService()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider();

        var @event = TestGrpc.MakeEvent("order.created");
        await provider.GetRequiredService<EventPublisher>()
            .PublishEventAsync(@event, cancellationToken: ct);

        var received = Assert.Single(_fixture.Service.Received);
        Assert.Equal(@event.Id, received.Id);
        Assert.Equal("order.created", received.Type);
        Assert.Equal(@event.Source!.ToString(), received.Source);
        Assert.Equal(@event.DataContentType, received.DataContentType);
    }

    [Fact]
    public async Task PublishAsync_EndpointHeaders_ArePropagatedToGrpcCall()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(options =>
            options.Endpoints[0].Headers["x-test-header"] = "grpc-integration");

        await provider.GetRequiredService<EventPublisher>()
            .PublishEventAsync(TestGrpc.MakeEvent(), cancellationToken: ct);

        Assert.Single(_fixture.Service.RequestHeaders);
        Assert.Equal(
            "grpc-integration",
            _fixture.Service.RequestHeaders[0].GetValue("x-test-header"));
    }

    [Fact]
    public async Task PublishBatchAsync_ClientStreaming_AllEventsAreReceivedInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider();

        var events = new[]
        {
            TestGrpc.MakeEvent("order.created"),
            TestGrpc.MakeEvent("order.confirmed"),
            TestGrpc.MakeEvent("order.shipped"),
        };

        await provider.GetRequiredService<IBatchEventPublishChannel>()
            .PublishBatchAsync(events, cancellationToken: ct);

        var batch = Assert.Single(_fixture.Service.ReceivedBatches);
        Assert.Equal(
            events.Select(e => e.Id).ToArray(),
            batch.Select(e => e.Id).ToArray());
        Assert.Equal(3, batch.Count);
    }

    [Fact]
    public async Task PublishAsync_ServerReturnsNonOkStatus_ThrowsGrpcTransportException()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider();
        _fixture.Service.ThrowOnPublish = new RpcException(
            new Status(StatusCode.InvalidArgument, "the event payload is invalid"));

        var channel = provider.GetRequiredKeyedService<IEventPublishChannel>("");

        var ex = await Assert.ThrowsAsync<GrpcTransportException>(() =>
            channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        Assert.Contains("the event payload is invalid", ex.Message);
    }

    [Fact]
    public async Task PublishAsync_DeadlineExceeded_ThrowsGrpcTransportException()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(options =>
            options.Endpoints[0].Deadline = TimeSpan.FromMilliseconds(200));
        _fixture.Service.Delay = TimeSpan.FromSeconds(10);

        var channel = provider.GetRequiredKeyedService<IEventPublishChannel>("");

        await Assert.ThrowsAsync<GrpcTransportException>(() =>
            channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct));
    }

    [Fact]
    public async Task PublishAsync_ServerFailure_EngagesDeadLetterHandler()
    {
        var ct = TestContext.Current.CancellationToken;
        var recorder = new DeadLetterRecorder();
        await using var provider = BuildProvider(
            configureServices: builder => builder.AddDeadLetter(
                deadLetter => deadLetter.UseHandler(context => recorder.Entries.Add(context))),
            throwOnErrors: false);
        _fixture.Service.ThrowOnPublish = new RpcException(
            new Status(StatusCode.Unavailable, "the service is down"));

        await provider.GetRequiredService<EventPublisher>()
            .PublishEventAsync(TestGrpc.MakeEvent("order.created"), cancellationToken: ct);

        var captured = Assert.Single(recorder.Entries);
        Assert.Equal("order.created", captured.Event.Type);
        Assert.Equal("GrpcPublishChannel", captured.ChannelType?.Name);
        var transport = Assert.IsType<GrpcTransportException>(captured.Exception);
        Assert.Equal(StatusCode.Unavailable, transport.StatusCode);
    }

    [Fact]
    public async Task PublishAsync_MultipleEndpoints_AllServersReceiveEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var second = new GrpcServerFixture();
        await using var provider = BuildMultiEndpointProvider(_fixture, second);

        var @event = TestGrpc.MakeEvent("order.created");
        await provider.GetRequiredService<EventPublisher>()
            .PublishEventAsync(@event, cancellationToken: ct);

        var firstReceived = Assert.Single(_fixture.Service.Received);
        var secondReceived = Assert.Single(second.Service.Received);
        Assert.Equal(@event.Id, firstReceived.Id);
        Assert.Equal(@event.Id, secondReceived.Id);
    }

    private ServiceProvider BuildProvider(
        Action<GrpcPublishOptions>? configureOptions = null,
        Action<EventPublisherBuilder>? configureServices = null,
        bool throwOnErrors = true)
    {
        _fixture.Service.Reset();

        var services = new ServiceCollection().AddLogging();
        var builder = services.AddEventPublisher(options =>
        {
            options.Source = new Uri("https://publisher.example.com/test");
            options.ThrowOnErrors = throwOnErrors;
        });
        builder.AddGrpcEventPublisherChannel(options =>
        {
            options.Endpoints =
            [
                new GrpcEndpoint
                {
                    Address = _fixture.Server.BaseAddress.ToString(),
                    HttpClientName = HttpClientName,
                },
            ];
            configureOptions?.Invoke(options);
        });
        builder.AddGrpcEventSender<OrderEventsGrpcSender>();
        configureServices?.Invoke(builder);

        services.AddHttpClient(HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _fixture.Server.CreateHandler());

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildMultiEndpointProvider(
        GrpcServerFixture first,
        GrpcServerFixture second)
    {
        first.Service.Reset();
        second.Service.Reset();

        var services = new ServiceCollection().AddLogging();
        var builder = services.AddEventPublisher(options =>
        {
            options.Source = new Uri("https://publisher.example.com/test");
            options.ThrowOnErrors = true;
        });
        builder.AddGrpcEventPublisherChannel(options =>
        {
            options.Endpoints =
            [
                new GrpcEndpoint
                {
                    Address = first.Server.BaseAddress.ToString(),
                    HttpClientName = "test-grpc-client-1",
                },
                new GrpcEndpoint
                {
                    Address = second.Server.BaseAddress.ToString(),
                    HttpClientName = "test-grpc-client-2",
                },
            ];
        });
        builder.AddGrpcEventSender<OrderEventsGrpcSender>();

        services.AddHttpClient("test-grpc-client-1")
            .ConfigurePrimaryHttpMessageHandler(() => first.Server.CreateHandler());
        services.AddHttpClient("test-grpc-client-2")
            .ConfigurePrimaryHttpMessageHandler(() => second.Server.CreateHandler());

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A dead-letter handler recorder (mirrors the DeadLetter test helpers).
    /// </summary>
    private sealed class DeadLetterRecorder
    {
        public List<DeadLetterContext> Entries { get; } = new();
    }
}
