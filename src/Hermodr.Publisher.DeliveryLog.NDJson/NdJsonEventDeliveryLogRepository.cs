using System.Text.Json;

using Hermodr.NDJson;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr;

/// <summary>
/// An NDJson file-backed repository for event delivery log records.
/// </summary>
public class NdJsonEventDeliveryLogRepository : IEventPublishDeliveryLog, IDisposable
{
    private readonly NdJsonDeliveryLogOptions _options;
    private readonly IEventSystemTime _systemTime;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private string? _currentFilePath;
    private long _currentFileSize;
    private DateTimeOffset? _currentFileCreatedAt;

    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new CloudEventJsonConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="NdJsonEventDeliveryLogRepository"/> class.
    /// </summary>
    /// <param name="options">The NDJson delivery log options.</param>
    /// <param name="systemTime">An optional clock abstraction for timestamping.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public NdJsonEventDeliveryLogRepository(
        IOptions<NdJsonDeliveryLogOptions> options,
        IEventSystemTime? systemTime = null,
        ILogger<NdJsonEventDeliveryLogRepository>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _systemTime = systemTime ?? EventSystemTime.Instance;
        _logger = logger ?? NullLogger<NdJsonEventDeliveryLogRepository>.Instance;
        _jsonOptions = DefaultJsonOptions;
        EnsureDirectoryExists();
    }

    /// <inheritdoc />
    public string ProviderName => "NDJson";

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_options.DirectoryPath))
            Directory.CreateDirectory(_options.DirectoryPath);
    }

    /// <inheritdoc />
    public async ValueTask RecordAsync(EventDeliveryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(record, _jsonOptions);
            var writeByteCount = System.Text.Encoding.UTF8.GetByteCount(json) + Environment.NewLine.Length;

            var filePath = ResolveFilePath(writeByteCount);

            await using var stream = File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            await using var writer = new StreamWriter(stream);
            await writer.WriteLineAsync(json);

            CleanupOldFiles();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private string ResolveFilePath(long writeByteCount)
    {
        var now = _systemTime.UtcNow;
        var needsRoll = _currentFilePath == null ||
                        _currentFileSize + writeByteCount > _options.MaxFileSizeBytes ||
                        (_options.RollInterval.HasValue &&
                         _currentFileCreatedAt.HasValue &&
                         now - _currentFileCreatedAt.Value >= _options.RollInterval.Value);

        if (needsRoll)
        {
            var timestamp = now.ToString("yyyyMMdd-HHmmss");
            _currentFilePath = Path.Join(_options.DirectoryPath, $"delivery-log-{timestamp}.ndjson");
            _currentFileCreatedAt = now;
            _currentFileSize = 0;
        }

        _currentFileSize += writeByteCount;
        return _currentFilePath!;
    }

    private void CleanupOldFiles()
    {
        if (_options.MaxFileCount <= 0) return;

        try
        {
            var files = Directory.GetFiles(_options.DirectoryPath, "delivery-log-*.ndjson")
                .OrderByDescending(f => f)
                .ToList();

            if (files.Count <= _options.MaxFileCount)
                return;

            foreach (var file in files.Skip(_options.MaxFileCount))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException ex)
                {
                    _logger.LogFailedToDeleteOldDeliveryLogFile(ex, file);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogFailedToDeleteOldDeliveryLogFile(ex, file);
                }
            }
        }
        catch (IOException ex)
        {
            _logger.LogFailedToCleanUpOldDeliveryLogFiles(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogFailedToCleanUpOldDeliveryLogFiles(ex);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var results = ReadAllRecords()
            .Where(r => string.Equals(r.Event?.Id, eventId, StringComparison.Ordinal))
            .OrderBy(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByChannelAsync(string channelName, CancellationToken cancellationToken = default)
    {
        var results = ReadAllRecords()
            .Where(r => string.Equals(r.ChannelName, channelName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByOutcomeAsync(EventDeliveryOutcome outcome, CancellationToken cancellationToken = default)
    {
        var results = ReadAllRecords()
            .Where(r => r.Outcome == outcome)
            .OrderByDescending(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByTimeRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var results = ReadAllRecords()
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    private IEnumerable<EventDeliveryRecord> ReadAllRecords()
    {
        var directory = _options.DirectoryPath;
        if (!Directory.Exists(directory))
            yield break;

        var files = Directory.GetFiles(directory, "delivery-log-*.ndjson")
            .OrderBy(f => f);

        foreach (var file in files)
        {
            foreach (var record in ReadRecordsFromFile(file))
            {
                yield return record;
            }
        }
    }

    private static IEnumerable<EventDeliveryRecord> ReadRecordsFromFile(string filePath)
    {
        using var reader = new StreamReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            EventDeliveryRecord? record = null;
            try
            {
                record = JsonSerializer.Deserialize<EventDeliveryRecord>(line, DefaultJsonOptions);
            }
            catch (JsonException)
            {
            }

            if (record != null)
                yield return record;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _writeLock.Dispose();
    }
}
