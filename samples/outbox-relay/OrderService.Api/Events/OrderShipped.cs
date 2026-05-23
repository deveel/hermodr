using Hermodr;

namespace OrderService.Events;

/// <summary>
/// Published when an order has been dispatched to the carrier.
/// </summary>
[Event("order.shipped", "1.0", Description = "An order was dispatched to the delivery carrier")]
public sealed class OrderShipped
{
    /// <summary>The unique identifier of the shipped order.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The identifier of the customer who placed the order.</summary>
    public string CustomerId { get; set; } = default!;

    /// <summary>The tracking number assigned by the carrier.</summary>
    public string TrackingNumber { get; set; } = default!;

    /// <summary>The name of the carrier (e.g. "FedEx", "UPS").</summary>
    public string Carrier { get; set; } = default!;

    /// <summary>When the order was shipped (UTC).</summary>
    public DateTimeOffset ShippedAt { get; set; }
}

