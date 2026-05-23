//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

internal sealed class DeadLetterPublishErrorHandler(IEventDeadLetterHandler handler) : IEventPublishErrorHandler
{
    private readonly IEventDeadLetterHandler _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    public Task HandleAsync(EventPublishErrorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Stage != EventPublishStage.ChannelPublish ||
            context.Event == null ||
            context.RawOptions is DeadLetterReplayPublishOptions)
            return Task.CompletedTask;

        return _handler.HandleAsync(new DeadLetterContext(context));
    }
}
