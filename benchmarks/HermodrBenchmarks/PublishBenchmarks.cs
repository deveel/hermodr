#pragma warning disable CS8618

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using CloudNative.CloudEvents;

using Hermodr;

using Microsoft.Extensions.DependencyInjection;

using System.Text.Json;

namespace HermodrBenchmarks
{
    //[SimpleJob(RuntimeMoniker.Net60)]
    //[SimpleJob(RuntimeMoniker.Net70)]
    //[SimpleJob(RuntimeMoniker.Net80)]
    //[MemoryDiagnoser]
    //[RyuJitX64Job, RyuJitX86Job]
    public class PublishBenchmarks
    {
        private readonly EventPublisher _publisher;

        public PublishBenchmarks()
        {
            var services = new ServiceCollection();

            var builder = services
                .AddEventPublisher(options =>
                {
                    options.Source = new Uri("https://api.svc.deveel.com/test-service");
                    options.Attributes.Add("env", "test");
                })
                .AddTestChannel(_ => { });

            var provider = services.BuildServiceProvider();
            _publisher = provider.GetRequiredService<EventPublisher>();
        }

        [Benchmark]
        public async Task Publish_CloudEventAsParameter()
        {
            var @event = new CloudEvent
            {
                Type = "person.created",
                DataSchema = new Uri("http://example.com/schema/1.0"),
                Source = new Uri("https://api.svc.deveel.com/test-service"),
                Time = DateTime.UtcNow,
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = JsonSerializer.Serialize(new
                {
                    FirstName = "John",
                    LastName = "Doe"
                }),
            };

            @event[CloudEventAttribute.CreateExtension("env", CloudEventAttributeType.String)] = "test";

            await _publisher.PublishEventAsync(@event);
        }

        [Benchmark]
        public async Task Publish_EventDataAsParameter()
        {
            var @event = new PersonCreated
            {
                FirstName = "John",
                LastName = "Doe"
            };

            await _publisher.PublishAsync(@event);
        }

        [Benchmark]
        public async Task Publish_EventFactory()
        {
            var @event = new PersonDeleted
            {
                FirstName = "John",
                LastName = "Doe"
            };

            await _publisher.PublishAsync(@event);
        }

        [Event("person.created", "1.0")]
        class PersonCreated
        {
            public string FirstName { get; set; }

            public string LastName { get; set; }
        }

        class PersonDeleted : IEventConvertible
        {
            public string FirstName { get; set; }

            public string LastName { get; set; }

            public CloudEvent ToCloudEvent()
            {
                return new CloudEvent
                {
                    Type = "person.deleted",
                    DataSchema = new Uri("http://example.com/schema/1.0"),
                    Source = new Uri("https://api.svc.deveel.com/test-service"),
                    Time = DateTime.UtcNow,
                    Id = Guid.NewGuid().ToString("N"),
                    DataContentType = "application/cloudevents+json",
                    Data = JsonSerializer.Serialize(new
                    {
                        FirstName,
                        LastName
                    }),
                };
            }
        }
    }
}
