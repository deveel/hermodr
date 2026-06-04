using AuditTrail.NDJson.Sample.Events;
using CloudNative.CloudEvents;
using Hermodr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

var source = new Uri("https://samples.deveel.events/audit-trail-ndjson");
var auditDir = Path.Combine(AppContext.BaseDirectory, "audit-trail-ndjson");
Directory.CreateDirectory(auditDir);

// Register the publisher with the audit trail channel (writes events to NDJson files)
builder.Services.AddEventPublisher(o =>
{
    o.Source = source;
    o.DataSchemaBaseUri = new Uri("https://schemas.deveel.events/");
})
    .AddChannel<ConsoleChannel>(channelName: "console")
    .AddAuditTrail(audit => audit.UseNDJson(options =>
    {
        options.DirectoryPath = auditDir;
        options.MaxFileSizeBytes = 1024 * 1024;        // 1 MB
        options.RollInterval = TimeSpan.FromMinutes(5); // time-based rolling
        options.MaxFileCount = 20;                      // keep at most 20 files
    }));

// Register the reader infrastructure (TryAdd keeps the same singleton shared with the publisher)
builder.Services.AddNDJsonAuditTrailQuerying(options =>
{
    options.DirectoryPath = auditDir;
});

var app = builder.Build();

Console.WriteLine($"Audit trail directory: {auditDir}");

// ─── Publishing Endpoints ────────────────────────────────────────────────

app.MapPost("/api/orders", async (IEventPublisher publisher) =>
{
    var orderId = $"ORD-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

    await publisher.PublishAsync(new OrderSubmitted
    {
        OrderId = orderId,
        CustomerId = "CUST-42",
        TotalAmount = 149.90m
    });

    await publisher.PublishAsync(new OrderConfirmed
    {
        OrderId = orderId,
        ConfirmedAt = DateTimeOffset.UtcNow
    });

    await publisher.PublishAsync(new PaymentProcessed
    {
        PaymentId = $"PAY-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
        OrderId = orderId,
        Amount = 149.90m
    });

    await publisher.PublishAsync(new OrderShipped
    {
        OrderId = orderId,
        TrackingNumber = $"TRK-{Random.Shared.Next(100000, 999999)}"
    });

    return Results.Ok(new { OrderId = orderId, Message = "Order created and all events persisted to NDJson files." });
});

// ─── Reading Endpoints ──────────────────────────────────────────────────

app.MapGet("/api/audit-trail", async (
    IAuditTrailReader<AuditTrailEntry> reader,
    string? eventType,
    string? source,
    string? subject,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int? limit) =>
{
    var query = new AuditTrailStreamQuery
    {
        EventType = eventType,
        Source = source,
        Subject = subject,
        From = from,
        To = to
    };

    var entries = new List<AuditTrailEntry>();
    var count = 0;
    var maxResults = limit ?? 100;

    await foreach (var entry in reader.ReadAsync(query))
    {
        if (count >= maxResults) break;
        entries.Add(entry);
        count++;
    }

    return Results.Ok(new
    {
        Total = entries.Count,
        Query = new { EventType = query.EventType, Source = query.Source, Subject = query.Subject, From = query.From, To = query.To, Limit = maxResults },
        Entries = entries.Select(e => new
        {
            e.Id,
            e.EventId,
            e.EventType,
            e.EventDataClassName,
            e.Source,
            e.Subject,
            e.Timestamp,
            e.DataContentType,
            e.DataSchema,
            e.StoredAt,
            HasEventData = e.EventData != null
        })
    });
});

app.MapGet("/api/audit-trail/event/{eventId}", async (
    string eventId,
    IAuditTrailReader<AuditTrailEntry> reader) =>
{
    var entries = new List<AuditTrailEntry>();

    await foreach (var entry in reader.ReadAsync())
    {
        if (entry.EventId == eventId)
            entries.Add(entry);
    }

    return Results.Ok(new
    {
        EventId = eventId,
        Total = entries.Count,
        Entries = entries.Select(e => new
        {
            e.Id,
            e.EventType,
            e.Source,
            e.Subject,
            e.Timestamp,
            e.StoredAt
        })
    });
});

app.MapGet("/api/audit-trail/type/{eventType}", async (
    string eventType,
    IAuditTrailReader<AuditTrailEntry> reader,
    int? limit) =>
{
    var query = new AuditTrailStreamQuery { EventType = eventType };
    var entries = new List<AuditTrailEntry>();
    var count = 0;
    var maxResults = limit ?? 100;

    await foreach (var entry in reader.ReadAsync(query))
    {
        if (count >= maxResults) break;
        entries.Add(entry);
        count++;
    }

    return Results.Ok(new
    {
        EventType = eventType,
        Total = entries.Count,
        Entries = entries.Select(e => new
        {
            e.Id,
            e.EventId,
            e.Source,
            e.Subject,
            e.Timestamp,
            e.StoredAt
        })
    });
});

app.MapGet("/api/audit-trail/stats", async (
    IAuditTrailReader<AuditTrailEntry> reader) =>
{
    var allEntries = new List<AuditTrailEntry>();
    await foreach (var entry in reader.ReadAsync())
    {
        allEntries.Add(entry);
    }

    var stats = new
    {
        TotalEvents = allEntries.Count,
        ByType = allEntries
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList(),
        BySource = allEntries
            .GroupBy(e => e.Source)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList(),
        EarliestEvent = allEntries.Any() ? allEntries.Min(e => e.Timestamp) : (DateTimeOffset?)null,
        LatestEvent = allEntries.Any() ? allEntries.Max(e => e.Timestamp) : (DateTimeOffset?)null,
        OldestStoredAt = allEntries.Any() ? allEntries.Min(e => e.StoredAt) : (DateTimeOffset?)null,
        NewestStoredAt = allEntries.Any() ? allEntries.Max(e => e.StoredAt) : (DateTimeOffset?)null
    };

    return Results.Ok(stats);
});

app.Run();

internal sealed class ConsoleChannel : IEventPublishChannel
{
    public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"  [ConsoleChannel] Published: {@event.Type} (Id: {@event.Id})");
        return Task.CompletedTask;
    }
}
