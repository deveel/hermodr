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

namespace Deveel.Events;

/// <summary>
/// An <see cref="EventPublishChannel{TOptions}"/> implementation that persists events
/// to a transactional outbox store rather than dispatching them immediately.
/// </summary>
/// <typeparam name="TMessage">
/// The outbox message entity type created and stored by this channel.
/// Must be a reference type and implement <see cref="IOutboxMessage"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// This channel implements the <em>Transactional Outbox</em> pattern: instead of
/// publishing a <see cref="CloudEvent"/> directly to a broker, it serialises the event
/// into a <typeparamref name="TMessage"/> record and writes it atomically to the same
/// transactional store as the business data.  A separate relay process (e.g., a hosted
/// service or a Change Data Capture listener) then reads the pending messages and
/// forwards them to the actual broker.
/// </para>
/// <para>
/// The channel relies on two collaborators supplied through the DI container:
/// <list type="bullet">
///   <item>
///     An <see cref="IOutboxMessageFactory{TMessage}"/> that turns a
///     <see cref="CloudEvent"/> into a <typeparamref name="TMessage"/> entity.
///   </item>
///   <item>
///     An <see cref="IOutboxMessageRepository{TMessage}"/> that persists the entity.
///   </item>
/// </list>
/// </para>
/// <para>
/// Because the channel is registered as a singleton (publisher channels are always
/// singletons) and <see cref="OutboxMessageManager{TMessage}"/> may be scoped (e.g.
/// when the underlying repository wraps a scoped EF Core <c>DbContext</c>), the channel
/// resolves the manager from a fresh <see cref="IServiceScope"/> on every publish call
/// rather than capturing it at construction time.  This is the same pattern used by
/// <see cref="OutboxRelayProcessor{TMessage}"/>.
/// </para>
/// </remarks>
internal sealed class OutboxPublishChannel<TMessage> : EventPublishChannel<OutboxPublishOptions>
    where TMessage : class, IOutboxMessage
{
    private readonly IOutboxMessageFactory<TMessage> _messageFactory;
    private readonly IServiceScopeFactory _scopeFactory;
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
        IEnumerable<IValidateOptions<OutboxPublishOptions>>? validators = null,
        ILogger<OutboxPublishChannel<TMessage>>? logger = null)
        : base(options.Value, validators)
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

        try
        {
            // Resolve the manager from a fresh scope so the channel (singleton) does not
            // capture a scoped dependency.  Singleton repositories (in-memory fakes, etc.)
            // are returned as-is by the DI container regardless of the scope lifetime.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<OutboxMessageManager<TMessage>>();

            var message = _messageFactory.Create(@event, options);
            var result = await manager.AddAsync(message, cancellationToken);

            if (!result.IsSuccess())
                throw new InvalidOperationException(
                    $"Failed to save event '{@event.Type}' to outbox: {result.Error}");

            _logger.LogEventSavedToOutbox(@event.Type);
        }
        catch (Exception ex)
        {
            _logger.LogErrorSavingEventToOutbox(ex, @event.Type);
            throw;
        }
    }
}