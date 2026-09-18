using System.Text.Json;

using CloudNative.CloudEvents;

using Hermodr;

using OrderService.GrpcEvents;

namespace OrderService.Publisher.GrpcSenders;

/// <summary>
/// Bridges the Hermodr publish channel to the gRPC client generated from
/// <c>protos/order_events.proto</c>: the channel resolves the endpoint's
/// <see cref="GrpcChannel"/> and call configuration, while this sender only
/// maps the CloudEvent to the application protobuf message and invokes the RPC.
/// </summary>
public sealed class OrderEventsGrpcSender : IGrpcEventSender
{
    public async Task SendAsync(CloudEvent @event, GrpcCallContext context)
    {
        var client = new OrderEvents.OrderEventsClient(context.CallInvoker);

        await client.PublishEventAsync(
            ToMessage(@event),
            headers: context.Headers,
            deadline: context.Deadline,
            cancellationToken: context.CancellationToken);
    }

    public async Task SendBatchAsync(IReadOnlyList<CloudEvent> events, GrpcCallContext context)
    {
        var client = new OrderEvents.OrderEventsClient(context.CallInvoker);

        using var call = client.PublishEventBatch(
            headers: context.Headers,
            deadline: context.Deadline,
            cancellationToken: context.CancellationToken);

        foreach (var @event in events)
            await call.RequestStream.WriteAsync(ToMessage(@event));

        await call.RequestStream.CompleteAsync();
        await call;
    }

    private static OrderEventMessage ToMessage(CloudEvent @event) => new()
    {
        Id = @event.Id,
        Type = @event.Type ?? string.Empty,
        Source = @event.Source?.ToString() ?? string.Empty,
        DataContentType = @event.DataContentType ?? string.Empty,
        Data = @event.Data switch
        {
            null => string.Empty,
            string json => json,
            _ => JsonSerializer.Serialize(@event.Data, @event.Data.GetType()),
        },
    };
}
