//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Identifies the stage of the publish operation where an error occurred.
/// </summary>
public enum EventPublishStage
{
    /// <summary>
    /// The error occurred while creating a <see cref="CloudNative.CloudEvents.CloudEvent"/>
    /// from an event data object.
    /// </summary>
    EventCreation,

    /// <summary>
    /// The error occurred while converting an <see cref="IEventConvertible"/> into a
    /// <see cref="CloudNative.CloudEvents.CloudEvent"/>.
    /// </summary>
    EventConversion,

    /// <summary>
    /// The error occurred while dispatching a <see cref="CloudNative.CloudEvents.CloudEvent"/>
    /// to a publish channel.
    /// </summary>
    ChannelPublish
}
