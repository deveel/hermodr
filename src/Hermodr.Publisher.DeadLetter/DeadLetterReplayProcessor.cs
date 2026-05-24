//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr;

internal sealed class DeadLetterReplayProcessor : IDeadLetterReplayProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDeadLetterMessageReplayer _replayer;
    private readonly DeadLetterReplayOptions _options;
    private readonly ILogger _logger;

    public DeadLetterReplayProcessor(
        IServiceScopeFactory scopeFactory,
        IDeadLetterMessageReplayer replayer,
        IOptions<DeadLetterReplayOptions> options,
        ILogger<DeadLetterReplayProcessor>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _replayer = replayer;
        _options = options.Value;
        _logger = logger ?? NullLogger<DeadLetterReplayProcessor>.Instance;
    }

    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDeadLetterMessageStore>();
        var pending = await store.GetPendingMessagesAsync(_options.MaxBatchSize > 0 ? _options.MaxBatchSize : null, cancellationToken);

        if (pending.Count == 0)
        {
            _logger.LogNoPendingDeadLetterMessages();
            return;
        }

        _logger.LogProcessingPendingDeadLetterMessages(pending.Count);

        foreach (var message in pending)
        {
            try
            {
                await _replayer.ReplayAsync(message, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogErrorProcessingDeadLetterMessage(ex, message.Event.Type, message.Id);
            }
        }
    }
}
