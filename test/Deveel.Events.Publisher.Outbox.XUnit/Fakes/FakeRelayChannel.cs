//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel.Events;

namespace Deveel.Events.Fakes;

/// <summary>
/// An in-memory <see cref="IEventPublishChannel"/> that records every
/// <see cref="CloudEvent"/> forwarded to it by the outbox relay, for use in unit tests.
/// </summary>
internal sealed class FakeRelayChannel : IEventPublishChannel
{
    private readonly List<CloudEvent> _published = new();

    /// <summary>Snapshot of every event forwarded by the relay.</summary>
    public IReadOnlyList<CloudEvent> Published => _published;

    public Task PublishAsync(
        CloudEvent @event,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _published.Add(@event);
        return Task.CompletedTask;
    }
}

