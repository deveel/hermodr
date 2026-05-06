//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// Handles failures that occur during an event publishing operation.
/// </summary>
public interface IEventPublishErrorHandler
{
    /// <summary>
    /// Handles a publish failure.
    /// </summary>
    /// <param name="context">
    /// The context describing the stage, exception, event, and channel information
    /// associated with the failure.
    /// </param>
    /// <returns>
    /// A task that completes when the error has been handled.
    /// </returns>
    Task HandleAsync(EventPublishErrorContext context);
}
