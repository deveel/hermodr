//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hermodr;

internal sealed class DeadLetterStorageHandler : IEventDeadLetterHandler
{
    private readonly DeadLetterTelemetry _telemetry = new();
    private readonly IDeadLetterMessageFactory _factory;
    private readonly IDeadLetterMessageStore _store;
    private readonly ILogger _logger;

    public DeadLetterStorageHandler(
        IDeadLetterMessageFactory factory,
        IDeadLetterMessageStore store,
        ILogger<DeadLetterStorageHandler>? logger = null)
    {
        _factory = factory;
        _store = store;
        _logger = logger ?? NullLogger<DeadLetterStorageHandler>.Instance;
    }

    public async Task HandleAsync(DeadLetterContext context)
    {
        var message = _factory.Create(context);
        _logger.LogSavingDeadLetterEvent(context.Event.Type, message.Id);

        using var activity = _telemetry.StartStoreActivity(context.Event.Type ?? "unknown");

        try
        {
            await _store.AddAsync(message, context.CancellationToken);
            _logger.LogDeadLetterEventSaved(context.Event.Type, message.Id);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _telemetry.AddDeadLetterCreated(context.Event.Type ?? "unknown");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
