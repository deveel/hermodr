using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.IO.Abstractions;

namespace Hermodr;

/// <summary>
/// An implementation of <see cref="IAuditTrailWriter"/> and
/// <see cref="IAuditTrailReader{TEntry}"/> that stores audit trail entries
/// as newline-delimited JSON (NDJSON) files on disk, with automatic file rolling
/// and cleanup.
/// </summary>
/// <remarks>
/// All I/O is performed through an injected <see cref="IFileSystem"/>, which allows
/// the local file system to be replaced with another storage backend (for example,
/// Azure Blob Storage or Amazon S3) by supplying a compatible
/// <see cref="IFileSystem"/> implementation.
/// </remarks>
public class NdJsonAuditTrail : IAuditTrailWriter, IAuditTrailReader<AuditTrailEntry>, IDisposable
{
    private readonly NdJsonAuditTrailOptions _options;
    private readonly IFileSystem _fileSystem;
    private readonly IEventSystemTime _systemTime;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private string? _currentFilePath;
    private long _currentFileSize;
    private DateTimeOffset? _currentFileCreatedAt;
    private long _fileSequence;

    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Creates a new instance of <see cref="NdJsonAuditTrail"/>.
    /// </summary>
    /// <param name="options">
    /// The options used to configure the NDJSON storage backend.
    /// </param>
    /// <param name="fileSystem">
    /// The filesystem abstraction used to perform all I/O. Defaults to
    /// <see cref="FileSystem"/>, which uses the local file system.
    /// Inject a different implementation to use another storage backend
    /// (for example, Azure Blob Storage or Amazon S3).
    /// </param>
    /// <param name="systemTime">
    /// An optional service for obtaining the current UTC time; defaults to
    /// <see cref="EventSystemTime.Instance"/>.
    /// </param>
    /// <param name="logger">
    /// An optional logger for diagnostic output.
    /// </param>
    public NdJsonAuditTrail(
        IOptions<NdJsonAuditTrailOptions> options,
        IFileSystem? fileSystem = null,
        IEventSystemTime? systemTime = null,
        ILogger<NdJsonAuditTrail>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _fileSystem = fileSystem ?? new FileSystem();
        _systemTime = systemTime ?? EventSystemTime.Instance;
        _logger = logger ?? NullLogger<NdJsonAuditTrail>.Instance;
        _jsonOptions = DefaultJsonOptions;

        EnsureDirectoryExists();
    }

    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    public string ProviderName => "NDJson";

    private void EnsureDirectoryExists()
    {
        if (!_fileSystem.Directory.Exists(_options.DirectoryPath))
            _fileSystem.Directory.CreateDirectory(_options.DirectoryPath);
    }

    /// <summary>
    /// Appends a CloudEvent to the audit trail by writing it as a JSON line
    /// to the current NDJSON file, creating a new file when the configured
    /// size or time rolling conditions are met.
    /// </summary>
    /// <param name="event">
    /// The CloudEvent to append.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    public async Task AppendAsync(CloudEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var entry = AuditTrailEntry.FromCloudEvent(@event);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(entry, _jsonOptions);
            var writeByteCount = Encoding.UTF8.GetByteCount(json) + 1;

            var filePath = ResolveFilePath(writeByteCount);

            await using var stream = _fileSystem.File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteLineAsync(json);

            _logger.LogAuditTrailEntryAppended(entry.EventId, entry.EventType, filePath);

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
            _fileSequence++;
            var timestamp = now.ToString("yyyyMMdd-HHmmss");
            var fileName = $"{_options.FileNamePrefix}-{timestamp}-{_fileSequence:D6}{_options.FileExtension}";
            _currentFilePath = _fileSystem.Path.Combine(_options.DirectoryPath, fileName);
            _currentFileCreatedAt = now;
            _currentFileSize = 0;

            _logger.LogAuditTrailFileRolled(_currentFilePath!);
        }

        _currentFileSize += writeByteCount;
        return _currentFilePath!;
    }

    private void CleanupOldFiles()
    {
        if (_options.MaxFileCount <= 0) return;

        try
        {
            var pattern = $"{_options.FileNamePrefix}-*{_options.FileExtension}";
            var files = _fileSystem.Directory
                .GetFiles(_options.DirectoryPath, pattern)
                .OrderByDescending(f => f)
                .ToList();

            if (files.Count <= _options.MaxFileCount)
                return;

            foreach (var file in files.Skip(_options.MaxFileCount))
            {
                try
                {
                    _fileSystem.File.Delete(file);
                }
                catch (IOException ex)
                {
                    _logger.LogFailedToDeleteOldAuditTrailFile(ex, file);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogFailedToDeleteOldAuditTrailFile(ex, file);
                }
            }
        }
        catch (IOException ex)
        {
            _logger.LogFailedToCleanUpOldAuditTrailFiles(ex, _options.DirectoryPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogFailedToCleanUpOldAuditTrailFiles(ex, _options.DirectoryPath);
        }
    }

    /// <summary>
    /// Reads events from the NDJSON audit trail, optionally filtered by the query.
    /// </summary>
    /// <remarks>
    /// Files are opened with <see cref="FileShare.ReadWrite"/> so that concurrent
    /// writers are never blocked by an in-progress read. Each NDJSON file is
    /// streamed asynchronously line by line, so the full file content is never
    /// loaded into memory. The matched entries are buffered briefly in memory
    /// only to honor the chronological ordering contract; the raw file content
    /// is consumed incrementally.
    /// </remarks>
    /// <param name="query">
    /// An optional query to filter the results.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    /// <returns>
    /// An asynchronous enumerable of audit trail entries matching the query,
    /// yielded in chronological order.
    /// </returns>
    public async IAsyncEnumerable<AuditTrailEntry> ReadAsync(
        AuditTrailStreamQuery? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.Directory.Exists(_options.DirectoryPath))
            yield break;

        var pattern = $"{_options.FileNamePrefix}-*{_options.FileExtension}";
        var files = _fileSystem.Directory
            .GetFiles(_options.DirectoryPath, pattern)
            .OrderBy(f => f, StringComparer.Ordinal);

        var collected = new List<AuditTrailEntry>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await foreach (var entry in ReadEntriesFromFileAsync(file, cancellationToken))
                {
                    if (MatchesQuery(entry, query))
                        collected.Add(entry);
                }
            }
            catch (FileNotFoundException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                yield break;
            }
            catch (IOException ex)
            {
                _logger.LogFailedToReadAuditTrailFile(ex, file);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogFailedToReadAuditTrailFile(ex, file);
            }
        }

        foreach (var entry in collected.OrderBy(e => e.Timestamp))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogAuditTrailEntryRead(entry.Id, entry.EventId, entry.EventType);
            yield return entry;
        }
    }

    private async IAsyncEnumerable<AuditTrailEntry> ReadEntriesFromFileAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var streamOptions = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite,
            BufferSize = _options.ReadBufferSize,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        };
        await using Stream stream = _fileSystem.File.Open(filePath, streamOptions);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var lineNumber = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                yield break;

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            AuditTrailEntry? entry = null;
            try
            {
                entry = JsonSerializer.Deserialize<AuditTrailEntry>(line, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogFailedToDeserializeAuditTrailEntry(ex, filePath, lineNumber);
            }

            if (entry != null)
                yield return entry;
        }
    }

    private static bool MatchesQuery(AuditTrailEntry entry, AuditTrailStreamQuery? query)
    {
        if (query is null)
            return true;

        if (query.EventType is not null && entry.EventType != query.EventType)
            return false;
        if (query.EventDataClassName is not null && entry.EventDataClassName != query.EventDataClassName)
            return false;
        if (query.Source is not null && entry.Source != query.Source)
            return false;
        if (query.Subject is not null && entry.Subject != query.Subject)
            return false;
        if (query.From.HasValue && entry.Timestamp < query.From.Value)
            return false;
        if (query.To.HasValue && entry.Timestamp > query.To.Value)
            return false;
        if (query.Attributes is not null && !query.Attributes.All(attr =>
                entry.ExtensionAttributes is not null
                && entry.ExtensionAttributes.TryGetValue(attr.Key, out var v)
                && v == attr.Value))
            return false;
        return true;
    }

    /// <summary>
    /// Releases the write lock semaphore used by this audit trail.
    /// </summary>
    public void Dispose()
    {
        _writeLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
