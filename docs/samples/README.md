# Sample Projects

The [`samples/`](https://github.com/deveel/deveel.events/tree/main/samples) directory of the repository contains runnable projects that demonstrate the major use cases of the **Deveel Events** framework.

Each sample is self-contained and ships with its own infrastructure (via `docker-compose.yml`) so you can explore it without any other setup beyond Docker and the .NET SDK.

---

## Available samples

| Sample | Transport | Highlights |
|--------|-----------|------------|
| [OrderService — Minimal API + RabbitMQ](aspnet-publisher-rabbitmq.md) | RabbitMQ | Annotated event classes, typed channels, `IEventPublisher` in a service |
| [OrderService — In-Process Outbox + RabbitMQ](outbox-inapp-rabbitmq.md) | RabbitMQ | Transactional Outbox with in-process relay; EF Core SQLite; `[AmqpExchange]` / `[AmqpRoutingKey]` annotations |
| [OrderService — Split Outbox + MassTransit RabbitMQ](outbox-relay-masstransit.md) | MassTransit / RabbitMQ | Split outbox across two processes; API has no transport dependency; external relay worker; MassTransit publish channel |

---

## How to run a sample

All samples follow the same three-step workflow:

```bash
# 1. Navigate to the sample folder
cd samples/<sample-folder>

# 2. Start the required infrastructure
docker compose up -d

# 3. Run the project
dotnet run
```

Refer to each sample's own `README.md` for endpoint details, curl examples, and configuration options.

