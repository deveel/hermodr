namespace Hermodr;

public class NdJsonDeliveryLogOptions
{
    public string DirectoryPath { get; set; } = Path.Join(Path.GetTempPath(), "delivery-logs");

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    public TimeSpan? RollInterval { get; set; }

    public int MaxFileCount { get; set; } = 30;
}
