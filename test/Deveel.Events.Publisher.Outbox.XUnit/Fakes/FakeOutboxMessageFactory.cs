using CloudNative.CloudEvents;
namespace Deveel.Events.Fakes;
/// <summary>
/// A simple factory that wraps a <see cref="CloudEvent"/> in a
/// <see cref="FakeOutboxMessage"/> and records every creation call.
/// </summary>
internal sealed class FakeOutboxMessageFactory : IOutboxMessageFactory<FakeOutboxMessage>
{
    public List<FakeOutboxMessage> Created { get; } = new();
    public FakeOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
    {
        var msg = new FakeOutboxMessage(cloudEvent);
        Created.Add(msg);
        return msg;
    }
}
