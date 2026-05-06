//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Deveel.Events;

internal sealed class DeadLetterReplayService : DeadLetterReplayServiceBase
{
    private readonly DeadLetterReplayProcessor _processor;
    private readonly DeadLetterReplayOptions _options;
    private readonly ILogger _logger;

    public DeadLetterReplayService(
        DeadLetterReplayProcessor processor,
        IOptions<DeadLetterReplayOptions> options,
        ILogger<DeadLetterReplayService>? logger = null)
    {
        _processor = processor;
        _options = options.Value;
        _logger = logger ?? NullLogger<DeadLetterReplayService>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDeadLetterReplayServiceStarted(_options.Interval);

        using var timer = new PeriodicTimer(_options.Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;

                _logger.LogDeadLetterReplayServiceTick();
                await _processor.ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogErrorRunningDeadLetterReplayService(ex);
            }
        }

        _logger.LogDeadLetterReplayServiceStopped();
    }
}
