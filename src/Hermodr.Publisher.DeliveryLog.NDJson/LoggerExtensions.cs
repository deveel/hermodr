using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Hermodr.NDJson;

[ExcludeFromCodeCoverage]
static partial class LoggerExtensions
{
    [LoggerMessage(33005, LogLevel.Warning, "Failed to delete old delivery log file: {FilePath}")]
    public static partial void LogFailedToDeleteOldDeliveryLogFile(this ILogger logger, Exception ex, string filePath);

    [LoggerMessage(33006, LogLevel.Warning, "Failed to clean up old delivery log files.")]
    public static partial void LogFailedToCleanUpOldDeliveryLogFiles(this ILogger logger, Exception ex);
}
