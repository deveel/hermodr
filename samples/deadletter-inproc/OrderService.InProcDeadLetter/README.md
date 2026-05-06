# OrderService — In-Process Dead-Letter Replay

This sample demonstrates the **in-process** dead-letter flow in `Deveel.Events.Publisher.DeadLetter`.

## What it demonstrates

| Concern | Implementation |
|---------|---------------|
| Initial delivery | `FailingPrimaryChannel` always throws to simulate a broken transport |
| Dead-letter handling | `AddDeadLetter(...).UseHandler(...)` intercepts the failure in the same process |
| Replay path | The dead-letter callback republishes the captured `CloudEvent` through `IEventPublisher` |
| Replay target | `RecoveryChannel` receives the replayed event via `DeadLetterReplayPublishOptions` |

## Flow

```
OrderSubmitted
    │
    ▼
primary channel  ──── throws
    │
    ▼
dead-letter callback  ──── immediate replay through IEventPublisher
    │
    ▼
recovery channel  ──── accepts the CloudEvent
```

The replay stays inside the same process and uses the normal publisher pipeline, but the replay marker prevents the replay attempt from being captured as a brand-new dead letter.

## Running the sample

```bash
cd samples/deadletter-inproc/OrderService.InProcDeadLetter
dotnet run
```

Expected output:

1. the primary channel reports a simulated transport failure
2. the dead-letter callback reports that it is replaying the event
3. the recovery channel receives the replayed `OrderSubmitted` event
4. the original publish still throws, showing that the caller can observe the failed transport even when a fallback replay succeeds

## Configuration

| Key | Default | Description |
|-----|---------|-------------|
| `Sample:Source` | `https://samples.deveel.events/deadletter/inproc` | CloudEvent source used by the publisher |
| `Sample:DataSchemaBaseUri` | `https://samples.deveel.events/schema/` | Base URI used to derive the event `dataschema` from the `[Event]` version |
| `Sample:InitialChannel` | `primary` | The channel targeted by the first publish attempt |
| `Sample:ReplayChannel` | `recovery` | The named channel used for the in-process replay |
