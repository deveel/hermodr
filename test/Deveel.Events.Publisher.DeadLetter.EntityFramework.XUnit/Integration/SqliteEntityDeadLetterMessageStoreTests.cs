//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Deveel.Events;

[Trait("Category", "Integration")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "DeadLetterEntityFramework")]
public class SqliteEntityDeadLetterMessageStoreTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 01, 15, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task StoreAndReplayStateTransitions_WorkWithSqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<DeadLetterDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new DeadLetterDbContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var clock = new TestSystemTime(FixedNow);
        var store = new EntityDeadLetterMessageStore<DbDeadLetterMessage>(context, clock);
        var message = CreateMessage();

        await store.AddAsync(message, TestContext.Current.CancellationToken);

        var pending = await store.GetPendingMessagesAsync(cancellationToken: TestContext.Current.CancellationToken);
        var stored = Assert.Single(pending);
        Assert.Equal("test.event", ((IDeadLetterMessage)stored).Event.Type);
        Assert.Equal("primary", stored.ChannelName);

        await store.SetRetryAsync(stored, "retry", FixedNow.AddMinutes(5), TestContext.Current.CancellationToken);
        pending = await store.GetPendingMessagesAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(pending);

        stored.NextReplayAt = FixedNow.AddMinutes(-1);
        await store.SetReplayingAsync(stored, TestContext.Current.CancellationToken);
        Assert.Equal(DeadLetterMessageStatus.Replaying, stored.Status);

        await store.SetReplayedAsync(stored, TestContext.Current.CancellationToken);
        Assert.Equal(DeadLetterMessageStatus.Replayed, stored.Status);
    }

    private static DbDeadLetterMessage CreateMessage()
    {
        var message = new DbDeadLetterMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            EventType = "test.event",
            Source = "https://example.com/source",
            PublisherName = "publisher",
            ChannelName = "primary",
            ChannelType = typeof(object).FullName,
            ErrorMessage = "failed",
            Status = DeadLetterMessageStatus.Pending,
            CreatedAt = FixedNow,
            DataText = "{\"ok\":true}"
        };

        message.Attributes.Add(new DbDeadLetterAttribute
        {
            MessageId = message.Id,
            Name = "env",
            Value = "test",
            ValueType = "string"
        });

        return message;
    }

    private sealed class TestSystemTime(DateTimeOffset utcNow) : IEventSystemTime
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
