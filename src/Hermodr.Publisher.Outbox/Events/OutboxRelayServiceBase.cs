//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Hosting;

namespace Hermodr;

/// <summary>
/// A non-generic public base class for the outbox relay <see cref="BackgroundService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Concrete relay services (e.g. <c>OutboxRelayService&lt;TMessage&gt;</c>) inherit from
/// this class so that host infrastructure and tests can reference the relay service
/// through a stable public type without depending on the internal generic implementation.
/// </para>
/// <para>
/// To verify that the relay hosted service has been registered, resolve
/// <see cref="IEnumerable{T}"/> of <see cref="IHostedService"/> from the DI container
/// and check whether any entry is an instance of <see cref="OutboxRelayServiceBase"/>.
/// </para>
/// </remarks>
public abstract class OutboxRelayServiceBase : BackgroundService
{
}

