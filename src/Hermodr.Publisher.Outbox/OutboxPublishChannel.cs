//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;

using Deveel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr;

internal sealed class OutboxPublishChannel<TMessage> : EventPublishChannel<OutboxPublishOptions>
    where TMessage : class, IOutboxMessage
{
    private readonly OutboxTelemetry _telemetry = new();
    private readonly IOutboxMessageFactory<TMessage> _messageFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventSystemTime _systemTime;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises the channel with its dependencies.
    /// </summary>
    /// <param name="options">
    /// The channel-level default options resolved from the DI container via
    /// <see cref="IOptions{TOptions}"/>.
    /// </param>
    /// <param name="messageFactory">
    /// The factory that converts a <see cref="CloudEvent"/> into a
    /// <typeparamref name="TMessage"/> for persistence.
    /// </param>
    /// <param name="scopeFactory">
    /// Used to create a fresh DI scope on each publish call so that a scoped
    /// <see cref="OutboxMessageManager{TMessage}"/> (and its underlying repository)
    /// can be resolved without causing a scoped-in-singleton lifetime violation.
    /// </param>
    /// <param name="systemTime">
    /// The clock abstraction used to evaluate whether a scheduled publish time is
    /// in the future and should be deferred in the outbox.
    /// </param>
    /// <param name="validators">
    /// An optional collection of <see cref="IValidateOptions{TOptions}"/> services.
    /// When empty or <c>null</c> validation falls back to DataAnnotations.
    /// </param>
    /// <param name="logger">
    /// An optional logger; when <c>null</c> a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public OutboxPublishChannel(
        IOptions<OutboxPublishOptions> options,
        IOutboxMessageFactory<TMessage> messageFactory,
        IServiceScopeFactory scopeFactory,
        IEventSystemTime systemTime,
        IEnumerable<IValidateOptions<OutboxPublishOptions>>? validators = null,
        ILogger<OutboxPublishChannel<TMessage>>? logger = null)
        : base(options.Value, validators)
    {
        ArgumentNullException.ThrowIfNull(messageFactory);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(systemTime);

        _messageFactory = messageFactory;
        _scopeFactory   = scopeFactory;
        _systemTime     = systemTime;
        _logger = logger ?? NullLogger<OutboxPublishChannel<TMessage>>.Instance;
    }

    /// <inheritdoc/>
    protected override async Task PublishCoreAsync(
        CloudEvent @event,
        OutboxPublishOptions options,
        CancellationToken cancellationToken)
    {
        // Skip persistence when the publish was initiated by the relay processor.
        // This prevents an infinite loop in same-process deployments and is harmless
        // in cross-process deployments (where this channel is not registered at all).
        if (options is OutboxRelayPublishOptions)
        {
            _logger.LogSkippingRelayPublishSignal(@event.Type);
            return;
        }

        _logger.LogSavingEventToOutbox(@event.Type);

        var sw = Stopwatch.StartNew();
        using var activity = _telemetry.StartStoreActivity(@event.Type ?? "unknown");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<OutboxMessageManager<TMessage>>();

            var message = _messageFactory.Create(@event, options);
            var result = await manager.AddAsync(message, cancellationToken);

            if (!result.IsSuccess())
                throw new InvalidOperationException(
                    $"Failed to save event '{@event.Type}' to outbox: {result.Error}");

            if (options.ScheduleDeliveryAt is { } scheduleDeliveryAt && scheduleDeliveryAt > _systemTime.UtcNow)
            {
                var deferred = await manager.ScheduleDeferredDeliveryAsync(message, scheduleDeliveryAt);
                if (!deferred.IsSuccess())
                    throw new InvalidOperationException(
                        $"Failed to schedule event '{@event.Type}' in outbox: {deferred.Error}");
            }

            _logger.LogEventSavedToOutbox(@event.Type);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _telemetry.AddMessageAdded(@event.Type ?? "unknown");
        }
        catch (Exception ex)
        {
            _logger.LogErrorSavingEventToOutbox(ex, @event.Type);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            _telemetry.RecordStoreDuration(sw.Elapsed.TotalSeconds, @event.Type ?? "unknown");
        }
    }
}