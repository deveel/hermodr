using System.Net.Mime;

using CloudNative.CloudEvents.SystemTextJson;

using Deveel;
using Deveel.Events;

using EventGeneration.ConsoleSample;
using EventGeneration.ConsoleSample.Events;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;

// ---------------------------------------------------------------------------
// Assembly-level generator defaults (compile-time):
//
// [EventDataSchemaUri] causes the source generator to bake the full dataschema URI
// directly into the generated ToCloudEvent() implementation as a const string,
// so no runtime lookup is needed for the schema URI.
//
// [EventJsonSerializationOptions] points to a type that exposes a static
// GetOptions() method; the generator emits a direct call to it instead of
// reading EventGeneratorContext.JsonSerializerOptions at runtime.
// ---------------------------------------------------------------------------
[assembly: EventDataSchemaUri("https://schemas.example.com/events/")]
[assembly: EventJsonSerializationOptions(typeof(SampleJsonOptions))]

var services = new ServiceCollection();

services.AddLogging(options =>
{
    options.ClearProviders();
    options.AddSimpleConsole(console =>
    {
        console.SingleLine = true;
        console.TimestampFormat = "HH:mm:ss ";
    });
});

var publishEndpoint = Substitute.For<IPublishEndpoint>();
var sendEndpointProvider = Substitute.For<ISendEndpointProvider>();

services.AddSingleton(publishEndpoint);
services.AddSingleton(sendEndpointProvider);

services
    .AddEventPublisher(options =>
    {
        options.Source = new Uri("https://samples.deveel.dev/event-genration");
        // DataSchemaBaseUri is still useful for the runtime path (IEventFactory,
        // hand-written IEventConvertible, etc.) even when the assembly attribute
        // already bakes it in for generated code.
        options.DataSchemaBaseUri = new Uri("https://schemas.example.com/events");
    })
    .AddMassTransit();

using var provider = services.BuildServiceProvider();
var publisher = provider.GetRequiredService<IEventPublisher>();

ICloudEventMessage? capturedMessage = null;

await publishEndpoint.Publish<ICloudEventMessage>(
    Arg.Do<object>(message => capturedMessage = (ICloudEventMessage)message),
    Arg.Any<IPipe<PublishContext<ICloudEventMessage>>>(),
    Arg.Any<CancellationToken>());

var registered = new PersonRegistered
{
    PersonId = "person-123",
    Email = "person-123@example.com"
};

// This call compiles only because the source generator adds IEventConvertible.
PrintGeneratedConversion(registered);

await publisher.PublishAsync(registered);

if (capturedMessage is null)
{
    Console.WriteLine("No message captured from IPublishEndpoint. Check channel configuration.");
    return;
}

var formatter = new JsonEventFormatter();
var cloudEvent = formatter.DecodeStructuredModeMessage(
    capturedMessage.Body,
    new ContentType(capturedMessage.ContentType),
    null);

Console.WriteLine(
    "MassTransit channel published CloudEvent => Type: {0}, Id: {1}",
    cloudEvent.Type,
    cloudEvent.Id);

static void PrintGeneratedConversion(IEventConvertible convertible)
{
    var cloudEvent = convertible.ToCloudEvent();

    Console.WriteLine(
        "Generated converter produced CloudEvent => Type: {0}, DataSchema: {1}",
        cloudEvent.Type,
        cloudEvent.DataSchema);
}


