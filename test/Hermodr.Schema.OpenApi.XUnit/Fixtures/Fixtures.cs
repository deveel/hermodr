//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;

namespace Hermodr {
    /// <summary>
    /// Shared fixtures for the OpenAPI test project. Mirrors the AsyncApi
    /// test fixtures so the two suites remain independent.
    /// </summary>
    public enum OrderStatus { Pending, Confirmed, Cancelled }

    [Event("person.created", "1.0")]
    public class OpenApiPersonCreatedData {
        [EventProperty("first_name")] [Required]
        public string FirstName { get; set; } = "";

        [EventProperty("last_name")] [Required]
        public string LastName { get; set; } = "";

        [EventProperty("age")]
        [Range(0, 150)]
        public int Age { get; set; }

        [EventProperty("email")]
        public string? Email { get; set; }
    }

    [Event("order.placed", "2.3")]
    public class OpenApiOrderPlacedData {
        [EventProperty("order_id")] [Required]
        public string OrderId { get; set; } = "";

        [EventProperty("status")]
        public OrderStatus Status { get; set; }

        [EventProperty("amount")]
        [Range(0.0, 9999999.0)]
        public double Amount { get; set; }

        [EventProperty("tags")]
        public string[] Tags { get; set; } = new string[0];
    }

    internal static class OpenApiTestSchemas {
        public static EventSchema SimpleSchema() =>
            EventSchema.Build("user.registered")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .WithDescription("User registration event")
                .AddProperty("user_id", p => p.OfType("guid").Required())
                .AddProperty("email",   p => p.OfType("string").Required())
                .AddProperty("age",     p => p.OfType("int").WithRange<int>(18, 120))
                .AddProperty("nickname", p => p.OfType("string").Nullable())
                .Build();

        public static EventSchema NestedSchema() =>
            EventSchema.Build("order.shipped")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .AddProperty("order_id", p => p.OfType("guid").Required())
                .AddProperty("address", p => p
                    .OfType("object")
                    .AddProperty("street", b => b.OfType("string").Required())
                    .AddProperty("city",   b => b.OfType("string").Required())
                    .AddProperty("zip",    b => b.OfType("string")))
                .Build();
    }
}