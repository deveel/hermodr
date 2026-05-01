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
/// Extension methods for <see cref="OutboxChannelBuilder"/> that register
/// the Entity Framework-backed outbox message repository.
/// </summary>
public static class OutboxChannelBuilderExtensions
{
    /// <summary>
    /// Registers an <see cref="EntityOutboxMessageRepository{TMessage,TContext}"/> as the
    /// <see cref="IOutboxMessageRepository{TMessage}"/> implementation for the outbox channel.
    /// </summary>
    /// <typeparam name="TContext">
    /// The <see cref="DbContext"/> type that owns the message entity set determined by
    /// <see cref="OutboxChannelBuilder.MessageType"/>.
    /// The context must already be registered in the DI container.
    /// </typeparam>
    /// <param name="builder">
    /// The <see cref="OutboxChannelBuilder"/> to configure.
    /// </param>
    /// <param name="lifetime">
    /// The DI service lifetime for the repository.
    /// Defaults to <see cref="ServiceLifetime.Scoped"/>, which is the typical lifetime for
    /// Entity Framework contexts.
    /// </param>
    /// <returns>
    /// The same <see cref="OutboxChannelBuilder"/> instance for chaining.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="OutboxChannelBuilder.MessageType"/> does not implement
    /// <see cref="IOutboxMessageEntity"/>, which is required by
    /// <see cref="EntityOutboxMessageRepository{TMessage,TContext}"/>.
    /// </exception>
    /// <example>
    /// <code language="csharp">
    /// services.AddEventPublisher()
    ///     .AddOutbox&lt;MyOutboxMessage&gt;()
    ///     .WithEntityFrameworkRepository&lt;MyDbContext&gt;()
    ///     .WithFactory&lt;MyOutboxMessageFactory&gt;()
    ///     .WithRelay();
    ///
    /// // The DbContext must be registered separately, e.g.:
    /// services.AddDbContext&lt;MyDbContext&gt;(options =>
    ///     options.UseSqlServer(connectionString));
    /// </code>
    /// </example>
    public static OutboxChannelBuilder WithEntityFramework<TContext>(
        this OutboxChannelBuilder builder,
        Action<DbContextOptionsBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : OutboxDbContext
    {
        if (!typeof(DbOutboxMessage).IsAssignableFrom(builder.MessageType))
            throw new InvalidOperationException(
                $"The message type '{builder.MessageType.FullName}' must implement " +
                $"'{typeof(DbOutboxMessage).FullName}' to use the Entity Framework repository.");

        var managerType = typeof(OutboxMessageManager<>)
            .MakeGenericType(builder.MessageType);
        var repositoryType = typeof(EntityOutboxMessageRepository<,>)
            .MakeGenericType(builder.MessageType, typeof(TContext));
        var repositoryInterfaceType = typeof(IOutboxMessageRepository<>)
            .MakeGenericType(builder.MessageType);
        
        var validatorType = typeof(OutboxMessageValidator<>).MakeGenericType(builder.MessageType);
        // var baseValidatorType = typeof(IEntityValidator<,>).MakeGenericType(builder.MessageType, typeof(string));

        builder.Services.AddDbContext<TContext>(configure);
        builder.Services.AddEntityManager(managerType, lifetime);
        builder.Services.AddRepository(repositoryType, lifetime);
        builder.Services.TryAdd(ServiceDescriptor.Describe(
            repositoryInterfaceType,
            sp => sp.GetRequiredService(repositoryType),
            lifetime));
        builder.Services.AddEntityValidator(validatorType, lifetime);

        return builder;
    }

    /// <summary>
    /// Registers a custom <typeparamref name="TRepository"/> that extends
    /// <see cref="EntityOutboxMessageRepository{TMessage,TContext}"/> as the
    /// <see cref="IOutboxMessageRepository{TMessage}"/> for the outbox channel.
    /// </summary>
    /// <typeparam name="TContext">
    /// The <see cref="DbContext"/> type used by the repository.
    /// </typeparam>
    /// <typeparam name="TRepository">
    /// A concrete type that implements <see cref="IOutboxMessageRepository{TMessage}"/>
    /// for the message type carried by <see cref="OutboxChannelBuilder.MessageType"/>.
    /// At runtime the builder validates that <typeparamref name="TRepository"/> implements
    /// <c>IOutboxMessageRepository&lt;MessageType&gt;</c>.
    /// </typeparam>
    /// <param name="builder">
    /// The <see cref="OutboxChannelBuilder"/> to configure.
    /// </param>
    /// <param name="lifetime">
    /// The DI service lifetime for the repository.  Defaults to
    /// <see cref="ServiceLifetime.Scoped"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="OutboxChannelBuilder"/> instance for chaining.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TRepository"/> does not implement
    /// <c>IOutboxMessageRepository&lt;MessageType&gt;</c>.
    /// </exception>
    public static OutboxChannelBuilder WithEntityFrameworkRepository<TContext, TRepository>(
        this OutboxChannelBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
        where TRepository : class
    {
        var repositoryInterfaceType = typeof(IOutboxMessageRepository<>)
            .MakeGenericType(builder.MessageType);

        if (!repositoryInterfaceType.IsAssignableFrom(typeof(TRepository)))
            throw new InvalidOperationException(
                $"Repository type '{typeof(TRepository).FullName}' does not implement " +
                $"'{repositoryInterfaceType.FullName}'.");

        builder.Services.AddRepository(typeof(TRepository), lifetime);
        builder.Services.TryAdd(ServiceDescriptor.Describe(
            repositoryInterfaceType,
            sp => sp.GetRequiredService(typeof(TRepository)),
            lifetime));

        return builder;
    }
}
