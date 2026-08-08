//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Hermodr;

namespace Hermodr.AsyncApi.Exporter.Fixtures;

/// <summary>
/// Event data types used by the exporter tests. Annotated with
/// <see cref="EventAttribute"/> so <see cref="EventSchemaDiscovery"/> can
/// discover them by reflecting over the compiled fixtures assembly.
/// </summary>
public enum OrderStatus { Pending, Confirmed, Cancelled }

/// <summary>
/// Raised when a customer places an order.
/// </summary>
[Event("order.placed", "1.0", Description = "Raised when a customer places an order")]
public class OrderPlacedData
{
    [EventProperty("order_id")]
    public string OrderId { get; set; } = "";

    [EventProperty("status")]
    public OrderStatus Status { get; set; }

    [EventProperty("amount")]
    public double Amount { get; set; }
}

/// <summary>
/// Raised when an order is confirmed.
/// </summary>
[Event("order.confirmed", "1.0", Description = "Raised when an order is confirmed")]
public class OrderConfirmedData
{
    [EventProperty("order_id")]
    public string OrderId { get; set; } = "";

    [EventProperty("confirmed_at")]
    public DateTimeOffset ConfirmedAt { get; set; }
}