//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    public interface IEventPublishChannel<TEvent> : IEventPublishChannel
        where TEvent : class
    {
    }
}
