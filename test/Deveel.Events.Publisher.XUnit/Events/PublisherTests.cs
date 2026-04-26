using System.Text.Json;
using System.Text.Json.Serialization;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

			await Publisher.PublishEventAsync(@event, null, TestContext.Current.CancellationToken);

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
			}, null, TestContext.Current.CancellationToken);

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

            await Publisher.PublishAsync(personDeleted, null, TestContext.Current.CancellationToken);

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

        [Fact]
        public async Task PublishEvent_IdAlreadySet_IsNotOverridden()
        {
            var existingId = "fixed-id-12345";
            var @event = new CloudEvent
            {
                Type = "test.event",
                Id = existingId,
                Source = new Uri("https://api.svc.deveel.com/test-service"),
            };

            await Publisher.PublishEventAsync(@event, null,  TestContext.Current.CancellationToken);

            Assert.Single(Events);
            Assert.Equal(existingId, Events[0].Id);
        }

        [Fact]
        public async Task PublishEvent_SourceAlreadySet_IsNotOverridden()
        {
            var customSource = new Uri("https://custom.source.example.com");
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = customSource,
            };

            await Publisher.PublishEventAsync(@event, null, TestContext.Current.CancellationToken);

            Assert.Single(Events);
            Assert.Equal(customSource, Events[0].Source);
        }

        [Fact]
        public async Task PublishEvent_TimeAlreadySet_IsNotOverridden()
        {
            var fixedTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.svc.deveel.com/test-service"),
                Time = fixedTime,
            };

            await Publisher.PublishEventAsync(@event, null, TestContext.Current.CancellationToken);

            Assert.Single(Events);
            Assert.Equal(fixedTime, Events[0].Time);
        }

        [Fact]
        public async Task PublishEvent_NoSource_GetsSourceFromOptions()
        {
            var @event = new CloudEvent
            {
                Type = "test.event",
            };

            await Publisher.PublishEventAsync(@event,  null, TestContext.Current.CancellationToken);

            Assert.Single(Events);
            Assert.Equal(new Uri("https://api.svc.deveel.com/test-service"), Events[0].Source);
        }

        [Fact]
        public async Task PublishEvent_NoTime_GetsTimeAutoSet()
        {
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.svc.deveel.com"),
            };

            await Publisher.PublishEventAsync(@event, null, TestContext.Current.CancellationToken);

            Assert.Single(Events);
            Assert.NotNull(Events[0].Time);
        }

        [Fact]
        public async Task PublishEvent_NoId_GetsIdAutoSet()
        {
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.svc.deveel.com"),
            };

            await Publisher.PublishEventAsync(@event,  null, TestContext.Current.CancellationToken);

            Assert.Single(Events);
            Assert.NotNull(Events[0].Id);
            Assert.NotEmpty(Events[0].Id!);
        }

        [Fact]
        public async Task PublishEvent_Attributes_VariousTypes_AreSet()
        {
            // Build a publisher with various attribute types
            var publishedEvents = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.Attributes["strattr"] = "hello";
                options.Attributes["intattr"] = 42;
                options.Attributes["boolattr"] = true;
                options.Attributes["uriattr"] = new Uri("https://example.com");
                options.Attributes["tsattr"] = DateTimeOffset.UtcNow;
            }).AddTestChannel(e => publishedEvents.Add(e));

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();
            await publisher.PublishEventAsync(new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.example.com"),
            }, null, TestContext.Current.CancellationToken);

            Assert.Single(publishedEvents);
            // Just verify the event was published successfully with all attribute types
        }

        [Fact]
        public async Task PublishEvent_ChannelThrows_ThrowOnErrorsFalse_Swallows()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.ThrowOnErrors = false;
            });
            services.AddSingleton<IEventPublishChannel>(new ThrowingChannel());

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>();

            // Should not throw
            await publisher.PublishEventAsync(new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.example.com"),
            }, null, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task PublishEvent_ChannelThrows_ThrowOnErrorsTrue_Throws()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.ThrowOnErrors = true;
            });
            services.AddSingleton<IEventPublishChannel>(new ThrowingChannel());

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>();

            await Assert.ThrowsAsync<EventPublishException>(() =>
                publisher.PublishEventAsync(new CloudEvent
                {
                    Type = "test.event",
                    Source = new Uri("https://api.example.com"),
                }, null, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task PublishEventData_ThrowOnErrorsFalse_WhenNoEventCreator_Swallows()
        {
            // Publisher without EventCreator - CreateEventFromData throws NotSupportedException
            var services = new ServiceCollection();
            services.AddOptions<EventPublisherOptions>();
            services.AddSingleton<IEventPublishChannel>(new ThrowingChannel());
            services.AddSingleton<IEventIdGenerator>(EventGuidGenerator.Default);
            services.AddSingleton<IEventSystemTime>(EventSystemTime.Instance);

            var provider = services.BuildServiceProvider();
            var publisher = new EventPublisher(
                provider.GetRequiredService<IOptions<EventPublisherOptions>>(),
                provider.GetRequiredService<IEnumerable<IEventPublishChannel>>(),
                eventCreator: null  // No event creator
            );

            // ThrowOnErrors = false by default, should swallow
            await publisher.PublishAsync(typeof(PersonCreated), new PersonCreated { Id = "1" }, null, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task PublishEventData_ThrowOnErrorsTrue_WhenNoEventCreator_Throws()
        {
            var services = new ServiceCollection();
            services.AddOptions<EventPublisherOptions>()
                .Configure(o => o.ThrowOnErrors = true);
            services.AddSingleton<IEventPublishChannel>(new ThrowingChannel());
            services.AddSingleton<IEventIdGenerator>(EventGuidGenerator.Default);
            services.AddSingleton<IEventSystemTime>(EventSystemTime.Instance);

            var provider = services.BuildServiceProvider();
            var publisher = new EventPublisher(
                provider.GetRequiredService<IOptions<EventPublisherOptions>>(),
                provider.GetRequiredService<IEnumerable<IEventPublishChannel>>(),
                eventCreator: null
            );

            await Assert.ThrowsAsync<EventPublishException>(() =>
                publisher.PublishAsync(typeof(PersonCreated), new PersonCreated { Id = "1" }, null, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task PublishEventFactory_ThrowOnErrorsFalse_WhenFactoryThrows_Swallows()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.ThrowOnErrors = false;
            }).AddTestChannel(_ => { });

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Should not throw
            await publisher.PublishAsync(new BrokenConvertibleEvent(), null, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task PublishEventFactory_ThrowOnErrorsTrue_WhenFactoryThrows_Throws()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.ThrowOnErrors = true;
            }).AddTestChannel(_ => { });

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            await Assert.ThrowsAsync<EventPublishException>(() =>
                publisher.PublishAsync(new BrokenConvertibleEvent(), null, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task PublishAsync_Generic_UsesDataType()
        {
            await Publisher.PublishAsync(new PersonCreated
            {
                Id = "generic-test",
                FirstName = "Generic",
                LastName = "Test"
            }, null, TestContext.Current.CancellationToken);

            Assert.Single(Events);
            Assert.Equal("person.created", Events[0].Type);
        }

        [Fact]
        public async Task PublishEventConvertibleThrowErrorDisabled_NullEvent_NotThrows()
        {
            await Publisher.PublishAsync<PersonDeleted>(null!, null, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task PublishEventConvertibleThrowErrorEnabled_NullEvent_Throws()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://api.svc.deveel.com/test-service");
                options.ThrowOnErrors = true;
            }).AddTestChannel(_ => { });

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            await Assert.ThrowsAsync<EventPublishException>(() =>
                publisher.PublishAsync<PersonDeleted>(null!, null, TestContext.Current.CancellationToken));
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

		class PersonDeleted : IEventConvertible
		{
			public string Id { get; set; }

			public string FirstName { get; set; }

            public string LastName { get; set; }

            public CloudEvent ToEvent()
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

        private class ThrowingChannel : IEventPublishChannel
        {
            public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Channel failure simulation");
        }

        private class BrokenConvertibleEvent : IEventConvertible
        {
            public CloudEvent ToEvent() => throw new InvalidOperationException("Factory broken");
        }
    }
}
