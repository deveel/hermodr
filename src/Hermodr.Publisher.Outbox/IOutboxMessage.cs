//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr;

/// <summary>
/// Represents a message stored in the outbox table that wraps a <see cref="Event"/>
/// and tracks its current delivery state.
/// </summary>
/// <remarks>
/// Implementations must be concrete, persistable entities; they are created by an
/// <see cref="IOutboxMessageFactory{TMessage}"/> from a <see cref="Event"/> and stored
/// via an <see cref="IOutboxMessageRepository{TMessage}"/>.
/// </remarks>
public interface IOutboxMessage
{
    /// <summary>
    /// Gets the <see cref="Event"/> payload that this outbox message wraps.
    /// </summary>
    CloudEvent Event { get; }

    /// <summary>
    /// Gets the current delivery status of this message.
    /// </summary>
    OutboxMessageStatus Status { get; }

    /// <summary>
    /// Gets an optional error description when <see cref="Status"/> is
    /// <see cref="OutboxMessageStatus.Failed"/> or when a delivery attempt has failed
    /// and a retry is scheduled.  <c>null</c> when not applicable.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Gets the number of delivery attempts that have been made for this message.
    /// Starts at <c>0</c> and is incremented each time a transient failure is recorded
    /// via <see cref="IOutboxMessageRepository{TMessage}.SetRetryAsync"/>.
    /// </summary>
    int RetryCount { get; }

    /// <summary>
    /// Gets the UTC point in time at which the relay should next attempt delivery;
    /// <c>null</c> means the message is eligible for immediate dispatch.
    /// </summary>
    DateTimeOffset? NextRetryAt { get; }
}