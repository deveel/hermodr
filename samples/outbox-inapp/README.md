# OrderService — In-App Transactional Outbox with AMQP

This sample demonstrates the **Transactional Outbox** pattern running within a **single ASP.NET process**:

| Component | Role |
|---|---|
| Minimal API | Accepts HTTP requests; persists CloudEvents to SQLite outbox via EF Core |
| BackgroundService | Polls the outbox every 5 seconds and forwards pending events to **RabbitMQ via AMQP** |

### Difference from `outbox-relay`

In `outbox-relay` the API and relay are **separate processes** (the relay runs as an independent `.NET Worker Service`) and uses **MassTransit** as the messaging abstraction.

Here both components run in the **same ASP.NET process** and the relay uses the **raw AMQP channel** to communicate with RabbitMQ directly. This is simpler but less scalable.

---

## Architecture

```
HTTP client
    │
    ▼
OrderService.InAppOutbox  (ASP.NET Minimal API — port 5000)
    │
    │  IEventPublisher.PublishAsync(event)
    ▼
OutboxPublishChannel
    │  EF Core INSERT
    ▼
outbox.db  (SQLite)
    ▲
    │  poll every 5 s
    │
OutboxRelayService  (BackgroundService inside same process)
    │
    │  IEventPublisher.PublishAsync(event, OutboxRelayPublishOptions)
    ▼
AMQP channel
    │
    ▼
RabbitMQ broker  (localhost:5672)
```

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/) (for RabbitMQ)

---

## Building dependencies

Before running the sample, you need to build the Deveel.Events library dependencies and output them to a `libs` folder. This approach allows you to reference compiled binaries instead of project references.

### Build dependencies

Choose the build script appropriate for your operating system:

**macOS / Linux:**
```bash
./build-libs.sh
```

**Windows (PowerShell):**
```powershell
.\build-libs.ps1
```

**Windows (Command Prompt):**
```cmd
build-libs.bat
```

The build scripts will:
1. Compile all required Deveel.Events library dependencies
2. Copy the compiled binaries to a `libs` folder at the sample root
3. Display the location of the compiled binaries
4. Delegate build/copy logic to shared core scripts in `samples/build-libs-core.sh`, `samples/build-libs-core.ps1`, and `samples/build-libs-core.bat`

### Referencing the binaries

After running the build script, update `OrderService.InAppOutbox/OrderService.InAppOutbox.csproj` to reference the binaries from the `libs` folder instead of the project references. Replace the `<ProjectReference>` items with `<Reference>` items pointing to the `libs` folder.

---

## Running the sample

**1. Start RabbitMQ**

```bash
docker compose up -d
```

The RabbitMQ management UI is available at <http://localhost:15672> (`guest` / `guest`).

**2. Start the API**

```bash
cd OrderService.InAppOutbox
dotnet run
```

The API listens on <http://localhost:5000>. The SQLite outbox file (`outbox.db`) is created automatically in the working directory.

---

## Trying it out

Create an order:

```bash
curl -s -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "customer-42",
    "items": [
      { "productId": "sku-001", "productName": "Widget", "quantity": 2, "unitPrice": 9.99 }
    ]
  }' | jq .
```

Within ~5 seconds you will see console logs indicating:
- The outbox relay processing the pending event
- The AMQP channel publishing the CloudEvent to RabbitMQ

The corresponding message should appear in the RabbitMQ management UI under the relevant exchange.

---

## Key configuration

### `OrderService.InAppOutbox/appsettings.json`

```json
{
  "ConnectionStrings": {
    "Outbox": "Data Source=outbox.db"
  }
}
```

Override any value with an environment variable using the `__` separator, e.g.:

```bash
ConnectionStrings__Outbox="Data Source=/shared/outbox.db" dotnet run
```

## Key components

- `OrderService.InAppOutbox/Program.cs` — Host setup, event publisher configuration, outbox relay service registration
- `OrderService.InAppOutbox/Services/OutboxRelayService.cs` — BackgroundService that polls and relays events
- `OrderService.InAppOutbox/Endpoints/` — API endpoints for order management
- `OrderService.InAppOutbox/Events/` — Domain event classes

## Notes

- The in-app relay is suitable for single-process deployments with low event volume.
- For distributed systems where API and relay can scale independently, see the `outbox-relay` sample.
- The outbox pattern ensures at-least-once event delivery even if the relay fails.


