//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Hosting;

namespace Deveel.Events;

/// <summary>
/// A non-generic public base class for the dead-letter replay background service.
/// </summary>
public abstract class DeadLetterReplayServiceBase : BackgroundService
{
}
