using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Hermodr;

[ExcludeFromCodeCoverage]
static partial class LoggerExtensions
{
    [LoggerMessage(39001, LogLevel.Debug, "Audit trail entry appended: EventId={EventId}, EventType={EventType} to file {FilePath}")]
    public static partial void LogAuditTrailEntryAppended(this ILogger logger, string eventId, string eventType, string filePath);

    [LoggerMessage(39002, LogLevel.Debug, "Audit trail entry read: Id={Id}, EventId={EventId}, EventType={EventType}")]
    public static partial void LogAuditTrailEntryRead(this ILogger logger, string id, string eventId, string eventType);

    [LoggerMessage(39003, LogLevel.Debug, "Audit trail file rolled: {FilePath}")]
    public static partial void LogAuditTrailFileRolled(this ILogger logger, string filePath);

    [LoggerMessage(39004, LogLevel.Warning, "Failed to delete old audit trail file {FilePath}")]
    public static partial void LogFailedToDeleteOldAuditTrailFile(this ILogger logger, Exception exception, string filePath);

    [LoggerMessage(39005, LogLevel.Warning, "Failed to clean up old audit trail files in directory {DirectoryPath}")]
    public static partial void LogFailedToCleanUpOldAuditTrailFiles(this ILogger logger, Exception exception, string directoryPath);

    [LoggerMessage(39006, LogLevel.Warning, "Failed to deserialize audit trail entry from file {FilePath} at line {LineNumber}")]
    public static partial void LogFailedToDeserializeAuditTrailEntry(this ILogger logger, Exception exception, string filePath, int lineNumber);

    [LoggerMessage(39007, LogLevel.Warning, "Failed to read audit trail file {FilePath}")]
    public static partial void LogFailedToReadAuditTrailFile(this ILogger logger, Exception exception, string filePath);
}
