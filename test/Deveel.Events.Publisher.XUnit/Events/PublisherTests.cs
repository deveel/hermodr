using System.Text.Json;
using System.Text.Json.Serialization;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Deveel.Events {
	public class PublisherTests
    {
		public PublisherTests(ITestOutputHelper outputHelper) {
			var services = new ServiceCollection()
				.AddLogging(logging => logging.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Debug));

			var builder = services
				.AddEventPublisher(options => {
					options.Source = new Uri("https://api.svc.deveel.com/test-service");
					options.Attributes.Add("env", "test");
				})
				.AddTestChannel(@event => Events.Add(@event));

			var provider = services.BuildServiceProvider();
			Publisher = provider.GetRequiredService<EventPublisher>();
		}

		private IList<CloudEvent> Events { get; } = new List<CloudEvent>();

		private EventPublisher Publisher { get; }

		[Fact]
		public async Task PublishSimpleEvent() {
			var @event = new CloudEvent {
				Type = "person.created",
				DataSchema = new Uri("http://example.com/schema/1.0"),
				Source = new Uri("https://api.svc.deveel.com/test-service"),
				Time = DateTime.UtcNow,
				Id = Guid.NewGuid().ToString("N"),
				DataContentType = "application/json",
				Data = JsonSerializer.Serialize(new {
					FirstName = "John",
					LastName = "Doe"
				}),
			};

			@event[CloudEventAttribute.CreateExtension("env", CloudEventAttributeType.String)] = "test";

			await Publisher.PublishEventAsync(@event);

			Assert.Single(Events);
			Assert.Equal("person.created", Events[0].Type);
			Assert.NotNull(Events[0].DataSchema);
			Assert.Equal("http://example.com/schema/1.0", Events[0].DataSchema!.ToString());

			Assert.NotNull(Events[0].Id);
			Assert.NotNull(Events[0].Source);
			Assert.Equal("https://api.svc.deveel.com/test-service", Events[0].Source!.ToString());
			Assert.Equal("test", Events[0]["env"]);
		}

		[Fact]
		public async Task PublishEventData() {
			await Publisher.PublishAsync(new PersonCreated {
				Id = "123",
				FirstName = "John",
				LastName = "Doe"
			});

			Assert.Single(Events);
			Assert.Equal("person.created", Events[0].Type);
			Assert.Equal("https://example.com/events/person.created/1.0", Events[0].DataSchema!.ToString());
			Assert.NotNull(Events[0].Id);
			Assert.Equal("https://api.svc.deveel.com/test-service", Events[0].Source!.ToString());
			Assert.Equal("test", Events[0]["env"]);

			Assert.Equal("application/cloudevents+json", Events[0].DataContentType);
			var json = Assert.IsType<string>(Events[0].Data);

            var data = JsonSerializer.Deserialize<PersonCreated>(json);

            Assert.NotNull(data);
            Assert.Equal("123", data.Id);
            Assert.Equal("John", data.FirstName);
            Assert.Equal("Doe", data.LastName);
        }

        [Fact]
        public async Task PublishEventFactory()
        {
            var personDeleted = new PersonDeleted
            {
                Id = "123",
                FirstName = "John",
                LastName = "Doe"
            };

            await Publisher.PublishEventAsync(personDeleted);

            Assert.Single(Events);
            Assert.Equal("person.deleted", Events[0].Type);
            Assert.Equal("https://example.com/events/person.deleted/1.0", Events[0].DataSchema!.ToString());
            Assert.NotNull(Events[0].Id);
            Assert.Equal("https://api.svc.deveel.com/test-service", Events[0].Source!.ToString());
            Assert.Equal("test", Events[0]["env"]);

            Assert.Equal("application/json", Events[0].DataContentType);
            var json = Assert.IsType<string>(Events[0].Data);

            var data = JsonSerializer.Deserialize<PersonDeleted>(json);

            Assert.NotNull(data);
            Assert.Equal("123", data.Id);
            Assert.Equal("John", data.FirstName);
            Assert.Equal("Doe", data.LastName);
        }

        [Event("person.created", "https://example.com/events/person.created/1.0")]
		class PersonCreated {
			[JsonPropertyName("id")]
			public string Id { get; set; }

			[JsonPropertyName("first_name")]
			public string FirstName { get; set; }

			[JsonPropertyName("last_name")]
			public string LastName { get; set; }
		}

		class PersonDeleted : IEventFactory
		{
			public string Id { get; set; }

			public string FirstName { get; set; }

            public string LastName { get; set; }

            public CloudEvent CreateEvent()
			{
                return new CloudEvent
                {
                    Type = "person.deleted",
                    DataSchema = new Uri("https://example.com/events/person.deleted/1.0"),
                    Source = new Uri("https://api.svc.deveel.com/test-service"),
                    Time = DateTime.UtcNow,
                    Id = Guid.NewGuid().ToString("N"),
                    DataContentType = "application/json",
                    Data = JsonSerializer.Serialize(this),
                };
            }
		}
    }
}
