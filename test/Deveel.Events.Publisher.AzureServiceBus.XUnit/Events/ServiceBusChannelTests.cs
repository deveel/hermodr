using System.ComponentModel.DataAnnotations;
using System.Text;

using Azure.Messaging;
using Azure.Messaging.ServiceBus;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Deveel.Events {
	[Trait("Channel", "ServiceBus")]
	[Trait("Function", "Publish")]
	public class ServiceBusChannelTests : IDisposable
	{
		public ServiceBusChannelTests(ITestOutputHelper outputHelper)
		{
			var services = new ServiceCollection();
			services.AddLogging(builder => builder.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Debug));

			services.AddSingleton<IServiceBusClientFactory>(new TestServiceBusClientFactory(OnMessageSent));

			services.AddEventPublisher(options =>
			{
				options.DataSchemaBaseUri = new Uri("http://example.com/events/schema");
                options.Source = new Uri("https://api.svc.deveel.com/test-service");
			})
			.AddServiceBusChannel(options =>
			{
				options.ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc12345";
				options.QueueName = "test-queue";
			});

			Services = services.BuildServiceProvider();
		}

		private IServiceProvider Services { get; }

		private EventPublisher Publisher => Services.GetRequiredService<EventPublisher>();

		private ServiceBusMessage? MessageSent { get; set; }

		private void OnMessageSent(ServiceBusMessage message)
		{
			MessageSent = message;
		}

		public void Dispose()
		{
			(Services as IDisposable)?.Dispose();
		}

		[Fact]
		public async Task PublishEventWithBinaryData()
		{
			var cloudEvent = new CloudNative.CloudEvents.CloudEvent
			{
				Subject = "test",
				DataContentType = "application/binary",
				Data = Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes("Hello, World!"))),
				Source = new Uri("https://api.svc.deveel.com/test-service"),
				Type = "test.created",
				Time = DateTime.UtcNow,
				Id = Guid.NewGuid().ToString("N"),
				DataSchema = new Uri("http://example.com/schema/1.0")
			};

			await Publisher.PublishEventAsync(cloudEvent);

			Assert.NotNull(MessageSent);
			Assert.Equal("test", MessageSent!.Subject);
			Assert.Equal("application/binary", MessageSent.ContentType);
			Assert.NotNull(MessageSent.Body);
			Assert.NotNull(MessageSent.ApplicationProperties);
			Assert.NotEmpty(MessageSent.ApplicationProperties);
			Assert.NotNull(MessageSent.ApplicationProperties[ServiceBusMessageProperties.EventType]);
			Assert.Equal("test.created", MessageSent.ApplicationProperties[ServiceBusMessageProperties.EventType]);
		}

		[Fact]
		public async Task PublishEventWithJsonData()
		{
			var cloudEvent = new CloudNative.CloudEvents.CloudEvent
			{
				Subject = "test",
				DataContentType = "application/json",
				Data = Encoding.UTF8.GetBytes("{\"message\": \"Hello, World!\"}"),
				Source = new Uri("https://api.svc.deveel.com/test-service"),
				Type = "test.created",
				Time = DateTime.UtcNow,
				Id = Guid.NewGuid().ToString("N"),
				DataSchema = new Uri("http://example.com/schema/1.0")
			};

			await Publisher.PublishEventAsync(cloudEvent);

			Assert.NotNull(MessageSent);
			Assert.Equal("test", MessageSent!.Subject);
			Assert.Equal("application/json", MessageSent.ContentType);
			Assert.NotNull(MessageSent.Body);
			Assert.NotNull(MessageSent.ApplicationProperties);
			Assert.NotEmpty(MessageSent.ApplicationProperties);
			Assert.NotNull(MessageSent.ApplicationProperties[ServiceBusMessageProperties.EventType]);
			Assert.Equal("test.created", MessageSent.ApplicationProperties[ServiceBusMessageProperties.EventType]);
		}

		[Fact]
		public async Task PublishEventData()
		{
			var personCreated = new PersonCreated
            {
                Name = "John Doe",
                Age = 30
            };

            await Publisher.PublishAsync(personCreated);

            Assert.NotNull(MessageSent);

            Assert.NotNull(MessageSent.Body);
            Assert.NotNull(MessageSent.ApplicationProperties);
            Assert.NotEmpty(MessageSent.ApplicationProperties);
            Assert.NotNull(MessageSent.ApplicationProperties[ServiceBusMessageProperties.EventType]);
            Assert.Equal("person.created", MessageSent.ApplicationProperties[ServiceBusMessageProperties.EventType]);
        }

		[Event("person.created", "1.0")]
		class PersonCreated
		{
			[Required]
            public string Name { get; set; }

			[Range(1, 100)]
            public int Age { get; set; }
        }
	}
}
