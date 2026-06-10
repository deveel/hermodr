using Microsoft.EntityFrameworkCore;

namespace Hermodr;

/// <summary>
/// Provides extension methods for <see cref="EventPublisherBuilder"/> to configure Entity Framework-backed outbox storage.
/// </summary>
public static class EventPublisherBuilderExtensions
{
    /// <summary>
    /// Adds an Entity Framework-backed outbox channel to the publisher pipeline.
    /// </summary>
    /// <param name="builder">The <see cref="EventPublisherBuilder"/> to extend.</param>
    /// <param name="configure">An optional delegate to configure the <see cref="DbContextOptionsBuilder"/>.</param>
    /// <returns>An <see cref="OutboxChannelBuilder"/> for further configuration.</returns>
    public static OutboxChannelBuilder AddEntityFrameworkOutbox(this EventPublisherBuilder builder,
        Action<DbContextOptionsBuilder>? configure = null)
    {
        return builder.AddOutbox<DbOutboxMessage>().WithEntityFramework(configure);
    }
}