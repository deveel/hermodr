using Deveel.Events;

namespace EventGeneration.ConsoleSample.Events;

[Event("com.example.people.person-registered", "1.0", ContentType = "application/json")]
[EventAttributes("region", "eu-west-1")]
[EventAttributes("confidentiality", "internal")]
public partial class PersonRegistered
{
    public required string PersonId { get; init; }

    public required string Email { get; init; }

    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;
}

