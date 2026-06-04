using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Hermodr;

[ExcludeFromCodeCoverage]
static partial class LoggerExtensions
{
    [LoggerMessage(37001, LogLevel.Debug, "Audit trail entry appended: EventId={EventId}, EventType={EventType}")]
    public static partial void LogAuditTrailEntryAppended(this ILogger logger, string eventId, string eventType);
}
