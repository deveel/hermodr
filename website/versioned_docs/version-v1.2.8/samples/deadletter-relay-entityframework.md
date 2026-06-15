# OrderService — Split Dead-Letter Replay with Entity Framework

**Location:** [`samples/deadletter-relay/`](https://github.com/deveel/hermodr/tree/main/samples/deadletter-relay)  
**Transport:** SQLite-backed dead-letter repository + in-memory recovery channel  
**Pattern:** Persistent dead-letter storage with a separate replay worker

## What it demonstrates

- `Hermodr.Publisher.DeadLetter.EntityFramework` as a persistence layer
- one application that persists failed deliveries into the dead-letter store
- a second application that polls the shared store and replays pending messages
- background replay through `WithReplayWorker(...)`

## Flow

```
OrderService.Publisher
    │
    ▼
failing channel  ──── stores dead-letter row in SQLite
    ▲
    │  poll every 5 s
    │
OrderService.DeadLetterWorker
    │
    ▼
recovery channel
```

This sample keeps the publisher and the replay worker completely separate, with the shared database acting as the hand-off point for replay.

## Run it

```bash
cd samples/deadletter-relay/OrderService.DeadLetterWorker
dotnet run

cd samples/deadletter-relay/OrderService.Publisher
dotnet run
```

For the full walkthrough, see the [sample README](../../samples/deadletter-relay/README.md).
