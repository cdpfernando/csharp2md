# T43 test quality review

## Audit matrix

| Detector | Positive evidence | Negative evidence | Lookalike evidence | Result |
| --- | --- | --- | --- | --- |
| ASP.NET Core | MVC verbs, Minimal API methods, metadata, health checks, entrypoint | NonAction and unrelated calls | Foreign app and authorization types | covered |
| Dependency injection | lifetimes, `typeof`, keyed registration, factories, expansion | plain configuration call | foreign collections and same-named extension | covered |
| HTTP | typed and named clients, every request method, headers, URL, timeout | non-request methods | foreign client and factory | covered |
| gRPC | confirmed unary and streaming calls | configuration-only call | unrelated `ClientBase` | covered |
| Messaging | Publish/PublishAsync and Subscribe/SubscribeAsync | unrelated calls | same-named incompatible interface methods | covered |
| Compile-time | indexed and external project references, packages | no references | package/project name collision | covered |

## Assertion review

The runtime detector theory suites assert a matching emitted detail, exact or partial resolution, relation partition, non-empty evidence, detector ID and version provenance, null target, and an unresolved reason. Their negative and lookalike cases assert absence of the requested fact. Compile-time cases assert the legal partition, non-runtime status, exact resolution, target or unresolved reason, and provenance; they intentionally do not require source evidence because evaluated references have no document span.

No assertion-free or trivial-only new tests were added. The early `Assert.Empty` branches are the full specified outcome for negative and lookalike cases.

## Discrimination checks

- Changing `AddKeyedTransient` from `transient` to `scoped` failed the focused DI suite.
- Changing Minimal API `MapPatch` from `PATCH` to `POST` failed the focused ASP.NET suite.
- Both mutations were reverted before the final gate.
