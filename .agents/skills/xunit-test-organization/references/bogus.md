# Bogus References

This reference supports the skill's guidance on randomized test data and
reproducible faker-based test setup.

## Official Resources

- Bogus repository: <https://github.com/bchavez/Bogus>
- Bogus NuGet package: <https://www.nuget.org/packages/Bogus>

## Skill-specific interpretation

- Define one `Faker<T>` per entity type per fixture
- Prefer explicit `RuleFor` mappings for every property you care about
- Use `f.Random.Guid()` instead of `Guid.NewGuid()` when you want generation to participate in the faker seed
- Avoid hardcoded domain literals when representative randomized values are more appropriate
- Use seeded fakers only when deterministic reproduction is required, such as theory datasets or regression reproduction

## Related patterns from the skill

- Put faker instances in fixtures or shared test support helpers
- Use `[MemberData]` when the generated input is too complex for `[InlineData]`
- Prefer exact assertions when values are intentionally deterministic (seeded
  or explicitly overridden); otherwise assert on behaviour, shape, and
  constraints for non-deterministic generated values

