# OrderService — Split Dead-Letter Replay with Entity Framework

This sample demonstrates the **split dead-letter** pattern across **two separate processes** that share the same SQLite-backed Entity Framework repository.

| Process | Project | Role |
|---|---|---|
| Console publisher | `OrderService.Publisher` | Publishes to a failing channel and persists the failed CloudEvent into the dead-letter store |
| Worker | `OrderService.DeadLetterWorker` | Polls the shared dead-letter database and replays pending CloudEvents through a recovery channel |

## Architecture

```
OrderService.Publisher
    │
    ▼
primary channel  ──── throws
    │
    ▼
dead-letter EF store  ──── INSERT into shared/deadletters.db
    ▲
    │  poll every 5 s
    │
OrderService.DeadLetterWorker
    │
    ▼
IEventPublisher.PublishEventAsync(event, DeadLetterReplayPublishOptions)
    │
    ▼
recovery channel  ──── accepts the replayed CloudEvent
```

Unlike the in-process sample, the publisher and the replay worker do not need to live in the same application. The database becomes the hand-off point between the failing publisher and the recovery process.

## Running the sample

Open two terminals from the repository root.

**Terminal 1 — start the replay worker**

```bash
cd samples/deadletter-relay/OrderService.DeadLetterWorker
dotnet run
```

**Terminal 2 — publish a failing event**

```bash
cd samples/deadletter-relay/OrderService.Publisher
dotnet run
```

Expected behaviour:

1. the publisher reports a simulated transport failure
2. the failed event is stored in `samples/deadletter-relay/shared/deadletters.db`
3. the worker detects the pending message and replays it on the next polling cycle
4. the recovery channel logs the replayed CloudEvent and its stored payload

## Configuration

| Project | Key | Default |
|---------|-----|---------|
| `OrderService.Publisher` | `ConnectionStrings:DeadLetter` | `Data Source=../shared/deadletters.db` |
| `OrderService.Publisher` | `Sample:DataSchemaBaseUri` | `https://samples.deveel.events/schema/` |
| `OrderService.DeadLetterWorker` | `ConnectionStrings:DeadLetter` | `Data Source=../shared/deadletters.db` |
| `OrderService.DeadLetterWorker` | `Sample:DataSchemaBaseUri` | `https://samples.deveel.events/schema/` |
| `OrderService.DeadLetterWorker` | `Sample:ReplayIntervalSeconds` | `5` |

Both processes resolve the relative SQLite path against their own project directory and create the shared folder automatically.
