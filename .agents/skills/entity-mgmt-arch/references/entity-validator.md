# IEntityValidator Implementation Reference

Use this reference when the user asks how to implement entity validation in a
`Deveel.Repository`-based architecture.

## Why use a dedicated IEntityValidator

A dedicated `IEntityValidator` keeps reusable validation logic out of handlers,
controllers, and repositories.

Use it to:

- centralize entity-specific validation policies
- avoid duplicated checks across create, mutation, and deletion flows
- keep `IRepository` persistence-focused
- let `EntityManager<>` orchestrate when async validation results are consumed

## Responsibility split

- **Domain model**: enforces core invariants in constructors and behavior methods
- **IEntityValidator**: returns `IAsyncEnumerable<ValidationResult>` for reusable lifecycle validation checks
- **EntityManager<>**: decides when validator checks execute and orchestrates persistence
- **IRepository**: persists state only; no direct post-set update orchestration or validation pipeline ownership

## Implementation pattern

### 1) Define validation result model

Treat validation as a streamed sequence of findings.

`IEntityValidator` should return `IAsyncEnumerable<ValidationResult>` where each
`ValidationResult` represents one issue (or one grouped issue set, if your
contract supports grouping).

Typical fields per validation item:

- `code`
- `field`
- `message`
- optional severity/metadata

### 2) Implement IEntityValidator per aggregate or entity

Create a dedicated validator for each aggregate root (and optionally for complex
internal entities when rules justify it).

Validation should cover:

- create-time rules
- mutation-time rules
- delete-time policy checks when applicable

### 3) Keep validator checks pure and deterministic

Prefer checks based on current entity state and explicit inputs.

If external dependencies are needed:

- isolate them behind interfaces
- keep side effects out of validator methods
- surface domain-relevant errors

### 4) Wire validator into EntityManager<>

`EntityManager<>` should:

1. load or create aggregate
2. invoke domain behavior
3. enumerate `IEntityValidator` results asynchronously
4. persist only when validation succeeds

This keeps update validation and persistence sequencing in one orchestration path.

### 5) Do not move orchestration into IRepository

`IRepository` implementations should not:

- run an independent validation pipeline after set operations
- trigger autonomous update rules outside `EntityManager<>`
- mutate entities as part of persistence concerns

## Pseudocode example

```csharp
public sealed class OrderValidator : IEntityValidator<Order>
{
    public async IAsyncEnumerable<ValidationResult> ValidateAsync(
        Order entity,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entity.CustomerName))
            yield return new ValidationResult("order.customer.required", "CustomerName", "Customer name is required");

        if (entity.Total < 0)
            yield return new ValidationResult("order.total.negative", "Total", "Total cannot be negative");

        await Task.CompletedTask;
    }
}

public sealed class OrderEntityManager : EntityManager<Order>
{
    private readonly IEntityValidator<Order> validator;
    private readonly IRepository<Order> repository;

    public OrderEntityManager(IEntityValidator<Order> validator, IRepository<Order> repository)
    {
        this.validator = validator;
        this.repository = repository;
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationResult>();

        await foreach (var result in validator.ValidateAsync(order, cancellationToken))
            failures.Add(result);

        if (failures.Count > 0)
            throw new EntityValidationException(failures);

        await repository.UpdateAsync(order, cancellationToken);
    }
}
```

## Testing guidance

Add tests at two levels:

- validator unit tests for async streamed rule coverage and validation payloads
- entity manager tests proving streamed validation runs before persistence and blocks invalid updates

Also add a guard test confirming repository paths do not bypass manager-driven validation.

## Common mistakes

- putting all validation only in repositories
- duplicating the same checks in controllers and handlers
- assuming a single `IsValid` flag instead of consuming the async validation stream
- allowing persistence updates when the streamed validator returns any failures
- treating `IEntityValidator` as a replacement for domain invariants

A healthy design keeps invariants in the domain model, reusable policy checks in
`IEntityValidator`, orchestration in `EntityManager<>`, and persistence concerns
in `IRepository`.


