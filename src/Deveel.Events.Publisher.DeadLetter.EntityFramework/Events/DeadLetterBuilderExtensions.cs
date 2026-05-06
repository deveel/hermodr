//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Deveel.Events;

/// <summary>
/// Extensions that register Entity Framework-backed dead-letter storage.
/// </summary>
public static class DeadLetterBuilderExtensions
{
    /// <summary>
    /// Registers an Entity Framework Core dead-letter store and default entity factory.
    /// </summary>
    public static DeadLetterBuilder WithEntityFramework(
        this DeadLetterBuilder builder,
        Action<DbContextOptionsBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContext<DeadLetterDbContext>(configure, lifetime);
        builder.UseRepository<DbDeadLetterMessage, EntityDeadLetterMessageStore<DbDeadLetterMessage>>(lifetime);
        builder.Services.AddRepository<EntityDeadLetterMessageStore<DbDeadLetterMessage>>();
        builder.WithFactory<DbDeadLetterMessage, DeadLetterMessageEntityFactory<DbDeadLetterMessage>>();
        return builder;
    }
}
