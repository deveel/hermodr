using Microsoft.EntityFrameworkCore;

namespace Deveel.Events;

public class OutboxDbContext : DbContext
{
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
    
    public virtual DbSet<DbOutboxMessage> OutboxMessages { get; set; } = null!;
    
    public virtual DbSet<DbCloudEventAttribute> OutboxMessageAttributes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OutboxDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}