namespace Deveel.Events;

/// <summary>
/// An error handler that records failed event delivery attempts into an
/// <see cref="IEventPublishDeliveryLog"/>.
/// </summary>
public sealed class DeliveryLogPublishErrorHandler : IEventPublishErrorHandler
{
    private readonly IEventPublishDeliveryLog _deliveryLog;
    private readonly IEventSystemTime _systemTime;

    /// <summary>
    /// Creates a new instance of the <see cref="DeliveryLogPublishErrorHandler"/>.
    /// </summary>
    /// <param name="deliveryLog">
    /// The service used to record delivery attempts.
    /// </param>
    /// <param name="systemTime">
    /// An optional service for obtaining the current UTC time; defaults to <see cref="EventSystemTime.Instance"/>.
    /// </param>
    public DeliveryLogPublishErrorHandler(
        IEventPublishDeliveryLog deliveryLog,
        IEventSystemTime? systemTime = null)
    {
        _deliveryLog = deliveryLog ?? throw new ArgumentNullException(nameof(deliveryLog));
        _systemTime = systemTime ?? EventSystemTime.Instance;
    }

    /// <summary>
    /// Handles the error by recording a failed delivery record.
    /// </summary>
    /// <param name="context">
    /// The context of the publish error.
    /// </param>
    public async Task HandleAsync(EventPublishErrorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Stage != EventPublishStage.ChannelPublish ||
            context.Event == null)
            return;

        var record = new EventDeliveryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Event = context.Event,
            PublisherName = context.PublisherName,
            ChannelName = context.ChannelName,
            ChannelType = context.ChannelType?.AssemblyQualifiedName ?? context.ChannelType?.FullName,
            AttemptNumber = 1,
            Timestamp = _systemTime.UtcNow,
            Outcome = EventDeliveryOutcome.Failed,
            ErrorCode = context.Exception?.GetType().Name,
            ErrorMessage = context.Exception?.Message,
            ElapsedTime = TimeSpan.Zero
        };

        await _deliveryLog.RecordAsync(record, context.CancellationToken);
    }
}
