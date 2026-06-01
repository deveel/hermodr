using CloudNative.CloudEvents;

namespace Hermodr.AuditTrail.InMemory.XUnit.Unit;

public class InMemoryAuditTrailEdgeCaseTests
{
    [Fact]
    public void Constructor_NullBag_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new InMemoryAuditTrail(null!));
    }

    [Fact]
    public async Task AppendAsync_NullEvent_ShouldThrowArgumentNullException()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var auditTrail = new InMemoryAuditTrail(bag);

        await Assert.ThrowsAsync<ArgumentNullException>(() => auditTrail.AppendAsync(null!));
    }

    [Fact]
    public async Task ReadAsync_WithFromOnly_ShouldFilterCorrectly()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var auditTrail = new InMemoryAuditTrail(bag);
        var now = DateTimeOffset.UtcNow;

        await auditTrail.AppendAsync(new CloudEvent { Id = "1", Type = "t", Source = new Uri("https://test.com"), Time = now.AddHours(-5) });
        await auditTrail.AppendAsync(new CloudEvent { Id = "2", Type = "t", Source = new Uri("https://test.com"), Time = now.AddHours(-1) });

        var query = new AuditTrailStreamQuery { From = now.AddHours(-3) };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("2", results[0].EventId);
    }

    [Fact]
    public async Task ReadAsync_WithToOnly_ShouldFilterCorrectly()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var auditTrail = new InMemoryAuditTrail(bag);
        var now = DateTimeOffset.UtcNow;

        await auditTrail.AppendAsync(new CloudEvent { Id = "1", Type = "t", Source = new Uri("https://test.com"), Time = now.AddHours(-5) });
        await auditTrail.AppendAsync(new CloudEvent { Id = "2", Type = "t", Source = new Uri("https://test.com"), Time = now.AddHours(-1) });

        var query = new AuditTrailStreamQuery { To = now.AddHours(-3) };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("1", results[0].EventId);
    }

    [Fact]
    public async Task ReadAsync_WithMultipleAttributes_ShouldFilterCorrectly()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var auditTrail = new InMemoryAuditTrail(bag);

        var ce1 = new CloudEvent { Id = "1", Type = "t", Source = new Uri("https://test.com") };
        ce1["attr1"] = "val1";
        ce1["attr2"] = "val2";

        var ce2 = new CloudEvent { Id = "2", Type = "t", Source = new Uri("https://test.com") };
        ce2["attr1"] = "val1";

        await auditTrail.AppendAsync(ce1);
        await auditTrail.AppendAsync(ce2);

        var query = new AuditTrailStreamQuery
        {
            Attributes = new Dictionary<string, string> { ["attr1"] = "val1", ["attr2"] = "val2" }
        };
        var results = new List<AuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync(query))
        {
            results.Add(entry);
        }

        Assert.Single(results);
        Assert.Equal("1", results[0].EventId);
    }

    [Fact]
    public async Task ReadAsync_CancellationRequested_ShouldThrowOperationCanceled()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var auditTrail = new InMemoryAuditTrail(bag);

        await auditTrail.AppendAsync(new CloudEvent { Id = "1", Type = "t", Source = new Uri("https://test.com") });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        {
            return Task.Run(async () =>
            {
                await foreach (var entry in auditTrail.ReadAsync(cancellationToken: cts.Token))
                {
                    // Should not reach here
                }
            });
        });
    }

    [Fact]
    public async Task ReadAsync_EmptyBag_ShouldReturnEmpty()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var auditTrail = new InMemoryAuditTrail(bag);

        var results = new List<AuditTrailEntry>();
        await foreach (var entry in auditTrail.ReadAsync())
        {
            results.Add(entry);
        }

        Assert.Empty(results);
    }

    [Fact]
    public async Task ConcurrentAppendAndRead_ShouldNotThrow()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var auditTrail = new InMemoryAuditTrail(bag);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var writeTasks = Enumerable.Range(0, 50).Select(i =>
            Task.Run(() => auditTrail.AppendAsync(new CloudEvent
            {
                Id = i.ToString(),
                Type = "test",
                Source = new Uri("https://test.com")
            }), timeout.Token)).ToArray();

        var readTask = Task.Run(async () =>
        {
            var seen = new HashSet<string>();
            while (seen.Count < 50 && !timeout.Token.IsCancellationRequested)
            {
                await foreach (var entry in auditTrail.ReadAsync(cancellationToken: timeout.Token))
                {
                    seen.Add(entry.EventId);
                }

                if (seen.Count < 50)
                    await Task.Delay(1, timeout.Token);
            }
            return seen;
        }, timeout.Token);

        await Task.WhenAll(writeTasks);
        var observed = await readTask;

        Assert.Equal(50, observed.Count);
    }
}
