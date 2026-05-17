using System.Collections.Concurrent;
using System.Text.Json;

using Deveel.Data;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Deveel.Events;

/// <summary>
/// An implementation of <see cref="IEventDeliveryLogRepository"/> that stores delivery
/// records as newline-delimited JSON (NDJSON) files on disk, with automatic file rolling
/// and cleanup.
/// </summary>
public class NdJsonEventDeliveryLogRepository : IEventDeliveryLogRepository, IDisposable
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
    /// Creates a new instance of <see cref="NdJsonEventDeliveryLogRepository"/>.
    /// </summary>
    /// <param name="options">
    /// The options used to configure the NDJSON storage backend.
    /// </param>
    /// <param name="systemTime">
    /// An optional service for obtaining the current UTC time; defaults to <see cref="EventSystemTime.Instance"/>.
    /// </param>
    /// <param name="logger">
    /// An optional logger for diagnostic output.
    /// </param>
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

    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    public string ProviderName => "NDJson";

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_options.DirectoryPath))
            Directory.CreateDirectory(_options.DirectoryPath);
    }

    /// <summary>
    /// Records a delivery attempt by appending a JSON line to the current NDJSON file.
    /// </summary>
    /// <param name="record">
    /// The record of the event delivery to be stored.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    public async Task RecordAsync(IEventDeliveryRecord record, CancellationToken cancellationToken = default)
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
            _currentFilePath = Path.Combine(_options.DirectoryPath, $"delivery-log-{timestamp}.ndjson");
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old delivery log file: {FilePath}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up old delivery log files.");
        }
    }

    /// <summary>
    /// Retrieves all delivery records associated with the given event identifier.
    /// </summary>
    /// <param name="eventId">The identifier of the event.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records for the specified event.
    /// </returns>
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var results = ReadAllRecords()
            .Where(r => string.Equals(r.Event?.Id, eventId, StringComparison.Ordinal))
            .OrderBy(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    /// <summary>
    /// Retrieves all delivery records for the given channel name.
    /// </summary>
    /// <param name="channelName">The name of the channel.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records for the specified channel.
    /// </returns>
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByChannelAsync(string channelName, CancellationToken cancellationToken = default)
    {
        var results = ReadAllRecords()
            .Where(r => string.Equals(r.ChannelName, channelName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    /// <summary>
    /// Retrieves all delivery records matching the specified outcome.
    /// </summary>
    /// <param name="outcome">The delivery outcome to filter by.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records with the specified outcome.
    /// </returns>
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByOutcomeAsync(EventDeliveryOutcome outcome, CancellationToken cancellationToken = default)
    {
        var results = ReadAllRecords()
            .Where(r => r.Outcome == outcome)
            .OrderByDescending(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    /// <summary>
    /// Retrieves all delivery records within the specified time range.
    /// </summary>
    /// <param name="from">The start of the time range.</param>
    /// <param name="to">The end of the time range.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records within the specified time range.
    /// </returns>
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
            catch
            {
            }

            if (record != null)
                yield return record;
        }
    }

    async Task IRepository<EventDeliveryRecord, object>.AddAsync(EventDeliveryRecord entity, CancellationToken cancellationToken)
    {
        await RecordAsync(entity, cancellationToken);
    }

    Task IRepository<EventDeliveryRecord, object>.AddRangeAsync(IEnumerable<EventDeliveryRecord> entities, CancellationToken cancellationToken)
        => throw new NotSupportedException("Bulk add is not supported by the NDJson delivery log store.");

    Task<bool> IRepository<EventDeliveryRecord, object>.UpdateAsync(EventDeliveryRecord entity, CancellationToken cancellationToken)
        => throw new NotSupportedException("Update is not supported by the NDJson delivery log store.");

    Task<bool> IRepository<EventDeliveryRecord, object>.RemoveAsync(EventDeliveryRecord entity, CancellationToken cancellationToken)
        => throw new NotSupportedException("Remove is not supported by the NDJson delivery log store.");

    Task IRepository<EventDeliveryRecord, object>.RemoveRangeAsync(IEnumerable<EventDeliveryRecord> entities, CancellationToken cancellationToken)
        => throw new NotSupportedException("Bulk remove is not supported by the NDJson delivery log store.");

    Task<EventDeliveryRecord?> IRepository<EventDeliveryRecord, object>.FindAsync(object key, CancellationToken cancellationToken)
        => Task<EventDeliveryRecord?>.FromResult((EventDeliveryRecord?)null);

    object? IRepository<EventDeliveryRecord, object>.GetEntityKey(EventDeliveryRecord entity) => entity.Id;

    /// <summary>
    /// Releases the write lock semaphore used by the repository.
    /// </summary>
    public void Dispose()
    {
        _writeLock.Dispose();
    }
}
