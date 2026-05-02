using Deveel.Events;

namespace OrderService.Events;

/// <summary>
/// Published when an order is confirmed and ready to be shipped.
/// </summary>
[Event("order.confirmed", "1.0", Description = "An order was confirmed and is ready for fulfilment")]
public sealed class OrderConfirmed
{
    /// <summary>The unique identifier of the confirmed order.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The identifier of the customer who placed the order.</summary>
    public string CustomerId { get; set; } = default!;

    /// <summary>Total monetary amount of the confirmed order.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>When the order was confirmed (UTC).</summary>
    public DateTimeOffset ConfirmedAt { get; set; }
}

