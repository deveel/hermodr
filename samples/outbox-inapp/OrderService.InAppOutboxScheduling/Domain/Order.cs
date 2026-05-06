namespace OrderService.Domain;

/// <summary>
/// The status of an Order entity.
/// </summary>
public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}

/// <summary>
/// Represents a single line item inside an order.
/// </summary>
public sealed class OrderItem
{
    public string ProductId { get; set; } = default!;
    public string ProductName { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;
}

/// <summary>
/// The core Order aggregate root.
/// </summary>
public sealed class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CustomerId { get; set; } = default!;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public IList<OrderItem> Items { get; init; } = new List<OrderItem>();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public decimal TotalAmount => Items.Sum(i => i.TotalPrice);
}

