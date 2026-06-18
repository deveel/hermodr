//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Controls the behavior of <see cref="EventUpcastingMiddleware"/> when no upcaster
    /// is registered for a required version transition.
    /// </summary>
    public enum MissingUpcasterBehavior
    {
        /// <summary>
        /// Leave the event unchanged and continue the pipeline.
        /// </summary>
        PassThrough,

        /// <summary>
        /// Throw an <see cref="EventUpcastingException"/>.
        /// </summary>
        Throw
    }
}
