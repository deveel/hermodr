//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;

namespace Hermodr
{
    [Trait("Function", "SchemaValidation")]
    public class SchemaValidationMiddlewareTests
    {
        private static CloudEvent MakeEvent(string type = "test.event", string? version = null, string? jsonData = null)
        {
            var cloudEvent = new CloudEvent
            {
                Type = type,
                Source = new Uri("https://test.example.com/source"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data = jsonData ?? """{"name": "John", "email": "john@test.com"}"""
            };

            if (version != null)
            {
                var attr = CloudEventAttribute.CreateExtension("dataversion", CloudEventAttributeType.String);
                cloudEvent[attr] = version;
            }

            return cloudEvent;
        }

        private static (IServiceProvider Provider, List<CloudEvent> Received) BuildProvider(
            Action<EventPublisherBuilder>? configure = null,
            Action<EventSchemaServicesOptions>? configureSchema = null)
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection().AddLogging();
            var builder = services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://test.example.com/source"));

            // Register schema services with test schemas
            services.AddEventSchemaServices(schemaOptions =>
            {
                schemaOptions.SchemaRegistrations.Add(registry =>
                {
                    var schemaV1 = new EventSchema("test.event", "1.0", "application/json");
                    schemaV1.Properties.Add(new EventProperty("name", "string", "1.0") { IsNullable = false });
                    schemaV1.Properties[0]!.Constraints.Add(new PropertyRequiredConstraint());
                    schemaV1.Properties.Add(new EventProperty("email", "string", "1.0") { IsNullable = false });
                    schemaV1.Properties[1]!.Constraints.Add(new PropertyRequiredConstraint());
                    registry.Register(schemaV1);

                    var schemaV2 = new EventSchema("test.event", "2.0", "application/json");
                    schemaV2.Properties.Add(new EventProperty("name", "string", "2.0") { IsNullable = false });
                    schemaV2.Properties[0]!.Constraints.Add(new PropertyRequiredConstraint());
                    schemaV2.Properties.Add(new EventProperty("email", "string", "2.0") { IsNullable = false });
                    schemaV2.Properties[1]!.Constraints.Add(new PropertyRequiredConstraint());
                    schemaV2.Properties.Add(new EventProperty("age", "int", "2.0") { IsNullable = true });
                    registry.Register(schemaV2);

                    var strictSchema = new EventSchema("strict.event", "1.0", "application/json");
                    strictSchema.Properties.Add(new EventProperty("name", "string", "1.0") { IsNullable = false });
                    strictSchema.Properties[0]!.Constraints.Add(new PropertyRequiredConstraint());
                    strictSchema.Properties.Add(new EventProperty("age", "int", "1.0") { IsNullable = false });
                    strictSchema.Properties[1]!.Constraints.Add(new PropertyRequiredConstraint());
                    registry.Register(strictSchema);
                });
            });

            configure?.Invoke(builder);
            builder.AddTestChannel(e => received.Add(e));
            return (services.BuildServiceProvider(), received);
        }

        // ─── Missing schema behavior ─────────────────────────────────────

        [Fact]
        public async Task Middleware_MissingSchemaAllow_Publishes()
        {
            var (provider, received) = BuildProvider(builder =>
                builder.UseSchemaValidation(opts =>
                    opts.MissingSchemaBehavior = MissingSchemaBehavior.Allow));

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("unknown.event");

            await publisher.PublishEventAsync(cloudEvent);

            Assert.Single(received);
        }

        [Fact]
        public async Task Middleware_MissingSchemaBlock_Throws()
        {
            var (provider, _) = BuildProvider(builder =>
                builder.UseSchemaValidation(opts =>
                    opts.MissingSchemaBehavior = MissingSchemaBehavior.Block));

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("unknown.event");

            var ex = await Assert.ThrowsAsync<SchemaValidationException>(() =>
                publisher.PublishEventAsync(cloudEvent));

            Assert.Contains("unknown.event", ex.Message);
            Assert.Equal("unknown.event", ex.EventType);
        }

        [Fact]
        public async Task Middleware_MissingSchemaFallback_Publishes()
        {
            var (provider, received) = BuildProvider(builder =>
                builder.UseSchemaValidation(opts =>
                    opts.MissingSchemaBehavior = MissingSchemaBehavior.Fallback));

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("unknown.event");

            await publisher.PublishEventAsync(cloudEvent);

            Assert.Single(received);
        }

        // ─── Validation failure behavior ───────────────────────────────

        [Fact]
        public async Task Middleware_ValidationFailureThrow_Throws()
        {
            var (provider, _) = BuildProvider(builder =>
                builder.UseSchemaValidation(opts =>
                {
                    opts.MissingSchemaBehavior = MissingSchemaBehavior.Block;
                    opts.ValidationFailureBehavior = ValidationFailureBehavior.Throw;
                }));

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("test.event", "1.0", """{"name": "John"}"""); // Missing email

            var ex = await Assert.ThrowsAsync<SchemaValidationException>(() =>
                publisher.PublishEventAsync(cloudEvent));

            Assert.Contains("email", ex.Message);
            Assert.NotEmpty(ex.Errors);
        }

        // ─── Valid event passes ─────────────────────────────────────────

        [Fact]
        public async Task Middleware_ValidEvent_Publishes()
        {
            var (provider, received) = BuildProvider(builder =>
                builder.UseSchemaValidation(opts =>
                {
                    opts.MissingSchemaBehavior = MissingSchemaBehavior.Block;
                    opts.ValidationFailureBehavior = ValidationFailureBehavior.Throw;
                }));

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("test.event", "1.0", """{"name": "John", "email": "john@test.com"}""");

            await publisher.PublishEventAsync(cloudEvent);

            Assert.Single(received);
        }

        // ─── Version-aware validation ───────────────────────────────────

        [Fact]
        public async Task Middleware_VersionedEvent_ValidatesAgainstCorrectVersion()
        {
            var (provider, received) = BuildProvider(builder =>
                builder.UseSchemaValidation(opts =>
                {
                    opts.MissingSchemaBehavior = MissingSchemaBehavior.Block;
                    opts.ValidationFailureBehavior = ValidationFailureBehavior.Throw;
                }));

            var publisher = provider.GetRequiredService<IEventPublisher>();

            // v1.0 requires name + email, v2.0 adds optional age
            var cloudEvent = MakeEvent("test.event", "1.0", """{"name": "John", "email": "john@test.com", "age": 30}""");

            await publisher.PublishEventAsync(cloudEvent);

            Assert.Single(received);
        }

        [Fact]
        public async Task Middleware_VersionedEvent_UnknownVersion_Blocks()
        {
            var (provider, _) = BuildProvider(builder =>
                builder.UseSchemaValidation(opts =>
                {
                    opts.MissingSchemaBehavior = MissingSchemaBehavior.Block;
                    opts.ValidationFailureBehavior = ValidationFailureBehavior.Throw;
                }));

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("test.event", "99.0", """{"name": "John"}""");

            var ex = await Assert.ThrowsAsync<SchemaValidationException>(() =>
                publisher.PublishEventAsync(cloudEvent));

            Assert.Contains("99.0", ex.Message);
        }

        // ─── Null event type ───────────────────────────────────────────

        [Fact]
        public async Task Middleware_EmptyEventType_Blocks()
        {
            var (provider, _) = BuildProvider(builder =>
                builder.UseSchemaValidation(opts =>
                {
                    opts.MissingSchemaBehavior = MissingSchemaBehavior.Block;
                    opts.ValidationFailureBehavior = ValidationFailureBehavior.Throw;
                }));

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = new CloudEvent
            {
                Source = new Uri("https://test.example.com/source"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data = """{"name": "John"}"""
            };

            var ex = await Assert.ThrowsAsync<SchemaValidationException>(() =>
                publisher.PublishEventAsync(cloudEvent));

            Assert.Contains("event type", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ─── Multiple validation errors ──────────────────────────────────

        [Fact]
        public async Task Middleware_MultipleValidationErrors_AllReported()
        {
            var (provider, _) = BuildProvider(builder =>
                builder.UseSchemaValidation(opts =>
                {
                    opts.MissingSchemaBehavior = MissingSchemaBehavior.Block;
                    opts.ValidationFailureBehavior = ValidationFailureBehavior.Throw;
                }));

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("strict.event", "1.0", """{}"""); // Missing name + age

            var ex = await Assert.ThrowsAsync<SchemaValidationException>(() =>
                publisher.PublishEventAsync(cloudEvent));

            Assert.Equal(2, ex.Errors.Count);
            Assert.Contains(ex.Errors, e => e.MemberNames.First() == "name");
            Assert.Contains(ex.Errors, e => e.MemberNames.First() == "age");
        }

        // ─── Schema validation disabled (no UseSchemaValidation) ────────

        [Fact]
        public async Task Middleware_NotRegistered_NoValidation()
        {
            var (provider, received) = BuildProvider(); // No UseSchemaValidation

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("test.event", "1.0", """{"name": "John"}"""); // Missing email

            await publisher.PublishEventAsync(cloudEvent);

            Assert.Single(received); // Published without validation
        }

        // ─── Schema services auto-registration ─────────────────────────

        [Fact]
        public async Task Middleware_AutoRegistersSchemaServices()
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection().AddLogging();
            var builder = services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://test.example.com/source"));

            // No explicit AddEventSchemaServices - should be auto-registered
            builder.UseSchemaValidation(opts =>
            {
                opts.MissingSchemaBehavior = MissingSchemaBehavior.Block;
                opts.ValidationFailureBehavior = ValidationFailureBehavior.Throw;
            }, schemaOptions =>
            {
                schemaOptions.SchemaRegistrations.Add(registry =>
                {
                    var schema = new EventSchema("auto.event", "1.0", "application/json");
                    schema.Properties.Add(new EventProperty("name", "string", "1.0"));
                    schema.Properties[0]!.Constraints.Add(new PropertyRequiredConstraint());
                    registry.Register(schema);
                });
            });

            builder.AddTestChannel(e => received.Add(e));
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            // Valid event should pass
            var validEvent = MakeEvent("auto.event", "1.0", """{"name": "AutoTest"}""");
            await publisher.PublishEventAsync(validEvent);
            Assert.Single(received);

            // Invalid event should throw
            var invalidEvent = MakeEvent("auto.event", "1.0", """{}""");
            await Assert.ThrowsAsync<SchemaValidationException>(() =>
                publisher.PublishEventAsync(invalidEvent));
        }

        // ─── Event with no version uses latest ─────────────────────────

        [Fact]
        public async Task Middleware_NoVersion_UsesLatest()
        {
            var (provider, received) = BuildProvider(builder =>
                builder.UseSchemaValidation(opts =>
                {
                    opts.MissingSchemaBehavior = MissingSchemaBehavior.Block;
                    opts.ValidationFailureBehavior = ValidationFailureBehavior.Throw;
                }));

            var publisher = provider.GetRequiredService<IEventPublisher>();

            // No version specified - should use latest (2.0) which allows optional age
            var cloudEvent = MakeEvent("test.event", version: null, jsonData: """{"name": "John", "email": "john@test.com", "age": 30}""");

            await publisher.PublishEventAsync(cloudEvent);

            Assert.Single(received);
        }
    }
}
