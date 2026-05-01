using Deveel.Data;
using Deveel.Data.Caching;

using Microsoft.Extensions.Logging;

namespace Deveel.Events;

public class OutboxMessageManager<TMessage> : EntityManager<TMessage, string>
    where TMessage : class, IOutboxMessage
{
    public OutboxMessageManager(
        IOutboxMessageRepository<TMessage> repository, 
        IEntityValidator<TMessage, string>? validator = null, 
        IEntityCache<TMessage>? cache = null, 
        ISystemTime? systemTime = null, 
        IOperationErrorFactory<TMessage>? errorFactory = null, 
        IServiceProvider? services = null, ILoggerFactory? loggerFactory = null) : base(repository, validator, cache, systemTime, errorFactory, services, loggerFactory)
    {
    }
    
    protected IOutboxMessageRepository<TMessage> MessageRepository => (IOutboxMessageRepository<TMessage>) Repository;
    
    public Task<OutboxMessageStatus> GetStatusAsync(TMessage message) {
        return MessageRepository.GetStatusAsync(message, CancellationToken);
    }

    public Task<IReadOnlyList<TMessage>> GetPendingMessagesAsync(int? limit = null) {
        return MessageRepository.GetPendingMessagesAsync(limit, CancellationToken);
    }
    
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