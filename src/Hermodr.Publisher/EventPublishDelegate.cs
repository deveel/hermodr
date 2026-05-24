//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Represents a step in the event-publish middleware pipeline.
    /// </summary>
    /// <param name="context">
    /// The <see cref="EventContext"/> that carries the
    /// <see cref="CloudNative.CloudEvents.CloudEvent"/>, the ambient
    /// <see cref="IServiceProvider"/>, the <see cref="System.Threading.CancellationToken"/>,
    /// and the per-call options through the pipeline.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when the step has finished.</returns>
    public delegate Task EventPublishDelegate(EventContext context);
}
