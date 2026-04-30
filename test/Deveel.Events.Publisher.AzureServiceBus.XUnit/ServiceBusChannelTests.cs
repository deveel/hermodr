using System.ComponentModel.DataAnnotations;
using System.Text;

using Azure.Messaging.ServiceBus;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Deveel.Events {
	[Trait("Channel", "ServiceBus")]
	[Trait("Function", "Publish")]
	public class ServiceBusChannelTests : IClassFixture<ServiceBusTestServer>, IAsyncLifetime
	{
		private const string QueueName = "test-queue";

		private readonly string _connectionString;
		private ServiceBusClient? _receiverClient;
		private ServiceBusReceiver? _receiver;

		public ServiceBusChannelTests(ServiceBusTestServer testServer, ITestOutputHelper outputHelper)
		{
			_connectionString = testServer.ConnectionString;

			var services = new ServiceCollection();
			services.AddLogging(builder => builder.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Debug));

			services.AddEventPublisher(options =>
			{
				options.DataSchemaBaseUri = new Uri("http://example.com/events/schema");
                options.Source = new Uri("https://api.svc.deveel.com/test-service");
			})
			.AddServiceBus(options =>
			{
				options.ConnectionString = _connectionString;
				options.QueueName = QueueName;
			});

			Services = services.BuildServiceProvider();
		}

		private IServiceProvider Services { get; }

		private EventPublisher Publisher => Services.GetRequiredService<EventPublisher>();

		public async ValueTask InitializeAsync()
		{
			_receiverClient = new ServiceBusClient(_connectionString);
			_receiver = _receiverClient.CreateReceiver(QueueName);
		}

		public async ValueTask DisposeAsync()
		{
			if (_receiver != null)
				await _receiver.DisposeAsync();

			if (_receiverClient != null)
				await _receiverClient.DisposeAsync();

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
				Time = DateTimeOffset.UtcNow,
				Id = Guid.NewGuid().ToString("N"),
				DataSchema = new Uri("http://example.com/schema/1.0")
			};

			await Publisher.PublishEventAsync(cloudEvent, cancellationToken: TestContext.Current.CancellationToken);

			var received = await _receiver!.ReceiveMessageAsync(
				maxWaitTime: TimeSpan.FromSeconds(30),
				cancellationToken: TestContext.Current.CancellationToken);

			Assert.NotNull(received);
			Assert.Equal("test", received.Subject);
			Assert.Equal("application/binary", received.ContentType);
			Assert.NotNull(received.Body);
			Assert.NotNull(received.ApplicationProperties);
			Assert.NotEmpty(received.ApplicationProperties);
			Assert.True(received.ApplicationProperties.ContainsKey(ServiceBusMessageProperties.EventType));
			Assert.Equal("test.created", received.ApplicationProperties[ServiceBusMessageProperties.EventType]);

			await _receiver.CompleteMessageAsync(received, TestContext.Current.CancellationToken);
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
				Time = DateTimeOffset.UtcNow,
				Id = Guid.NewGuid().ToString("N"),
				DataSchema = new Uri("http://example.com/schema/1.0")
			};

			await Publisher.PublishEventAsync(cloudEvent, cancellationToken: TestContext.Current.CancellationToken);

			var received = await _receiver!.ReceiveMessageAsync(
				maxWaitTime: TimeSpan.FromSeconds(30),
				cancellationToken: TestContext.Current.CancellationToken);

			Assert.NotNull(received);
			Assert.Equal("test", received.Subject);
			Assert.Equal("application/json", received.ContentType);
			Assert.NotNull(received.Body);
			Assert.NotNull(received.ApplicationProperties);
			Assert.NotEmpty(received.ApplicationProperties);
			Assert.True(received.ApplicationProperties.ContainsKey(ServiceBusMessageProperties.EventType));
			Assert.Equal("test.created", received.ApplicationProperties[ServiceBusMessageProperties.EventType]);

			await _receiver.CompleteMessageAsync(received, TestContext.Current.CancellationToken);
		}

		[Fact]
		public async Task PublishEventData()
		{
			var personCreated = new PersonCreated
            {
                Name = "John Doe",
                Age = 30
            };

            await Publisher.PublishAsync(personCreated, cancellationToken: TestContext.Current.CancellationToken);

			var received = await _receiver!.ReceiveMessageAsync(
				maxWaitTime: TimeSpan.FromSeconds(30),
				cancellationToken: TestContext.Current.CancellationToken);

			Assert.NotNull(received);
            Assert.NotNull(received.Body);
            Assert.NotNull(received.ApplicationProperties);
            Assert.NotEmpty(received.ApplicationProperties);
            Assert.True(received.ApplicationProperties.ContainsKey(ServiceBusMessageProperties.EventType));
            Assert.Equal("person.created", received.ApplicationProperties[ServiceBusMessageProperties.EventType]);

			await _receiver.CompleteMessageAsync(received, TestContext.Current.CancellationToken);
        }

		[Event("person.created", "1.0")]
		class PersonCreated
		{
			[Required]
            public string Name { get; set; } = string.Empty;

			[Range(1, 100)]
            public int Age { get; set; }
        }
	}
}
