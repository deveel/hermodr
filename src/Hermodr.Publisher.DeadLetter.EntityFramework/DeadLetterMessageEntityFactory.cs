//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Creates Entity Framework dead-letter entities from dead-letter contexts.
/// </summary>
/// <typeparam name="TMessage">The EF dead-letter entity type.</typeparam>
public sealed class DeadLetterMessageEntityFactory<TMessage> : IDeadLetterMessageFactory<TMessage>
    where TMessage : DbDeadLetterMessage, new()
{
    private readonly IEventSystemTime _systemTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeadLetterMessageEntityFactory{TMessage}"/> class.
    /// </summary>
    /// <param name="systemTime">The optional clock used to timestamp created entities; defaults to the system time when <see langword="null"/>.</param>
    public DeadLetterMessageEntityFactory(IEventSystemTime? systemTime = null)
    {
        _systemTime = systemTime ?? EventSystemTime.Instance;
    }

    /// <summary>
    /// Creates a new dead-letter entity from the specified context.
    /// </summary>
    /// <param name="context">The dead-letter context describing the failed delivery.</param>
    /// <returns>A new <typeparamref name="TMessage"/> instance populated from the context.</returns>
    public TMessage Create(DeadLetterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = new TMessage();
        message.PopulateFromDeadLetterContext(context, _systemTime.UtcNow);
        return message;
    }
}
