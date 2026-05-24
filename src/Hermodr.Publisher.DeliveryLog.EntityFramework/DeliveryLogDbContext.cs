using Microsoft.EntityFrameworkCore;

namespace Hermodr;

/// <summary>
/// An Entity Framework Core <see cref="DbContext"/> for managing event delivery records.
/// </summary>
public class DeliveryLogDbContext : DbContext
{
    /// <summary>
    /// Creates a new instance of <see cref="DeliveryLogDbContext"/> with the given options.
    /// </summary>
    /// <param name="options">
    /// The options to configure the database context.
    /// </param>
    public DeliveryLogDbContext(DbContextOptions<DeliveryLogDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="DeliveryLogDbContext"/> with the given non-generic options.
    /// </summary>
    /// <param name="options">
    /// The options to configure the database context.
    /// </param>
    protected DeliveryLogDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the set of delivery records in the database.
    /// </summary>
    public virtual DbSet<DbEventDeliveryRecord> DeliveryRecords { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryLogDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
