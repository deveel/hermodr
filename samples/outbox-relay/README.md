# OrderService — Split Transactional Outbox with MassTransit

This sample demonstrates the **split Transactional Outbox** pattern across **two separate processes**:

| Process | Project | Role |
|---|---|---|
| Minimal API | `OrderService.Api` | Accepts HTTP requests; writes CloudEvents to the SQLite outbox only |
| Console worker | `OrderService.RelayWorker` | Polls the shared outbox; forwards events to **RabbitMQ via MassTransit** |

### Difference from `outbox-inapp`

In `outbox-inapp` both the API and the relay live inside the **same ASP.NET process**, and RabbitMQ is used directly via the raw AMQP channel.

Here the relay runs in an **independent process** (a `.NET Worker Service`) and uses **MassTransit** as the messaging abstraction on top of RabbitMQ. This decoupling lets you:

- Scale the relay independently from the API.
- Replace RabbitMQ with another MassTransit-supported transport (Azure Service Bus, Amazon SQS, …) with zero API changes.
- Restart the relay after failures without affecting API availability.

---

## Architecture

```
HTTP client
    │
    ▼
OrderService.Api  (ASP.NET Minimal API — port 5000)
    │
    │  IEventPublisher.PublishAsync(event)
    ▼
OutboxPublishChannel
    │  EF Core INSERT
    ▼
outbox.db  (SQLite — shared file)
    ▲
    │  poll every 5 s
    │
OutboxRelayService  (BackgroundService inside RelayWorker)
    │
    │  IEventPublisher.PublishAsync(event, OutboxRelayPublishOptions)
    ▼
MassTransitPublishChannel
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

After running the build script, update both project files to reference the binaries from the `libs` folder instead of the project references:
- `OrderService.Api/OrderService.Api.csproj`
- `OrderService.RelayWorker/OrderService.RelayWorker.csproj`

Replace the `<ProjectReference>` items with `<Reference>` items pointing to the `libs` folder.

---

## Running the sample

**1. Start RabbitMQ**

```bash
docker compose up -d
```

The RabbitMQ management UI is available at <http://localhost:15672> (`guest` / `guest`).

**2. Start the API** (terminal 1)

```bash
cd OrderService.Api
dotnet run
```

The API listens on <http://localhost:5000>. The SQLite outbox file (`outbox.db`) is created automatically in the API working directory.

**3. Start the relay worker** (terminal 2)

```bash
cd OrderService.RelayWorker
dotnet run
```

The worker connects to the **same** `outbox.db` (via the relative path configured in `appsettings.json`) and starts polling every 5 seconds.

> **Tip:** If you run both processes from different directories you may need to adjust the `ConnectionStrings:Outbox` path in `OrderService.RelayWorker/appsettings.json`, or set the `ConnectionStrings__Outbox` environment variable to an absolute path.

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

Within ~5 seconds you will see the relay worker log a `Sending` update and a `Sent` confirmation. The corresponding MassTransit message appears in the RabbitMQ management UI under the `orders` exchange.

---

## Key configuration

### `OrderService.Api/appsettings.json`

```json
{
  "ConnectionStrings": {
    "Outbox": "Data Source=outbox.db"
  }
}
```

### `OrderService.RelayWorker/appsettings.json`

```json
{
  "ConnectionStrings": {
    "Outbox": "Data Source=../OrderService.Api/outbox.db"
  },
  "Events": {
    "MassTransit": {
      "RabbitMq": {
        "Host":     "localhost",
        "Username": "guest",
        "Password": "guest"
      }
    }
  }
}
```

Override any value with an environment variable using the `__` separator, e.g.:

```bash
ConnectionStrings__Outbox="Data Source=/shared/outbox.db" dotnet run
```

