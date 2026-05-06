# OrderService — Simple ASP.NET Publisher

This sample demonstrates a minimal ASP.NET Minimal API that publishes domain events to **RabbitMQ** using the **Deveel.Events** framework with the raw AMQP transport channel.

## Architecture

```
HTTP client
    │
    ▼
OrderService.SimplePublisher  (ASP.NET Minimal API — port 5000)
    │
    │  IEventPublisher.PublishAsync(event)
    ▼
RabbitMQ direct channel
    │
    ▼
RabbitMQ broker  (localhost:5672)
```

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/) (for RabbitMQ)

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

After running the build script, update `OrderService.SimplePublisher/OrderService.SimplePublisher.csproj` to reference the binaries from the `libs` folder instead of the project references. Replace the `<ProjectReference>` items with `<Reference>` items pointing to the `libs` folder.

## Running the sample

**1. Start RabbitMQ**

```bash
docker compose up -d
```

The RabbitMQ management UI is available at <http://localhost:15672> (`guest` / `guest`).

**2. Start the API**

```bash
cd OrderService.SimplePublisher
dotnet run
```

The API listens on <http://localhost:5000>.

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

You can observe the CloudEvent message published to RabbitMQ in the management UI.

## Key components

- `OrderService.SimplePublisher/Program.cs` — Host setup, event publisher configuration
- `OrderService.SimplePublisher/Endpoints/` — API endpoints for order management
- `OrderService.SimplePublisher/Events/` — Domain event classes

## Notes

- This sample uses the raw AMQP channel for direct RabbitMQ communication.
- The sample publishes events synchronously but can be modified to support queuing patterns.


