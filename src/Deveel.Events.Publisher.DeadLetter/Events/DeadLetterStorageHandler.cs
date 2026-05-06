//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deveel.Events;

internal sealed class DeadLetterStorageHandler : IEventDeadLetterHandler
{
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
        await _store.AddAsync(message, context.CancellationToken);
        _logger.LogDeadLetterEventSaved(context.Event.Type, message.Id);
    }
}
