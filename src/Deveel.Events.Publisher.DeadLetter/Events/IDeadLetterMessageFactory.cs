//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// Creates persisted dead-letter messages from failed publish attempts.
/// </summary>
public interface IDeadLetterMessageFactory
{
    /// <summary>
    /// Creates a new persisted dead-letter message from the given failure context.
    /// </summary>
    IDeadLetterMessage Create(DeadLetterContext context);
}

/// <summary>
/// Creates persisted dead-letter messages from failed publish attempts.
/// </summary>
/// <typeparam name="TMessage">The concrete dead-letter message type.</typeparam>
public interface IDeadLetterMessageFactory<TMessage>
    where TMessage : class, IDeadLetterMessage
{
    /// <summary>
    /// Creates a new persisted dead-letter message from the given failure context.
    /// </summary>
    TMessage Create(DeadLetterContext context);
}
