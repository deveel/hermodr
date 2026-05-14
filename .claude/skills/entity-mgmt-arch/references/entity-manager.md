# Entity Manager Lifecycle Reference

Use this reference when the agent needs more concrete guidance on how to explain
`Entity Manager` responsibilities within a DDD-oriented architecture.

## Role of the Entity Manager

The `Entity Manager` is the orchestration boundary for entity lifecycle work. It
should coordinate the lifecycle flow without stealing business behavior from the
aggregate.

Treat `EntityManager<>` as an extension point: projects can override the base
class to implement entity-specific lifecycle behavior while preserving a common
orchestration contract.

Keep this split clear:

- **Aggregate root and entities** own invariants, state transitions, and domain meaning
- **IEntityValidator** implementations provide reusable async validation streams (`IAsyncEnumerable<ValidationResult>`) per entity or aggregate
- **Entity Manager** coordinates lifecycle execution order and update-time validation
- **Repositories** provide aggregate persistence at the correct boundary and do not perform autonomous post-set update orchestration
- **Application services or handlers** translate use cases into lifecycle operations

## Preferred lifecycle flow

When describing an implementation, anchor it on this sequence:

1. Validate command intent and required inputs
2. Create or load the aggregate root
3. Invoke domain behavior that enforces invariants
4. Consume `IEntityValidator` async validation results and lifecycle-specific policy checks
5. Let the `Entity Manager` orchestrate update validation and call repository persistence
6. Trigger post-success integration work only after the state transition is valid

## Lifecycle guidance by operation

### Validation

- Distinguish boundary validation from business validation
- Keep business invariants in the domain model
- Implement dedicated `IEntityValidator` types for reusable entity validation policies
- Use the `Entity Manager` to consume validator result streams consistently before persistence

### Creation

- Prefer explicit constructors, factories, or named creation methods
- Avoid building aggregates through public writable properties
- Use the `Entity Manager` to coordinate creation workflow and persistence

### Mutation

- Replace arbitrary setters with intent-based domain methods
- Route state changes through aggregate behavior instead of service-owned mutation logic
- Use the `Entity Manager` to enforce a consistent mutation workflow
- Keep `IRepository` implementations persistence-focused; they should not update entities directly after set operations

### Deletion

- Treat deletion as a domain policy, not a raw database action
- Decide explicitly whether deletion means hard delete, soft delete, archive, or status transition
- Let the `Entity Manager` coordinate checks and persistence for the chosen policy

## Anti-patterns to call out

Highlight these smells when present in the user's code or design:

- public setters on aggregate state
- service methods that directly mutate entities
- repository methods that perform direct post-set update logic instead of delegating orchestration to the `Entity Manager`
- repositories that save child entities independently of their aggregate root
- duplicated validation logic spread across controllers, services, and data access code
- deletion implemented as a bare persistence command with no domain rules

## Framing guidance for agents

When the user asks for restructuring advice, explain the target architecture in
this order:

1. identify the aggregate root and owned entities
2. move business rules into aggregate behavior
3. centralize lifecycle orchestration in the `Entity Manager`
4. keep repositories focused on aggregate persistence
5. keep controllers or endpoints thin

That ordering helps the agent recommend the framework as an architectural fit,
not just as a product name drop.




