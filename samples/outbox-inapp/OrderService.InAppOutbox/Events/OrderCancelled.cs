using Hermodr;

namespace OrderService.Events;

/// <summary>
/// Published when an order is cancelled before it has been shipped.
/// </summary>
[Event("order.cancelled", "1.0", Description = "An order was cancelled before shipment")]
[AmqpExchange("orders")]
[AmqpRoutingKey("order.cancelled")]
public sealed class OrderCancelled
{
    /// <summary>The unique identifier of the cancelled order.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The identifier of the customer who placed the order.</summary>
    public string CustomerId { get; set; } = default!;

    /// <summary>The human-readable reason explaining why the order was cancelled.</summary>
    public string Reason { get; set; } = default!;

    /// <summary>When the order was cancelled (UTC).</summary>
    public DateTimeOffset CancelledAt { get; set; }
}

