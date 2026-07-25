using Hermodr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

// ──────────────────────────────────────────────────────────────────────────
// App wiring + endpoint exposing both exported documents.
// ──────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEventPublisher(options => {
    options.Source = new Uri("https://samples.deveel.events/asyncapi-export");
});

var app = builder.Build();

// /asyncapi.json  -> AsyncAPI 2.x document (auto-discovered schemas, no live channels)
// /asyncapi-with-channels.json -> AsyncAPI enriched with a declarative RabbitMQ channel
// /openapi.json   -> OpenAPI 3.1 webhook document
app.MapGet("/asyncapi.json", () => Results.Content(WriteAsyncApi(includeChannels: false), "application/json"));
app.MapGet("/asyncapi-with-channels.json", () => Results.Content(WriteAsyncApi(includeChannels: true), "application/json"));
app.MapGet("/openapi.json", () => Results.Content(WriteOpenApi(), "application/json"));

app.Run();

// ──────────────────────────────────────────────────────────────────────────
// Document builders. In a real app these would be wired once at startup; the
// endpoints here recompute them on every request for clarity.
// ──────────────────────────────────────────────────────────────────────────

static string WriteAsyncApi(bool includeChannels) {
    var schemas = new EventSchemaDiscovery()
        .DiscoverSchemas(Assembly.GetExecutingAssembly());

    var builder = new AsyncApiDocumentBuilder()
        .WithTitle("Sample Events API")
        .WithVersion("1.0")
        .WithSchemas(schemas);

    if (includeChannels) {
        // Declarative channel bindings (the CLI tool reads these from --channels-file).
        builder.WithChannels(new[] {
            new EventPublishChannelMetadata(
                "order-placed",
                EventTransports.RabbitMq,
                new Dictionary<string, string?> {
                    ["exchange"] = "orders.x",
                    ["routingKey"] = "#"
                })
        });
    }

    var doc = builder.Build();
    return AsyncApiSerializer.Serialize(doc, AsyncApiFormat.Json);
}

static string WriteOpenApi() {
    var schemas = new EventSchemaDiscovery()
        .DiscoverSchemas(Assembly.GetExecutingAssembly());

    var doc = new OpenApiWebhookDocumentBuilder()
        .WithTitle("Sample Events API")
        .WithVersion("1.0")
        .WithSchemas(schemas)
        .Build();

    using var ms = new MemoryStream();
    OpenApiDocumentWriter.Serialize(ms, doc, OpenApiFormat.Json);
    ms.Position = 0;
    return new StreamReader(ms).ReadToEnd();
}