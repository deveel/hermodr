//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// Creates Entity Framework dead-letter entities from dead-letter contexts.
/// </summary>
/// <typeparam name="TMessage">The EF dead-letter entity type.</typeparam>
public sealed class DeadLetterMessageEntityFactory<TMessage> : IDeadLetterMessageFactory<TMessage>
    where TMessage : DbDeadLetterMessage, new()
{
    private readonly IEventSystemTime _systemTime;

    public DeadLetterMessageEntityFactory(IEventSystemTime? systemTime = null)
    {
        _systemTime = systemTime ?? EventSystemTime.Instance;
    }

    public TMessage Create(DeadLetterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = new TMessage();
        message.PopulateFromDeadLetterContext(context, _systemTime.UtcNow);
        return message;
    }
}
