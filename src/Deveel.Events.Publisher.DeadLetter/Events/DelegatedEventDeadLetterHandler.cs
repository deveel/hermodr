//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

internal sealed class DelegatedEventDeadLetterHandler(Func<DeadLetterContext, Task> handler) : IEventDeadLetterHandler
{
    private readonly Func<DeadLetterContext, Task> _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    public Task HandleAsync(DeadLetterContext context) => _handler(context);
}
