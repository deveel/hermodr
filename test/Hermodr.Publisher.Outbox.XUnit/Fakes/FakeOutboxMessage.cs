using CloudNative.CloudEvents;
namespace Hermodr.Fakes;
/// <summary>
/// A simple in-memory <see cref="IOutboxMessage"/> implementation used in tests.
/// </summary>
internal sealed class FakeOutboxMessage : IOutboxMessage
{
    public FakeOutboxMessage(CloudEvent cloudEvent)
    {
        Event = cloudEvent;
        Status     = OutboxMessageStatus.Pending;
    }
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public CloudEvent Event { get; }
    public OutboxMessageStatus Status { get; internal set; }
    public string? ErrorMessage { get; internal set; }
    public int RetryCount { get; internal set; }
    public DateTimeOffset? NextRetryAt { get; internal set; }
}
