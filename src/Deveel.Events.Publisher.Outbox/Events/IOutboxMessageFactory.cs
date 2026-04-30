//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Creates concrete <typeparamref name="TMessage"/> instances from a
/// <see cref="CloudEvent"/> so they can be persisted in the outbox store.
/// </summary>
/// <typeparam name="TMessage">
/// The outbox message entity type produced by this factory.
/// Must implement <see cref="IOutboxMessage"/>.
/// </typeparam>
public interface IOutboxMessageFactory<out TMessage>
    where TMessage : IOutboxMessage
{
    /// <summary>
    /// Creates a new <typeparamref name="TMessage"/> from the supplied
    /// <paramref name="cloudEvent"/>.
    /// </summary>
    /// <param name="cloudEvent">
    /// The event to wrap inside the outbox message.  Must not be <c>null</c>.
    /// </param>
    /// <param name="options">
    /// Optional publish options that were in effect when the publish call was made.
    /// Implementations may use these to influence how the message is constructed
    /// (e.g., to embed routing metadata), but the parameter is optional.
    /// </param>
    /// <returns>
    /// A new <typeparamref name="TMessage"/> wrapping <paramref name="cloudEvent"/>
    /// with an initial status of <see cref="OutboxMessageStatus.Pending"/>.
    /// </returns>
    TMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null);
}