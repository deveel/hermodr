using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using System.Text;
using System.Text.Json;

namespace Hermodr
{
    [Trait("Channel", "RabbitMQ")]
    [Trait("Function", "Publish")]
    [Trait("DisableCICD", "Windows")]
    public class RabbitMqChannelPublishTests : IClassFixture<RabbitMqTestServer>, IAsyncLifetime
    {
        private IChannel? _channel;
        private TaskCompletionSource<CloudEvent> _receivedTcs = new();

        public RabbitMqChannelPublishTests(RabbitMqTestServer testServer, ITestOutputHelper outputHelper)
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.DataSchemaBaseUri = new System.Uri("http://example.com/events/schema");
                options.Source = new System.Uri("https://api.svc.deveel.com");
            })
                .AddRabbitMq(options =>
                {
                    options.ConnectionString = testServer.ConnectionString;
                    // Disable confirms in tests to keep setup simple
                    options.PublisherConfirms = false;
                });

            services.AddLogging(logging => logging.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Trace));

            Services = services.BuildServiceProvider();
        }

        private IServiceProvider Services { get; }

        private EventPublisher Publisher => Services.GetRequiredService<EventPublisher>();

        public async ValueTask InitializeAsync()
        {
            _receivedTcs = new TaskCompletionSource<CloudEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

            var connection = Services.GetRequiredService<IConnection>();
            _channel = await connection.CreateChannelAsync();

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += (sender, args) =>
            {
                var body = args.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                var json = JsonSerializer.Deserialize<JsonElement>(message);
                var formatter = new JsonEventFormatter();
                var received = formatter.ConvertFromJsonElement(json, null);
                _receivedTcs.TrySetResult(received);

                return Task.CompletedTask;
            };

            await _channel.ExchangeDeclareAsync("test", ExchangeType.Topic);
            await _channel.QueueDeclareAsync("test.queue1", durable: true, exclusive: false, autoDelete: false, arguments: null);
            await _channel.QueueBindAsync("test.queue1", "test", "test.event1");
            await _channel.BasicConsumeAsync("test.queue1", autoAck: true, consumer: consumer);
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel != null)
                await _channel.DisposeAsync();

            (Services as IDisposable)?.Dispose();
        }

        [Fact]
        public async Task PublishCloudEventToRabbitMq()
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
                DataSchema = new Uri("http://example.com/schema/1.0"),
            };

            cloudEvent["amqproutingkey"] = "test.event1";
            cloudEvent["amqpexchange"] = "test";

            await Publisher.PublishEventAsync(cloudEvent);

            var received = await _receivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.NotNull(received);
            Assert.Equal(cloudEvent.Id, received.Id);
            Assert.Equal(cloudEvent.Type, received.Type);
            Assert.Equal(cloudEvent.Source, received.Source);
            Assert.Equal(cloudEvent.Subject, received.Subject);
        }

        [Fact]
        public async Task PublishEventDataToRabbitMq()
        {
            var personCreated = new PersonCreated
            {
                Name = "John Doe",
                Age = 30
            };

            await Publisher.PublishAsync(personCreated);

            var received = await _receivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.NotNull(received);

            Assert.Equal("person.created", received.Type);
            Assert.Equal("test", received["amqpexchange"]);
            Assert.Equal("test.event1", received["amqproutingkey"]);

            // TODO: deserialize the event data from JSON
        }

        [Event("person.created", "1.0")]
        [AmqpExchange("test"), AmqpRoutingKey("test.event1")]
        class PersonCreated
        {
            public string Name { get; set; }

            public int Age { get; set; }
        }
    }
}
