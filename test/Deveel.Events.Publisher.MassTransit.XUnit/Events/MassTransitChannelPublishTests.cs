//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;

using System.Text.Json;
using System.Text.Json.Serialization;

using Xunit.Abstractions;

namespace Deveel.Events
{
    [Trait("Channel", "MassTransit")]
    [Trait("Function", "Publish")]
    public class MassTransitChannelPublishTests
    {
        private readonly IServiceProvider _services;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ISendEndpointProvider _sendEndpointProvider;

        public MassTransitChannelPublishTests(ITestOutputHelper outputHelper)
        {
            _publishEndpoint = Substitute.For<IPublishEndpoint>();
            _sendEndpointProvider = Substitute.For<ISendEndpointProvider>();

            var services = new ServiceCollection();

            services.AddLogging(logging =>
                logging.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Trace));

            services.AddSingleton(_publishEndpoint);
            services.AddSingleton(_sendEndpointProvider);

            services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://api.svc.deveel.com/test-service");
                options.DataSchemaBaseUri = new Uri("https://example.com/events/schema");
            })
            .UseMassTransit();

            _services = services.BuildServiceProvider();
        }

        private EventPublisher Publisher => _services.GetRequiredService<EventPublisher>();

        [Fact]
        public async Task PublishCloudEvent_UsesPublishEndpoint()
        {
            var cloudEvent = new CloudEvent
            {
                Type = "person.created",
                Source = new Uri("https://api.svc.deveel.com/test-service"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data = JsonSerializer.Serialize(new { FirstName = "John", LastName = "Doe" }),
            };

            await Publisher.PublishEventAsync(cloudEvent);

            // The extension method ultimately calls the interface method with IPipe<PublishContext<T>>
            await _publishEndpoint.Received(1)
                .Publish<ICloudEventMessage>(
                    Arg.Any<object>(),
                    Arg.Any<IPipe<PublishContext<ICloudEventMessage>>>(),
                    Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PublishCloudEvent_MessageBodyIsValidStructuredCloudEvent()
        {
            ICloudEventMessage? capturedMessage = null;

            await _publishEndpoint.Publish<ICloudEventMessage>(
                Arg.Do<object>(o => capturedMessage = (ICloudEventMessage)o),
                Arg.Any<IPipe<PublishContext<ICloudEventMessage>>>(),
                Arg.Any<CancellationToken>());

            var cloudEvent = new CloudEvent
            {
                Type = "order.placed",
                Source = new Uri("https://api.svc.deveel.com/orders"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data = JsonSerializer.Serialize(new { OrderId = "ord-001" }),
            };

            await Publisher.PublishEventAsync(cloudEvent);

            Assert.NotNull(capturedMessage);
            Assert.NotEmpty(capturedMessage.Body);

            var formatter = new JsonEventFormatter();
            var decoded = formatter.DecodeStructuredModeMessage(
                capturedMessage.Body,
                new System.Net.Mime.ContentType(capturedMessage.ContentType),
                null);

            Assert.Equal(cloudEvent.Id, decoded.Id);
            Assert.Equal(cloudEvent.Type, decoded.Type);
            Assert.Equal(cloudEvent.Source, decoded.Source);
        }

        [Fact]
        public async Task PublishEventData_CloudEventTypeIsSet()
        {
            ICloudEventMessage? capturedMessage = null;

            await _publishEndpoint.Publish<ICloudEventMessage>(
                Arg.Do<object>(o => capturedMessage = (ICloudEventMessage)o),
                Arg.Any<IPipe<PublishContext<ICloudEventMessage>>>(),
                Arg.Any<CancellationToken>());

            await Publisher.PublishAsync(new PersonCreated
            {
                FirstName = "Jane",
                LastName = "Smith",
            });

            Assert.NotNull(capturedMessage);

            var formatter = new JsonEventFormatter();
            var decoded = formatter.DecodeStructuredModeMessage(
                capturedMessage.Body,
                new System.Net.Mime.ContentType(capturedMessage.ContentType),
                null);

            Assert.Equal("person.created", decoded.Type);
        }

        [Fact]
        public async Task PublishCloudEvent_NullEvent_ThrowsArgumentNullException()
        {
            var channel = _services.GetRequiredService<IEventPublishChannel>();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                channel.PublishAsync(null!, CancellationToken.None));
        }

        [Event("person.created", "1.0")]
        private class PersonCreated
        {
            [JsonPropertyName("first_name")]
            public string FirstName { get; set; } = string.Empty;

            [JsonPropertyName("last_name")]
            public string LastName { get; set; } = string.Empty;
        }
    }
}




