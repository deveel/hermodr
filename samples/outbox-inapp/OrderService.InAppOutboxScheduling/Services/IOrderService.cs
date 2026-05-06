using OrderService.Domain;

namespace OrderService.Services;

/// <summary>
/// Manages the lifecycle of <see cref="Order"/> entities.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Creates an order and publishes the <c>order.created</c> event immediately or via
    /// delayed transport delivery when requested.
    /// </summary>
    Task<Order> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<Order> ConfirmAsync(Guid orderId, CancellationToken ct = default);
    Task<Order> ShipAsync(Guid orderId, ShipOrderRequest request, CancellationToken ct = default);
    Task<Order> DeliverAsync(Guid orderId, CancellationToken ct = default);
    Task<Order> CancelAsync(Guid orderId, CancelOrderRequest request, CancellationToken ct = default);
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct = default);
}

