using System.ComponentModel.DataAnnotations;

namespace Hermodr;

public class DbEventDeliveryRecord
{
    [Key]
    public string Id { get; set; } = null!;

    public string EventId { get; set; } = null!;

    public string? EventType { get; set; }

    public string? EventData { get; set; }

    public string? PublisherName { get; set; }

    public string? ChannelName { get; set; }

    public string? ChannelType { get; set; }

    public int AttemptNumber { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string Outcome { get; set; } = null!;

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public long ElapsedTimeTicks { get; set; }
}
