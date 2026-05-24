using Hermodr;

namespace OrderService.OpenTelemetry.Events;

[Event("order.submitted", "1.0", Description = "A new order has been submitted.")]
public record OrderSubmitted(string OrderId, string CustomerId, decimal TotalAmount);
