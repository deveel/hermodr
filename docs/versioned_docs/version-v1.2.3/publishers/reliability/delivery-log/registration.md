# Delivery Log Registration

## Via EventPublisherBuilder

`AddDeliveryLog()` is an extension on `EventPublisherBuilder`. It adds the delivery log middleware to the pipeline.

### With In-Memory Storage

```csharp
builder.Services
    .AddEventPublisher(options =>
        options.Source = new Uri("https://orders.example.com"))
    .AddDeliveryLog(log => log.UseInMemory());
```

### With NDJSON Backend

```csharp
builder.Services
    .AddEventPublisher(options =>
        options.Source = new Uri("https://orders.example.com"))
    .AddDeliveryLog(log => log.UseNDJson(opts =>
    {
        opts.DirectoryPath = "/var/logs/delivery-logs";
        opts.MaxFileSizeBytes = 10 * 1024 * 1024;
        opts.RollInterval = TimeSpan.FromHours(6);
        opts.MaxFileCount = 30;
    }));
```

### With EF Core Backend

```csharp
builder.Services
    .AddEventPublisher(options =>
        options.Source = new Uri("https://orders.example.com"))
    .AddDeliveryLog(log => log.UseEntityFramework(opts =>
        opts.UseSqlite("Data Source=delivery-log.db")));
```

### With Error Handler

```csharp
builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log
        .UseInMemory()
        .UseErrorHandler());
```

### With Custom Store

```csharp
builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log.UseStore<MyCustomStore>());

// Or with instance:
var myStore = new MyCustomStore();
builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log.UseStore(myStore));
```

> **Note:** Only one storage backend is active — subsequent `Use*` calls replace the previous registration.

---

## Standalone (Without Publisher)

If you only need the delivery log services independent of the publisher pipeline:

```csharp
builder.Services.AddDeliveryLog(log => log.UseNDJson(opts =>
{
    opts.DirectoryPath = "/var/logs/events";
    opts.MaxFileSizeBytes = 50 * 1024 * 1024;
}));
```

This registers the `IEventPublishDeliveryLog` service without a middleware pipeline.

---

## Related Pages

- [Delivery Log Overview](README.md)
- [Middleware](middleware.md)
- [Storage Backends](storage-backends.md)
