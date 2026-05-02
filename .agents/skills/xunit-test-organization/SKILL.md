---
description: Guides the agent in writing, naming, and organizing xUnit tests for .NET class libraries. Use this skill when asked to add or refactor tests, write unit or integration tests, set up fixtures or traits, organize test data with Bogus, or apply AAA structure and naming conventions to test methods.
license: MIT
metadata:
    author: Antonello Provenzano
    compatibility:
        - github-copilot
        - claude-code
        - openai-codex
    github-path: plugins/dotnet-tests/skills/xunit-test-organization
    github-ref: refs/heads/main
    github-repo: https://github.com/deveel/agents-skills
    github-tree-sha: 47c2eb99650de494fe45647e97f60c95eee4157c
    version: "1.0"
name: xunit-test-organization
---
# xUnit Test Organization

This skill covers the coding-level conventions for xUnit tests in .NET —
test method naming, class structure, test data generation with Bogus, fixture
patterns, trait categorization, and integration test rules. For project layout,
MSBuild configuration, and package selection see the `xunit-test-arch` skill
(in the `dotnet-arch` plugin).

---

## 1. Test Naming Convention

All test methods follow the pattern:

```
Should_{ExpectedResult}_When_{Scenario}
```

Examples:
```csharp
Should_ReturnNull_When_InputIsEmpty()
Should_ThrowArgumentException_When_ValueIsNegative()
Should_ParseCorrectly_When_ValidJsonIsProvided()
Should_ReturnCachedResult_When_CalledTwice()
```

Rules:
- Use PascalCase for each segment
- `ExpectedResult` describes the observable outcome
- `Scenario` describes the condition or input state
- Never abbreviate — clarity is more important than brevity
- Test class name: `{ClassName}Tests` (e.g. `OrderServiceTests`) — note the
  class suffix remains `Tests` even though the project suffix is `.XUnit`

---

## 2. Test Class Structure

Each test class must follow this internal layout order:

```csharp
namespace MyLib.XUnit.Unit;

public class OrderServiceTests : IClassFixture<OrderServiceFixture>
{
    // 1. Fields
    private readonly OrderServiceFixture _fixture;

    // 2. Constructor
    public OrderServiceTests(OrderServiceFixture fixture)
    {
        _fixture = fixture;
    }

    // 3. [Fact] tests grouped by the method under test,
    //    with a comment header per group
    #region ProcessOrder

    [Fact]
    public void Should_ReturnConfirmed_When_OrderIsValid()
    {
        // Arrange
        var order = _fixture.BuildValidOrder();

        // Act
        var result = _fixture.Sut.ProcessOrder(order);

        // Assert
        Assert.Equal(OrderStatus.Confirmed, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_ThrowArgumentException_When_QuantityIsNotPositive(int quantity)
    {
        // Arrange
        var order = _fixture.BuildOrderWithQuantity(quantity);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => _fixture.Sut.ProcessOrder(order));
        Assert.Contains("quantity", ex.Message);
    }

    #endregion
}
```

For xUnit v3 async tests that call async APIs with a `CancellationToken`
parameter, always flow the test runner token into those calls:

```csharp
[Fact]
public async Task Should_ReturnOrder_When_OrderExistsAsync()
{
    // Arrange
    var cancellationToken = TestContext.Current.CancellationToken;
    var order = _fixture.BuildValidOrder();
    await _fixture.Sut.SaveAsync(order, cancellationToken);

    // Act
    var result = await _fixture.Sut.GetByIdAsync(order.Id, cancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(order.Id, result.Id);
}
```

Rules:
- Always use `// Arrange`, `// Act`, `// Assert` comments — no exceptions
- One assertion concept per `[Fact]`; multiple related assertions are allowed
  within the same concept (e.g. asserting multiple properties of the same result)
- `[Theory]` is required whenever the same logic is tested with multiple inputs;
  never duplicate `[Fact]` methods with hardcoded variations
- Use `[InlineData]` for simple scalar values; use `[MemberData]` pointing to a
  `public static IEnumerable<object[]>` property for complex objects
- In xUnit v3 async `[Fact]` / `[Theory]` methods, when the async API under test
  accepts a `CancellationToken`, pass `TestContext.Current.CancellationToken`
  through every awaited call so cancelled test runs do not remain blocked waiting
  on the underlying operation

---

## 3. Test Data — Bogus & Randomization

All test data must be generated using **Bogus**. Never use hardcoded literals
for names, emails, addresses, IDs, amounts, or any domain value that could
realistically vary. Randomized data catches edge cases that fixed values miss
and prevents tests from accidentally passing due to magic constants.

### Required package
```xml
<PackageReference Include="Bogus" Version="34.*" />
```

### Faker placement

Fakers are defined as `static readonly` fields inside the fixture that owns
the entity, not inside individual test methods. This keeps data generation
centralized and reusable.

```csharp
// test/MyLib.Testing/Fixtures/OrderServiceFixture.cs
namespace MyLib.Testing.Fixtures;

public class OrderServiceFixture
{
    // One Faker<T> per domain entity, defined once
    private static readonly Faker<Order> OrderFaker = new Faker<Order>()
        .RuleFor(o => o.Id, f => f.Random.Guid())
        .RuleFor(o => o.ProductId, f => f.Commerce.Ean13())
        .RuleFor(o => o.Quantity, f => f.Random.Int(1, 100))
        .RuleFor(o => o.CustomerName, f => f.Name.FullName())
        .RuleFor(o => o.Email, f => f.Internet.Email())
        .RuleFor(o => o.CreatedAt, f => f.Date.RecentOffset().UtcDateTime);

    public OrderService Sut { get; }

    public OrderServiceFixture()
    {
        var repo = new InMemoryOrderRepository();
        Sut = new OrderService(repo);
    }

    // Builder methods delegate to the Faker, with overrides for specific scenarios
    public Order BuildValidOrder() =>
        OrderFaker.Generate();

    public Order BuildOrderWithQuantity(int quantity) =>
        OrderFaker.Clone().RuleFor(o => o.Quantity, quantity).Generate();

    public IEnumerable<Order> BuildOrders(int count) =>
        OrderFaker.Generate(count);
}
```

### Seeding for reproducibility

When a test fails due to randomized data, the seed must be reproducible.
Use a fixed seed only in `[Theory]` / `[MemberData]` scenarios where
determinism is required; elsewhere let Bogus randomize freely.

```csharp
// Deterministic seed for MemberData — use when the exact values matter
private static readonly Faker<Order> SeededOrderFaker =
    new Faker<Order>("en").UseSeed(12345)
        .RuleFor(o => o.Id, f => f.Random.Guid())
        .RuleFor(o => o.Quantity, f => f.Random.Int(1, 100));
```

When a randomly seeded test fails, xUnit's output includes the data values —
capture them and promote the failing case to a named `[InlineData]` or
`[MemberData]` entry so it becomes a permanent regression test.

### MemberData with Bogus

Use `[MemberData]` with a Bogus-generated dataset for `[Theory]` tests on
complex objects:

```csharp
public static IEnumerable<object[]> InvalidOrders =>
    new Faker<Order>()
        .RuleFor(o => o.Id, f => f.Random.Guid())
        .RuleFor(o => o.Quantity, f => f.Random.Int(-100, 0)) // always invalid
        .RuleFor(o => o.ProductId, f => f.Commerce.Ean13())
        .Generate(5)
        .Select(o => new object[] { o });

[Theory]
[MemberData(nameof(InvalidOrders))]
[Trait("Category", "Unit")]
[Trait("Layer", "Domain")]
[Trait("Feature", "OrderProcessing")]
public void Should_ThrowArgumentException_When_QuantityIsNotPositive(Order order)
{
    // Act & Assert
    var ex = Assert.Throws<ArgumentException>(
        () => _fixture.Sut.ProcessOrder(order));
    Assert.Contains("quantity", ex.Message);
}
```

### Rules
- Always use `Faker<T>` with explicit `RuleFor` for every property — never
  rely on Bogus auto-generation without rules, as it can produce unexpected nulls
- Define one `Faker<T>` per entity type per fixture — do not instantiate
  new `Faker<T>` inside individual test methods
- Use `f.Random.Guid()` instead of `Guid.NewGuid()` so randomization flows
  through the Bogus seed
- Use locale `"en"` explicitly when string format matters (e.g. phone numbers,
  postcodes): `new Faker<Order>("en")`
- Prefer exact-value assertions when the expected value is deterministic and
  meaningful for the test intent; when using non-deterministic randomized data,
  assert on behaviour, shape, or range instead (e.g. `Assert.True(result > 0)`)

---

## 4. Fixtures

Fixtures that are reused across multiple test projects belong in the shared
`.Testing` project (e.g. `MyLib.Testing`). Fixtures used by a single test
project can live in its `Fixtures/` subfolder directly.

### ClassFixture — shared across all tests in one class
Use when the system under test (SUT) is expensive to construct and is stateless
between tests, or when its state is always reset in the fixture constructor.

```csharp
// test/MyLib.Testing/Fixtures/OrderServiceFixture.cs
namespace MyLib.Testing.Fixtures;

public class OrderServiceFixture
{
    // Bogus Faker defined once — see Section 3 for full rules
    private static readonly Faker<Order> OrderFaker = new Faker<Order>("en")
        .RuleFor(o => o.Id, f => f.Random.Guid())
        .RuleFor(o => o.ProductId, f => f.Commerce.Ean13())
        .RuleFor(o => o.Quantity, f => f.Random.Int(1, 100))
        .RuleFor(o => o.CustomerName, f => f.Name.FullName())
        .RuleFor(o => o.Email, f => f.Internet.Email());

    public OrderService Sut { get; }

    public OrderServiceFixture()
    {
        var repo = new InMemoryOrderRepository();
        Sut = new OrderService(repo);
    }

    public Order BuildValidOrder() =>
        OrderFaker.Generate();

    public Order BuildOrderWithQuantity(int quantity) =>
        OrderFaker.Clone().RuleFor(o => o.Quantity, quantity).Generate();
}
```

### CollectionFixture — shared across multiple test classes
Use for expensive shared infrastructure (e.g. a single in-memory database or
HTTP server) that must be shared across more than one test class.

```csharp
// test/MyLib.Testing/Fixtures/DatabaseFixture.cs
namespace MyLib.Testing.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    public IDbConnection Connection { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        await Connection.OpenAsync();
        // Run migrations / seed data
    }

    public async Task DisposeAsync() => await Connection.DisposeAsync();
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
```

Apply to test classes with:
```csharp
[Collection("Database")]
public class OrderRepositoryTests
{
    private readonly DatabaseFixture _db;
    public OrderRepositoryTests(DatabaseFixture db) => _db = db;
    // ...
}
```

---

## 5. Traits — Categorization

All tests must be decorated with `[Trait]` to allow selective test runs in CI.

### Standard trait keys

| Key        | Values                              |
|------------|-------------------------------------|
| `Category` | `Unit`, `Integration`               |
| `Layer`    | `Domain`, `Application`, `Infrastructure` |
| `Feature`  | The feature or module name (e.g. `OrderProcessing`) |

Examples:
```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("Layer", "Domain")]
[Trait("Feature", "OrderProcessing")]
public void Should_ReturnConfirmed_When_OrderIsValid() { ... }

[Fact]
[Trait("Category", "Integration")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "OrderProcessing")]
public void Should_PersistOrder_When_ProcessingSucceeds() { ... }
```

To run only unit tests in CI:
```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Feature=OrderProcessing"
```

---

## 6. Integration Tests

Integration tests live in the `Integration/` subfolder and follow the same
naming and structure conventions as unit tests, with these additional rules:

- Class must be decorated with `[Collection("...")]` referencing a
  `CollectionFixture` that owns the shared infrastructure
- Tests must be fully isolated — always clean up or reset state in the fixture's
  `InitializeAsync` / `DisposeAsync`
- No mocking of infrastructure in integration tests; use real implementations
  or in-memory equivalents (e.g. `SqliteConnection`, `WebApplicationFactory`)
- Use `IAsyncLifetime` on fixtures that manage async resources

```csharp
[Collection("Database")]
[Trait("Category", "Integration")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "OrderRepository")]
public class OrderRepositoryIntegrationTests
{
    private readonly DatabaseFixture _db;

    public OrderRepositoryIntegrationTests(DatabaseFixture db) => _db = db;

    [Fact]
    public async Task Should_PersistOrder_When_SaveAsyncIsCalled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var repo = new OrderRepository(_db.Connection);
        var order = _db.OrderFaker.Generate(); // use Bogus, not hardcoded values

        // Act
        await repo.SaveAsync(order, cancellationToken);
        var retrieved = await repo.GetByIdAsync(order.Id, cancellationToken);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(order.Quantity, retrieved.Quantity);
    }
}
```

---

## 7. What the Agent Must Never Do

- Do not use `[Fact]` where `[Theory]` is more appropriate
- Do not write test methods without all three AAA comment sections
- Do not use FluentAssertions — always use xUnit's built-in `Assert` class
- Do not share mutable state between tests without a fixture
- Do not name test methods with generic names like `Test1`, `TestProcess`, etc.
- Do not skip adding `[Trait]` attributes
- Do not hardcode test data values (names, IDs, quantities, emails, etc.) — always use Bogus
- Do not instantiate `Faker<T>` inside individual test methods — define it in the fixture
- Do not use brittle exact-value assertions against non-deterministic
  Bogus-generated values; use exact assertions when values are intentionally
  deterministic (for example seeded or explicitly overridden)
- In xUnit v3 async tests, do not omit `TestContext.Current.CancellationToken`
  when calling async APIs that already expose a `CancellationToken` parameter

---

## 8. Local References

Additional supporting material for this skill is available in the
[`references/README.md`](./references/README.md) index beside this file.

- [`references/README.md`](./references/README.md) — overview and topic index
- [`references/xunit.md`](./references/xunit.md) — xUnit framework, fixtures, theories, and traits
- [`references/dotnet-testing-platform.md`](./references/dotnet-testing-platform.md) — runner and platform guidance by target framework
- [`references/bogus.md`](./references/bogus.md) — randomized test data guidance

Use these files when deeper background or authoritative external links are
helpful, while treating this `SKILL.md` as the primary instruction source.

