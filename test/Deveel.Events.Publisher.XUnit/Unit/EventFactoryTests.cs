// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Microsoft.Extensions.DependencyInjection;

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deveel.Events
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Application")]
    [Trait("Feature", "EventFactory")]
    public class EventFactoryTests
    {
        // ── Fields ────────────────────────────────────────────────────────────

        private readonly IEventFactory _eventFactory;

        // ── Constructor ───────────────────────────────────────────────────────

        public EventFactoryTests()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher();
            _eventFactory = services.BuildServiceProvider().GetRequiredService<IEventFactory>();
        }

        // ── CreateEventFromData ───────────────────────────────────────────────

        #region CreateEventFromData

        [Fact]
        public void Should_CreateCloudEvent_When_AnnotatedTypeIsProvided()
        {
            // Arrange
            var data = new PersonCreated { FirstName = "John", LastName = "Doe", Id = "12345", Age = 30 };

            // Act
            var @event = _eventFactory.CreateEventFromData(data);

            // Assert
            Assert.Equal("person.created", @event.Type);
            Assert.Equal("https://deveel.com/events/person/schema/1.0", @event.DataSchema!.ToString());
            Assert.Null(@event.Id);
            Assert.Null(@event.Source);

            var attributes = @event.GetPopulatedAttributes()?.ToDictionary(x => x.Key.Name, y => y.Value);
            Assert.NotNull(attributes);
            Assert.NotEmpty(attributes);
            Assert.Contains("streamtype", attributes.Keys);
            Assert.Equal("person", attributes["streamtype"]);
            Assert.Equal("application/cloudevents+json", @event.DataContentType);

            var json = Assert.IsType<string>(@event.Data);
            var personCreated = JsonSerializer.Deserialize<PersonCreated>(json);
            Assert.NotNull(personCreated);
            Assert.Equal(data.FirstName, personCreated.FirstName);
            Assert.Equal(data.LastName, personCreated.LastName);
            Assert.Equal(data.Id, personCreated.Id);
            Assert.Equal(data.Age, personCreated.Age);
        }

        [Fact]
        public void Should_ThrowArgumentException_When_TypeHasNoEventAttribute()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _eventFactory.CreateEventFromData(new { Name = "John" }));
        }

        [Fact]
        public void Should_SetDataSchema_When_VersionAndDataSchemaBaseUriAreConfigured()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.DataSchemaBaseUri = new Uri("https://example.com/events");
            });
            var creator = services.BuildServiceProvider().GetRequiredService<IEventFactory>();

            // Act
            var @event = creator.CreateEventFromData(typeof(PersonCreatedByVersion), new PersonCreatedByVersion
            {
                FirstName = "Jane",
                LastName  = "Doe"
            });

            // Assert
            Assert.Equal("person.created.versioned", @event.Type);
            Assert.NotNull(@event.DataSchema);
            Assert.Contains("person.created.versioned", @event.DataSchema!.ToString());
            Assert.Contains("2.0", @event.DataSchema!.ToString());
        }

        [Fact]
        public void Should_ThrowInvalidOperationException_When_VersionedTypeHasNoDataSchemaBaseUri()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();  // DataSchemaBaseUri not set
            var creator = services.BuildServiceProvider().GetRequiredService<IEventFactory>();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                creator.CreateEventFromData(typeof(PersonCreatedByVersion), new PersonCreatedByVersion()));

            Assert.Contains("EventPublisherOptions.DataSchemaBaseUri", ex.Message);
            Assert.Contains("AddEventPublisher", ex.Message);
        }

        [Fact]
        public void Should_SetDataSchema_When_VersionedTypeHasAssemblyDataSchemaUriAttribute()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();
            var creator = services.BuildServiceProvider().GetRequiredService<IEventFactory>();

            var eventType = CreateDynamicEventType(
                eventTypeName: "person.created.dynamic",
                dataVersion: "1.2",
                assemblyDataSchemaBaseUri: "https://schemas.dynamic.example/events");

            var instance = Activator.CreateInstance(eventType);

            // Act
            var @event = creator.CreateEventFromData(eventType, instance);

            // Assert
            Assert.NotNull(@event.DataSchema);
            Assert.Equal("https://schemas.dynamic.example/events/person.created.dynamic/1.2", @event.DataSchema!.ToString());
        }

        [Fact]
        public void Should_PreferOptionsDataSchemaBaseUri_OverAssemblyDataSchemaUriAttribute()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.DataSchemaBaseUri = new Uri("https://options.example/events");
            });
            var creator = services.BuildServiceProvider().GetRequiredService<IEventFactory>();

            var eventType = CreateDynamicEventType(
                eventTypeName: "person.created.precedence",
                dataVersion: "9.0",
                assemblyDataSchemaBaseUri: "https://assembly.example/events");

            var instance = Activator.CreateInstance(eventType);

            // Act
            var @event = creator.CreateEventFromData(eventType, instance);

            // Assert
            Assert.NotNull(@event.DataSchema);
            Assert.Equal("https://options.example/events/person.created.precedence/9.0", @event.DataSchema!.ToString());
        }

        [Fact]
        public void Should_ThrowArgumentNullException_When_NullDataTypeIsProvided()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _eventFactory.CreateEventFromData(null!, new object()));
        }

        [Fact]
        public void Should_ThrowInvalidOperationException_When_ContentTypeIsNullAndDefaultContentTypeIsNull()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher(options => options.DefaultContentType = null);
            var creator = services.BuildServiceProvider().GetRequiredService<IEventFactory>();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                creator.CreateEventFromData(typeof(EventWithNoContentType), new EventWithNoContentType()));

            Assert.Contains("content type", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        // ── Domain types ──────────────────────────────────────────────────────

        [Event("no.content.type", "1.0")]
        private class EventWithNoContentType { }

        [Event("person.created", "https://deveel.com/events/person/schema/1.0")]
        [EventAttributes("streamtype", "person")]
        private class PersonCreated
        {
            [JsonPropertyName("first_name"), Required]
            public string FirstName { get; set; } = string.Empty;

            [JsonPropertyName("last_name"), Required]
            public string LastName { get; set; } = string.Empty;

            [JsonPropertyName("id"), Required]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("age")]
            public int? Age { get; set; }

            [JsonPropertyName("email")]
            public Email? Email { get; set; }
        }

        [Event("person.created.versioned", "2.0")]
        private class PersonCreatedByVersion
        {
            [JsonPropertyName("first_name")]
            public string? FirstName { get; set; }

            [JsonPropertyName("last_name")]
            public string? LastName { get; set; }
        }

        private class Email
        {
            [JsonPropertyName("display_name")]
            public string? DisplayName { get; set; }

            [JsonPropertyName("address"), Required]
            public string Address { get; set; } = string.Empty;

            [JsonPropertyName("type")]
            public string? Type { get; set; }
        }

        private static Type CreateDynamicEventType(string eventTypeName, string dataVersion, string? assemblyDataSchemaBaseUri)
        {
            var assemblyName = new AssemblyName($"Deveel.Events.DynamicTests.{Guid.NewGuid():N}");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

            if (!String.IsNullOrWhiteSpace(assemblyDataSchemaBaseUri))
            {
                var assemblyAttributeCtor = typeof(EventDataSchemaUriAttribute)
                    .GetConstructor(new[] { typeof(string) })!;
                var assemblyAttribute = new CustomAttributeBuilder(assemblyAttributeCtor, new object[] { assemblyDataSchemaBaseUri! });
                assemblyBuilder.SetCustomAttribute(assemblyAttribute);
            }

            var moduleBuilder = assemblyBuilder.DefineDynamicModule($"{assemblyName.Name}.dll");
            var typeBuilder = moduleBuilder.DefineType(
                $"Dynamic.{Guid.NewGuid():N}.EventData",
                TypeAttributes.Class | TypeAttributes.Public);

            var eventAttributeCtor = typeof(EventAttribute)
                .GetConstructor(new[] { typeof(string), typeof(string) })!;
            var eventAttribute = new CustomAttributeBuilder(eventAttributeCtor, new object[] { eventTypeName, dataVersion });
            typeBuilder.SetCustomAttribute(eventAttribute);

            return typeBuilder.CreateType()!;
        }
    }
}
