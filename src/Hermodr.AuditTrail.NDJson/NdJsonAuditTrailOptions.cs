namespace Hermodr;

/// <summary>
/// Provides configuration options for the <see cref="NdJsonAuditTrail"/> storage backend.
/// </summary>
public class NdJsonAuditTrailOptions
{
    /// <summary>
    /// Gets or sets the directory path where NDJSON audit trail files are stored.
    /// Defaults to a temporary directory named "audit-trail".
    /// </summary>
    public string DirectoryPath { get; set; } = Path.Join(Path.GetTempPath(), "audit-trail");

    /// <summary>
    /// Gets or sets the maximum size of a single NDJSON file in bytes before rolling
    /// to a new file. Defaults to 10 MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the time interval after which a new NDJSON file is created.
    /// When <c>null</c>, rolling by time is disabled.
    /// </summary>
    public TimeSpan? RollInterval { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of NDJSON files to retain on disk.
    /// When the limit is exceeded, the oldest files are deleted. Defaults to 30.
    /// </summary>
    public int MaxFileCount { get; set; } = 30;

    /// <summary>
    /// Gets or sets the prefix used when generating NDJSON file names.
    /// Files are named using the pattern <c>{FileNamePrefix}-yyyyMMdd-HHmmss.ndjson</c>.
    /// </summary>
    public string FileNamePrefix { get; set; } = "audit-trail";

    /// <summary>
    /// Gets or sets the file extension used for NDJSON files (including the leading dot).
    /// Defaults to <c>.ndjson</c>.
    /// </summary>
    public string FileExtension { get; set; } = ".ndjson";

    /// <summary>
    /// Gets or sets the buffer size, in bytes, used when opening NDJSON files for
    /// reading. A larger buffer reduces the number of I/O calls when streaming
    /// large files. Defaults to 4096 bytes.
    /// </summary>
    public int ReadBufferSize { get; set; } = 4096;
}
