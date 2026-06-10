//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Adapts a typed <see cref="IDeadLetterMessageFactory{TMessage}"/> to the non-generic
/// <see cref="IDeadLetterMessageFactory"/> interface.
/// </summary>
/// <typeparam name="TMessage">The concrete dead-letter message type produced by the inner factory.</typeparam>
public sealed class DeadLetterMessageFactoryAdapter<TMessage> : IDeadLetterMessageFactory
    where TMessage : class, IDeadLetterMessage
{
    private readonly IDeadLetterMessageFactory<TMessage> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeadLetterMessageFactoryAdapter{TMessage}"/> class.
    /// </summary>
    /// <param name="factory">The typed factory to adapt.</param>
    public DeadLetterMessageFactoryAdapter(IDeadLetterMessageFactory<TMessage> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <inheritdoc />
    public IDeadLetterMessage Create(DeadLetterContext context) => _factory.Create(context);
}
