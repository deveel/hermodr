//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;

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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the channel with its dependencies.
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
    /// <see cref="IOutboxMessageStore{TMessage}"/> can be resolved without causing
    /// a scoped-in-singleton lifetime violation.
    /// </param>
    /// <param name="logger">
    /// An optional logger; when <c>null</c> a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public OutboxPublishChannel(
        IOptions<OutboxPublishOptions> options,
        IOutboxMessageFactory<TMessage> messageFactory,
        IServiceScopeFactory scopeFactory,
        IServiceProvider serviceProvider,
        ILogger<OutboxPublishChannel<TMessage>>? logger = null)
        : base(options.Value, serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(messageFactory);
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _messageFactory = messageFactory;
        _scopeFactory   = scopeFactory;
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
            var store = scope.ServiceProvider.GetRequiredService<IOutboxMessageStore<TMessage>>();

            var message = _messageFactory.Create(@event, options);
            await store.AddAsync(message, cancellationToken);

            if (options.ScheduleDeliveryAt is { } scheduleDeliveryAt && scheduleDeliveryAt > ServiceProvider.GetRequiredService<IEventSystemTime>().UtcNow)
            {
                await store.SetDeferredAsync(message, scheduleDeliveryAt, cancellationToken);
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