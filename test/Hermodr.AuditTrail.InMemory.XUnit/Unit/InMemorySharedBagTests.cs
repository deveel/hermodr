namespace Hermodr.AuditTrail.InMemory.XUnit.Unit;

public class InMemorySharedBagTests
{
    [Fact]
    public void Add_ShouldStoreEntry()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var entry = new AuditTrailEntry { Id = "1", EventType = "test" };

        bag.Add(entry);

        var all = bag.GetAll();
        Assert.Single(all);
        Assert.Equal("1", all[0].Id);
    }

    [Fact]
    public void GetAll_EmptyBag_ShouldReturnEmpty()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var all = bag.GetAll();
        Assert.Empty(all);
    }

    [Fact]
    public void GetAll_WithEntries_ShouldReturnAll()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        bag.Add(new AuditTrailEntry { Id = "1", EventType = "type1" });
        bag.Add(new AuditTrailEntry { Id = "2", EventType = "type2" });

        var all = bag.GetAll();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Query_ShouldFilterByPredicate()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        bag.Add(new AuditTrailEntry { Id = "1", EventType = "type1" });
        bag.Add(new AuditTrailEntry { Id = "2", EventType = "type2" });
        bag.Add(new AuditTrailEntry { Id = "3", EventType = "type1" });

        var results = bag.Query(e => e.EventType == "type1").ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.Equal("type1", e.EventType));
    }

    [Fact]
    public void Query_EmptyBag_ShouldReturnEmpty()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var results = bag.Query(_ => true).ToList();
        Assert.Empty(results);
    }

    [Fact]
    public void Query_NoMatch_ShouldReturnEmpty()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        bag.Add(new AuditTrailEntry { Id = "1", EventType = "type1" });

        var results = bag.Query(e => e.EventType == "nonexistent").ToList();
        Assert.Empty(results);
    }

    [Fact]
    public void Add_OverwritesExistingEntry_SameId()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        bag.Add(new AuditTrailEntry { Id = "1", EventType = "type1" });
        bag.Add(new AuditTrailEntry { Id = "1", EventType = "type2" });

        var all = bag.GetAll();
        Assert.Single(all);
        Assert.Equal("type2", all[0].EventType);
    }

    [Fact]
    public void ConcurrentAdd_ShouldBeThreadSafe()
    {
        var bag = new InMemorySharedBag<AuditTrailEntry>();
        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => bag.Add(new AuditTrailEntry { Id = i.ToString(), EventType = $"type{i}" })));

        Task.WaitAll(tasks.ToArray());

        var all = bag.GetAll();
        Assert.Equal(100, all.Count);
    }
}
