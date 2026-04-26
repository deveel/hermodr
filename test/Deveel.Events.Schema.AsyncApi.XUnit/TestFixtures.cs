//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;

namespace Deveel.Events {
    // ──────────────────────────────────────────────────────────────────────────
    // Shared test fixtures used across all test classes in this assembly.
    // ──────────────────────────────────────────────────────────────────────────

    public enum OrderStatus { Pending, Confirmed, Cancelled }

    [Event("person.created", "1.0")]
    public class PersonCreatedData {
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
    public class OrderPlacedData {
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

    /// <summary>
    /// Central factory for schemas used by multiple test classes.
    /// </summary>
    internal static class TestSchemas {
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

        public static EventSchema AllDataTypesSchema() =>
            EventSchema.Build("all.types")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .AddProperty("f_string",         p => p.OfType("string"))
                .AddProperty("f_int",            p => p.OfType("int"))
                .AddProperty("f_long",           p => p.OfType("long"))
                .AddProperty("f_float",          p => p.OfType("float"))
                .AddProperty("f_double",         p => p.OfType("double"))
                .AddProperty("f_money",          p => p.OfType("money"))
                .AddProperty("f_boolean",        p => p.OfType("boolean"))
                .AddProperty("f_dateTime",       p => p.OfType("dateTime"))
                .AddProperty("f_dateTimeOffset", p => p.OfType("dateTimeOffset"))
                .AddProperty("f_date",           p => p.OfType("date"))
                .AddProperty("f_time",           p => p.OfType("time"))
                .AddProperty("f_duration",       p => p.OfType("duration"))
                .AddProperty("f_guid",           p => p.OfType("guid"))
                .AddProperty("f_array",          p => p.OfType("string[]"))
                .AddProperty("f_unknown",        p => p.OfType("System.Uri"))
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

        public static EventSchema EnumConstraintSchema() =>
            EventSchema.Build("order.status.changed")
                .WithVersion("1.0")
                .WithContentType("application/json")
                .AddProperty("status", p =>
                    p.OfType("string")
                     .WithAllowedValues<string>(new []{"Pending", "Confirmed", "Cancelled"}))
                .Build();

        public static EventSchema WithDescriptionSchema() =>
            EventSchema.Build("thing.happened")
                .WithVersion("3.5")
                .WithDescription("Something happened")
                .WithContentType("text/plain")
                .Build();
    }
}







