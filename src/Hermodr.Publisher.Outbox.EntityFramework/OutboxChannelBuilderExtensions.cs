//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Kista;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr;

/// <summary>
/// Extension methods for <see cref="OutboxChannelBuilder"/> that register
/// the Entity Framework-backed outbox message store.
/// </summary>
public static class OutboxChannelBuilderExtensions
{
    /// <summary>
    /// Registers an <see cref="EntityOutboxMessageRepository{TMessage}"/> as the
    /// <see cref="IOutboxMessageStore{TMessage}"/> implementation for the outbox channel.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="OutboxChannelBuilder"/> to configure.
    /// </param>
    /// <param name="lifetime">
    /// The DI service lifetime for the store.
    /// Defaults to <see cref="ServiceLifetime.Scoped"/>, which is the typical lifetime for
    /// Entity Framework contexts.
    /// </param>
    /// <returns>
    /// The same <see cref="OutboxChannelBuilder"/> instance for chaining.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="OutboxChannelBuilder.MessageType"/> does not implement
    /// <see cref="IOutboxMessage"/>, which is required by
    /// <see cref="EntityOutboxMessageRepository{TMessage}"/>.
    /// </exception>
    public static OutboxChannelBuilder WithEntityFramework(
        this OutboxChannelBuilder builder,
        Action<DbContextOptionsBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        if (!typeof(DbOutboxMessage).IsAssignableFrom(builder.MessageType))
            throw new InvalidOperationException(
                $"The message type '{builder.MessageType.FullName}' must implement " +
                $"'{typeof(DbOutboxMessage).FullName}' to use the Entity Framework repository.");

        var repositoryType = typeof(EntityOutboxMessageRepository<>)
            .MakeGenericType(builder.MessageType);
        var storeInterfaceType = typeof(IOutboxMessageStore<>)
            .MakeGenericType(builder.MessageType);

        builder.Services.AddRepositoryContext()
            .UseEntityFramework<OutboxDbContext>(b => b
                .ConfigureDbContext(configure ?? (_ => { }))
                .WithLifetime(lifetime))
            .AddRepository(repositoryType, lifetime);

        builder.Services.TryAdd(ServiceDescriptor.Describe(
            storeInterfaceType,
            sp => sp.GetRequiredService(repositoryType),
            lifetime));

        return builder;
    }
}
