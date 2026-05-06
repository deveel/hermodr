# Sample Projects

The [`samples/`](https://github.com/deveel/deveel.events/tree/main/samples) directory of the repository contains runnable projects that demonstrate the major use cases of the **Deveel Events** framework.

Each sample is self-contained. Some samples spin up external infrastructure with `docker-compose.yml`, while others are pure .NET projects that you can run directly.

---

## Available samples

| Sample | Transport | Highlights |
|--------|-----------|------------|
| [OrderService — Minimal API + RabbitMQ](aspnet-publisher-rabbitmq.md) | RabbitMQ | Annotated event classes, typed channels, `IEventPublisher` in a service |
| [OrderService — In-Process Outbox + RabbitMQ](outbox-inapp-rabbitmq.md) | RabbitMQ | Transactional Outbox with in-process relay; EF Core SQLite; `[AmqpExchange]` / `[AmqpRoutingKey]` annotations |
| [OrderService — Split Outbox + MassTransit RabbitMQ](outbox-relay-masstransit.md) | MassTransit / RabbitMQ | Split outbox across two processes; API has no transport dependency; external relay worker; MassTransit publish channel |
| [OrderService — In-Process Dead-Letter Replay](deadletter-inproc.md) | In-memory sample channels | Dead-letter callback with immediate replay to a recovery channel inside the same process |
| [OrderService — Split Dead-Letter Replay with Entity Framework](deadletter-relay-entityframework.md) | EF Core SQLite + in-memory recovery channel | Shared dead-letter repository with a separate worker replaying pending messages |

---

## How to run a sample

All samples follow the same basic workflow:

```bash
# 1. Navigate to the sample folder
cd samples/<sample-folder>

# 2. Start any required infrastructure (only when the sample needs it)
docker compose up -d

# 3. Run the project
dotnet run
```

Refer to each sample's own `README.md` for endpoint details, curl examples, and configuration options.
