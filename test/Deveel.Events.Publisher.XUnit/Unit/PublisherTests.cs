// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Text.Json;
using System.Text.Json.Serialization;

using Bogus;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Application")]
    [Trait("Feature", "EventPublisher")]
    public class PublisherTests
    {
        // ── Fields ────────────────────────────────────────────────────────────

        private static readonly Faker Faker = new("en");

        private readonly IList<CloudEvent> _published = new List<CloudEvent>();
        private readonly EventPublisher _publisher;

        // ── Constructor ───────────────────────────────────────────────────────

        public PublisherTests(ITestOutputHelper outputHelper)
        {
            var services = new ServiceCollection()
                .AddLogging(logging => logging.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Debug));

            services
                .AddEventPublisher(options =>
                {
                    options.Source = new Uri("https://api.svc.deveel.com/test-service");
                    options.Attributes.Add("env", "test");
                })
                .AddTestChannel(@event => _published.Add(@event));

            var provider = services.BuildServiceProvider();
            _publisher = provider.GetRequiredService<EventPublisher>();
        }

        // ── PublishEventAsync ─────────────────────────────────────────────────

        #region PublishEventAsync

        [Fact]
        public async Task Should_ThrowArgumentNullException_When_NullCloudEventIsPublished()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _publisher.PublishEventAsync((CloudEvent)null!, cancellationToken: cancellationToken));
        }

        [Fact]
        public async Task Should_PublishCloudEvent_When_AllFieldsAreSet()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var @event = new CloudEvent
            {
                Type            = "person.created",
                DataSchema      = new Uri("http://example.com/schema/1.0"),
                Source          = new Uri("https://api.svc.deveel.com/test-service"),
                Time            = DateTime.UtcNow,
                Id              = Faker.Random.Guid().ToString("N"),
                DataContentType = "application/json",
                Data            = JsonSerializer.Serialize(new { FirstName = Faker.Name.FirstName(), LastName = Faker.Name.LastName() }),
            };
            @event[CloudEventAttribute.CreateExtension("env", CloudEventAttributeType.String)] = "test";

            // Act
            await _publisher.PublishEventAsync(@event, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(_published);
            Assert.Equal("person.created", _published[0].Type);
            Assert.NotNull(_published[0].DataSchema);
            Assert.Equal("http://example.com/schema/1.0", _published[0].DataSchema!.ToString());
            Assert.NotNull(_published[0].Id);
            Assert.NotNull(_published[0].Source);
            Assert.Equal("https://api.svc.deveel.com/test-service", _published[0].Source!.ToString());
            Assert.Equal("test", _published[0]["env"]);
        }

        [Fact]
        public async Task Should_PublishAnnotatedEventData_When_EventAttributeIsPresent()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var personId = Faker.Random.AlphaNumeric(12);

            // Act
            await _publisher.PublishAsync(new PersonCreated
            {
                Id        = personId,
                FirstName = Faker.Name.FirstName(),
                LastName  = Faker.Name.LastName()
            }, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(_published);
            Assert.Equal("person.created", _published[0].Type);
            Assert.Equal("https://example.com/events/person.created/1.0", _published[0].DataSchema!.ToString());
            Assert.NotNull(_published[0].Id);
            Assert.Equal("https://api.svc.deveel.com/test-service", _published[0].Source!.ToString());
            Assert.Equal("test", _published[0]["env"]);
            Assert.Equal("application/cloudevents+json", _published[0].DataContentType);

            var json = Assert.IsType<string>(_published[0].Data);
            var data = JsonSerializer.Deserialize<PersonCreated>(json);
            Assert.NotNull(data);
            Assert.Equal(personId, data.Id);
        }

        [Fact]
        public async Task Should_PublishEventFromConvertible_When_IEventConvertibleIsProvided()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var personDeleted = new PersonDeleted
            {
                Id        = Faker.Random.AlphaNumeric(8),
                FirstName = Faker.Name.FirstName(),
                LastName  = Faker.Name.LastName()
            };

            // Act
            await _publisher.PublishAsync(personDeleted, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(_published);
            Assert.Equal("person.deleted", _published[0].Type);
            Assert.Equal("https://example.com/events/person.deleted/1.0", _published[0].DataSchema!.ToString());
            Assert.NotNull(_published[0].Id);
            Assert.Equal("application/json", _published[0].DataContentType);

            var json = Assert.IsType<string>(_published[0].Data);
            var data = JsonSerializer.Deserialize<PersonDeleted>(json);
            Assert.NotNull(data);
            Assert.Equal(personDeleted.Id, data.Id);
        }

        [Fact]
        public async Task Should_PreserveExistingId_When_IdIsAlreadySetOnCloudEvent()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var existingId = Faker.Random.Guid().ToString("N");
            var @event = new CloudEvent
            {
                Type   = "test.event",
                Id     = existingId,
                Source = new Uri("https://api.svc.deveel.com/test-service"),
            };

            // Act
            await _publisher.PublishEventAsync(@event, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(_published);
            Assert.Equal(existingId, _published[0].Id);
        }

        [Fact]
        public async Task Should_PreserveExistingSource_When_SourceIsAlreadySetOnCloudEvent()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var customSource = new Uri($"https://{Faker.Internet.DomainName()}/api");
            var @event = new CloudEvent { Type = "test.event", Source = customSource };

            // Act
            await _publisher.PublishEventAsync(@event, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(_published);
            Assert.Equal(customSource, _published[0].Source);
        }

        [Fact]
        public async Task Should_PreserveExistingTime_When_TimeIsAlreadySetOnCloudEvent()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var fixedTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var @event = new CloudEvent
            {
                Type   = "test.event",
                Source = new Uri("https://api.svc.deveel.com/test-service"),
                Time   = fixedTime,
            };

            // Act
            await _publisher.PublishEventAsync(@event, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(_published);
            Assert.Equal(fixedTime, _published[0].Time);
        }

        [Fact]
        public async Task Should_UseSourceFromOptions_When_CloudEventHasNoSource()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var @event = new CloudEvent { Type = "test.event" };

            // Act
            await _publisher.PublishEventAsync(@event, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(_published);
            Assert.Equal(new Uri("https://api.svc.deveel.com/test-service"), _published[0].Source);
        }

        [Fact]
        public async Task Should_AutoSetTime_When_CloudEventHasNoTime()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var @event = new CloudEvent { Type = "test.event", Source = new Uri("https://api.svc.deveel.com") };

            // Act
            await _publisher.PublishEventAsync(@event, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(_published);
            Assert.NotNull(_published[0].Time);
        }

        [Fact]
        public async Task Should_AutoSetId_When_CloudEventHasNoId()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var @event = new CloudEvent { Type = "test.event", Source = new Uri("https://api.svc.deveel.com") };

            // Act
            await _publisher.PublishEventAsync(@event, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(_published);
            Assert.NotNull(_published[0].Id);
            Assert.NotEmpty(_published[0].Id!);
        }

        [Fact]
        public async Task Should_PublishSuccessfully_When_AttributesContainVariousValueTypes()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var publishedEvents = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.Attributes["strattr"]  = Faker.Lorem.Word();
                options.Attributes["intattr"]  = Faker.Random.Int(1, 100);
                options.Attributes["boolattr"] = true;
                options.Attributes["uriattr"]  = new Uri($"https://{Faker.Internet.DomainName()}");
                options.Attributes["tsattr"]   = DateTimeOffset.UtcNow;
            }).AddTestChannel(e => publishedEvents.Add(e));
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act
            await publisher.PublishEventAsync(new CloudEvent
            {
                Type   = "test.event",
                Source = new Uri("https://api.example.com"),
            }, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(publishedEvents);
        }

        [Fact]
        public async Task Should_NotThrow_When_ChannelFailsAndThrowOnErrorsIsFalse()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var services = new ServiceCollection();
            services.AddEventPublisher(options => options.ThrowOnErrors = false)
                    .AddChannel(new ThrowingChannel());
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act & Assert (should not throw)
            await publisher.PublishEventAsync(new CloudEvent
            {
                Type   = "test.event",
                Source = new Uri("https://api.example.com"),
            }, cancellationToken: cancellationToken);
        }

        [Fact]
        public async Task Should_ThrowEventPublishChannelException_When_ChannelFailsAndThrowOnErrorsIsTrue()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var services = new ServiceCollection();
            services.AddEventPublisher(options => options.ThrowOnErrors = true)
                    .AddChannel(new ThrowingChannel());
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act & Assert
            await Assert.ThrowsAsync<EventPublishChannelException>(() =>
                publisher.PublishEventAsync(new CloudEvent
                {
                    Type   = "test.event",
                    Source = new Uri("https://api.example.com"),
                }, cancellationToken: cancellationToken));
        }

        [Fact]
        public async Task Should_SwallowError_When_NoEventFactoryAndThrowOnErrorsIsFalse()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var services = new ServiceCollection();
            services.AddOptions<EventPublisherOptions>();
            var provider = services.BuildServiceProvider();
            var publisher = new EventPublisher(
                provider.GetRequiredService<IOptions<EventPublisherOptions>>(),
                [new ThrowingChannel()],
                provider);

            // Act & Assert (ThrowOnErrors defaults to false — should swallow)
            await publisher.PublishAsync(typeof(PersonCreated), new PersonCreated { Id = "1" }, null, cancellationToken);
        }

        [Fact]
        public async Task Should_ThrowEventCreationException_When_NoEventFactoryAndThrowOnErrorsIsTrue()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var services = new ServiceCollection();
            services.AddOptions<EventPublisherOptions>().Configure(o => o.ThrowOnErrors = true);
            var provider = services.BuildServiceProvider();
            var publisher = new EventPublisher(
                provider.GetRequiredService<IOptions<EventPublisherOptions>>(),
                [new ThrowingChannel()],
                provider);

            // Act & Assert
            await Assert.ThrowsAsync<EventCreationException>(() =>
                publisher.PublishAsync(typeof(PersonCreated), new PersonCreated { Id = "1" }, null, cancellationToken));
        }

        [Fact]
        public async Task Should_SwallowError_When_ConvertibleFactoryThrowsAndThrowOnErrorsIsFalse()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var services = new ServiceCollection();
            services.AddEventPublisher(options => options.ThrowOnErrors = false)
                    .AddTestChannel(_ => { });
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act & Assert (should not throw)
            await publisher.PublishAsync(new BrokenConvertible(), cancellationToken: cancellationToken);
        }

        [Fact]
        public async Task Should_ThrowEventConversionException_When_ConvertibleFactoryThrowsAndThrowOnErrorsIsTrue()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var services = new ServiceCollection();
            services.AddEventPublisher(options => options.ThrowOnErrors = true)
                    .AddTestChannel(_ => { });
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act & Assert
            await Assert.ThrowsAsync<EventConversionException>(() =>
                publisher.PublishAsync(new BrokenConvertible(), cancellationToken: cancellationToken));
        }

        [Fact]
        public async Task Should_PublishUsingGenericType_When_GenericPublishAsyncIsCalled()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;

            // Act
            await _publisher.PublishAsync(new PersonCreated
            {
                Id        = Faker.Random.AlphaNumeric(8),
                FirstName = Faker.Name.FirstName(),
                LastName  = Faker.Name.LastName()
            }, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(_published);
            Assert.Equal("person.created", _published[0].Type);
        }

        [Fact]
        public async Task Should_SetBinaryAttribute_When_ByteArrayAttributeIsConfigured()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var binData = Faker.Random.Bytes(16);
            var publishedEvents = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher(options => options.Attributes["binattr"] = binData)
                    .AddTestChannel(e => publishedEvents.Add(e));
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act
            await publisher.PublishEventAsync(new CloudEvent
            {
                Type   = "test.event",
                Source = new Uri("https://api.example.com"),
            }, cancellationToken: cancellationToken);

            // Assert
            Assert.Single(publishedEvents);
            Assert.NotNull(publishedEvents[0]["binattr"]);
        }

        [Fact]
        public async Task Should_ThrowArgumentNullException_When_NullConvertibleIsPassedToPublishAsync()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _publisher.PublishAsync<PersonDeleted>(null!, cancellationToken: cancellationToken));
        }

        #endregion

        // ── Domain types ──────────────────────────────────────────────────────

        [Event("person.created", "https://example.com/events/person.created/1.0")]
        private class PersonCreated
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("first_name")]
            public string FirstName { get; set; } = string.Empty;

            [JsonPropertyName("last_name")]
            public string LastName { get; set; } = string.Empty;
        }

        private class PersonDeleted : IEventConvertible
        {
            public string Id { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;

            public CloudEvent ToCloudEvent() => new()
            {
                Type            = "person.deleted",
                DataSchema      = new Uri("https://example.com/events/person.deleted/1.0"),
                Source          = new Uri("https://api.svc.deveel.com/test-service"),
                Time            = DateTime.UtcNow,
                Id              = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data            = JsonSerializer.Serialize(this),
            };
        }

        private class ThrowingChannel : IEventPublishChannel
        {
            public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Channel failure simulation");
        }

        private class BrokenConvertible : IEventConvertible
        {
            public CloudEvent ToCloudEvent() => throw new InvalidOperationException("Factory broken");
        }
    }
}
