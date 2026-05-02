namespace OrderService.Domain;

// ── Requests ───────────────────────────────────────────────────────────────

public sealed record CreateOrderRequest(
    string CustomerId,
    IReadOnlyList<CreateOrderItemRequest> Items);

public sealed record CreateOrderItemRequest(
    string ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public sealed record ShipOrderRequest(
    string TrackingNumber,
    string Carrier);

public sealed record CancelOrderRequest(
    string Reason);

// ── Responses ──────────────────────────────────────────────────────────────

public sealed record OrderResponse(
    Guid Id,
    string CustomerId,
    string Status,
    IReadOnlyList<OrderItemResponse> Items,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static OrderResponse FromOrder(Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status.ToString(),
        order.Items.Select(i => new OrderItemResponse(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.TotalPrice)).ToList(),
        order.TotalAmount,
        order.CreatedAt,
        order.UpdatedAt);
}

public sealed record OrderItemResponse(
    string ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

