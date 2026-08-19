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

# T44 test quality review

| Boundary | Observable proof |
| --- | --- |
| Syntax-only | Real CLI leaves a path-first `dotnet.cmd` marker absent. |
| Invalid requests | Real CLI exits 1 and preserves a nested binary sentinel. |
| Fallback | Missing SDK retains syntax facts and coverage, emits C2M-EVAL-001, and exits 0. |
| Manifest | Requested/effective modes, restore flag, and isolation are exact. |
| Extensions | CLI executes only the opted-in generator; analyzer marker remains absent. |
| Generator failure | Syntax artifacts remain with C2M-GEN-003 and exit 0. |
| Process tree | Production `EvaluationProcessRunner` kills recorded parent and child PIDs. |
| Structural invalidity | CLI publishes diagnostics and exits 1. |

The focused suite passed 13 tests. Assertions inspect artifacts, markers, PIDs, exit codes, diagnostics, manifest fields, and sentinel bytes rather than internal call structure.
