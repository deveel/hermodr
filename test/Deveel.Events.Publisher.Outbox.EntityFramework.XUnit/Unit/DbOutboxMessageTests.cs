//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Bogus;

using CloudNative.CloudEvents;

namespace Deveel.Events.Unit;

/// <summary>
/// Pure unit tests for <see cref="DbOutboxMessage"/> — no database required.
/// Covers <see cref="DbOutboxMessage.PopulateFromCloudEvent"/> and the
/// <see cref="IOutboxMessage.CloudEvent"/> reconstruction path (BuildCloudEvent).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer",    "Infrastructure")]
[Trait("Feature",  "OutboxEntityFramework")]
public class DbOutboxMessageTests
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private static readonly Faker Faker = new("en");

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CloudEvent BuildCloudEvent(Action<CloudEvent>? configure = null)
    {
        var ce = new CloudEvent
        {
            Id              = Faker.Random.Guid().ToString("N"),
            Type            = $"{Faker.Lorem.Word()}.{Faker.Lorem.Word()}.occurred",
            Source          = new Uri($"https://{Faker.Internet.DomainName()}"),
            Subject         = Faker.Lorem.Word(),
            Time            = Faker.Date.RecentOffset(days: 1),
            DataContentType = "application/json",
            Data            = $"{{\"value\":\"{Faker.Lorem.Word()}\"}}"
        };
        configure?.Invoke(ce);
        return ce;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PopulateFromCloudEvent – scalar fields
    // ══════════════════════════════════════════════════════════════════════════

    #region PopulateFromCloudEvent – scalar fields

    [Fact]
    public void Should_SetAllScalarFields_When_PopulateFromCloudEventIsCalled()
    {
        // Arrange
        var source  = BuildCloudEvent();
        var message = new DbOutboxMessage();

        // Act
        message.PopulateFromCloudEvent(source);

        // Assert
        Assert.Equal(source.Id,                message.Id);
        Assert.Equal(source.Type,              message.EventType);
        Assert.Equal(source.Source!.ToString(), message.Source);
        Assert.Equal(source.Subject,           message.Subject);
        Assert.Equal(source.Time,              message.EventTime);
        Assert.Equal(source.DataContentType,   message.DataContentType);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(0,                        message.RetryCount);
    }

    [Fact]
    public void Should_SetDataSchema_When_CloudEventHasDataSchema()
    {
        // Arrange
        var schema  = new Uri($"https://{Faker.Internet.DomainName()}/schema/v1");
        var source  = BuildCloudEvent(ce => ce.DataSchema = schema);
        var message = new DbOutboxMessage();

        // Act
        message.PopulateFromCloudEvent(source);

        // Assert
        Assert.Equal(schema.ToString(), message.DataSchema);
    }

    [Fact]
    public void Should_GenerateId_When_CloudEventIdIsNull()
    {
        // Arrange
        var source  = BuildCloudEvent(ce => ce.Id = null);
        var message = new DbOutboxMessage();

        // Act
        message.PopulateFromCloudEvent(source);

        // Assert
        Assert.NotNull(message.Id);
        Assert.NotEmpty(message.Id);
    }

    [Fact]
    public void Should_ThrowArgumentException_When_CloudEventTypeIsMissing()
    {
        // Arrange
        var source  = BuildCloudEvent(ce => ce.Type = null);
        var message = new DbOutboxMessage();

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => message.PopulateFromCloudEvent(source));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_CloudEventSourceIsMissing()
    {
        // Arrange
        var source  = BuildCloudEvent(ce => ce.Source = null);
        var message = new DbOutboxMessage();

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => message.PopulateFromCloudEvent(source));
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // PopulateFromCloudEvent – data payload
    // ══════════════════════════════════════════════════════════════════════════

    #region PopulateFromCloudEvent – data payload

    [Fact]
    public void Should_SetDataText_When_CloudEventDataIsString()
    {
        // Arrange
        var payload = $"{{\"orderId\":\"{Faker.Random.Guid()}\"}}";
        var source  = BuildCloudEvent(ce => ce.Data = payload);
        var message = new DbOutboxMessage();

        // Act
        message.PopulateFromCloudEvent(source);

        // Assert
        Assert.Equal(payload, message.DataText);
        Assert.Null(message.DataBytes);
    }

    [Fact]
    public void Should_SetDataBytes_When_CloudEventDataIsByteArray()
    {
        // Arrange
        var bytes  = Faker.Random.Bytes(64);
        var source = BuildCloudEvent(ce =>
        {
            ce.DataContentType = "application/octet-stream";
            ce.Data            = bytes;
        });
        var message = new DbOutboxMessage();

        // Act
        message.PopulateFromCloudEvent(source);

        // Assert
        Assert.Null(message.DataText);
        Assert.Equal(bytes, message.DataBytes);
    }

    [Fact]
    public void Should_SetNullPayload_When_CloudEventDataIsNull()
    {
        // Arrange
        var source  = BuildCloudEvent(ce => ce.Data = null);
        var message = new DbOutboxMessage();

        // Act
        message.PopulateFromCloudEvent(source);

        // Assert
        Assert.Null(message.DataText);
        Assert.Null(message.DataBytes);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // PopulateFromCloudEvent – extension attributes
    // ══════════════════════════════════════════════════════════════════════════

    #region PopulateFromCloudEvent – extension attributes

    [Fact]
    public void Should_CreateAttributeRows_When_CloudEventHasExtensionAttributes()
    {
        // Arrange
        var tenant = Faker.Random.AlphaNumeric(8).ToLowerInvariant();
        var seq    = Faker.Random.Int(1, 9999);
        var source = BuildCloudEvent(ce =>
        {
            ce[CloudEventAttribute.CreateExtension("tenantid",   CloudEventAttributeType.String)]  = tenant;
            ce[CloudEventAttribute.CreateExtension("sequenceid", CloudEventAttributeType.Integer)] = seq;
        });
        var message = new DbOutboxMessage();

        // Act
        message.PopulateFromCloudEvent(source);

        // Assert
        Assert.Equal(2, message.Attributes.Count);

        var tenantAttr = message.Attributes.Single(a => a.Name == "tenantid");
        Assert.Equal("string", tenantAttr.ValueType);
        Assert.Equal(tenant,   tenantAttr.Value);

        var seqAttr = message.Attributes.Single(a => a.Name == "sequenceid");
        Assert.Equal("integer",       seqAttr.ValueType);
        Assert.Equal(seq.ToString(),  seqAttr.Value);
    }

    [Fact]
    public void Should_NotCreateAttributeRows_When_CloudEventHasNoExtensionAttributes()
    {
        // Arrange
        var source  = BuildCloudEvent(); // no extensions
        var message = new DbOutboxMessage();

        // Act
        message.PopulateFromCloudEvent(source);

        // Assert
        Assert.Empty(message.Attributes);
    }

    [Fact]
    public void Should_ClearPreviousAttributes_When_PopulateIsCalledTwice()
    {
        // Arrange
        var first = BuildCloudEvent(ce =>
            ce[CloudEventAttribute.CreateExtension("trace", CloudEventAttributeType.String)] = "abc");
        var second = BuildCloudEvent(); // no extensions
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(first);

        // Act – repopulate from a second event with no extension attributes
        message.PopulateFromCloudEvent(second);

        // Assert – attributes from the first call must have been cleared
        Assert.Empty(message.Attributes);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // BuildCloudEvent (via IOutboxMessage.CloudEvent)
    // ══════════════════════════════════════════════════════════════════════════

    #region BuildCloudEvent reconstruction

    [Fact]
    public void Should_ReconstructAllScalarFields_When_BuildCloudEventIsCalled()
    {
        // Arrange
        var original = BuildCloudEvent();
        var message  = new DbOutboxMessage();
        message.PopulateFromCloudEvent(original);

        // Act
        var rebuilt = ((IOutboxMessage)message).CloudEvent;

        // Assert
        Assert.Equal(original.Id,              rebuilt.Id);
        Assert.Equal(original.Type,            rebuilt.Type);
        Assert.Equal(original.Source,          rebuilt.Source);
        Assert.Equal(original.Subject,         rebuilt.Subject);
        Assert.Equal(original.DataContentType, rebuilt.DataContentType);
    }

    [Fact]
    public void Should_ReconstructDataSchema_When_OriginalEventHasDataSchema()
    {
        // Arrange
        var schema   = new Uri($"https://{Faker.Internet.DomainName()}/schema/v1");
        var original = BuildCloudEvent(ce => ce.DataSchema = schema);
        var message  = new DbOutboxMessage();
        message.PopulateFromCloudEvent(original);

        // Act
        var rebuilt = ((IOutboxMessage)message).CloudEvent;

        // Assert
        Assert.Equal(schema, rebuilt.DataSchema);
    }

    [Fact]
    public void Should_ReconstructTextData_When_OriginalEventHasStringPayload()
    {
        // Arrange
        var payload  = $"{{\"id\":\"{Faker.Random.Guid()}\"}}";
        var original = BuildCloudEvent(ce => ce.Data = payload);
        var message  = new DbOutboxMessage();
        message.PopulateFromCloudEvent(original);

        // Act
        var rebuilt = ((IOutboxMessage)message).CloudEvent;

        // Assert
        Assert.Equal(payload, rebuilt.Data);
    }

    [Fact]
    public void Should_ReconstructBinaryData_When_OriginalEventHasByteArrayPayload()
    {
        // Arrange
        var bytes    = Faker.Random.Bytes(32);
        var original = BuildCloudEvent(ce =>
        {
            ce.DataContentType = "application/octet-stream";
            ce.Data            = bytes;
        });
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(original);

        // Act
        var rebuilt = ((IOutboxMessage)message).CloudEvent;

        // Assert
        Assert.Equal(bytes, rebuilt.Data);
    }

    [Fact]
    public void Should_ReconstructExtensionAttributes_When_OriginalEventHasExtensions()
    {
        // Arrange
        var envVal   = Faker.PickRandom("prod", "staging", "dev");
        var original = BuildCloudEvent(ce =>
            ce[CloudEventAttribute.CreateExtension("env", CloudEventAttributeType.String)] = envVal);
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(original);

        // Act
        var rebuilt = ((IOutboxMessage)message).CloudEvent;

        // Assert
        var envAttr = CloudEventAttribute.CreateExtension("env", CloudEventAttributeType.String);
        Assert.Equal(envVal, rebuilt[envAttr]?.ToString());
    }

    #endregion
}

