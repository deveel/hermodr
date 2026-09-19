//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Hermodr.TestServices;

namespace Hermodr;

/// <summary>
/// An <see cref="IGrpcEventSender"/> backed by the <c>order_events.proto</c>
/// generated client: it maps the CloudEvent to the application-defined
/// protobuf message and forwards the <see cref="GrpcCallContext"/> values
/// (headers, deadline, cancellation token) to the RPC call — the same pattern
/// a production sender would follow.
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
        Data = @event.Data?.ToString() ?? string.Empty,
    };
}
