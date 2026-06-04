using CloudNative.CloudEvents;

namespace Hermodr;

/// <summary>
/// Provides a service to append events to the audit trail.
/// </summary>
public interface IAuditTrailWriter
{
    /// <summary>
    /// Appends a CloudEvent to the audit trail.
    /// </summary>
    /// <param name="event">
    /// The CloudEvent to append.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    Task AppendAsync(CloudEvent @event, CancellationToken cancellationToken = default);
}
