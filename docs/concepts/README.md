# Core Concepts

This section explains the fundamental building blocks of the Deveel Events framework.

## Overview

```
┌──────────────────────────────────────────────────────┐
│                   Your application                   │
│                                                      │
│   ┌──────────────┐      ┌──────────────────────────┐ │
│   │  Event data  │─────▶│      EventPublisher      │ │
│   │  (annotated  │      └──────────┬───────────────┘ │
│   │   classes)   │                 │ fan-out         │
│   └──────────────┘        ┌────────┴────────┐        │
│                           │                 │        │
│                   ┌───────▼─────┐   ┌───────▼──────┐ │
│                   │  Channel A  │   │  Channel B   │ │
│                   │ (Azure SB)  │   │  (Webhook)   │ │
│                   └─────────────┘   └──────────────┘ │
└──────────────────────────────────────────────────────┘
```

1. You describe an event using an annotated data class (or construct a raw `CloudEvent`).
2. You call `EventPublisher.PublishAsync` (or `PublishEventAsync`).
3. The publisher fans the event out to **every registered `IEventPublishChannel`**.
4. Each channel serialises the event and dispatches it to the appropriate transport.

## Key Abstractions

| Abstraction | Role |
|-------------|------|
| `EventPublisher` | The single entry point for publishing events |
| `IEventMiddleware` | A composable step in the publish pipeline (enrichment, validation, observability, …) |
| `EventContext` | Carries the current event, scoped services, options, and a data-sharing `Items` bag through the pipeline |
| `IEventPublishChannel` | One transport target (Azure Service Bus queue, RabbitMQ exchange, …) |
| `IBatchEventPublishChannel` | A channel that also supports delivering multiple events in a single batched call |
| `IEventFactory` | Converts an annotated data object into a `CloudEvent` |
| `IEventIdGenerator` | Generates unique identifiers for events (default: GUID) |
| `IEventSystemTime` | Supplies the event timestamp (replaceable for testing) |
| `IEventPublisherFactory` | Resolves a named publisher pipeline by name at runtime |

## Pages in this section

- [CloudEvents Standard](cloudevents.md)
- [Event Publisher](event-publisher.md)
- [Publish Pipeline & Middleware](publish-pipeline.md)
- [Publish Channels](publish-channels.md)
- [Event Annotations](event-annotations.md)

