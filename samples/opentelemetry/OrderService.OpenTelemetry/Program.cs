using System.Diagnostics;
using CloudNative.CloudEvents;
using Hermodr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderService.OpenTelemetry.Events;

var builder = Host.CreateApplicationBuilder(args);

var source = new Uri(builder.Configuration["Sample:Source"] ?? "https://samples.deveel.events/opentelemetry");
var dataSchemaBaseUri = new Uri(builder.Configuration["Sample:DataSchemaBaseUri"] ?? "https://samples.deveel.events/schema/");

builder.Services
    .AddEventPublisher(options =>
    {
        options.Source = source;
        options.DataSchemaBaseUri = dataSchemaBaseUri;
        options.ThrowOnErrors = true;
    })
    .UseOpenTelemetry(opts =>
    {
        opts.ActivitySourceName = "Hermodr.Sample";
        opts.RecordException = true;
        opts.EnrichWithEvent = (activity, cloudEvent) =>
        {
            if (cloudEvent.Data is OrderSubmitted order)
            {
                activity.SetTag("order.customer_id", order.CustomerId);
                activity.SetTag("order.total_amount", order.TotalAmount);
            }
        };
    })
    .AddChannel<LoggingChannel>(channelName: "default");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "OrderService.OpenTelemetry",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing.AddSource("Hermodr.Sample")
            .AddSource("OrderService.Sample")
            .AddConsoleExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Hermodr");
    });

using var host = builder.Build();
await host.StartAsync();

var publisher = host.Services.GetRequiredService<IEventPublisher>();

using var sampleActivitySource = new ActivitySource("OrderService.Sample");

Console.WriteLine("=== Hermodr OpenTelemetry Sample ===");
Console.WriteLine();

using (var parentActivity = sampleActivitySource.StartActivity("ProcessOrderWorkflow", ActivityKind.Server))
{
    Console.WriteLine($"Parent activity started: {parentActivity?.TraceId}");
    Console.WriteLine();

    var orderEvent = new OrderSubmitted(
        OrderId: Guid.NewGuid().ToString("N"),
        CustomerId: "customer-42",
        TotalAmount: 149.90m);

    Console.WriteLine($"Publishing '{orderEvent.OrderId}' with active trace context...");
    await publisher.PublishAsync(orderEvent, "default");

    Console.WriteLine();
    Console.WriteLine("Publish completed. Check the OpenTelemetry console exporter output above for:");
    Console.WriteLine("  - Producer span: hermodr.publish.order.submitted");
    Console.WriteLine("  - Trace context injected as CloudEvent extensions (traceparent/tracestate)");
    Console.WriteLine("  - Custom tags: order.customer_id, order.total_amount");
}

Console.WriteLine();
Console.WriteLine("=== Sample completed ===");

await host.StopAsync();

internal sealed class LoggingChannel : IEventPublishChannel
{
    public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Channel received event: {@event.Type}");
        Console.WriteLine($"  CloudEvent ID: {@event.Id}");

        var traceparentAttr = CloudEventAttribute.CreateExtension("traceparent", CloudEventAttributeType.String);
        if (@event[traceparentAttr] is string traceparent)
        {
            Console.WriteLine($"  traceparent extension: {traceparent}");
        }

        var tracestateAttr = CloudEventAttribute.CreateExtension("tracestate", CloudEventAttributeType.String);
        if (@event[tracestateAttr] is string tracestate)
        {
            Console.WriteLine($"  tracestate extension: {tracestate}");
        }

        if (@event.Data is OrderSubmitted order)
        {
            Console.WriteLine($"  Order: {order.OrderId}, Customer: {order.CustomerId}, Total: {order.TotalAmount:C}");
        }

        return Task.CompletedTask;
    }
}
