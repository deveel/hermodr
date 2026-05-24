//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Handles failed channel deliveries by capturing the event and its delivery metadata.
/// </summary>
public interface IEventDeadLetterHandler
{
    /// <summary>
    /// Handles a dead-letter event.
    /// </summary>
    /// <param name="context">
    /// The context describing the failed event delivery.
    /// </param>
    /// <returns>
    /// A task that completes when the dead-letter event has been handled.
    /// </returns>
    Task HandleAsync(DeadLetterContext context);
}
