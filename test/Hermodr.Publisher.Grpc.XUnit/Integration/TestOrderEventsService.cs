//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Grpc.Core;

using Hermodr.TestServices;

namespace Hermodr;

/// <summary>
/// An in-memory gRPC service implementation used by the integration tests: it
/// records every received event (and its request headers) and can be
/// configured to fail or delay responses.
/// </summary>
public sealed class TestOrderEventsService : OrderEvents.OrderEventsBase
{
    private readonly object _lock = new();

    /// <summary>Gets every event received through the unary RPC, in order.</summary>
    public List<OrderEventMessage> Received { get; } = new();

    /// <summary>Gets every batch received through the client-streaming RPC, in order.</summary>
    public List<IReadOnlyList<OrderEventMessage>> ReceivedBatches { get; } = new();

    /// <summary>Gets the request headers of every RPC received.</summary>
    public List<Metadata> RequestHeaders { get; } = new();

    /// <summary>
    /// Gets or sets an exception to throw on the next RPC. A
    /// <see cref="RpcException"/> surfaces its gRPC status code to the caller.
    /// </summary>
    public Exception? ThrowOnPublish { get; set; }

    /// <summary>Gets or sets a delay applied before responding to the unary RPC.</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>Clears all recorded state and resets the failure/delay behavior.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            Received.Clear();
            ReceivedBatches.Clear();
            RequestHeaders.Clear();
        }

        ThrowOnPublish = null;
        Delay = TimeSpan.Zero;
    }

    public override async Task<PublishReceipt> PublishEvent(
        OrderEventMessage request,
        ServerCallContext context)
    {
        lock (_lock)
        {
            Received.Add(request);
            RequestHeaders.Add(context.RequestHeaders);
        }

        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, context.CancellationToken);

        if (ThrowOnPublish is not null)
            throw ThrowOnPublish;

        return new PublishReceipt { Id = request.Id };
    }

    public override async Task<BatchPublishReceipt> PublishEventBatch(
        IAsyncStreamReader<OrderEventMessage> requestStream,
        ServerCallContext context)
    {
        var batch = new List<OrderEventMessage>();
        while (await requestStream.MoveNext(context.CancellationToken))
            batch.Add(requestStream.Current);

        lock (_lock)
        {
            ReceivedBatches.Add(batch);
            RequestHeaders.Add(context.RequestHeaders);
        }

        if (ThrowOnPublish is not null)
            throw ThrowOnPublish;

        return new BatchPublishReceipt { Count = batch.Count };
    }
}
