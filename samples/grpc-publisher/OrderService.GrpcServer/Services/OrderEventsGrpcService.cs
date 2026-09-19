using Grpc.Core;

using OrderService.GrpcEvents;

namespace OrderService.GrpcServer.Services;

/// <summary>
/// Receives the events published by the sample publisher: single events arrive
/// through the unary <c>PublishEvent</c> RPC, batches through the
/// client-streaming <c>PublishEventBatch</c> RPC.
/// </summary>
public sealed class OrderEventsGrpcService(ILogger<OrderEventsGrpcService> logger)
    : OrderEvents.OrderEventsBase
{
    public override Task<PublishReceipt> PublishEvent(
        OrderEventMessage request,
        ServerCallContext context)
    {
        logger.LogInformation(
            "Unary RPC received event '{Type}' [{Id}] from '{Source}' (headers: {Headers}): {Data}",
            request.Type,
            request.Id,
            request.Source,
            string.Join(", ", context.RequestHeaders.Select(h => $"{h.Key}={h.Value}")),
            request.Data);

        return Task.FromResult(new PublishReceipt { Id = request.Id });
    }

    public override async Task<BatchPublishReceipt> PublishEventBatch(
        IAsyncStreamReader<OrderEventMessage> requestStream,
        ServerCallContext context)
    {
        var count = 0;
        while (await requestStream.MoveNext(context.CancellationToken))
        {
            count++;
            logger.LogInformation(
                "Client-streaming RPC received event '{Type}' [{Id}]: {Data}",
                requestStream.Current.Type,
                requestStream.Current.Id,
                requestStream.Current.Data);
        }

        logger.LogInformation(
            "Client-streaming RPC completed: {Count} event(s) received",
            count);

        return new BatchPublishReceipt { Count = count };
    }
}
