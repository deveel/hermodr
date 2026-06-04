namespace Hermodr;

/// <summary>
/// Configuration options for the audit trail publish channel.
/// </summary>
public class AuditTrailPublishOptions : EventPublishOptions
{
}

/// <summary>
/// An internal marker subclass of <see cref="AuditTrailPublishOptions"/> used to signal
/// that the publish was initiated by a replay or relay process, causing the audit trail
/// channel to skip recording to prevent infinite loops.
/// </summary>
public sealed class BypassAuditTrailOptions : AuditTrailPublishOptions
{
}
