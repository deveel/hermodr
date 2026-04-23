# Installation

Deveel Events is distributed as a set of NuGet packages.  Install only the packages you actually need.

## Prerequisites

- .NET 8, 9, or 10
- A project that uses Microsoft Dependency Injection (`Microsoft.Extensions.DependencyInjection`)

## Core package

Every application that publishes events needs the core publisher package:

```bash
dotnet add package Deveel.Events.Publisher
```

## Channel packages

Add one or more channel packages depending on the transports you want to use:

```bash
# Azure Service Bus
dotnet add package Deveel.Events.Publisher.AzureServiceBus

# RabbitMQ
dotnet add package Deveel.Events.Publisher.RabbitMq

# MassTransit
dotnet add package Deveel.Events.Publisher.MassTransit

# HTTP Webhooks
dotnet add package Deveel.Events.Publisher.Webhook
```

## Annotation package

If you want to annotate your data-transfer classes with event metadata:

```bash
dotnet add package Deveel.Events.Annotations
```

For AMQP-specific routing metadata (exchange name, routing key):

```bash
dotnet add package Deveel.Events.Amqp.Annotations
```

## Schema packages

```bash
# Core schema model, builder, JSON writer, and validator
dotnet add package Deveel.Events.Schema

# Export schemas as YAML
dotnet add package Deveel.Events.Schema.Yaml

# Export schemas as AsyncAPI 2.x documents (JSON or YAML)
dotnet add package Deveel.Events.Schema.AsyncApi
```

## Test package

```bash
dotnet add package Deveel.Events.TestPublisher
```

## Pre-release builds

Pre-release packages are published to **GitHub Packages**.  To consume them, add the Deveel GitHub Packages feed to your NuGet sources:

```xml
<!-- nuget.config -->
<configuration>
  <packageSources>
    <add key="deveel-github" value="https://nuget.pkg.github.com/deveel/index.json" />
  </packageSources>
</configuration>
```

You will also need a GitHub Personal Access Token (PAT) with `read:packages` scope and add it as a credential for the feed.

## What's next?

→ [Quick Start](quick-start.md) — publish your first event in minutes.

