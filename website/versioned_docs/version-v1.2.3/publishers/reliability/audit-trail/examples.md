# Audit Trail Examples

This section shows common usage patterns and scenarios for the audit trail feature.

## Example 1: Basic Audit Trail Setup

Minimal setup for development and testing:

```csharp
var builder = WebApplication.CreateBuilder(args);

var auditDir = Path.Combine(AppContext.BaseDirectory, "audit-trail");
Directory.CreateDirectory(auditDir);

builder.Services.AddEventPublisher(o =>
{
    o.Source = new Uri("https://orders.example.com");
})
.AddAuditTrail(audit => audit.UseNDJson(options =>
{
    options.DirectoryPath = auditDir;
    options.MaxFileSizeBytes = 10 * 1024 * 1024;
    options.RollInterval = TimeSpan.FromMinutes(5);
    options.MaxFileCount = 20;
}));

// Also register the reader for querying
builder.Services.AddNDJsonAuditTrailQuerying(options =>
{
    options.DirectoryPath = auditDir;
});

var app = builder.Build();

app.MapPost("/api/orders", async (IEventPublisher publisher, CreateOrderCommand cmd) =>
{
    await publisher.PublishAsync(new OrderPlaced
    {
        OrderId = Guid.NewGuid(),
        CustomerId = cmd.CustomerId,
        Total = cmd.Total
    });
    
    return Results.Ok();
});

app.Run();
```

## Example 2: Compliance-Ready Configuration

Production setup with long retention and secure storage:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Secure audit directory with restricted permissions
var auditDir = builder.Configuration["AuditTrail:DirectoryPath"] 
    ?? "/secure/audit/hermodr";

if (!Directory.Exists(auditDir))
{
    Directory.CreateDirectory(auditDir);
    
    // Set restrictive permissions (Unix-like systems)
    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
        var fileInfo = new FileInfo(Path.Combine(auditDir, ".gitkeep"));
        // Only owner can read/write
        fileInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    }
}

builder.Services.AddEventPublisher(o =>
{
    o.Source = new Uri("https://orders.example.com");
    o.DataSchemaBaseUri = new Uri("https://schemas.example.com/");
})
.AddAuditTrail(audit => audit.UseNDJson(options =>
{
    options.DirectoryPath = auditDir;
    options.MaxFileSizeBytes = 50 * 1024 * 1024;  // 50 MB
    options.RollInterval = TimeSpan.FromHours(12);
    options.MaxFileCount = 730;  // Keep ~1 year at 2 rolls/day
}));

// Register reader for compliance queries
builder.Services.AddNDJsonAuditTrailQuerying(options =>
{
    options.DirectoryPath = auditDir;
});

var app = builder.Build();
app.Run();
```

## Example 3: Multi-Tenant Audit Trail

Separate audit trails per tenant:

```csharp
public class TenantAwareAuditTrailMiddleware : IEventMiddleware
{
    private readonly ITenantResolver _tenantResolver;
    private readonly IServiceProvider _services;

    public TenantAwareAuditTrailMiddleware(
        ITenantResolver tenantResolver,
        IServiceProvider services)
    {
        _tenantResolver = tenantResolver;
        _services = services;
    }

    public async Task InvokeAsync(EventContext context, Func<Task> next)
    {
        var tenant = await _tenantResolver.ResolveAsync(context);
        
        // Get tenant-specific audit writer
        var writer = _services.GetRequiredKeyedService<IAuditTrailWriter>(tenant.Id);
        
        // Record event to tenant-specific audit trail
        await writer.WriteAsync(context.Event);
        
        await next();
    }
}

// Registration
builder.Services
    .AddEventPublisher()
    .AddAuditTrail(audit => audit.UseNDJson(options =>
    {
        options.DirectoryPath = "/audit/tenant-a";
    }), key: "tenant-a")
    .AddAuditTrail(audit => audit.UseNDJson(options =>
    {
        options.DirectoryPath = "/audit/tenant-b";
    }), key: "tenant-b");
```

## Example 4: Audit Trail Query API

Full-featured query endpoint:

```csharp
public static class AuditTrailEndpoints
{
    public static void MapAuditTrailEndpoints(this IEndpointRouteBuilder app)
    {
        // Query audit trail with filters
        app.MapGet("/api/audit-trail", async (
            IAuditTrailReader<AuditTrailEntry> reader,
            string? eventType, string? source, string? subject,
            DateTimeOffset? from, DateTimeOffset? to, int? limit) =>
        {
            var query = new AuditTrailStreamQuery
            {
                EventType = eventType,
                Source = source,
                Subject = subject,
                From = from,
                To = to,
                Limit = limit ?? 100
            };

            var entries = new List<AuditTrailEntry>();
            var max = query.Limit ?? 100;
            
            await foreach (var entry in reader.ReadAsync(query))
            {
                if (entries.Count >= max) break;
                entries.Add(entry);
            }
            
            return Results.Ok(entries);
        })
        .WithName("QueryAuditTrail")
        .WithOpenApi();

        // Get entries for a specific event
        app.MapGet("/api/audit-trail/event/{eventId}", async (
            string eventId,
            IAuditTrailReader<AuditTrailEntry> reader) =>
        {
            var query = new AuditTrailStreamQuery { Limit = 1000 };

            await foreach (var entry in reader.ReadAsync(query))
            {
                if (entry.EventId == eventId)
                {
                    return Results.Ok(entry);
                }
            }

            return Results.NotFound();
        })
        .WithName("GetAuditTrailEntry")
        .WithOpenApi();

        // Get all events of a specific type
        app.MapGet("/api/audit-trail/type/{eventType}", async (
            string eventType,
            IAuditTrailReader<AuditTrailEntry> reader,
            int? limit) =>
        {
            var query = new AuditTrailStreamQuery
            {
                EventType = eventType,
                Limit = limit ?? 100
            };

            var entries = new List<AuditTrailEntry>();
            await foreach (var entry in reader.ReadAsync(query))
            {
                entries.Add(entry);
                if (entries.Count >= query.Limit) break;
            }

            return Results.Ok(entries);
        })
        .WithName("GetEventsByType")
        .WithOpenApi();

        // Get aggregate statistics
        app.MapGet("/api/audit-trail/stats", async (
            IAuditTrailReader<AuditTrailEntry> reader,
            DateTimeOffset? from, DateTimeOffset? to) =>
        {
            var query = new AuditTrailStreamQuery
            {
                From = from ?? DateTimeOffset.UtcNow.AddHours(-24),
                To = to ?? DateTimeOffset.UtcNow
            };

            var eventTypeCounts = new Dictionary<string, int>();
            var totalCount = 0;
            
            await foreach (var entry in reader.ReadAsync(query))
            {
                totalCount++;
                
                if (!eventTypeCounts.TryGetValue(entry.EventType, out var count))
                {
                    count = 0;
                }
                eventTypeCounts[entry.EventType] = count + 1;
            }

            return Results.Ok(new
            {
                From = query.From,
                To = query.To,
                TotalEvents = totalCount,
                EventsByType = eventTypeCounts,
                GeneratedAt = DateTimeOffset.UtcNow
            });
        })
        .WithName("GetAuditTrailStats")
        .WithOpenApi();
    }
}

// Usage in Program.cs
var app = builder.Build();
app.MapAuditTrailEndpoints();
app.Run();
```

## Example 5: Archival Process

Move old audit files to cold storage:

```csharp
public class AuditTrailArchiver
{
    private readonly string _sourceDirectory;
    private readonly string _archiveDirectory;
    private readonly ILogger<AuditTrailArchiver> _logger;

    public AuditTrailArchiver(
        string sourceDirectory,
        string archiveDirectory,
        ILogger<AuditTrailArchiver> logger)
    {
        _sourceDirectory = sourceDirectory;
        _archiveDirectory = archiveDirectory;
        _logger = logger;
    }

    public async Task ArchiveOldFilesAsync(int retentionDays = 90)
    {
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        var files = Directory.GetFiles(_sourceDirectory, "*.ndjson")
            .Select(f => new FileInfo(f))
            .Where(f => f.LastWriteTime < cutoffDate)
            .OrderBy(f => f.LastWriteTime)
            .ToList();

        foreach (var file in files)
        {
            var archivePath = Path.Combine(
                _archiveDirectory,
                $"archive-{file.Name}");

            _logger.LogInformation("Archiving {File} to {ArchivePath}", 
                file.Name, archivePath);

            // Move to archive (or upload to cloud storage)
            File.Move(file.FullName, archivePath);
            
            // Optionally compress
            // await CompressFileAsync(archivePath);
        }

        _logger.LogInformation("Archived {Count} files", files.Count);
    }
}

// Register as hosted service
builder.Services.AddHostedService<ArchivalBackgroundService>();

public class ArchivalBackgroundService : BackgroundService
{
    private readonly AuditTrailArchiver _archiver;
    private readonly ILogger<ArchivalBackgroundService> _logger;

    public ArchivalBackgroundService(
        AuditTrailArchiver archiver,
        ILogger<ArchivalBackgroundService> logger)
    {
        _archiver = archiver;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Run archival daily at 2 AM
                var now = DateTimeOffset.UtcNow;
                var nextRun = now.Hour == 2 ? 
                    now.AddDays(1).Date.AddHours(2) : 
                    now.Date.AddHours(2);

                var delay = nextRun - now;
                _logger.LogInformation("Next archival run in {Delay}", delay);

                await Task.Delay(delay, stoppingToken);
                
                await _archiver.ArchiveOldFilesAsync(retentionDays: 90);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Archival failed");
            }
        }
    }
}
```

## Related Pages

- [Audit Trail Overview](README.md)
- [Querying](querying.md)
- [NDJSON Backend](ndjson-backend.md)
