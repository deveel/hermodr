using System.Text.Json;

using Bogus;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Options;

using System.IO.Abstractions.TestingHelpers;

namespace Hermodr.AuditTrail.NDJson.XUnit.Unit;

[Trait("Category", "Unit")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "AuditTrail")]
public class NdJsonAuditTrailTests : IDisposable
{
    private static readonly Faker Faker = new("en");
    private readonly string _tempDir;
    private readonly List<string> _createdDirs = new();

    public NdJsonAuditTrailTests()
    {
        _tempDir = Path.Join(Path.GetTempPath(), $"ndjson-audit-{Guid.NewGuid():N}");
        _createdDirs.Add(_tempDir);
    }

    private NdJsonAuditTrailOptions CreateOptions(
        long? maxFileSizeBytes = null,
        TimeSpan? rollInterval = null,
        int maxFileCount = 10,
        string? fileNamePrefix = null,
        string? fileExtension = null)
    {
        return new NdJsonAuditTrailOptions
        {
            DirectoryPath = _tempDir,
            MaxFileSizeBytes = maxFileSizeBytes ?? 10 * 1024 * 1024,
            RollInterval = rollInterval,
            MaxFileCount = maxFileCount,
            FileNamePrefix = fileNamePrefix ?? "audit-trail",
            FileExtension = fileExtension ?? ".ndjson"
        };
    }

    private static CloudEvent CreateCloudEvent(
        string? type = null,
        string? source = null,
        string? subject = null,
        DateTimeOffset? time = null,
        string? id = null)
    {
        return new CloudEvent
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            Type = type ?? "com.example.order.created",
            Source = new Uri(source ?? "https://example.com/orders"),
            Subject = subject,
            Time = time ?? DateTimeOffset.UtcNow,
            Data = JsonSerializer.Serialize(new { OrderId = 123, Amount = 99.99 })
        };
    }

    public void Dispose()
    {
        foreach (var dir in _createdDirs.Where(Directory.Exists))
        {
            try { Directory.Delete(dir, true); }
            catch (IOException) { /* cleanup best-effort */ }
            catch (UnauthorizedAccessException) { /* cleanup best-effort */ }
        }
    }

    // ── Constructor ─────────────────────────────────────────────────────────

    [Fact]
    public void Should_CreateDirectory_When_NotExists()
    {
        Assert.False(Directory.Exists(_tempDir));

        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));

        Assert.True(Directory.Exists(_tempDir));
    }

    [Fact]
    public void Should_ThrowOnNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new NdJsonAuditTrail(null!));
    }

    // ── AppendAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_AppendEvent_ToFile()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        var cloudEvent = CreateCloudEvent();

        await store.AppendAsync(cloudEvent, TestContext.Current.CancellationToken);

        var files = Directory.GetFiles(_tempDir, "*.ndjson");
        Assert.NotEmpty(files);
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_EventIsNull()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.AppendAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_AppendMultipleEvents_ToSingleFile()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));

        for (var i = 0; i < 10; i++)
        {
            await store.AppendAsync(CreateCloudEvent(type: $"event.type.{i}"), TestContext.Current.CancellationToken);
        }

        var files = Directory.GetFiles(_tempDir, "*.ndjson");
        Assert.Single(files);

        var lines = await File.ReadAllLinesAsync(files[0], TestContext.Current.CancellationToken);
        Assert.Equal(10, lines.Length);
    }

    [Fact]
    public async Task Should_AppendEachEvent_AsSeparateLine()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        var ce1 = CreateCloudEvent(id: "evt-1");
        var ce2 = CreateCloudEvent(id: "evt-2");

        await store.AppendAsync(ce1, TestContext.Current.CancellationToken);
        await store.AppendAsync(ce2, TestContext.Current.CancellationToken);

        var file = Directory.GetFiles(_tempDir, "*.ndjson").Single();
        var lines = await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken);

        Assert.Equal(2, lines.Length);
        var first = JsonSerializer.Deserialize<AuditTrailEntry>(lines[0], new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var second = JsonSerializer.Deserialize<AuditTrailEntry>(lines[1], new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal("evt-1", first.EventId);
        Assert.Equal("evt-2", second.EventId);
    }

    // ── File rolling ────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RollFile_When_MaxFileSizeBytes_Exceeded()
    {
        var options = CreateOptions(maxFileSizeBytes: 200);
        using var store = new NdJsonAuditTrail(Options.Create(options));

        for (var i = 0; i < 20; i++)
        {
            await store.AppendAsync(CreateCloudEvent(id: $"evt-{i}"), TestContext.Current.CancellationToken);
        }

        var files = Directory.GetFiles(_tempDir, "*.ndjson");
        Assert.True(files.Length >= 2, $"Expected at least 2 files but found {files.Length}.");
    }

    [Fact]
    public async Task Should_RollFile_After_RollInterval()
    {
        var systemTime = new TestableSystemTime();
        var options = CreateOptions(rollInterval: TimeSpan.FromSeconds(5));
        using var store = new NdJsonAuditTrail(
            Options.Create(options),
            systemTime: systemTime);

        await store.AppendAsync(CreateCloudEvent(id: "evt-1"), TestContext.Current.CancellationToken);

        systemTime.Advance(TimeSpan.FromSeconds(10));
        await store.AppendAsync(CreateCloudEvent(id: "evt-2"), TestContext.Current.CancellationToken);

        var files = Directory.GetFiles(_tempDir, "*.ndjson");
        Assert.Equal(2, files.Length);
    }

    // ── Cleanup ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_DeleteOldFiles_Beyond_MaxFileCount()
    {
        var options = CreateOptions(maxFileSizeBytes: 100, maxFileCount: 2);
        using var store = new NdJsonAuditTrail(Options.Create(options));

        for (var i = 0; i < 20; i++)
        {
            await store.AppendAsync(CreateCloudEvent(id: $"evt-{i}"), TestContext.Current.CancellationToken);
        }

        var files = Directory.GetFiles(_tempDir, "*.ndjson");
        Assert.True(files.Length <= 2, $"Expected at most 2 files but found {files.Length}.");
    }

    [Fact]
    public async Task Should_NotDeleteFiles_When_MaxFileCount_IsZero()
    {
        var options = CreateOptions(maxFileSizeBytes: 100, maxFileCount: 0);
        using var store = new NdJsonAuditTrail(Options.Create(options));

        for (var i = 0; i < 10; i++)
        {
            await store.AppendAsync(CreateCloudEvent(id: $"evt-{i}"), TestContext.Current.CancellationToken);
        }

        var files = Directory.GetFiles(_tempDir, "*.ndjson");
        Assert.True(files.Length > 2, $"Expected more than 2 files but found {files.Length}.");
    }

    // ── ReadAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_ReadAllEntries()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        await store.AppendAsync(CreateCloudEvent(type: "evt.1"), TestContext.Current.CancellationToken);
        await store.AppendAsync(CreateCloudEvent(type: "evt.2"), TestContext.Current.CancellationToken);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Should_ReadEntries_AcrossMultipleFiles()
    {
        var options = CreateOptions(maxFileSizeBytes: 150);
        using var store = new NdJsonAuditTrail(Options.Create(options));

        for (var i = 0; i < 10; i++)
        {
            await store.AppendAsync(CreateCloudEvent(id: $"evt-{i}"), TestContext.Current.CancellationToken);
        }

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task Should_FilterByEventType()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        await store.AppendAsync(CreateCloudEvent(type: "order.created"), TestContext.Current.CancellationToken);
        await store.AppendAsync(CreateCloudEvent(type: "order.shipped"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { EventType = "order.created" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("order.created", results[0].EventType);
    }

    [Fact]
    public async Task Should_FilterBySource()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        await store.AppendAsync(CreateCloudEvent(source: "https://service-a.com/"), TestContext.Current.CancellationToken);
        await store.AppendAsync(CreateCloudEvent(source: "https://service-b.com/"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { Source = "https://service-a.com/" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("https://service-a.com/", results[0].Source);
    }

    [Fact]
    public async Task Should_FilterBySubject()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        await store.AppendAsync(CreateCloudEvent(subject: "ord-1"), TestContext.Current.CancellationToken);
        await store.AppendAsync(CreateCloudEvent(subject: "ord-2"), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { Subject = "ord-1" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("ord-1", results[0].Subject);
    }

    [Fact]
    public async Task Should_FilterByTimeRange()
    {
        var now = DateTimeOffset.UtcNow;
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        await store.AppendAsync(CreateCloudEvent(time: now.AddHours(-5)), TestContext.Current.CancellationToken);
        await store.AppendAsync(CreateCloudEvent(time: now.AddHours(-1)), TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery { From = now.AddHours(-3) };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
    }

    [Fact]
    public async Task Should_FilterByExtensionAttributes()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        var ce1 = CreateCloudEvent();
        ce1["traceid"] = "trace-123";
        var ce2 = CreateCloudEvent();
        await store.AppendAsync(ce1, TestContext.Current.CancellationToken);
        await store.AppendAsync(ce2, TestContext.Current.CancellationToken);

        var query = new AuditTrailStreamQuery
        {
            Attributes = new Dictionary<string, string> { ["traceid"] = "trace-123" }
        };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Single(results);
    }

    [Fact]
    public async Task Should_ReturnEntriesInChronologicalOrder()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        var now = DateTimeOffset.UtcNow;
        await store.AppendAsync(CreateCloudEvent(time: now.AddMinutes(-10), id: "1"), TestContext.Current.CancellationToken);
        await store.AppendAsync(CreateCloudEvent(time: now.AddMinutes(0), id: "2"), TestContext.Current.CancellationToken);
        await store.AppendAsync(CreateCloudEvent(time: now.AddMinutes(-5), id: "3"), TestContext.Current.CancellationToken);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(3, results.Count);
        for (var i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].Timestamp <= results[i].Timestamp);
        }
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_DirectoryDoesNotExist()
    {
        var dir = Path.Join(Path.GetTempPath(), $"ndjson-audit-empty-{Guid.NewGuid():N}");
        var options = CreateOptions();
        options.DirectoryPath = dir;
        using var store = new NdJsonAuditTrail(Options.Create(options));

        Directory.Delete(dir, recursive: true);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Empty(results);
    }

    [Fact]
    public async Task Should_SkipBlankAndInvalidLines()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        await store.AppendAsync(CreateCloudEvent(id: "valid-1"), TestContext.Current.CancellationToken);

        var file = Directory.GetFiles(_tempDir, "*.ndjson").Single();
        await File.AppendAllTextAsync(file, "\nnot valid json\n", TestContext.Current.CancellationToken);

        await store.AppendAsync(CreateCloudEvent(id: "valid-2"), TestContext.Current.CancellationToken);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task ReadAsync_CancellationRequested_ShouldThrow()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        await store.AppendAsync(CreateCloudEvent(), TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in store.ReadAsync(cancellationToken: cts.Token))
            {
                // enumeration force-feed to trigger cancellation
            }
        });
    }

    [Fact]
    public async Task Should_YieldEntries_Incrementally_WithoutBufferingFullFile()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));

        for (var i = 0; i < 50; i++)
        {
            await store.AppendAsync(CreateCloudEvent(id: $"evt-{i:D3}"), TestContext.Current.CancellationToken);
        }

        var observed = new List<string>();
        await foreach (var entry in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            observed.Add(entry.EventId);
        }

        Assert.Equal(50, observed.Count);
    }

    [Fact]
    public async Task Should_ApplyFilter_WhileStreaming()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        for (var i = 0; i < 20; i++)
        {
            await store.AppendAsync(
                CreateCloudEvent(id: $"evt-{i}", type: i % 2 == 0 ? "even" : "odd"),
                TestContext.Current.CancellationToken);
        }

        var query = new AuditTrailStreamQuery { EventType = "odd" };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(10, results.Count);
        Assert.All(results, e => Assert.Equal("odd", e.EventType));
    }

    [Fact]
    public async Task Should_NotBlockWriter_DuringConcurrentRead()
    {
        var options = CreateOptions(maxFileSizeBytes: 10 * 1024 * 1024);
        using var store = new NdJsonAuditTrail(Options.Create(options));
        for (var i = 0; i < 100; i++)
        {
            await store.AppendAsync(CreateCloudEvent(id: $"pre-{i}"), TestContext.Current.CancellationToken);
        }

        var initialCount = 0;
        await foreach (var _ in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            initialCount++;
        }
        Assert.Equal(100, initialCount);

        var readStarted = new TaskCompletionSource();
        var readCanFinish = new TaskCompletionSource();
        var readTask = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var _ in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
            {
                count++;
                if (count == 10)
                    readStarted.TrySetResult();
                if (count == 10)
                    await readCanFinish.Task;
                if (count % 5 == 0)
                    await Task.Yield();
            }
            return count;
        }, TestContext.Current.CancellationToken);

        await readStarted.Task;

        var writeStopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 50; i++)
        {
            await store.AppendAsync(CreateCloudEvent(id: $"during-read-{i}"), TestContext.Current.CancellationToken);
        }
        writeStopwatch.Stop();

        readCanFinish.TrySetResult();
        var totalRead = await readTask;

        Assert.True(writeStopwatch.Elapsed < TimeSpan.FromSeconds(15),
            $"Writes were apparently blocked: took {writeStopwatch.Elapsed}.");
        Assert.True(totalRead >= 100, $"Expected at least 100 entries read, got {totalRead}.");
    }

    [Fact]
    public async Task Should_ContinueReading_WhenFileDeletedMidRead()
    {
        var options = CreateOptions(maxFileSizeBytes: 200, maxFileCount: 0);
        using var store = new NdJsonAuditTrail(Options.Create(options));

        for (var i = 0; i < 10; i++)
        {
            await store.AppendAsync(CreateCloudEvent(id: $"evt-{i}"), TestContext.Current.CancellationToken);
        }

        var files = Directory.GetFiles(_tempDir, "*.ndjson");
        Assert.True(files.Length >= 2);

        File.Delete(files[0]);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task Should_ContinueReading_WhenDirectoryDeletedMidRead()
    {
        var options = CreateOptions();
        using var store = new NdJsonAuditTrail(Options.Create(options));

        await store.AppendAsync(CreateCloudEvent(id: "first"), TestContext.Current.CancellationToken);
        var files = Directory.GetFiles(_tempDir, "*.ndjson");
        File.Delete(files[0]);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Empty(results);
    }

    [Fact]
    public async Task Should_OpenFile_WithReadBufferSize()
    {
        var options = CreateOptions();
        options.ReadBufferSize = 65_536;
        using var store = new NdJsonAuditTrail(Options.Create(options));

        for (var i = 0; i < 5; i++)
        {
            await store.AppendAsync(CreateCloudEvent(id: $"evt-{i}"), TestContext.Current.CancellationToken);
        }

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(5, results.Count);
    }

    // ── Filesystem abstraction ─────────────────────────────────────────────

    [Fact]
    public async Task Should_Use_Injected_FileSystem_For_Storage()
    {
        var mockFs = new MockFileSystem();
        var dir = mockFs.Path.Combine(mockFs.Path.GetTempPath(), $"mock-{Guid.NewGuid():N}");
        mockFs.AddDirectory(dir);

        var options = new NdJsonAuditTrailOptions
        {
            DirectoryPath = dir,
            MaxFileCount = 5
        };

        using (var store = new NdJsonAuditTrail(Options.Create(options), fileSystem: mockFs))
        {
            await store.AppendAsync(CreateCloudEvent(id: "mock-1"), TestContext.Current.CancellationToken);
            await store.AppendAsync(CreateCloudEvent(id: "mock-2"), TestContext.Current.CancellationToken);
        }

        var files = mockFs.Directory.GetFiles(dir, "*.ndjson");
        Assert.NotEmpty(files);
        var content = mockFs.File.ReadAllText(files[0]);
        Assert.Contains("mock-1", content);
        Assert.Contains("mock-2", content);
    }

    [Fact]
    public async Task Should_Append_And_Read_Through_MockFileSystem()
    {
        var mockFs = new MockFileSystem();
        var dir = mockFs.Path.Combine(mockFs.Path.GetTempPath(), $"mock-{Guid.NewGuid():N}");
        mockFs.AddDirectory(dir);

        var options = new NdJsonAuditTrailOptions { DirectoryPath = dir };
        using var store = new NdJsonAuditTrail(Options.Create(options), fileSystem: mockFs);

        await store.AppendAsync(CreateCloudEvent(id: "x"), TestContext.Current.CancellationToken);
        await store.AppendAsync(CreateCloudEvent(id: "y"), TestContext.Current.CancellationToken);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in store.ReadAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal("x", results[0].EventId);
        Assert.Equal("y", results[1].EventId);
    }

    [Fact]
    public async Task Should_Honor_Custom_FileNamePrefix()
    {
        var options = CreateOptions(fileNamePrefix: "events");
        using var store = new NdJsonAuditTrail(Options.Create(options));
        await store.AppendAsync(CreateCloudEvent(), TestContext.Current.CancellationToken);

        var eventsFiles = Directory.GetFiles(_tempDir, "events-*.ndjson");
        var auditFiles = Directory.GetFiles(_tempDir, "audit-trail-*.ndjson");

        Assert.NotEmpty(eventsFiles);
        Assert.Empty(auditFiles);
    }

    [Fact]
    public async Task Should_Honor_Custom_FileExtension()
    {
        var options = CreateOptions(fileExtension: ".jsonl");
        using var store = new NdJsonAuditTrail(Options.Create(options));
        await store.AppendAsync(CreateCloudEvent(), TestContext.Current.CancellationToken);

        var jsonlFiles = Directory.GetFiles(_tempDir, "*.jsonl");
        var ndjsonFiles = Directory.GetFiles(_tempDir, "*.ndjson");

        Assert.NotEmpty(jsonlFiles);
        Assert.Empty(ndjsonFiles);
    }

    // ── Provider / disposable contract ─────────────────────────────────────

    [Fact]
    public void Should_Report_ProviderName()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        Assert.Equal("NDJson", store.ProviderName);
    }

    [Fact]
    public void Should_Implement_IDisposable()
    {
        using var store = new NdJsonAuditTrail(Options.Create(CreateOptions()));
        store.Dispose();
    }

    private sealed class TestableSystemTime : IEventSystemTime
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public DateTimeOffset UtcNow => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
