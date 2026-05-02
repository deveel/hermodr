//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Options that control the behaviour of <see cref="EventDispatcher"/>.
    /// </summary>
    public sealed class EventDispatcherOptions
    {
        /// <summary>
        /// When <c>true</c>, an exception thrown by a subscription handler propagates to the
        /// current publish call and aborts dispatching to any subsequent subscribers.
        /// When <c>false</c> (the default) the exception is only logged and dispatching continues.
        /// </summary>
        public bool ThrowOnHandlerError { get; set; } = false;
    }
}
