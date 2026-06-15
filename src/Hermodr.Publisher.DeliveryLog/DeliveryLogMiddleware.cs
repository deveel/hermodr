using CloudNative.CloudEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hermodr;

/// <summary>
/// A middleware that intercepts event publish operations and records delivery
/// attempts into an <see cref="IEventPublishDeliveryLog"/>.
/// </summary>
public class DeliveryLogMiddleware : IEventMiddleware
{
    private readonly IEventPublishDeliveryLog _deliveryLog;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Creates a new instance of the <see cref="DeliveryLogMiddleware"/>.
    /// </summary>
    /// <param name="deliveryLog">
    /// The service used to record delivery attempts.
    /// </param>
    /// <param name="logger">
    /// An optional logger for diagnostic output.
    /// </param>
    /// <param name="serviceProvider">
    /// The service provider for lazy resolution of dependencies.
    /// </param>
    public DeliveryLogMiddleware(
        IEventPublishDeliveryLog deliveryLog,
        ILogger<DeliveryLogMiddleware>? logger = null,
        IServiceProvider? serviceProvider = null)
    {
        _deliveryLog = deliveryLog ?? throw new ArgumentNullException(nameof(deliveryLog));
        _logger = logger ?? NullLogger<DeliveryLogMiddleware>.Instance;
        _serviceProvider = serviceProvider!;
    }

    private IEventSystemTime SystemTime => _serviceProvider.GetRequiredService<IEventSystemTime>();

    /// <summary>
    /// Invokes the middleware, recording the delivery outcome after the next delegate completes.
    /// </summary>
    /// <param name="context">
    /// The event context for the publish operation.
    /// </param>
    /// <param name="next">
    /// The next delegate in the middleware pipeline.
    /// </param>
    public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var startTime = SystemTime.UtcNow;
        var channelName = (context.Options as INamedChannelFilter)?.ChannelName;
        var attempt = GetAttemptNumber(context);
        var outcome = EventDeliveryOutcome.Succeeded;
        string? errorCode = null;
        string? errorMessage = null;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            outcome = EventDeliveryOutcome.Failed;
            errorCode = ex.GetType().Name;
            errorMessage = ex.Message;
            _logger.LogDeliveryLogMiddlewareError(ex, context.Event?.Type);
            throw;
        }
        finally
        {
            var elapsed = SystemTime.UtcNow - startTime;

            var record = new EventDeliveryRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Event = context.Event,
                ChannelName = channelName,
                AttemptNumber = attempt,
                Timestamp = startTime,
                Outcome = outcome,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                ElapsedTime = elapsed
            };

            try
            {
                await _deliveryLog.RecordAsync(record, context.CancellationToken);
                _logger.LogDeliveryRecordWritten(record.Event?.Id, record.ChannelName, record.Outcome);
            }
            catch (IOException ex)
            {
                _logger.LogDeliveryRecordFailed(ex, record.Event?.Id, record.ChannelName);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogDeliveryRecordFailed(ex, record.Event?.Id, record.ChannelName);
            }
        }
    }

    private static int GetAttemptNumber(EventContext context)
    {
        const string key = "Hermodr.DeliveryLog.AttemptNumber";
        if (context.Items.TryGetValue(key, out var value) && value is int existing)
        {
            context.Items[key] = existing + 1;
            return existing;
        }

        context.Items[key] = 2;
        return 1;
    }
}
