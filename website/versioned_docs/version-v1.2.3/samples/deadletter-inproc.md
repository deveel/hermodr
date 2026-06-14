# OrderService — In-Process Dead-Letter Replay

**Location:** [`samples/deadletter-inproc/OrderService.InProcDeadLetter/`](https://github.com/deveel/hermodr/tree/main/samples/deadletter-inproc/OrderService.InProcDeadLetter)  
**Transport:** In-memory sample channels  
**Pattern:** Dead-letter callback + immediate replay through the publisher pipeline

## What it demonstrates

- a failing publish channel that simulates a broken transport
- `AddDeadLetter(...).UseHandler(...)` for in-process dead-letter interception
- replaying the captured `CloudEvent` back through `IEventPublisher`
- targeting a recovery channel with `DeadLetterReplayPublishOptions`

## Flow

```
OrderSubmitted
    │
    ▼
primary channel  ──── throws
    │
    ▼
dead-letter callback  ──── immediate replay
    │
    ▼
recovery channel
```

The replay stays inside the same process and uses the same publisher pipeline. The replay marker prevents the replay attempt from being captured as a new dead-letter record.

## Run it

```bash
cd samples/deadletter-inproc/OrderService.InProcDeadLetter
dotnet run
```

For the full walkthrough, see the [sample README](../../samples/deadletter-inproc/OrderService.InProcDeadLetter/README.md).
