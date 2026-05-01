using System.ComponentModel.DataAnnotations;
using System.Text;

using Azure.Messaging.ServiceBus;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Deveel.Events
{
    /// <summary>
    /// Integration tests for the Azure Service Bus publish channel using the
    /// Testcontainers Azure Service Bus emulator.
    /// </summary>
    [Trait("Channel", "ServiceBus")]
    [Trait("Function", "Publish")]
    [Trait("Kind", "Integration")]
    [Trait("CICD","WindowsExclude")]
    public class ServiceBusChannelContainerTests : IClassFixture<ServiceBusTestServer>, IAsyncLifetime
    {
        private const string QueueName = "test-queue";

        private readonly string _connectionString;
        private ServiceBusClient? _receiverClient;
        private ServiceBusReceiver? _receiver;

        public ServiceBusChannelContainerTests(
            ServiceBusTestServer testServer,
            ITestOutputHelper outputHelper)
        {
            _connectionString = testServer.ConnectionString;

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Debug));

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
        public async Task PublishCloudEventWithBinaryData()
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

            await Publisher.PublishEventAsync(cloudEvent);

            var received = await _receiver!.ReceiveMessageAsync(maxWaitTime: TimeSpan.FromSeconds(30));

            Assert.NotNull(received);
            Assert.Equal("test", received.Subject);
            Assert.Equal("application/binary", received.ContentType);
            Assert.NotNull(received.Body);
            Assert.True(received.ApplicationProperties.ContainsKey(ServiceBusMessageProperties.EventType));
            Assert.Equal("test.created", received.ApplicationProperties[ServiceBusMessageProperties.EventType]);

            await _receiver.CompleteMessageAsync(received);
        }

        [Fact]
        public async Task PublishCloudEventWithJsonData()
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

            await Publisher.PublishEventAsync(cloudEvent);

            var received = await _receiver!.ReceiveMessageAsync(maxWaitTime: TimeSpan.FromSeconds(30));

            Assert.NotNull(received);
            Assert.Equal("test", received.Subject);
            Assert.Equal("application/json", received.ContentType);
            Assert.NotNull(received.Body);
            Assert.True(received.ApplicationProperties.ContainsKey(ServiceBusMessageProperties.EventType));
            Assert.Equal("test.created", received.ApplicationProperties[ServiceBusMessageProperties.EventType]);

            await _receiver.CompleteMessageAsync(received);
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

            var received = await _receiver!.ReceiveMessageAsync(maxWaitTime: TimeSpan.FromSeconds(30));

            Assert.NotNull(received);
            Assert.NotNull(received.Body);
            Assert.True(received.ApplicationProperties.ContainsKey(ServiceBusMessageProperties.EventType));
            Assert.Equal("person.created", received.ApplicationProperties[ServiceBusMessageProperties.EventType]);

            await _receiver.CompleteMessageAsync(received);
        }

        [Fact]
        public async Task PublishEventAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var cloudEvent = new CloudNative.CloudEvents.CloudEvent
            {
                Subject         = "test",
                DataContentType = "application/json",
                Data            = Encoding.UTF8.GetBytes("{\"msg\":\"hi\"}"),
                Source          = new Uri("https://api.svc.deveel.com/test-service"),
                Type            = "test.created",
                Time            = DateTimeOffset.UtcNow,
                Id              = Guid.NewGuid().ToString("N"),
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Publisher.PublishEventAsync(cloudEvent, cancellationToken: cts.Token));
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
