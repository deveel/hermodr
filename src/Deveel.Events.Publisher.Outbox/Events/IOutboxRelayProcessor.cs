//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// Defines the relay half of the Transactional Outbox pattern: reads all pending
/// outbox messages from the store, forwards their <see cref="CloudNative.CloudEvents.CloudEvent"/>
/// payloads to every non-outbox transport channel, and marks each message as
/// <see cref="OutboxMessageStatus.Delivered"/> or <see cref="OutboxMessageStatus.Failed"/>.
/// </summary>
/// <remarks>
/// The default implementation is registered automatically when you call
/// <c>WithRelay()</c> on the outbox channel builder. You can also resolve this
/// interface directly from the DI container to trigger a relay run on demand
/// (e.g. from tests or a manual endpoint).
/// </remarks>
public interface IOutboxRelayProcessor
{
    /// <summary>
    /// Fetches all pending outbox messages, dispatches their
    /// <see cref="CloudNative.CloudEvents.CloudEvent"/> payloads to the registered
    /// non-outbox transport channels, and updates each message's status.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default);
}

