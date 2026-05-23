//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

public sealed class DeadLetterMessageFactoryAdapter<TMessage> : IDeadLetterMessageFactory
    where TMessage : class, IDeadLetterMessage
{
    private readonly IDeadLetterMessageFactory<TMessage> _factory;

    public DeadLetterMessageFactoryAdapter(IDeadLetterMessageFactory<TMessage> factory)
    {
        _factory = factory;
    }

    public IDeadLetterMessage Create(DeadLetterContext context) => _factory.Create(context);
}
