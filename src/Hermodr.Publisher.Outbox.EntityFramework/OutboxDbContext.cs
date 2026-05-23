using Microsoft.EntityFrameworkCore;

namespace Hermodr;

/// <summary>
/// A minimal <see cref="DbContext"/> that provides the entity sets required by the
/// outbox publish channel and exposes both <see cref="DbOutboxMessage"/> rows and their
/// <see cref="DbCloudEventAttribute"/> child rows.
/// </summary>
/// <remarks>
/// Derive from this class when your application does not already have a custom
/// <see cref="DbContext"/>.  For existing contexts, simply add the requisite
/// <c>DbSet</c> properties and call
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/> (or apply
/// <see cref="DbOutboxMessageConfiguration"/> manually).
/// </remarks>
public class OutboxDbContext : DbContext
{
    /// <summary>
    /// Initialises the context with typed options.
    /// </summary>
    /// <param name="options">
    /// The <see cref="DbContextOptions{TContext}"/> supplied through the DI container.
    /// </param>
    public OutboxDbContext(DbContextOptions<OutboxDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Protected constructor that accepts the non-generic base options so that
    /// subclasses registered with their own typed options in DI can still chain up.
    /// </summary>
    protected OutboxDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the entity set for <see cref="DbOutboxMessage"/> rows (the outbox table).
    /// </summary>
    public virtual DbSet<DbOutboxMessage> OutboxMessages { get; set; } = null!;

    /// <summary>
    /// Gets or sets the entity set for <see cref="DbCloudEventAttribute"/> rows
    /// (extension attribute rows linked to their parent outbox message).
    /// </summary>
    public virtual DbSet<DbCloudEventAttribute> OutboxMessageAttributes { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OutboxDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}