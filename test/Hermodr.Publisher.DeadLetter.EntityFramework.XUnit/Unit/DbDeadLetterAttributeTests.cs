//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Reflection;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "DeadLetterEntityFramework")]
public class DbDeadLetterAttributeTests
{
    private static CloudEventAttribute GetAttribute(string name, CloudEventAttributeType type)
        => CloudEventAttribute.CreateExtension(name, type);

    [Fact]
    public void GetAttributeType_MapsString()
    {
        var attr = new DbDeadLetterAttribute { ValueType = "string" };
        var method = typeof(DbDeadLetterAttribute).GetMethod("GetAttributeType", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (CloudEventAttributeType)method.Invoke(attr, Array.Empty<object>())!;
        Assert.Equal(CloudEventAttributeType.String, result);
    }

    [Fact]
    public void GetAttributeType_MapsInteger()
    {
        var attr = new DbDeadLetterAttribute { ValueType = "integer" };
        var method = typeof(DbDeadLetterAttribute).GetMethod("GetAttributeType", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (CloudEventAttributeType)method.Invoke(attr, Array.Empty<object>())!;
        Assert.Equal(CloudEventAttributeType.Integer, result);
    }

    [Fact]
    public void GetAttributeType_MapsBoolean()
    {
        var attr = new DbDeadLetterAttribute { ValueType = "boolean" };
        var method = typeof(DbDeadLetterAttribute).GetMethod("GetAttributeType", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (CloudEventAttributeType)method.Invoke(attr, Array.Empty<object>())!;
        Assert.Equal(CloudEventAttributeType.Boolean, result);
    }

    [Fact]
    public void GetAttributeType_MapsUri()
    {
        var attr = new DbDeadLetterAttribute { ValueType = "uri" };
        var method = typeof(DbDeadLetterAttribute).GetMethod("GetAttributeType", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (CloudEventAttributeType)method.Invoke(attr, Array.Empty<object>())!;
        Assert.Equal(CloudEventAttributeType.Uri, result);
    }

    [Fact]
    public void GetAttributeType_MapsUriReference()
    {
        var attr = new DbDeadLetterAttribute { ValueType = "urireference" };
        var method = typeof(DbDeadLetterAttribute).GetMethod("GetAttributeType", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (CloudEventAttributeType)method.Invoke(attr, Array.Empty<object>())!;
        Assert.Equal(CloudEventAttributeType.UriReference, result);
    }

    [Fact]
    public void GetAttributeType_MapsTimestamp()
    {
        var attr = new DbDeadLetterAttribute { ValueType = "timestamp" };
        var method = typeof(DbDeadLetterAttribute).GetMethod("GetAttributeType", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (CloudEventAttributeType)method.Invoke(attr, Array.Empty<object>())!;
        Assert.Equal(CloudEventAttributeType.Timestamp, result);
    }

    [Fact]
    public void GetAttributeType_MapsBinary()
    {
        var attr = new DbDeadLetterAttribute { ValueType = "binary" };
        var method = typeof(DbDeadLetterAttribute).GetMethod("GetAttributeType", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (CloudEventAttributeType)method.Invoke(attr, Array.Empty<object>())!;
        Assert.Equal(CloudEventAttributeType.Binary, result);
    }

    [Fact]
    public void GetAttributeType_UnknownTypeFallsBackToString()
    {
        var attr = new DbDeadLetterAttribute { ValueType = "unknown" };
        var method = typeof(DbDeadLetterAttribute).GetMethod("GetAttributeType", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (CloudEventAttributeType)method.Invoke(attr, Array.Empty<object>())!;
        Assert.Equal(CloudEventAttributeType.String, result);
    }

    [Fact]
    public void ApplyTo_SkipsNullValue()
    {
        var attr = new DbDeadLetterAttribute { Name = "x", Value = null, ValueType = "string" };
        var cloudEvent = new CloudEvent
        {
            Type = "t",
            Source = new Uri("https://x", UriKind.RelativeOrAbsolute),
            Id = "1"
        };
        var method = typeof(DbDeadLetterAttribute).GetMethod("ApplyTo", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(attr, new object[] { cloudEvent });
        Assert.False(cloudEvent.GetPopulatedAttributes().Any(a => a.Key.Name == "x"));
    }

    [Fact]
    public void ApplyTo_SetsStringExtension()
    {
        var attr = new DbDeadLetterAttribute { Name = "myext", Value = "hello", ValueType = "string" };
        var cloudEvent = new CloudEvent
        {
            Type = "t",
            Source = new Uri("https://x", UriKind.RelativeOrAbsolute),
            Id = "1"
        };
        var method = typeof(DbDeadLetterAttribute).GetMethod("ApplyTo", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(attr, new object[] { cloudEvent });
        Assert.Equal("hello", cloudEvent["myext"]);
    }

    [Fact]
    public void FromAttribute_CreatesAttributeWithCorrectTypeName()
    {
        var method = typeof(DbDeadLetterAttribute).GetMethod("FromAttribute", BindingFlags.Static | BindingFlags.NonPublic)!;

        var stringAttr = (DbDeadLetterAttribute)method.Invoke(null, new object?[]
        {
            "msg-1",
            GetAttribute("s1", CloudEventAttributeType.String),
            "value-1"
        })!;
        Assert.Equal("string", stringAttr.ValueType);
        Assert.Equal("s1", stringAttr.Name);
        Assert.Equal("msg-1", stringAttr.MessageId);

        var intAttr = (DbDeadLetterAttribute)method.Invoke(null, new object?[]
        {
            "msg-2",
            GetAttribute("i1", CloudEventAttributeType.Integer),
            42
        })!;
        Assert.Equal("integer", intAttr.ValueType);

        var boolAttr = (DbDeadLetterAttribute)method.Invoke(null, new object?[]
        {
            "msg-3",
            GetAttribute("b1", CloudEventAttributeType.Boolean),
            true
        })!;
        Assert.Equal("boolean", boolAttr.ValueType);

        var uriAttr = (DbDeadLetterAttribute)method.Invoke(null, new object?[]
        {
            "msg-4",
            GetAttribute("u1", CloudEventAttributeType.Uri),
            new Uri("https://x", UriKind.RelativeOrAbsolute)
        })!;
        Assert.Equal("uri", uriAttr.ValueType);

        var uriRefAttr = (DbDeadLetterAttribute)method.Invoke(null, new object?[]
        {
            "msg-5",
            GetAttribute("ur1", CloudEventAttributeType.UriReference),
            new Uri("/path/to", UriKind.Relative)
        })!;
        Assert.Equal("urireference", uriRefAttr.ValueType);

        var tsAttr = (DbDeadLetterAttribute)method.Invoke(null, new object?[]
        {
            "msg-6",
            GetAttribute("t1", CloudEventAttributeType.Timestamp),
            DateTimeOffset.UtcNow
        })!;
        Assert.Equal("timestamp", tsAttr.ValueType);

        var binAttr = (DbDeadLetterAttribute)method.Invoke(null, new object?[]
        {
            "msg-7",
            GetAttribute("by1", CloudEventAttributeType.Binary),
            new byte[] { 1, 2, 3 }
        })!;
        Assert.Equal("binary", binAttr.ValueType);
    }
}

[Trait("Category", "Unit")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "DeadLetterEntityFramework")]
public class DeadLetterMessageEntityFactoryTests
{
    private static DeadLetterContext MakeContext()
    {
        var errorCtor = typeof(EventPublishErrorContext)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(c => c.GetParameters()[0].ParameterType == typeof(string));
        var errorCtx = errorCtor.Invoke(new object?[]
        {
            "test-publisher",
            EventPublishStage.ChannelPublish,
            new InvalidOperationException("boom"),
            new ServiceCollection().BuildServiceProvider(),
            default(CancellationToken),
            new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://example.com", UriKind.RelativeOrAbsolute),
                Id = "evt-1",
            },
            null, null, null, null, null, null
        });

        var ctxCtor = typeof(DeadLetterContext)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single();
        return (DeadLetterContext)ctxCtor.Invoke(new[] { errorCtx });
    }

    [Fact]
    public void Create_ReturnsPopulatedMessage()
    {
        var factory = new DeadLetterMessageEntityFactory<DbDeadLetterMessage>();
        var ctx = MakeContext();

        var msg = factory.Create(ctx);

        Assert.NotNull(msg);
        Assert.Equal("evt-1", msg.Id);
        Assert.Equal("test.event", msg.EventType);
        Assert.Equal("test-publisher", msg.PublisherName);
    }

    [Fact]
    public void Create_UsesProvidedSystemTime()
    {
        var fixedTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeSystemTime { UtcNowValue = fixedTime };
        var factory = new DeadLetterMessageEntityFactory<DbDeadLetterMessage>(fakeTime);
        var ctx = MakeContext();

        var msg = factory.Create(ctx);

        Assert.Equal(fixedTime, msg.CreatedAt);
    }

    [Fact]
    public void Create_ThrowsOnNullContext()
    {
        var factory = new DeadLetterMessageEntityFactory<DbDeadLetterMessage>();
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!));
    }

    private sealed class FakeSystemTime : IEventSystemTime
    {
        public DateTimeOffset UtcNowValue { get; set; }
        public DateTimeOffset UtcNow => UtcNowValue;
    }
}
