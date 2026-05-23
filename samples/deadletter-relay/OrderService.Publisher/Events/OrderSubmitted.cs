using Hermodr;

namespace OrderService.Publisher.Events;

[Event("order.submitted", "1.0", Description = "A new order submission that will be stored in the dead-letter repository.")]
public record OrderSubmitted(string OrderId, string CustomerId, decimal TotalAmount);
