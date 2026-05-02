using Microsoft.AspNetCore.Mvc;

using OrderService.Domain;
using OrderService.Services;

namespace OrderService.Endpoints;

/// <summary>
/// Maps all order-related routes onto a <see cref="WebApplication"/>.
/// </summary>
public static class OrderEndpoints
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        var orders = app.MapGroup("/orders")
                        .WithTags("Orders");

        // GET /orders
        orders.MapGet("/", ListOrders)
              .WithName("ListOrders")
              .WithSummary("List all orders");

        // GET /orders/{id}
        orders.MapGet("/{id:guid}", GetOrder)
              .WithName("GetOrder")
              .WithSummary("Get a single order by ID");

        // POST /orders
        orders.MapPost("/", CreateOrder)
              .WithName("CreateOrder")
              .WithSummary("Create a new order (writes order.created to outbox → relay worker forwards to RabbitMQ via MassTransit)");

        // PUT /orders/{id}/confirm
        orders.MapPut("/{id:guid}/confirm", ConfirmOrder)
              .WithName("ConfirmOrder")
              .WithSummary("Confirm an order (writes order.confirmed to outbox)");

        // PUT /orders/{id}/ship
        orders.MapPut("/{id:guid}/ship", ShipOrder)
              .WithName("ShipOrder")
              .WithSummary("Mark an order as shipped (writes order.shipped to outbox)");

        // PUT /orders/{id}/deliver
        orders.MapPut("/{id:guid}/deliver", DeliverOrder)
              .WithName("DeliverOrder")
              .WithSummary("Mark an order as delivered (writes order.delivered to outbox)");

        // PUT /orders/{id}/cancel
        orders.MapPut("/{id:guid}/cancel", CancelOrder)
              .WithName("CancelOrder")
              .WithSummary("Cancel an order (writes order.cancelled to outbox)");

        return app;
    }

    // ── Handlers ──────────────────────────────────────────────────────────

    private static async Task<IResult> ListOrders(
        IOrderService svc,
        CancellationToken ct)
    {
        var orders = await svc.ListAsync(ct);
        return Results.Ok(orders.Select(OrderResponse.FromOrder));
    }

    private static async Task<IResult> GetOrder(
        Guid id,
        IOrderService svc,
        CancellationToken ct)
    {
        var order = await svc.GetByIdAsync(id, ct);
        return order is null
            ? Results.NotFound(new { Message = $"Order {id} not found." })
            : Results.Ok(OrderResponse.FromOrder(order));
    }

    private static async Task<IResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        IOrderService svc,
        CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Items"] = ["At least one item is required."]
            });

        var order = await svc.CreateAsync(request, ct);
        return Results.CreatedAtRoute("GetOrder", new { id = order.Id }, OrderResponse.FromOrder(order));
    }

    private static async Task<IResult> ConfirmOrder(
        Guid id,
        IOrderService svc,
        CancellationToken ct)
    {
        try
        {
            var order = await svc.ConfirmAsync(id, ct);
            return Results.Ok(OrderResponse.FromOrder(order));
        }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { ex.Message }); }
    }

    private static async Task<IResult> ShipOrder(
        Guid id,
        [FromBody] ShipOrderRequest request,
        IOrderService svc,
        CancellationToken ct)
    {
        try
        {
            var order = await svc.ShipAsync(id, request, ct);
            return Results.Ok(OrderResponse.FromOrder(order));
        }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { ex.Message }); }
    }

    private static async Task<IResult> DeliverOrder(
        Guid id,
        IOrderService svc,
        CancellationToken ct)
    {
        try
        {
            var order = await svc.DeliverAsync(id, ct);
            return Results.Ok(OrderResponse.FromOrder(order));
        }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { ex.Message }); }
    }

    private static async Task<IResult> CancelOrder(
        Guid id,
        [FromBody] CancelOrderRequest request,
        IOrderService svc,
        CancellationToken ct)
    {
        try
        {
            var order = await svc.CancelAsync(id, request, ct);
            return Results.Ok(OrderResponse.FromOrder(order));
        }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { ex.Message }); }
    }
}

