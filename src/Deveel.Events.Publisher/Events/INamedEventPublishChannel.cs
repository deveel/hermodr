// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Deveel.Events
{
    /// <summary>
    /// Optionally implemented by an <see cref="IEventPublishChannel"/> to expose a
    /// logical name that allows the <see cref="EventPublisher"/> to route events
    /// selectively.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Channels that do <strong>not</strong> implement this interface are treated as
    /// anonymous and receive every event regardless of any
    /// <see cref="INamedChannelFilter.ChannelName"/> filter supplied at publish time.
    /// </para>
    /// <para>
    /// <see cref="EventPublishChannel{TOptions}"/> implements this interface
    /// automatically: it reads <see cref="Name"/> from the channel-level options when
    /// those options implement <see cref="INamedChannelFilter"/>.
    /// Custom channels can implement this interface directly to provide a name through
    /// any mechanism they prefer.
    /// </para>
    /// </remarks>
    public interface INamedEventPublishChannel : IEventPublishChannel
    {
        /// <summary>
        /// Gets the logical name of this channel instance.
        /// </summary>
        /// <remarks>
        /// When non-<c>null</c> and non-empty the <see cref="EventPublisher"/> uses
        /// this value to match against the <see cref="INamedChannelFilter.ChannelName"/>
        /// supplied in per-call options.  A <c>null</c> or empty value means the
        /// channel acts as unnamed and is never excluded by a name filter.
        /// </remarks>
        string? Name { get; }
    }
}

