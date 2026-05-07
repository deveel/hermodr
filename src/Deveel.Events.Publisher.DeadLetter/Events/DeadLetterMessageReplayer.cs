//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Deveel.Events;

/// <summary>
/// Replays persisted dead-letter messages through the configured publisher pipeline.
/// </summary>
public class DeadLetterMessageReplayer : IDeadLetterMessageReplayer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DeadLetterReplayOptions _options;
    private readonly IEventSystemTime _systemTime;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeadLetterMessageReplayer"/> class.
    /// </summary>
    public DeadLetterMessageReplayer(
        IServiceScopeFactory scopeFactory,
        IOptions<DeadLetterReplayOptions> options,
        IEventSystemTime? systemTime = null,
        ILogger<DeadLetterMessageReplayer>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _systemTime = systemTime ?? EventSystemTime.Instance;
        _logger = logger ?? NullLogger<DeadLetterMessageReplayer>.Instance;
    }

    /// <inheritdoc />
    public async Task ReplayAsync(IDeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        await ReplayCoreAsync(message, scope.ServiceProvider, cancellationToken);
    }

    private async Task ReplayCoreAsync(IDeadLetterMessage message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IDeadLetterMessageStore>();
        var publisher = ResolvePublisher(services, message);

        _logger.LogReplayingDeadLetterEvent(message.Event.Type, message.Id);

        try
        {
            await store.SetReplayingAsync(message, cancellationToken);
            await publisher.PublishEventAsync(
                message.Event,
                new DeadLetterReplayPublishOptions(),
                cancellationToken);
            await store.SetReplayedAsync(message, cancellationToken);
            _logger.LogDeadLetterEventReplayed(message.Event.Type, message.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorReplayingDeadLetterEvent(ex, message.Event.Type, message.Id);

            if (_options.MaxRetryCount > 0 && message.ReplayCount < _options.MaxRetryCount)
            {
                await store.SetRetryAsync(
                    message,
                    ex.Message,
                    _systemTime.UtcNow.Add(_options.RetryInterval),
                    cancellationToken);
            }
            else
            {
                await store.SetFailedAsync(message, ex.Message, cancellationToken);
            }

            throw;
        }
    }

    private IEventPublisher ResolvePublisher(IServiceProvider services, IDeadLetterMessage message)
    {
        if (!String.IsNullOrWhiteSpace(_options.TransportPublisherName))
            return services.GetRequiredKeyedService<IEventPublisher>(_options.TransportPublisherName);

        if (!String.IsNullOrWhiteSpace(message.PublisherName))
            return services.GetRequiredKeyedService<IEventPublisher>(message.PublisherName);

        return services.GetRequiredService<IEventPublisher>();
    }
}
