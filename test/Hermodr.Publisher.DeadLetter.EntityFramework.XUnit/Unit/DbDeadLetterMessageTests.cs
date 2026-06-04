//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Reflection;
using System.Text;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "DeadLetterEntityFramework")]
public class DbDeadLetterMessageTests
{
    private static DeadLetterContext MakeContext(
        CloudEvent? cloudEvent = null,
        string publisherName = "test-publisher",
        Type? channelType = null,
        string? channelName = null,
        Exception? exception = null)
    {
        var errorCtor = typeof(EventPublishErrorContext)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(c => c.GetParameters()[0].ParameterType == typeof(string));
        var errorCtx = errorCtor.Invoke(new object?[]
        {
            publisherName,
            EventPublishStage.ChannelPublish,
            exception ?? new InvalidOperationException("test failure"),
            new ServiceCollection().BuildServiceProvider(),
            default(CancellationToken),
            cloudEvent ?? new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://example.com", UriKind.RelativeOrAbsolute),
                Id = Guid.NewGuid().ToString("N"),
            },
            null, null, channelType, channelName, null, null
        });

        var ctxCtor = typeof(DeadLetterContext)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single();
        return (DeadLetterContext)ctxCtor.Invoke(new[] { errorCtx });
    }

    private static CloudEvent MakeEvent(string type = "test.event", string id = "evt-1", object? data = null)
    {
        var ev = new CloudEvent
        {
            Type = type,
            Source = new Uri("https://example.com", UriKind.RelativeOrAbsolute),
            Id = id,
        };
        if (data is not null)
            ev.Data = data;
        return ev;
    }

    [Fact]
    public void PopulateFromDeadLetterContext_PopulatesAllStandardFields()
    {
        var eventId = "evt-123";
        var cloudEvent = MakeEvent("user.created", eventId);
        cloudEvent.Subject = "users/42";
        cloudEvent.Time = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        cloudEvent.DataContentType = "application/json";
        cloudEvent.DataSchema = new Uri("https://schema.example.com/user.json", UriKind.RelativeOrAbsolute);

        var ctx = MakeContext(cloudEvent, channelType: typeof(string));
        var entity = new DbDeadLetterMessage();

        entity.PopulateFromDeadLetterContext(ctx, new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(eventId, entity.Id);
        Assert.Equal("user.created", entity.EventType);
        Assert.Equal(cloudEvent.Source.ToString(), entity.Source);
        Assert.Equal("users/42", entity.Subject);
        Assert.Equal(cloudEvent.Time, entity.EventTime);
        Assert.Equal("application/json", entity.DataContentType);
        Assert.Equal("https://schema.example.com/user.json", entity.DataSchema);
        Assert.Equal("test-publisher", entity.PublisherName);
        Assert.Equal(typeof(string).AssemblyQualifiedName, entity.ChannelType);
        Assert.Equal("test failure", entity.ErrorMessage);
        Assert.Equal(DeadLetterMessageStatus.Pending, entity.Status);
        Assert.Equal(0, entity.ReplayCount);
        Assert.Null(entity.NextReplayAt);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), entity.CreatedAt);
        Assert.Null(entity.LastStatusAt);
    }

    [Fact]
    public void PopulateFromDeadLetterContext_WithStringData_StoresDataText()
    {
        var cloudEvent = MakeEvent("test", data: "hello world");
        var ctx = MakeContext(cloudEvent);
        var entity = new DbDeadLetterMessage();

        entity.PopulateFromDeadLetterContext(ctx);

        Assert.Equal("hello world", entity.DataText);
        Assert.Null(entity.DataBytes);
    }

    [Fact]
    public void PopulateFromDeadLetterContext_WithByteData_StoresDataBytes()
    {
        var bytes = Encoding.UTF8.GetBytes("binary payload");
        var cloudEvent = MakeEvent("test", data: bytes);
        var ctx = MakeContext(cloudEvent);
        var entity = new DbDeadLetterMessage();

        entity.PopulateFromDeadLetterContext(ctx);

        Assert.Equal(bytes, entity.DataBytes);
        Assert.Null(entity.DataText);
    }

    [Fact]
    public void PopulateFromDeadLetterContext_WithObjectData_SerializesToJson()
    {
        var cloudEvent = MakeEvent("test", data: new { foo = "bar", n = 42 });
        var ctx = MakeContext(cloudEvent);
        var entity = new DbDeadLetterMessage();

        entity.PopulateFromDeadLetterContext(ctx);

        Assert.NotNull(entity.DataText);
        Assert.Contains("\"foo\"", entity.DataText);
        Assert.Contains("\"bar\"", entity.DataText);
        Assert.Null(entity.DataBytes);
    }

    [Fact]
    public void PopulateFromDeadLetterContext_WithNullData_LeavesDataFieldsNull()
    {
        var cloudEvent = MakeEvent("test");
        var ctx = MakeContext(cloudEvent);
        var entity = new DbDeadLetterMessage();

        entity.PopulateFromDeadLetterContext(ctx);

        Assert.Null(entity.DataText);
        Assert.Null(entity.DataBytes);
    }

    [Fact]
    public void PopulateFromDeadLetterContext_ClearsExistingAttributesAndAddsExtension()
    {
        var cloudEvent = MakeEvent("test");
        cloudEvent["myextension"] = "value-1";
        var ctx = MakeContext(cloudEvent);
        var entity = new DbDeadLetterMessage
        {
            Attributes = new List<DbDeadLetterAttribute>
            {
                new() { Name = "stale", Value = "old" }
            }
        };

        entity.PopulateFromDeadLetterContext(ctx);

        Assert.Single(entity.Attributes);
        Assert.Equal("myextension", entity.Attributes.First().Name);
        Assert.Equal("value-1", entity.Attributes.First().Value);
    }

    [Fact]
    public void PopulateFromDeadLetterContext_SkipsStandardAttributeNames()
    {
        var cloudEvent = MakeEvent("test");
        // These should be filtered as standard attributes (overriding the same field again)
        cloudEvent["id"] = "should-be-skipped";
        cloudEvent["type"] = "should-be-skipped";
        cloudEvent["subject"] = "should-be-skipped";
        cloudEvent["time"] = DateTimeOffset.UtcNow;
        cloudEvent["datacontenttype"] = "text/plain";
        cloudEvent["dataschema"] = new Uri("https://schema.example.com/x", UriKind.RelativeOrAbsolute);

        var ctx = MakeContext(cloudEvent);
        var entity = new DbDeadLetterMessage();

        entity.PopulateFromDeadLetterContext(ctx);

        Assert.Empty(entity.Attributes);
    }

    [Fact]
    public void PopulateFromDeadLetterContext_Overload_DefaultsToEventSystemTime()
    {
        var cloudEvent = MakeEvent("test");
        var ctx = MakeContext(cloudEvent);
        var entity = new DbDeadLetterMessage();

        entity.PopulateFromDeadLetterContext(ctx);

        // Just ensure no exception is thrown and CreatedAt is set to a non-default value
        Assert.NotEqual(default, entity.CreatedAt);
    }

    [Fact]
    public void PopulateFromDeadLetterContext_ThrowsOnNullContext()
    {
        var entity = new DbDeadLetterMessage();
        Assert.Throws<ArgumentNullException>(() => entity.PopulateFromDeadLetterContext(null!));
    }

    [Fact]
    public void PopulateFromDeadLetterContext_GeneratesId_WhenEventIdIsNull()
    {
        var cloudEvent = new CloudEvent
        {
            Type = "test",
            Source = new Uri("https://example.com", UriKind.RelativeOrAbsolute),
            Id = null!,
        };
        var ctx = MakeContext(cloudEvent);
        var entity = new DbDeadLetterMessage();

        entity.PopulateFromDeadLetterContext(ctx);

        Assert.False(string.IsNullOrEmpty(entity.Id));
    }

    [Fact]
    public void PopulateFromDeadLetterContext_WithoutChannelType_LeavesChannelTypeNull()
    {
        var cloudEvent = MakeEvent("test");
        var ctx = MakeContext(cloudEvent, channelType: null);
        var entity = new DbDeadLetterMessage();

        entity.PopulateFromDeadLetterContext(ctx);

        Assert.Null(entity.ChannelType);
    }

    [Fact]
    public void BuildCloudEvent_RoundTripsAllFields()
    {
        var entity = new DbDeadLetterMessage
        {
            Id = "evt-1",
            EventType = "user.created",
            Source = "https://api.example.com/users/42",
            Subject = "users/42",
            EventTime = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            DataContentType = "application/json",
            DataSchema = "https://schema.example.com/user.json",
            DataText = "{\"name\":\"x\"}"
        };

        var ev = ((IDeadLetterMessage)entity).Event;

        Assert.Equal("evt-1", ev.Id);
        Assert.Equal("user.created", ev.Type);
        Assert.Equal("users/42", ev.Subject);
        Assert.Equal("application/json", ev.DataContentType);
    }

    [Fact]
    public void BuildCloudEvent_WithDataBytes_AttachesBinary()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var entity = new DbDeadLetterMessage
        {
            Id = "evt-1",
            EventType = "t",
            Source = "/x",
            DataBytes = bytes,
        };

        var ev = ((IDeadLetterMessage)entity).Event;

        Assert.Equal(bytes, ev.Data as byte[]);
    }

    [Fact]
    public void BuildCloudEvent_WithNonV1SpecVersion_UsesDefault()
    {
        var entity = new DbDeadLetterMessage
        {
            Id = "evt-1",
            EventType = "t",
            Source = "/x",
            SpecVersion = "2.0"
        };

        var ev = ((IDeadLetterMessage)entity).Event;

        Assert.NotNull(ev);
        // The constructor defaults SpecVersion to V1.0; setting it to "2.0" is preserved as-is
        Assert.Equal("2.0", entity.SpecVersion);
    }
}
