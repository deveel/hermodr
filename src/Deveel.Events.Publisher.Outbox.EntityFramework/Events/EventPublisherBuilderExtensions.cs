using Microsoft.EntityFrameworkCore;

namespace Deveel.Events;

public static class EventPublisherBuilderExtensions
{
    public static OutboxChannelBuilder AddEntityFrameworkOutbox(this EventPublisherBuilder builder,
        Action<DbContextOptionsBuilder>? configure = null)
    {
        return builder.AddOutbox<DbOutboxMessage>().WithEntityFramework(configure);
    }
}