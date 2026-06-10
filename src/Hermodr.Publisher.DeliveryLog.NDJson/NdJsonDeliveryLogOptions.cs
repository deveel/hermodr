namespace Hermodr;

/// <summary>
/// Configuration options for the NDJson-based event delivery log repository.
/// </summary>
public class NdJsonDeliveryLogOptions
{
    /// <summary>
    /// Gets or sets the directory path where delivery log files are stored.
    /// </summary>
    public string DirectoryPath { get; set; } = Path.Join(Path.GetTempPath(), "delivery-logs");

    /// <summary>
    /// Gets or sets the maximum file size in bytes before a new file is created.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the time interval after which a new log file is created.
    /// When <c>null</c>, file rolling is based only on size.
    /// </summary>
    public TimeSpan? RollInterval { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of log files to retain. Older files are deleted when the limit is exceeded.
    /// </summary>
    public int MaxFileCount { get; set; } = 30;
}
