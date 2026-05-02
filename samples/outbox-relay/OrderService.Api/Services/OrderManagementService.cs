using Deveel.Events;

using OrderService.Domain;
using OrderService.Events;

namespace OrderService.Services;

/// <summary>
/// In-memory implementation of <see cref="IOrderService"/>.
/// Publishes domain events via <see cref="IEventPublisher"/> after each state transition.
/// Events are NOT sent directly to RabbitMQ here — the publisher routes them through
/// the <c>OutboxPublishChannel</c>, which persists them atomically to the shared SQLite database.
/// The external <c>OrderService.RelayWorker</c> process then picks them up and forwards
/// them to RabbitMQ via MassTransit.
/// </summary>
public sealed class OrderManagementService : IOrderService
{
    // Simple in-memory store — replace with a real database in production.
    private readonly Dictionary<Guid, Order> _store = new();
    private readonly IEventPublisher _publisher;
    private readonly ILogger<OrderManagementService> _logger;

    public OrderManagementService(IEventPublisher publisher, ILogger<OrderManagementService> logger)
    {
        _publisher = publisher;
        _logger    = logger;
    }

    // ── Create ────────────────────────────────────────────────────────────

    public async Task<Order> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var order = new Order
        {
            CustomerId = request.CustomerId,
            Status     = OrderStatus.Pending
        };

        foreach (var item in request.Items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId   = item.ProductId,
                ProductName = item.ProductName,
                Quantity    = item.Quantity,
                UnitPrice   = item.UnitPrice
            });
        }

        _store[order.Id] = order;
        _logger.LogInformation("Order {OrderId} created for customer {CustomerId}", order.Id, order.CustomerId);

        // Publishing saves the event to the outbox (SQLite).
        // The external relay worker will pick it up and forward it to RabbitMQ.
        await _publisher.PublishAsync(new OrderCreated
        {
            OrderId     = order.Id,
            CustomerId  = order.CustomerId,
            TotalAmount = order.TotalAmount,
            CreatedAt   = order.CreatedAt,
            Items       = order.Items.Select(i => new OrderCreatedItem
            {
                ProductId   = i.ProductId,
                ProductName = i.ProductName,
                Quantity    = i.Quantity,
                UnitPrice   = i.UnitPrice
            }).ToList()
        }, cancellationToken: ct);

        return order;
    }

    // ── Confirm ───────────────────────────────────────────────────────────

    public async Task<Order> ConfirmAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = GetOrThrow(orderId);
        EnsureStatus(order, OrderStatus.Pending);

        order.Status    = OrderStatus.Confirmed;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Order {OrderId} confirmed", order.Id);

        await _publisher.PublishAsync(new OrderConfirmed
        {
            OrderId     = order.Id,
            CustomerId  = order.CustomerId,
            TotalAmount = order.TotalAmount,
            ConfirmedAt = order.UpdatedAt!.Value
        }, cancellationToken: ct);

        return order;
    }

    // ── Ship ──────────────────────────────────────────────────────────────

    public async Task<Order> ShipAsync(Guid orderId, ShipOrderRequest request, CancellationToken ct = default)
    {
        var order = GetOrThrow(orderId);
        EnsureStatus(order, OrderStatus.Confirmed);

        order.Status    = OrderStatus.Shipped;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Order {OrderId} shipped via {Carrier} ({TrackingNumber})",
            order.Id, request.Carrier, request.TrackingNumber);

        await _publisher.PublishAsync(new OrderShipped
        {
            OrderId        = order.Id,
            CustomerId     = order.CustomerId,
            TrackingNumber = request.TrackingNumber,
            Carrier        = request.Carrier,
            ShippedAt      = order.UpdatedAt!.Value
        }, cancellationToken: ct);

        return order;
    }

    // ── Deliver ───────────────────────────────────────────────────────────

    public async Task<Order> DeliverAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = GetOrThrow(orderId);
        EnsureStatus(order, OrderStatus.Shipped);

        order.Status    = OrderStatus.Delivered;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Order {OrderId} delivered", order.Id);

        await _publisher.PublishAsync(new OrderDelivered
        {
            OrderId     = order.Id,
            CustomerId  = order.CustomerId,
            DeliveredAt = order.UpdatedAt!.Value
        }, cancellationToken: ct);

        return order;
    }

    // ── Cancel ────────────────────────────────────────────────────────────

    public async Task<Order> CancelAsync(Guid orderId, CancelOrderRequest request, CancellationToken ct = default)
    {
        var order = GetOrThrow(orderId);

        if (order.Status is OrderStatus.Delivered)
            throw new InvalidOperationException($"Order {orderId} has already been delivered and cannot be cancelled.");
        if (order.Status is OrderStatus.Cancelled)
            throw new InvalidOperationException($"Order {orderId} is already cancelled.");

        order.Status    = OrderStatus.Cancelled;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Order {OrderId} cancelled: {Reason}", order.Id, request.Reason);

        await _publisher.PublishAsync(new OrderCancelled
        {
            OrderId     = order.Id,
            CustomerId  = order.CustomerId,
            Reason      = request.Reason,
            CancelledAt = order.UpdatedAt!.Value
        }, cancellationToken: ct);

        return order;
    }

    // ── Query ─────────────────────────────────────────────────────────────

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default)
        => Task.FromResult(_store.GetValueOrDefault(orderId));

    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Order>>(_store.Values.ToList());

    // ── Helpers ───────────────────────────────────────────────────────────

    private Order GetOrThrow(Guid orderId)
        => _store.TryGetValue(orderId, out var order)
            ? order
            : throw new KeyNotFoundException($"Order {orderId} not found.");

    private static void EnsureStatus(Order order, OrderStatus expected)
    {
        if (order.Status != expected)
            throw new InvalidOperationException(
                $"Order {order.Id} is in status '{order.Status}' but '{expected}' was expected.");
    }
}

