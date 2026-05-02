using Deveel.Events;

namespace OrderService.RelayWorker.Events;

/// <summary>
/// Published when a new order is created (placed) by a customer.
/// Must match the event type registered by OrderService.Api exactly.
/// </summary>
[Event("order.created", "1.0", Description = "A new order was placed by a customer")]
public sealed class OrderCreated { }

/// <summary>
/// Published when an order is confirmed and ready to be shipped.
/// </summary>
[Event("order.confirmed", "1.0", Description = "An order was confirmed and is ready for fulfilment")]
public sealed class OrderConfirmed { }

/// <summary>
/// Published when an order has been dispatched to the carrier.
/// </summary>
[Event("order.shipped", "1.0", Description = "An order was dispatched to the delivery carrier")]
public sealed class OrderShipped { }

/// <summary>
/// Published when an order has been delivered to the customer.
/// </summary>
[Event("order.delivered", "1.0", Description = "An order was successfully delivered to the customer")]
public sealed class OrderDelivered { }

/// <summary>
/// Published when an order is cancelled before it has been shipped.
/// </summary>
[Event("order.cancelled", "1.0", Description = "An order was cancelled before shipment")]
public sealed class OrderCancelled { }

