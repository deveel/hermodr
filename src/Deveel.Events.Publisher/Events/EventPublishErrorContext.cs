//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Describes a failure that occurred during an event publishing operation.
/// </summary>
public sealed class EventPublishErrorContext
{
    internal EventPublishErrorContext(
        string publisherName,
        EventPublishStage stage,
        Exception exception,
        IServiceProvider services,
        CancellationToken cancellationToken = default,
        CloudEvent? @event = null,
        EventPublishOptions? options = null,
        EventPublishOptions? rawOptions = null,
        Type? channelType = null,
        string? channelName = null,
        Type? dataType = null,
        object? data = null)
    {
        PublisherName = publisherName ?? String.Empty;
        Stage = stage;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        CancellationToken = cancellationToken;
        Event = @event;
        Options = options;
        RawOptions = rawOptions;
        ChannelType = channelType;
        ChannelName = channelName;
        DataType = dataType;
        Data = data;
    }

    /// <summary>
    /// Gets the name of the publisher pipeline that produced the failure.
    /// </summary>
    public string PublisherName { get; }

    /// <summary>
    /// Gets the stage of the publish operation where the error occurred.
    /// </summary>
    public EventPublishStage Stage { get; }

    /// <summary>
    /// Gets the exception that was raised.
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
    /// Gets the event whose delivery failed, when available.
    /// </summary>
    public CloudEvent? Event { get; }

    /// <summary>
    /// Gets the effective per-call publish options, when available.
    /// </summary>
    public EventPublishOptions? Options { get; }

    /// <summary>
    /// Gets the original per-call publish options before transport wrappers were unwrapped.
    /// </summary>
    public EventPublishOptions? RawOptions { get; }

    /// <summary>
    /// Gets the concrete channel type involved in the failure, when available.
    /// </summary>
    public Type? ChannelType { get; }

    /// <summary>
    /// Gets the logical name of the channel involved in the failure, when available.
    /// </summary>
    public string? ChannelName { get; }

    /// <summary>
    /// Gets the CLR data type that was being published or converted, when available.
    /// </summary>
    public Type? DataType { get; }

    /// <summary>
    /// Gets the original data object associated with the publish attempt, when available.
    /// </summary>
    public object? Data { get; }
}
