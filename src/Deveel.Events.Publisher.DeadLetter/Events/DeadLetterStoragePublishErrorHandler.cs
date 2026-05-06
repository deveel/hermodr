//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

internal sealed class DeadLetterStoragePublishErrorHandler : IEventPublishErrorHandler
{
    private readonly DeadLetterPublishErrorHandler _inner;

    public DeadLetterStoragePublishErrorHandler(
        IDeadLetterMessageFactory factory,
        IDeadLetterMessageStore store,
        Microsoft.Extensions.Logging.ILogger<DeadLetterStorageHandler>? logger = null)
    {
        _inner = new DeadLetterPublishErrorHandler(new DeadLetterStorageHandler(factory, store, logger));
    }

    public Task HandleAsync(EventPublishErrorContext context) => _inner.HandleAsync(context);
}
