using Hermodr;

namespace OrderService.Publisher.Events;

[Event("order.created", "1.0", Description = "A new order has been created.")]
public record OrderCreated(string OrderId, string CustomerId, decimal TotalAmount);

[Event("order.confirmed", "1.0", Description = "An order has been confirmed.")]
public record OrderConfirmed(string OrderId, string CustomerId);

[Event("order.shipped", "1.0", Description = "An order has been shipped.")]
public record OrderShipped(string OrderId, string TrackingCode);
