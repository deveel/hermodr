using System.Diagnostics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr;

/// <summary>
/// An <see cref="EventPublishChannel{TOptions}"/> implementation that records every
/// published CloudEvent to the audit trail for compliance auditing and debugging.
/// </summary>
/// <remarks>
/// This channel receives all events dispatched through the publisher pipeline.
/// To record only specific event types, use the generic
/// <see cref="AuditTrailPublishChannel{TEvent}"/> variant instead.
/// </remarks>
public class AuditTrailPublishChannel : EventPublishChannel<AuditTrailPublishOptions>
{
    private readonly AuditTrailTelemetry _telemetry = new();

    /// <summary>
    /// The audit trail writer used to append events.
    /// </summary>
    protected readonly IAuditTrailWriter Writer;

    private readonly ILogger _logger;

    /// <summary>
    /// Creates a new instance of <see cref="AuditTrailPublishChannel"/>.
    /// </summary>
    /// <param name="writer">
    /// The audit trail writer used to append events.
    /// </param>
    /// <param name="options">
    /// The channel-level default options resolved from the DI container.
    /// </param>
    /// <param name="validators">
    /// An optional collection of <see cref="IValidateOptions{AuditTrailPublishOptions}"/> services.
    /// </param>
    /// <param name="logger">
    /// An optional logger.
    /// </param>
    public AuditTrailPublishChannel(
        IAuditTrailWriter writer,
        IOptions<AuditTrailPublishOptions> options,
        IEnumerable<IValidateOptions<AuditTrailPublishOptions>>? validators = null,
        ILogger<AuditTrailPublishChannel>? logger = null)
        : base(options.Value, validators)
    {
        Writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _logger = logger ?? NullLogger<AuditTrailPublishChannel>.Instance;
    }

    /// <inheritdoc/>
    protected override async Task PublishCoreAsync(
        CloudEvent @event,
        AuditTrailPublishOptions options,
        CancellationToken cancellationToken)
    {
        if (options is BypassAuditTrailOptions)
        {
            _logger.LogAuditTrailRecordSkipped(@event.Type);
            return;
        }

        var sw = Stopwatch.StartNew();
        using var activity = _telemetry.StartAppendActivity(@event.Type ?? "unknown");

        try
        {
            await Writer.AppendAsync(@event, cancellationToken);
            _logger.LogAuditTrailRecordAppended(@event.Type);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            _logger.LogAuditTrailRecordFailed(ex, @event.Type);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            _telemetry.RecordAppendDuration(sw.Elapsed.TotalSeconds, @event.Type ?? "unknown");
        }
    }
}
