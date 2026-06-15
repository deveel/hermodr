# Filesystem Abstraction for Audit Trail

The NDJSON audit trail backend uses the `IFileSystem` abstraction from [`System.IO.Abstractions`](https://www.nuget.org/packages/System.IO.Abstractions), enabling pluggable storage backends.

## IFileSystem Abstraction

All file I/O in the NDJSON backend goes through `IFileSystem`:

```csharp
public interface IFileSystem
{
    IFile File { get; }
    IDirectory Directory { get; }
    IDirectoryInfo DirectoryInfo { get; }
    IFileInfo FileInfo { get; }
    // ... other members
}
```

This means the audit trail code never touches `System.IO` directly — every call goes through the abstraction.

## Default: Physical File System

By default, the NDJSON backend uses the physical file system:

```csharp
builder.Services.AddEventPublisher()
    .AddAuditTrail(audit => audit.UseNDJson(options =>
    {
        options.DirectoryPath = "/var/audit/hermodr";
    }));
```

This uses `PhysicalFileSystem` from `System.IO.Abstractions`, which wraps the real `System.IO` APIs.

## Custom Filesystem Adapters

To use alternative storage (Azure Blob, AWS S3, etc.), register a compatible `IFileSystem` implementation **before** calling `UseNDJson`:

### Azure Blob Storage Example

```csharp
using System.IO.Abstractions;
using Azure.Storage.Blobs;

// Custom implementation wrapping Azure Blob Storage
public class AzureBlobFileSystem : IFileSystem
{
    private readonly BlobContainerClient _container;
    
    public AzureBlobFileSystem(string connectionString, string containerName)
    {
        var client = new BlobServiceClient(connectionString);
        _container = client.GetBlobContainerClient(containerName);
    }
    
    public IFile File => new AzureBlobFile(_container);
    public IDirectory Directory => new AzureBlobDirectory(_container);
    // ... implement other members
}

// Registration
builder.Services.AddSingleton<IFileSystem>(sp => 
    new AzureBlobFileSystem(
        connectionString: builder.Configuration["AzureStorage:ConnectionString"],
        containerName: "audit-trail"));

builder.Services.AddEventPublisher()
    .AddAuditTrail(audit => audit.UseNDJson(o => 
        o.DirectoryPath = "audit-trail"));  // Virtual path within container
```

### AWS S3 Example

```csharp
using Amazon.S3;
using System.IO.Abstractions;

public class S3FileSystem : IFileSystem
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    
    public S3FileSystem(IAmazonS3 s3Client, string bucketName)
    {
        _s3Client = s3Client;
        _bucketName = bucketName;
    }
    
    public IFile File => new S3File(_s3Client, _bucketName);
    public IDirectory Directory => new S3Directory(_s3Client, _bucketName);
    // ... implement other members
}

// Registration
builder.Services.AddSingleton<IFileSystem>(sp => 
    new S3FileSystem(
        s3Client: sp.GetRequiredService<IAmazonS3>(),
        bucketName: "my-audit-trail-bucket"));

builder.Services.AddEventPublisher()
    .AddAuditTrail(audit => audit.UseNDJson(o => 
        o.DirectoryPath = "audit-trail"));
```

## Benefits of Filesystem Abstraction

| Benefit | Description |
|---------|-------------|
| **Testability** | Use `MockFileSystem` in unit tests |
| **Cloud storage** | Swap in Azure Blob, AWS S3, Google Cloud Storage |
| **Compliance** | Use WORM (Write Once, Read Many) storage adapters |
| **Encryption** | Use encrypted filesystem wrappers |
| **Virtual filesystems** | Mount from network shares, containers, etc. |

## Testing with MockFileSystem

Use `MockFileSystem` from `System.IO.Abstractions.TestingHelpers` for unit tests:

```csharp
using System.IO.Abstractions.TestingHelpers;

[Fact]
public async Task AuditTrail_WritesToMockFileSystem()
{
    // Arrange
    var mockFileSystem = new MockFileSystem();
    mockFileSystem.AddDirectory("/audit-trail");
    
    var services = new ServiceCollection();
    services.AddSingleton<IFileSystem>(mockFileSystem);
    services.AddEventPublisher()
        .AddAuditTrail(audit => audit.UseNDJson(o => 
            o.DirectoryPath = "/audit-trail"));
    
    var provider = services.BuildServiceProvider();
    var publisher = provider.GetRequiredService<IEventPublisher>();
    
    // Act
    await publisher.PublishAsync(new TestEvent { Id = "123" });
    
    // Assert
    var files = mockFileSystem.AllFiles
        .Where(f => f.StartsWith("/audit-trail"))
        .ToList();
    
    Assert.NotEmpty(files);
}
```

## Available Filesystem Adapters

| Adapter | Package | Use Case |
|---------|---------|----------|
| **PhysicalFileSystem** | `System.IO.Abstractions` | Local disk (default) |
| **MockFileSystem** | `System.IO.Abstractions.TestingHelpers` | Unit testing |
| **Azure Blob FileSystem** | Custom implementation | Azure Storage |
| **AWS S3 FileSystem** | Custom implementation | AWS S3 |
| **EncryptedFileSystem** | Custom wrapper | Encryption at rest |
| **ReadOnlyFileSystem** | Custom wrapper | Compliance (WORM) |

## Implementing a Custom FileSystem

To implement a custom filesystem, you need to provide:

1. **IFileSystem** — The root interface
2. **IFile** — File operations (read, write, append)
3. **IDirectory** — Directory operations (create, list, delete)
4. **IFileInfo** / **IDirectoryInfo** — File/directory metadata

### Minimal Example

```csharp
using System.IO.Abstractions;

public class CustomFileSystem : IFileSystem
{
    public IFile File => new CustomFile();
    public IDirectory Directory => new CustomDirectory();
    public IDirectoryInfo DirectoryInfo => new CustomDirectoryInfo(this);
    public IFileInfo FileInfo => new CustomFileInfo(this);
    // ... other members
}

public class CustomFile : IFile
{
    public async Task AppendAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default)
    {
        // Implement append logic for your storage backend
    }
    
    // ... implement other IFile members
}
```

> **Tip:** Inherit from `FileSystemBase`, `FileBase`, `DirectoryBase`, etc. to get default implementations for members you don't need to override.

## Configuration Considerations

### Path Mapping

When using cloud storage, the `DirectoryPath` becomes a **virtual path** or **prefix**:

```csharp
// Azure Blob: container/audit-trail/
options.DirectoryPath = "audit-trail";

// AWS S3: bucket/prefix/audit-trail/
options.DirectoryPath = "audit-trail";
```

### Performance

| Operation | Local Disk | Cloud Storage |
|-----------|------------|---------------|
| **Sequential write** | Fast (~100 MB/s) | Moderate (~10-50 MB/s) |
| **Append** | Fast | May require read-modify-write |
| **List files** | Fast | API call, may be rate-limited |
| **Read** | Fast | Network latency |

For high-throughput scenarios, consider:
- Batching writes
- Using local cache + async upload
- Choosing a cloud storage with append support

## Related Pages

- [Audit Trail Overview](README.md)
- [NDJSON Backend](ndjson-backend.md)
