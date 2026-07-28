using Hermodr;

namespace asyncapi_export.Events;

public enum OrderStatus { Pending, Confirmed, Cancelled }

[Event("order.placed", "1.0", Description = "Raised when a customer places an order")]
public class OrderPlacedData {
    [EventProperty("order_id")]
    public string OrderId { get; set; } = "";

    [EventProperty("status")]
    public OrderStatus Status { get; set; }

    [EventProperty("amount")]
    public double Amount { get; set; }
}

[Event("order.confirmed", "1.0", Description = "Raised when an order is confirmed")]
public class OrderConfirmedData {
    [EventProperty("order_id")]
    public string OrderId { get; set; } = "";

    [EventProperty("confirmed_at")]
    public DateTimeOffset ConfirmedAt { get; set; }
}