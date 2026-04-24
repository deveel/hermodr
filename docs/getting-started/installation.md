# Installation

Deveel Events is distributed as a set of NuGet packages.  Install only the packages you actually need.

## Prerequisites

### Supported Runtimes

All packages in the Deveel Events solution multi-target the following .NET runtimes:

| Runtime | Version |
|---------|---------|
| .NET | 8, 9, 10 |

> **.NET 8** is the current Long-Term Support (LTS) release and the recommended minimum for new projects.  
> **.NET 10** is the next LTS release (expected November 2025) and is fully supported from day one.

### Required Infrastructure

All packages depend on the **Microsoft Dependency Injection** infrastructure. Make sure your project references:

```bash
dotnet add package Microsoft.Extensions.DependencyInjection
```

This is already provided automatically in ASP.NET Core, Worker Service, and most other host-based project templates.

### ASP.NET Core Requirement

The `Deveel.Events.Schema.AsyncApi` package references the **ASP.NET Core shared framework** (`Microsoft.AspNetCore.App`) because it integrates with the [Saunter](https://github.com/tehmantra/saunter) AsyncAPI middleware. It must be used in a project that targets the `Microsoft.NET.Sdk.Web` SDK or explicitly includes the `Microsoft.AspNetCore.App` framework reference.

### Per-Package Dependencies

The table below lists the key NuGet packages that each library brings in as transitive dependencies. You do not need to install these directly — they are declared in each package's `.nuspec` and restored automatically.

| Package | Key Transitive Dependencies |
|---------|----------------------------|
| `Deveel.Events.Annotations` | *(none — pure attribute library)* |
| `Deveel.Events.Publisher` | `CloudNative.CloudEvents` · `Microsoft.Extensions.Options` · `Microsoft.Extensions.Logging.Abstractions` |
| `Deveel.Events.Publisher.AzureServiceBus` | `Azure.Messaging.ServiceBus` ≥ 7.20 |
| `Deveel.Events.Publisher.RabbitMq` | `RabbitMQ.Client` ≥ 7.2 · `Deveel.Events.Amqp.Annotations` |
| `Deveel.Events.Publisher.MassTransit` | `MassTransit` ≥ 9.1 |
| `Deveel.Events.Publisher.Webhook` | `Microsoft.Extensions.Http` · `Microsoft.Extensions.Http.Resilience` ≥ 9.6 |
| `Deveel.Events.Schema` | `CloudNative.CloudEvents` |
| `Deveel.Events.Schema.Yaml` | `YamlDotNet` ≥ 16.3 |
| `Deveel.Events.Schema.AsyncApi` | `Saunter` ≥ 0.13 · `YamlDotNet` ≥ 16.3 · ASP.NET Core shared framework |
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

