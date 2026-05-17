namespace Deveel.Events;

/// <summary>
/// Provides configuration options for the <see cref="NdJsonEventDeliveryLogRepository"/>.
/// </summary>
public class NdJsonDeliveryLogOptions
{
    /// <summary>
    /// Gets or sets the directory path where NDJSON delivery log files are stored.
    /// Defaults to a temporary directory named "delivery-logs".
    /// </summary>
    public string DirectoryPath { get; set; } = Path.Combine(Path.GetTempPath(), "delivery-logs");

    /// <summary>
    /// Gets or sets the maximum size of a single NDJSON file in bytes before rolling to a new file.
    /// Defaults to 10 MB.
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
}
