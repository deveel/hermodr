using Hermodr;

namespace OrderService.Events;

/// <summary>
/// Published when a new order is created (placed) by a customer.
/// </summary>
[Event("order.created", "1.0", Description = "A new order was placed by a customer")]
[AmqpExchange("orders")]
[AmqpRoutingKey("order.created")]
public sealed class OrderCreated
{
    /// <summary>The unique identifier of the newly-created order.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The identifier of the customer who placed the order.</summary>
    public string CustomerId { get; set; } = default!;

    /// <summary>The line items included in the order.</summary>
    public IReadOnlyList<OrderCreatedItem> Items { get; set; } = [];

    /// <summary>Total monetary amount of the order.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>When the order was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A line item snapshot carried inside <see cref="OrderCreated"/>.
/// </summary>
public sealed class OrderCreatedItem
{
    public string ProductId { get; set; } = default!;
    public string ProductName { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

