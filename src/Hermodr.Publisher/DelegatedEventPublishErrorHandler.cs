//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

internal sealed class DelegatedEventPublishErrorHandler(Func<EventPublishErrorContext, Task> handler) : IEventPublishErrorHandler
{
    private readonly Func<EventPublishErrorContext, Task> _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    public Task HandleAsync(EventPublishErrorContext context) => _handler(context);
}
