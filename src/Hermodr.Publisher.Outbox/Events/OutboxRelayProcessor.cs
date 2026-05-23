//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr;

/// <summary>
/// Implements the relay half of the Transactional Outbox pattern for
/// <typeparamref name="TMessage"/> entities: it reads all pending messages from the
/// <see cref="IOutboxMessageRepository{TMessage}"/>, forwards their
/// <see cref="CloudEvent"/> payloads through the configured <see cref="IEventPublisher"/>
/// pipeline using an <see cref="OutboxRelayPublishOptions"/> skip signal, and updates
/// each message status to <see cref="OutboxMessageStatus.Delivered"/> or
/// <see cref="OutboxMessageStatus.Failed"/> accordingly.
/// </summary>
/// <typeparam name="TMessage">
/// The outbox message entity type.  Must be a reference type and implement
/// <see cref="IOutboxMessage"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// This class contains the testable relay logic and is consumed by
/// <see cref="OutboxRelayService{TMessage}"/>, which drives it on a
/// <see cref="System.Threading.PeriodicTimer"/> cadence.
/// </para>
/// <para>
/// The relay forwards events by calling <see cref="IEventPublisher.PublishEventAsync"/>
/// with an <see cref="OutboxRelayPublishOptions"/> instance.  Any
/// <see cref="OutboxPublishChannel{TMessage}"/> present in the pipeline recognises the
/// signal and short-circuits without re-persisting the event, preventing an infinite loop
/// in same-process deployments.  In the more common cross-process model where the relay
/// runs in a separate application, no outbox channel is registered and the signal is
/// harmless.
/// </para>
/// <para>
/// The target publisher pipeline is selected by
/// <see cref="OutboxRelayOptions.TransportPublisherName"/> (empty string = default pipeline).
/// </para>
/// <para>
/// Because <see cref="IOutboxMessageRepository{TMessage}"/> is typically registered with
/// a scoped lifetime (wrapping a scoped database context), the processor creates a fresh
/// DI scope on every <see cref="ProcessPendingMessagesAsync"/> invocation.
/// </para>
/// </remarks>
internal sealed class OutboxRelayProcessor<TMessage> : IOutboxRelayProcessor
    where TMessage : class, IOutboxMessage
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxRelayOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises the processor with its DI dependencies.
    /// </summary>
    public OutboxRelayProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxRelayOptions> options,
        ILogger<OutboxRelayProcessor<TMessage>>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _options      = options.Value;
        _logger       = logger ?? NullLogger<OutboxRelayProcessor<TMessage>>.Instance;
    }

    /// <summary>
    /// Fetches all pending outbox messages, dispatches their <see cref="CloudEvent"/>
    /// payloads through the configured <see cref="IEventPublisher"/> pipeline (with an
    /// <see cref="OutboxRelayPublishOptions"/> skip signal), and updates message statuses.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var manager = sp.GetRequiredService<OutboxMessageManager<TMessage>>();

        // Resolve the publisher to use for forwarding dequeued messages to transport.
        // When a name is specified in the options, resolve the keyed pipeline; otherwise
        // fall back to the default (non-keyed) IEventPublisher registration.
        var publisher = !string.IsNullOrEmpty(_options.TransportPublisherName)
            ? sp.GetRequiredKeyedService<IEventPublisher>(_options.TransportPublisherName)
            : sp.GetRequiredService<IEventPublisher>();

        var pending = await manager.GetPendingMessagesAsync();

        if (pending.Count == 0)
        {
            _logger.LogNoOutboxMessagesPending();
            return;
        }

        // Honour the optional batch-size cap.
        IEnumerable<TMessage> batch = _options.MaxBatchSize > 0
            ? pending.Take(_options.MaxBatchSize)
            : pending;

        _logger.LogProcessingOutboxMessages(pending.Count);

        foreach (var message in batch)
        {
            await RelayMessageAsync(manager, publisher, message, cancellationToken);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task RelayMessageAsync(
        OutboxMessageManager<TMessage> manager,
        IEventPublisher publisher,
        TMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogRelayingOutboxMessage(message.Event.Type);

        try
        {
            // Bypass the middleware pipeline for this relay publish call so that other
            // middlewares (e.g. the subscription dispatcher) do not re-run.  The inner
            // OutboxRelayPublishOptions is forwarded to channel resolution so that any
            // OutboxPublishChannel present in the same pipeline can recognise the relay
            // signal and short-circuit without re-persisting the event.
            await publisher.PublishEventAsync(
                message.Event,
                EventPublishOptions.BypassPipeline(new OutboxRelayPublishOptions()),
                cancellationToken);

            var result = await manager.MarkDeliveredAsync(message);
            if (!result.IsSuccess())
                _logger.LogCouldNotMarkOutboxMessageDelivered(result.Error);
            else
                _logger.LogOutboxMessageDelivered(message.Event.Type);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorRelayingOutboxMessage(ex, message.Event.Type);

            try
            {
                var result = await manager.MarkFailedAsync(message, ex.Message);
                if (!result.IsSuccess())
                    _logger.LogCouldNotMarkOutboxMessageFailed(result.Error);
            }
            catch (Exception repoEx)
            {
                _logger.LogErrorMarkingOutboxMessageFailed(repoEx, message.Event.Type);
            }
        }
    }
}

