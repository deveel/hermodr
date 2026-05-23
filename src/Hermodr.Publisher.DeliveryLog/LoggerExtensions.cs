using Microsoft.Extensions.Logging;

namespace Hermodr;

static partial class LoggerExtensions
{
    [LoggerMessage(33001, LogLevel.Debug, "Delivery record written: EventId={EventId}, Channel={Channel}, Outcome={Outcome}")]
    public static partial void LogDeliveryRecordWritten(this ILogger logger, string eventId, string? channel, EventDeliveryOutcome outcome);

    [LoggerMessage(33002, LogLevel.Error, "Failed to write delivery record for EventId={EventId}, Channel={Channel}")]
    public static partial void LogDeliveryRecordFailed(this ILogger logger, Exception exception, string? eventId, string? channel);

    [LoggerMessage(33003, LogLevel.Error, "Delivery log middleware error for event type {EventType}")]
    public static partial void LogDeliveryLogMiddlewareError(this ILogger logger, Exception exception, string? eventType);

    [LoggerMessage(33004, LogLevel.Debug, "Delivery log error handler recorded failure for EventId={EventId}, Channel={Channel}")]
    public static partial void LogDeliveryLogErrorHandlerRecorded(this ILogger logger, string eventId, string? channel);
}
