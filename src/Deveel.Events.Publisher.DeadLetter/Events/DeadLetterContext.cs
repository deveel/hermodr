//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Carries the details of a failed channel delivery to a dead-letter handler.
/// </summary>
public sealed class DeadLetterContext
{
    internal DeadLetterContext(EventPublishErrorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Event);

        PublisherName = context.PublisherName;
        Event = context.Event;
        Exception = context.Exception;
        Services = context.Services;
        CancellationToken = context.CancellationToken;
        Options = context.Options;
        ChannelType = context.ChannelType;
        ChannelName = context.ChannelName;
    }

    /// <summary>
    /// Gets the name of the publisher pipeline that produced the failure.
    /// </summary>
    public string PublisherName { get; }

    /// <summary>
    /// Gets the event whose delivery failed.
    /// </summary>
    public CloudEvent Event { get; }

    /// <summary>
    /// Gets the exception raised by the failed publish attempt.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the service provider associated with the current error-handling scope.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the cancellation token for the publish operation.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the effective per-call publish options, if any.
    /// </summary>
    public EventPublishOptions? Options { get; }

    /// <summary>
    /// Gets the concrete channel type that failed.
    /// </summary>
    public Type? ChannelType { get; }

    /// <summary>
    /// Gets the logical name of the failed channel, when available.
    /// </summary>
    public string? ChannelName { get; }
}
