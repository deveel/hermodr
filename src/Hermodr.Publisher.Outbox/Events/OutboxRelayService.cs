//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr;

/// <summary>
/// A long-running <see cref="BackgroundService"/> that periodically dequeues pending
/// messages from the outbox store and forwards them to the non-outbox transport channels
/// registered in the event publisher pipeline.
/// </summary>
/// <typeparam name="TMessage">
/// The outbox message entity type.  Must be a reference type and implement
/// <see cref="IOutboxMessage"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// The service uses a <see cref="PeriodicTimer"/> whose period is set by
/// <see cref="OutboxRelayOptions.Interval"/> (defaults to 30 seconds).  On each tick it
/// delegates all relay work to <see cref="OutboxRelayProcessor{TMessage}"/>, which
/// creates a DI scope, fetches pending messages and dispatches them.
/// </para>
/// <para>
/// The service is registered via
/// <see cref="OutboxChannelBuilder{TMessage}.WithRelay(Action{OutboxRelayOptions}?)"/>.
/// </para>
/// </remarks>
internal sealed class OutboxRelayService<TMessage> : OutboxRelayServiceBase
    where TMessage : class, IOutboxMessage
{
    private readonly OutboxRelayProcessor<TMessage> _processor;
    private readonly OutboxRelayOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises the relay service with its dependencies.
    /// </summary>
    public OutboxRelayService(
        OutboxRelayProcessor<TMessage> processor,
        IOptions<OutboxRelayOptions> options,
        ILogger<OutboxRelayService<TMessage>>? logger = null)
    {
        _processor = processor;
        _options   = options.Value;
        _logger    = logger ?? NullLogger<OutboxRelayService<TMessage>>.Instance;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogOutboxRelayStarted(_options.Interval);

        using var timer = new PeriodicTimer(_options.Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // WaitForNextTickAsync returns false when the timer is disposed.
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;

                _logger.LogOutboxRelayTick();
                await _processor.ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown – exit the loop gracefully.
                break;
            }
            catch (Exception ex)
            {
                // Log but keep the loop running so transient errors don't kill the service.
                _logger.LogErrorProcessingOutboxMessages(ex);
            }
        }

        _logger.LogOutboxRelayStopped();
    }
}
