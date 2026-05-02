using Deveel.Events;

namespace OrderService.Events;

/// <summary>
/// Published when an order has been delivered to the customer.
/// </summary>
[Event("order.delivered", "1.0", Description = "An order was successfully delivered to the customer")]
[AmqpExchange("orders")]
[AmqpRoutingKey("order.delivered")]
public sealed class OrderDelivered
{
    /// <summary>The unique identifier of the delivered order.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The identifier of the customer who placed the order.</summary>
    public string CustomerId { get; set; } = default!;

    /// <summary>When the order was delivered (UTC).</summary>
    public DateTimeOffset DeliveredAt { get; set; }
}

