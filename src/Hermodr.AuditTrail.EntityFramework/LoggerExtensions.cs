using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Hermodr;

[ExcludeFromCodeCoverage]
static partial class LoggerExtensions
{
    [LoggerMessage(36001, LogLevel.Debug, "Audit trail entry saved to database: EventId={EventId}, EventType={EventType}")]
    public static partial void LogAuditTrailEntrySavedToDatabase(this ILogger logger, string eventId, string eventType);
}
