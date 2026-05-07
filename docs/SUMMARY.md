# Summary

## Overview

* [Introduction](README.md)
* [Packages](packages.md)

## Getting Started

* [Installation](getting-started/installation.md)
* [Quick Start](getting-started/quick-start.md)

## Core Concepts

* [Overview](concepts/README.md)
* [CloudEvents Standard](concepts/cloudevents.md)
* [Event Publisher](concepts/event-publisher.md)
* [Event Creation](concepts/event-creation.md)
* [Publish Pipeline & Middleware](concepts/publish-pipeline.md)
* [Publish Channels](concepts/publish-channels.md)
* [Event Annotations](concepts/event-annotations.md)

## Publisher Channels

* [Overview](publishers/README.md)
* [Azure Service Bus](publishers/azure-service-bus.md)
* [RabbitMQ](publishers/rabbitmq.md)
  * [AMQP Annotations](publishers/rabbitmq.md#amqp-annotations)
* [MassTransit](publishers/masstransit.md)
* [Webhook](publishers/webhook.md)
* [Publish Error Handling](publishers/error-handling.md)
* [Transactional Outbox](publishers/outbox.md)
* [Dead-Letter Handling and Replay](publishers/dead-letter.md)
* [Typed Channels](publishers/typed-channels.md)
* [Named Channels](publishers/named-channels.md)

## Event Schema

* [Overview](schema/README.md)
* [Fluent Builder](schema/fluent-builder.md)
* [From Annotations](schema/from-annotations.md)
* [Export as JSON](schema/export-json.md)
* [Export as YAML](schema/export-yaml.md)
* [Export as AsyncAPI](schema/export-asyncapi.md)
* [Validation](schema/validation.md)

## Event Subscriptions

* [Overview](subscriptions/README.md)
* [Subscription Filters](subscriptions/filtering.md)
* [Filter Expressions](subscriptions/filter-expressions.md)
* [Event Dispatcher](subscriptions/dispatcher.md)
* [Routing Subscriptions](subscriptions/routing.md)
* [Custom Resolvers](subscriptions/custom-resolver.md)

## Testing

* [Test Publisher](testing/README.md)

## Samples

* [Overview](samples/README.md)
* [OrderService — Minimal API + RabbitMQ](samples/aspnet-publisher-rabbitmq.md)
* [OrderService — In-Process Outbox + RabbitMQ](samples/outbox-inapp-rabbitmq.md)
* [OrderService — In-Process Outbox with Scheduled Delivery](samples/outbox-inapp-scheduled-delivery.md)
* [OrderService — Split Outbox + MassTransit RabbitMQ](samples/outbox-relay-masstransit.md)
* [OrderService — In-Process Dead-Letter Replay](samples/deadletter-inproc.md)
* [OrderService — Split Dead-Letter Replay with Entity Framework](samples/deadletter-relay-entityframework.md)

## Project

* [Versioning](versioning.md)
* [AMQP Annotations (moved)](amqp/README.md)

## Contributing

* [Contributing](contributing.md)





