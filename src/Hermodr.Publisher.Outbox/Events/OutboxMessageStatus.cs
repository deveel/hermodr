//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Describes the delivery lifecycle of an <see cref="IOutboxMessage"/>.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>
    /// The message is pending to be sent.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The message is being sent.
    /// </summary>
    Sending = 1,

    /// <summary>
    /// The message has been delivered successfully.
    /// </summary>
    Delivered = 2,

    /// <summary>
    /// The message failed to be sent.
    /// </summary>
    Failed = 3
}