//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// A marker subclass of <see cref="OutboxPublishOptions"/> that signals to the
/// <see cref="OutboxPublishChannel{TMessage}"/> that the publish call originated from
/// the outbox relay processor and should be skipped (i.e., the event must <em>not</em>
/// be re-persisted into the outbox).
/// </summary>
/// <remarks>
/// <para>
/// When the relay processor forwards a dequeued outbox message back through
/// <see cref="IEventPublisher"/>, it passes this options object.  Any
/// <see cref="OutboxPublishChannel{TMessage}"/> present in that pipeline sees the
/// signal and returns immediately, preventing an infinite persistence loop.
/// </para>
/// <para>
/// In the most common cross-process deployment model the relay application does not
/// register an <see cref="OutboxPublishChannel{TMessage}"/> at all, so this class is
/// harmless: it is simply never matched.
/// </para>
/// <para>
/// Transport channels (RabbitMQ, Azure Service Bus, etc.) receive <c>null</c> options
/// because the publisher's <c>ResolveChannelOptions</c> returns <c>null</c> when the
/// runtime options type does not match the channel's expected options type.  Their
/// behaviour is therefore unchanged.
/// </para>
/// </remarks>
internal sealed class OutboxRelayPublishOptions : OutboxPublishOptions
{
}

