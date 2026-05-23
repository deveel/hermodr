using Deveel;
using Deveel.Data;
using Deveel.Data.Caching;

using Microsoft.Extensions.Logging;

namespace Hermodr;

/// <summary>
/// A domain-oriented manager for <typeparamref name="TMessage"/> outbox entities that
/// extends the standard <see cref="EntityManager{TEntity,TKey}"/> with outbox-specific
/// state-transition helpers.
/// </summary>
/// <typeparam name="TMessage">
/// The outbox message entity type.  Must be a reference type and implement
/// <see cref="IOutboxMessage"/>.
/// </typeparam>
public class OutboxMessageManager<TMessage> : EntityManager<TMessage, string>
    where TMessage : class, IOutboxMessage
{
    /// <summary>
    /// Initialises the manager with its dependencies.
    /// </summary>
    /// <param name="repository">The outbox message repository.</param>
    /// <param name="validator">Optional entity validator.</param>
    /// <param name="cache">Optional entity cache.</param>
    /// <param name="systemTime">Optional system-time abstraction.</param>
    /// <param name="errorFactory">Optional operation-error factory.</param>
    /// <param name="services">Optional service provider.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public OutboxMessageManager(
        IOutboxMessageRepository<TMessage> repository, 
        IEntityValidator<TMessage, string>? validator = null, 
        IEntityCache<TMessage>? cache = null, 
        ISystemTime? systemTime = null, 
        IOperationErrorFactory<TMessage>? errorFactory = null, 
        IServiceProvider? services = null, ILoggerFactory? loggerFactory = null) : base(repository, validator, cache, systemTime, errorFactory, services, loggerFactory)
    {
    }

    /// <summary>Gets the typed outbox message repository.</summary>
    protected IOutboxMessageRepository<TMessage> MessageRepository => (IOutboxMessageRepository<TMessage>) Repository;

    /// <summary>
    /// Returns the current <see cref="OutboxMessageStatus"/> of <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message to query.</param>
    /// <returns>The current status of the message.</returns>
    public Task<OutboxMessageStatus> GetStatusAsync(TMessage message) {
        return MessageRepository.GetStatusAsync(message, CancellationToken);
    }

    /// <summary>
    /// Returns messages eligible for relay, up to the optional <paramref name="limit"/>.
    /// </summary>
    /// <param name="limit">
    /// Maximum number of messages to return; <c>null</c> returns all pending messages.
    /// </param>
    /// <returns>A read-only list of pending messages.</returns>
    public Task<IReadOnlyList<TMessage>> GetPendingMessagesAsync(int? limit = null) {
        return MessageRepository.GetPendingMessagesAsync(limit, CancellationToken);
    }

    /// <summary>
    /// Transitions <paramref name="message"/> to <see cref="OutboxMessageStatus.Failed"/>
    /// and persists the <paramref name="errorMessage"/>.
    /// </summary>
    /// <param name="message">The message to mark as failed.</param>
    /// <param name="errorMessage">A description of the failure reason.</param>
    /// <returns>
    /// <see cref="OperationResult.NotChanged"/> when the message is already failed;
    /// an error result when the message is already delivered or in an invalid state;
    /// otherwise the result of the update operation.
    /// </returns>
    public virtual async Task<OperationResult> MarkFailedAsync(TMessage message, string errorMessage) {
        var status = await GetStatusAsync(message);
        if (status == OutboxMessageStatus.Failed)
            return OperationResult.NotChanged;
        
        if (status == OutboxMessageStatus.Delivered)
            return OperationResult.Fail("OUT0031", "OUTBOX", "Cannot mark a delivered message as failed.");
        
        if (status != OutboxMessageStatus.Pending && status != OutboxMessageStatus.Sending)
            return OperationResult.Fail("OUT0032", "OUTBOX", $"Cannot mark a message with status {status} as failed.");

        await MessageRepository.SetFailedAsync(message, errorMessage, CancellationToken);
        return await UpdateAsync(message);
    }

    /// <summary>
    /// Transitions <paramref name="message"/> to <see cref="OutboxMessageStatus.Delivered"/>.
    /// </summary>
    /// <param name="message">The message to mark as delivered.</param>
    /// <returns>
    /// <see cref="OperationResult.NotChanged"/> when the message is already delivered;
    /// an error result when the message is failed or in an invalid state;
    /// otherwise the result of the update operation.
    /// </returns>
    public virtual async Task<OperationResult> MarkDeliveredAsync(TMessage message) {
        var status = await GetStatusAsync(message);
        if (status == OutboxMessageStatus.Delivered)
            return OperationResult.NotChanged;
        
        if (status == OutboxMessageStatus.Failed)
            return OperationResult.Fail("OUT0033", "OUTBOX", "Cannot mark a failed message as delivered.");
        
        if (status != OutboxMessageStatus.Pending && status != OutboxMessageStatus.Sending)
            return OperationResult.Fail("OUT0034", "OUTBOX", $"Cannot mark a message with status {status} as delivered.");

        await MessageRepository.SetDeliveredAsync(message, CancellationToken);
        return await UpdateAsync(message);
    }

    /// <summary>
    /// Defers the first delivery attempt for <paramref name="message"/> until
    /// <paramref name="scheduledAt"/>.
    /// </summary>
    /// <param name="message">The message to defer.</param>
    /// <param name="scheduledAt">The UTC time when delivery becomes eligible.</param>
    /// <returns>
    /// An error result when the message is already failed or delivered;
    /// otherwise the result of the update operation.
    /// </returns>
    public virtual async Task<OperationResult> ScheduleDeferredDeliveryAsync(TMessage message, DateTimeOffset scheduledAt) {
        var status = await GetStatusAsync(message);
        if (status == OutboxMessageStatus.Failed)
            return OperationResult.Fail("OUT0038", "OUTBOX", "Cannot defer a failed message.");

        if (status == OutboxMessageStatus.Delivered)
            return OperationResult.Fail("OUT0039", "OUTBOX", "Cannot defer a delivered message.");

        if (status != OutboxMessageStatus.Pending && status != OutboxMessageStatus.Sending)
            return OperationResult.Fail("OUT0040", "OUTBOX", $"Cannot defer a message with status {status}.");

        await MessageRepository.SetDeferredAsync(message, scheduledAt, CancellationToken);
        return await UpdateAsync(message);
    }

    /// <summary>
    /// Schedules a retry for <paramref name="message"/> by recording the
    /// <paramref name="errorMessage"/> and the <paramref name="nextRetryAt"/> timestamp.
    /// </summary>
    /// <param name="message">The message to retry.</param>
    /// <param name="errorMessage">A description of the failure that triggered the retry.</param>
    /// <param name="nextRetryAt">The UTC time at which the relay should next attempt delivery.</param>
    /// <returns>
    /// An error result when the message is already failed, delivered, or in an invalid state;
    /// otherwise the result of the update operation.
    /// </returns>
    public virtual async Task<OperationResult> ScheduleRetryAsync(TMessage message, string errorMessage, DateTimeOffset nextRetryAt) {
        var status = await GetStatusAsync(message);
        if (status == OutboxMessageStatus.Failed)
            return OperationResult.Fail("OUT0035", "OUTBOX", "Cannot schedule a retry for a failed message.");
        
        if (status == OutboxMessageStatus.Delivered)
            return OperationResult.Fail("OUT0036", "OUTBOX", "Cannot schedule a retry for a delivered message.");
        
        if (status != OutboxMessageStatus.Pending && status != OutboxMessageStatus.Sending)
            return OperationResult.Fail("OUT0037", "OUTBOX", $"Cannot schedule a retry for a message with status {status}.");

        await MessageRepository.SetRetryAsync(message, errorMessage, nextRetryAt, CancellationToken);
        return await UpdateAsync(message);
    }
}