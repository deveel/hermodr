//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// Configuration options for the <see cref="OutboxRelayService{TMessage}"/> background
/// service that periodically dequeues pending outbox messages and forwards them to the
/// registered transport channels.
/// </summary>
public sealed class OutboxRelayOptions
{
    /// <summary>
    /// Gets or sets how often the relay wakes up to poll for pending outbox messages.
    /// </summary>
    /// <remarks>
    /// Shorter intervals reduce end-to-end latency at the cost of more frequent
    /// database round-trips. Defaults to <c>30 seconds</c>.
    /// </remarks>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of pending messages dequeued and forwarded in a
    /// single relay cycle.  Use this to bound the amount of work done per tick.
    /// </summary>
    /// <remarks>
    /// A value of <c>0</c> or negative means "no limit" – every pending message
    /// returned by
    /// <see cref="IOutboxMessageRepository{TMessage}.GetPendingMessagesAsync"/> is
    /// processed in one batch.  Defaults to <c>0</c> (no limit).
    /// </remarks>
    public int MaxBatchSize { get; set; } = 0;

    /// <summary>
    /// Gets or sets the name of the <see cref="IEventPublisher"/> pipeline the relay
    /// uses to forward dequeued outbox messages to transport channels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use an empty string (the default) to target the default unnamed publisher
    /// pipeline — the typical case when the relay application registers a single
    /// transport publisher (e.g., RabbitMQ or Azure Service Bus).
    /// </para>
    /// <para>
    /// Set this to a non-empty name when the relay application hosts multiple named
    /// publisher pipelines and you want to direct the relay to a specific one
    /// (e.g., a transport-only pipeline that does not include an outbox channel).
    /// </para>
    /// </remarks>
    public string TransportPublisherName { get; set; } = string.Empty;
}


