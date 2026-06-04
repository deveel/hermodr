using Hermodr;

namespace AuditTrail.Sample.Events;

/// <summary>
/// An event raised when an order is submitted.
/// </summary>
[Event("order.submitted", "1.0")]
public record OrderSubmitted
{
    [EventProperty("orderId")]
    public string OrderId { get; init; } = null!;

    [EventProperty("customerId")]
    public string CustomerId { get; init; } = null!;

    [EventProperty("totalAmount")]
    public decimal TotalAmount { get; init; }
}

/// <summary>
/// An event raised when an order is confirmed.
/// </summary>
[Event("order.confirmed", "1.0")]
public record OrderConfirmed
{
    [EventProperty("orderId")]
    public string OrderId { get; init; } = null!;

    [EventProperty("confirmedAt")]
    public DateTimeOffset ConfirmedAt { get; init; }
}

/// <summary>
/// An event raised when an order is shipped.
/// </summary>
[Event("order.shipped", "1.0")]
public record OrderShipped
{
    [EventProperty("orderId")]
    public string OrderId { get; init; } = null!;

    [EventProperty("trackingNumber")]
    public string TrackingNumber { get; init; } = null!;
}

/// <summary>
/// An event raised when a payment is processed.
/// </summary>
[Event("payment.processed", "1.0")]
public record PaymentProcessed
{
    [EventProperty("paymentId")]
    public string PaymentId { get; init; } = null!;

    [EventProperty("orderId")]
    public string OrderId { get; init; } = null!;

    [EventProperty("amount")]
    public decimal Amount { get; init; }
}
