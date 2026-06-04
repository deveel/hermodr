using Microsoft.EntityFrameworkCore;

namespace Hermodr;

/// <summary>
/// An Entity Framework Core <see cref="DbContext"/> for managing audit trail entries.
/// </summary>
public class AuditTrailDbContext : DbContext
{
    /// <summary>
    /// Creates a new instance of <see cref="AuditTrailDbContext"/> with the given options.
    /// </summary>
    /// <param name="options">
    /// The options to configure the database context.
    /// </param>
    public AuditTrailDbContext(DbContextOptions<AuditTrailDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="AuditTrailDbContext"/> with the given non-generic options.
    /// </summary>
    /// <param name="options">
    /// The options to configure the database context.
    /// </param>
    protected AuditTrailDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the set of audit trail entries in the database.
    /// </summary>
    public virtual DbSet<DbAuditTrailEntry> AuditTrailEntries { get; set; } = null!;

    /// <summary>
    /// Gets or sets the set of audit trail attributes in the database.
    /// </summary>
    public virtual DbSet<DbAuditTrailAttribute> AuditTrailAttributes { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditTrailDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
