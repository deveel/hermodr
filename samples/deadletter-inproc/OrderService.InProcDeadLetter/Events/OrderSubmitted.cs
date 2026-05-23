using Hermodr;

namespace OrderService.InProcDeadLetter.Events;

[Event("order.submitted", "1.0", Description = "A new order submission that will be replayed after a dead-letter failure.")]
public record OrderSubmitted(string OrderId, string CustomerId, decimal TotalAmount);
