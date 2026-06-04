using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Hermodr;

[ExcludeFromCodeCoverage]
static partial class LoggerExtensions
{
    [LoggerMessage(34001, LogLevel.Debug, "Audit trail record appended for event type {EventType}")]
    public static partial void LogAuditTrailRecordAppended(this ILogger logger, string? eventType);

    [LoggerMessage(34002, LogLevel.Error, "Failed to append audit trail record for event type {EventType}")]
    public static partial void LogAuditTrailRecordFailed(this ILogger logger, Exception exception, string? eventType);

    [LoggerMessage(34003, LogLevel.Debug, "Skipping audit trail record for event type {EventType} (bypass signal)")]
    public static partial void LogAuditTrailRecordSkipped(this ILogger logger, string? eventType);
}
