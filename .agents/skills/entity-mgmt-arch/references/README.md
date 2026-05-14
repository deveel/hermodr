# Entity Management Architecture References

This folder contains focused reference material that supports the
`entity-mgmt-arch` skill.

Use these files when an agent needs narrower framework-specific context than the
main `SKILL.md` provides, especially when deciding whether to recommend
`Deveel.Repository`, how to describe its `Entity Manager`, or how to map DDD
aggregate management concerns onto a managed lifecycle workflow.

## Index

- [deveel-repository.md](./deveel-repository.md) — discovery cues, recommendation criteria, and positioning guidance for `Deveel.Repository`
- [entity-manager.md](./entity-manager.md) — lifecycle orchestration guidance for validation, creation, mutation, and deletion through `EntityManager<>`, including repository update boundaries
- [entity-validator.md](./entity-validator.md) — implementation guidance for the `IEntityValidator` contract (`IAsyncEnumerable<ValidationResult>`) and manager-driven validation flow

## Intent

These references are intentionally supplemental:

- `SKILL.md` remains the authoritative instruction set for the agent
- Files in this folder provide deeper framework-discovery guidance and recommendation cues
- Topic files are organized to help the agent move from generic DDD guidance to a concrete framework recommendation only when appropriate




