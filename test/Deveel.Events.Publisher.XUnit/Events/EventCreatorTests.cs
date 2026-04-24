using Microsoft.Extensions.DependencyInjection;

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deveel.Events
{
    public class EventCreatorTests
    {
        public EventCreatorTests()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher();

            var provider = services.BuildServiceProvider();

            EventCreator = provider.GetRequiredService<IEventCreator>();
        }

        private IEventCreator EventCreator { get; }

        [Fact]
        public void CreateFromData()
        {
            var @event = EventCreator.CreateEventFromData(new PersonCreated
            {
                FirstName = "John",
                LastName = "Doe",
                Id = "12345",
                Age = 30
            });

            Assert.Equal("person.created", @event.Type);
            Assert.Equal("https://deveel.com/events/person/schema/1.0", @event.DataSchema.ToString());
            Assert.Null(@event.Id);
            Assert.Null(@event.Source);

            var attributes = @event.GetPopulatedAttributes()?.ToDictionary(x => x.Key.Name, y => y.Value);

            Assert.NotNull(attributes);
            Assert.NotEmpty(attributes);
            Assert.Contains("streamtype", attributes.Keys);
            Assert.Equal("person", attributes["streamtype"]);
            Assert.Equal("application/cloudevents+json", @event.DataContentType);

            var json = Assert.IsType<string>(@event.Data);

            Assert.NotNull(json);

            var personCreated = JsonSerializer.Deserialize<PersonCreated>(json);

            Assert.NotNull(personCreated);
            Assert.Equal("John", personCreated.FirstName);
            Assert.Equal("Doe", personCreated.LastName);
            Assert.Equal("12345", personCreated.Id);
            Assert.Equal(30, personCreated.Age);
        }

        [Fact]
        public void CreateFromDataWithoutEventAttribute()
        {
            Assert.Throws<ArgumentException>(() => EventCreator.CreateEventFromData(new { Name = "John" }));
        }

        [Fact]
        public void CreateFromData_WithVersion_AndDataSchemaBaseUri()
        {
            // PersonCreatedByVersion uses a version string, not a schema URI
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.DataSchemaBaseUri = new Uri("https://example.com/events");
            });
            var creator = services.BuildServiceProvider().GetRequiredService<IEventCreator>();

            var @event = creator.CreateEventFromData(typeof(PersonCreatedByVersion), new PersonCreatedByVersion
            {
                FirstName = "Jane",
                LastName = "Doe"
            });

            Assert.Equal("person.created.versioned", @event.Type);
            // DataSchema should be based on the base URI + event type + version
            Assert.NotNull(@event.DataSchema);
            Assert.Contains("person.created.versioned", @event.DataSchema!.ToString());
            Assert.Contains("2.0", @event.DataSchema!.ToString());
        }

        [Fact]
        public void CreateFromData_WithVersion_WithoutDataSchemaBaseUri_Throws()
        {
            // No DataSchemaBaseUri set → should throw when event has DataVersion only
            var services = new ServiceCollection();
            services.AddEventPublisher();  // DataSchemaBaseUri not set
            var creator = services.BuildServiceProvider().GetRequiredService<IEventCreator>();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                creator.CreateEventFromData(typeof(PersonCreatedByVersion), new PersonCreatedByVersion()));

            Assert.Contains("EventPublisherOptions.DataSchemaBaseUri", ex.Message);
            Assert.Contains("AddEventPublisher", ex.Message);
        }

        [Fact]
        public void CreateFromData_NullDataType_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                EventCreator.CreateEventFromData(null!, new object()));
        }

        [Event("person.created", "https://deveel.com/events/person/schema/1.0")]
        [EventAttributes("streamtype", "person")]
        class PersonCreated
        {
            [JsonPropertyName("first_name"), Required]
            public string FirstName { get; set; }

            [JsonPropertyName("last_name"), Required]

            public string LastName { get; set; }

            [JsonPropertyName("id"), Required]
            public string Id { get; set; }

            [JsonPropertyName("age")]
            public int? Age { get; set; }

            [JsonPropertyName("email")]
            public Email? Email { get; set; }
        }

        [Event("person.created.versioned", "2.0")]
        class PersonCreatedByVersion
        {
            [JsonPropertyName("first_name")]
            public string? FirstName { get; set; }

            [JsonPropertyName("last_name")]
            public string? LastName { get; set; }
        }

        class Email
        {
            [JsonPropertyName("display_name")]
            public string? DisplayName { get; set; }

            [JsonPropertyName("address"), Required]
            public string Address { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }
        }
    }
}
