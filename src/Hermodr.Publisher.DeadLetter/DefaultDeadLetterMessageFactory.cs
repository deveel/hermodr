//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Creates the default in-memory <see cref="DeadLetterMessage"/> instances from failed publish attempts.
/// </summary>
public sealed class DefaultDeadLetterMessageFactory : IDeadLetterMessageFactory<DeadLetterMessage>
{
    /// <inheritdoc />
    public DeadLetterMessage Create(DeadLetterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new DeadLetterMessage
        {
            Id = context.Event.Id ?? Guid.NewGuid().ToString("N"),
            Event = context.Event,
            PublisherName = context.PublisherName,
            ChannelName = context.ChannelName,
            ChannelType = context.ChannelType?.AssemblyQualifiedName ?? context.ChannelType?.FullName,
            ErrorMessage = context.Exception.Message,
            Status = DeadLetterMessageStatus.Pending
        };
    }
}
