# Engine Bootstrap Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and
Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the
full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**User-confirmed deviation from the standard flow:** the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by standing user request, as it was for
`symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver` and
`knowledge-taxonomy-contract`. No mutants are injected at any point. The Verifier's spec-anchored
outcome check, per-AC `file:line` evidence, and `validation.md` report still run as normal.

**Phase-end quality gate (user-confirmed, applies to every phase):** the last task of each phase - T5, T12,
T18, T23, T30, T37, T44, T52 - additionally runs `dotnet-skills:slopwatch` over that phase's changes and
reports clean before the phase is considered complete. This is in addition to each task's own `Tools` list.

---

**Spec**: `.specs/features/engine-bootstrap/spec.md`
**Context**: `.specs/features/engine-bootstrap/context.md`
**Design**: `.specs/features/engine-bootstrap/design.md`
**Status**: Approved

**Scope of this task list**: the whole feature. All 61 requirements `ENG-01` through `ENG-61` are broken down
here; nothing is deferred to a later pass.

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/Csharp2Md.Domain.Tests/Isolation/DomainIsolationTests.cs`,
> `Relations/ConfirmedRelationTests.cs`, `Relations/RelationShapeGuardsTests.cs`,
> `Registry/RegistryDriftGateTests.cs`, `Surface/RequirementCoverageTests.cs`,
> `tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`,
> `src/Csharp2Md.Cli/Program.cs`) and project guidelines. Guidelines found: `AGENTS.md` and `CLAUDE.md`
> (they route test quality to the `dotnet-test:*` skills as post-hoc gates and declare no coverage
> threshold), `Directory.Build.props:7` (`TreatWarningsAsErrors`), `.config/dotnet-tools.json` (slopwatch).
> No coverage-threshold tool config and no CI workflow exist in this repository, so strong defaults apply
> to the Coverage Expectation column.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Project and solution wiring (new `*.csproj`, `csharp2md.slnx`, `Directory.Packages.props`) | none | Build gate only - declarative MSBuild files with no branches | `src/Csharp2Md.*/*.csproj`, `tests/Csharp2Md.*.Tests/*.csproj`, `csharp2md.slnx` | build gate only |
| Domain relation construction (`ConfirmedRelation.Create`) | unit | 1:1 to ENG-55..ENG-61; every illegal shape and weaker evidence rejected through `Create` only; drift gate byte-identical; TAX-46/50/51/53 rows flipped | `tests/Csharp2Md.Domain.Tests/Relations/*Tests.cs`, `Registry/RegistryDriftGateTests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Assembly isolation invariants (project references, forbidden packages, public surface allowlist) | unit | 1:1 to ENG-03..ENG-08, ENG-11, ENG-12; each forbidden reference and type named in the failure | `tests/Csharp2Md.*.Tests/Isolation/*Tests.cs` | matching `dotnet test tests/Csharp2Md.<X>.Tests/Csharp2Md.<X>.Tests.csproj` |
| Storage port and in-memory adapter | unit | All branches; 1:1 to ENG-20..ENG-28; commit-once, manifest-last, abort-preserves-previous, order-independent commit, no interpretation | `tests/Csharp2Md.Storage.Tests/**/*Tests.cs`, `tests/Csharp2Md.Analysis.Tests/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Analysis request/result and facade | unit | All branches; 1:1 to ENG-10, ENG-17, ENG-29..ENG-31; empty and duplicate inputs named | `tests/Csharp2Md.Analysis.Tests/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Pipeline orchestrator and stubs | unit | All branches; 1:1 to ENG-13..ENG-19, ENG-22, ENG-24..ENG-27; substitution, cancellation, stage failure, corruption, unknowns | `tests/Csharp2Md.Analysis.Tests/Pipeline/*Tests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Multi-solution isolation and determinism | unit | 1:1 to ENG-32..ENG-36, ENG-16; one failing solution does not block others; shuffled input yields identical canonical order; no new files | `tests/Csharp2Md.Analysis.Tests/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| CLI `analyze` verb | unit | 1:1 to ENG-37..ENG-45; exit codes 0/1/2; stderr vs stdout; removed options absent | `tests/Csharp2Md.Cli.Tests/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj` |
| Projection marker assembly | unit | ENG-04: no project reference to Analysis, Storage or CLI | `tests/Csharp2Md.Projection.Tests/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |
| Port ledger and excision artifacts | none | Generated/deleted tree, asserted by presence/absence tests in the isolation layer above | `docs/architecture/legacy-port-ledger.md` | build gate only |
| Committed registry (`contracts/taxonomy-registry.json`) | none | Correctness is the existing drift gate plus ENG-60 | `contracts/taxonomy-registry.json` | build gate only |

## Gate Check Commands

> Generated from the repository's own build and test entry points (`csharp2md.slnx`, `global.json`,
> `tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`). No CI workflow file exists. The test stack
> is xUnit 2.9.3 on `Microsoft.NET.Test.Sdk` (VSTest). Until Core.Tests is deleted, **do not** use
> `dotnet test csharp2md.slnx` as a green gate: `MigrationLedgerTests` fails by missing a deleted v3 file.
> No `.editorconfig` exists, so `dotnet format` enforces whitespace and import ordering only.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks whose tests live in one test project | `dotnet test tests/Csharp2Md.<X>.Tests/Csharp2Md.<X>.Tests.csproj` for the project the task names |
| Full | After tasks spanning more than one new test project, and never Core.Tests | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj` (omit any project not yet created) |
| Build | After phase completion and for project- or artifact-only tasks | `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command of that moment |
| Solution | After Core.Tests is gone (T47 onward) | `dotnet test csharp2md.slnx` |

Every task carrying the `build` gate is a phase-end task, so it also runs `dotnet-skills:slopwatch` over the
phase's changes per the protocol above. The tool is pinned locally in `.config/dotnet-tools.json`.

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a
phase execute in the listed order. Each phase diagram shows the intra-phase chain; `Depends on` in a task body
names its execution predecessor, and the first task of each phase depends on the last task of the phase before
it.

### Phase 1: Reachable relation shape guards

Widen `ConfirmedRelation.Create` so TAX-46/50/51/53 are enforced through the public API, while Domain still
has no production consumer.

```
T1 → T2 → T3 → T4 → T5
```

### Phase 2: Target assemblies

Create Analysis, Storage, Projection and their test projects beside Domain and the still-present legacy CLI.

```
T6 → T7 → T8 → T9 → T10 → T11 → T12
```

### Phase 3: Transactional storage port

Port types on Analysis, in-memory adapter in Storage, commit/abort invariants.

```
T13 → T14 → T15 → T16 → T17 → T18
```

### Phase 4: Analysis facade contracts

Request, result, the single entry interface, and the public-surface allowlist.

```
T19 → T20 → T21 → T22 → T23
```

### Phase 5: Eight-stage pipeline

Internal stages, substitution, zeros, cancellation, stage failure.

```
T24 → T25 → T26 → T27 → T28 → T29 → T30
```

### Phase 6: Run semantics and isolation

Commit-once, corruption, unknowns, multi-solution isolation, determinism, no filesystem write.

```
T31 → T32 → T33 → T34 → T35 → T36 → T37
```

### Phase 7: Provisional analyze CLI

Rewrite the CLI onto the new facade. Packaging stays `csharp2md`.

```
T38 → T39 → T40 → T41 → T42 → T43 → T44
```

### Phase 8: Legacy excision

Port ledger, delete Core and its tests, prune packages, green solution.

```
T45 → T46 → T47 → T48 → T49 → T50 → T51 → T52
```

---

## Task Breakdown

### Phase 1: Reachable relation shape guards

#### T1: Require evidence method on Create

**What**: Add required `EvidenceMethod evidenceMethod` to `ConfirmedRelation.Create`, call
`RelationShapeGuards.RequireSufficientEvidence`, update every existing `Create` call site so Domain.Tests
still compile, and add a test that constructs an `invokes` relation through `Create` with syntactic evidence
and asserts rejection (ENG-58).
**Where**: `src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs`
**Depends on**: None
**Reuses**: `RelationShapeGuards.RequireSufficientEvidence`; `ConfirmedRelationTests.CreateValid`; relation
minimums in `src/Csharp2Md.Domain/Registry/RelationDescriptor.cs`
**Requirement**: ENG-58

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-api-design`

**Done when**:

- [x] `Create` takes `EvidenceMethod` after the analysis-variant array
- [x] Syntactic evidence on a relation whose minimum is semantic is rejected through `Create`, naming the relation
- [x] Existing Domain tests compile by passing a method that meets each relation's minimum
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(domain): require evidence method on confirmed relation construction`

---

#### T2: Wire callable shape through Create

**What**: Add optional `IFact? sourceFact` and `IFact? targetFact` to `Create`, reject reference mismatches,
and for callable kinds invoke `RequireCallableIfNeeded` as design.md specifies (`Executes` on the target
symbol; the other three on the source symbol). Test ENG-55 only through `Create`.
**Where**: `src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs`
**Depends on**: T1
**Reuses**: `RelationShapeGuards.RequireCallableIfNeeded`; `RelationShapeGuardsTests.MethodSymbol`
**Requirement**: ENG-55

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A callable kind constructed through `Create` with a non-callable `Symbol` is rejected
- [x] `Executes` uses `targetFact`; `Invokes`, `ImplementsOperation` and `AccessesData` use `sourceFact`
- [x] A supplied fact whose `Reference` does not equal the corresponding `FactReference` is rejected naming the fact parameter
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(domain): enforce callable shape at confirmed relation construction`

---

#### T3: Wire target shape through Create

**What**: For `Targets` and `OperatesOn`, require `targetFact` and call `RequireLegalTargetShape` from
`Create`. Test ENG-56 and ENG-57 only through `Create`.
**Where**: `src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs`
**Depends on**: T2
**Reuses**: `RelationShapeGuards.RequireLegalTargetShape`; inbound `BoundaryOperation` fixtures in
`RelationShapeGuardsTests`
**Requirement**: ENG-56, ENG-57

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `targets` through `Create` with an outbound boundary operation is rejected
- [x] `operates-on` through `Create` with a non data-object/data-field target is rejected
- [x] A missing `targetFact` for those kinds is rejected naming `targetFact`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(domain): enforce targets and operates-on shape at construction`

---

#### T4: Prove the three guards are reachable from Create

**What**: Add a test that the three shape-guard methods have at least one production call site inside
`ConfirmedRelation.Create` (ENG-59), by source inspection or by a fault that only `Create` can raise with
the guard's named parameter.
**Where**: `tests/Csharp2Md.Domain.Tests/Relations/ConfirmedRelationCreateGuardReachabilityTests.cs`
**Depends on**: T3
**Reuses**: `RelationShapeGuards.cs` method names; `ConfirmedRelation.Create`
**Requirement**: ENG-59

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] `RequireCallableIfNeeded`, `RequireLegalTargetShape` and `RequireSufficientEvidence` are each shown reachable from `Create`
- [x] The test does not call the guard helpers as the act-under-test
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(domain): prove relation shape guards run from Create`

---

#### T5: Keep the registry identical and close the TAX gaps

**What**: Assert the drift gate still passes byte-for-byte (ENG-60). Edit
`.specs/features/knowledge-taxonomy-contract/spec.md` so TAX-46, TAX-50, TAX-51 and TAX-53 read `Verified`
with no spec-precision-gap clause (ENG-61).
**Where**: `.specs/features/knowledge-taxonomy-contract/spec.md`
**Depends on**: T4
**Reuses**: `tests/Csharp2Md.Domain.Tests/Registry/RegistryDriftGateTests.cs`
**Requirement**: ENG-60, ENG-61

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`

**Done when**:

- [x] `CommittedRegistry_MatchesFreshEmission_ByteForByte` still passes
- [x] The four TAX rows no longer mention a spec-precision gap
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean over this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `docs(taxonomy): close the relation-guard spec-precision gaps`

---

### Phase 2: Target assemblies

#### T6: Add the Analysis project

**What**: Add `src/Csharp2Md.Analysis/Csharp2Md.Analysis.csproj` targeting `net10.0` with no package or
project reference, an `AssemblyMarker`, `InternalsVisibleTo` for Analysis.Tests, and a `csharp2md.slnx`
entry under `/src/`.
**Where**: `src/Csharp2Md.Analysis/Csharp2Md.Analysis.csproj`
**Depends on**: T5
**Reuses**: `src/Csharp2Md.Domain/Csharp2Md.Domain.csproj`; `src/Csharp2Md.Domain/AssemblyMarker.cs`
**Requirement**: ENG-01, ENG-09

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`, `dotnet-msbuild:directory-build-organization`

**Done when**:

- [x] `TargetFramework` is `net10.0` and the project has no package or project reference
- [x] The project is listed under `/src/` in `csharp2md.slnx`
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: none
**Gate**: build

**Commit**: `feat(analysis): add the analysis assembly`

---

#### T7: Add the Analysis test project

**What**: Add `tests/Csharp2Md.Analysis.Tests` using Domain.Tests' xUnit/Verify package set, referencing only
Analysis, registered under `/tests/`, with a repo-root helper and the `[Trait("Requirement", "ENG-nn")]`
convention.
**Where**: `tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
**Depends on**: T6
**Reuses**: `tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`; Domain `DomainTestPaths`
**Requirement**: ENG-01

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:scaffold-dotnet-test-project`, `dotnet-skills:project-structure`

**Done when**:

- [x] The test project references `Csharp2Md.Analysis` only
- [x] A smoke test loads `AssemblyMarker` so the project is not empty
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): add the analysis test project`

---

#### T8: Add the Storage project

**What**: Add `src/Csharp2Md.Storage` targeting `net10.0`, referencing only Analysis, with `AssemblyMarker`,
`InternalsVisibleTo` for Storage.Tests, and a slnx `/src/` entry.
**Where**: `src/Csharp2Md.Storage/Csharp2Md.Storage.csproj`
**Depends on**: T7
**Reuses**: Analysis csproj shape; Domain `AssemblyMarker`
**Requirement**: ENG-01, ENG-21

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:

- [x] The only project reference is `Csharp2Md.Analysis`
- [x] There is no package reference
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: none
**Gate**: build

**Commit**: `feat(storage): add the storage assembly`

---

#### T9: Add the Storage test project

**What**: Add `tests/Csharp2Md.Storage.Tests` referencing Storage, registered under `/tests/`.
**Where**: `tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
**Depends on**: T8
**Reuses**: Analysis.Tests csproj
**Requirement**: ENG-01

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:scaffold-dotnet-test-project`

**Done when**:

- [x] The test project references `Csharp2Md.Storage`
- [x] A smoke test loads Storage's `AssemblyMarker`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(storage): add the storage test project`

---

#### T10: Add the Projection project

**What**: Add `src/Csharp2Md.Projection` targeting `net10.0` with no package or project reference, an
`AssemblyMarker`, and a slnx `/src/` entry.
**Where**: `src/Csharp2Md.Projection/Csharp2Md.Projection.csproj`
**Depends on**: T9
**Reuses**: Domain csproj and `AssemblyMarker`
**Requirement**: ENG-01, ENG-04

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:

- [x] The project has no package or project reference
- [x] It is listed under `/src/` in `csharp2md.slnx`
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: none
**Gate**: build

**Commit**: `feat(projection): add the projection marker assembly`

---

#### T11: Add Projection tests and lock ENG-04

**What**: Add `tests/Csharp2Md.Projection.Tests` and assert Projection declares no project reference to
Analysis, Storage or CLI.
**Where**: `tests/Csharp2Md.Projection.Tests/Isolation/ProjectionIsolationTests.cs`
**Depends on**: T10
**Reuses**: `DomainIsolationTests` referenced-assembly walk
**Requirement**: ENG-04

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:scaffold-dotnet-test-project`, `dotnet-test:assertion-quality`

**Done when**:

- [x] Each forbidden project name is asserted, and the failure names it
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(projection): pin the marker assembly's isolation`

---

#### T12: Pin the three new assemblies in the solution

**What**: Assert `csharp2md.slnx` lists Analysis, Storage and Projection under `/src/` each targeting
`net10.0`, and that no production csproj in those three declares a `Microsoft.CodeAnalysis` or
`Microsoft.Build` package (ENG-01, ENG-06 partial).
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/SolutionTopologyTests.cs`
**Depends on**: T11
**Reuses**: slnx XML; csproj `PackageReference` walk
**Requirement**: ENG-01, ENG-06, ENG-09

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`, `dotnet-test:assertion-quality`

**Done when**:

- [x] The three project paths and `net10.0` are asserted from disk, not from memory
- [x] A forbidden Roslyn/MSBuild package reference fails naming the project and the package
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then Full (Domain + Analysis + Storage + Projection tests)
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean over this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `test(engine): pin the new assembly topology`

---

### Phase 3: Transactional storage port

#### T13: Define staged fragment types

**What**: Add `ArtifactRole`, `StagedFragment` and `CommittedPublication` on the Analysis public surface as
design.md specifies.
**Where**: `src/Csharp2Md.Analysis/Storage/StagedFragment.cs`
**Depends on**: T12
**Reuses**: Domain record style (explicit properties, `ImmutableArray<byte>`)
**Requirement**: ENG-11, ENG-20

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] `ArtifactRole` has exactly `Payload` and `Manifest`
- [x] `CommittedPublication` exposes artifacts in publication order
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add opaque staged fragment types`

---

#### T14: Declare the transactional storage port

**What**: Add `ITransactionalStore` and `IStoreSession` with `Open`, `Stage`, `Commit` and `Abort` as three
distinct operations (ENG-20).
**Where**: `src/Csharp2Md.Analysis/Storage/ITransactionalStore.cs`
**Depends on**: T13
**Reuses**: `StagedFragment`, `CommittedPublication`
**Requirement**: ENG-20

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] Staging, commit and abort are separate members
- [x] `Open` takes a solution key and returns a session
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): declare the transactional storage port`

---

#### T15: Implement in-memory Open and Stage

**What**: Add `InMemoryTransactionalStore` implementing the port, isolating sessions by solution key, with
no `System.IO` usage (ENG-21).
**Where**: `src/Csharp2Md.Storage/InMemoryTransactionalStore.cs`
**Depends on**: T14
**Reuses**: `ITransactionalStore`
**Requirement**: ENG-21

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Two keys never share a fragment list
- [x] Staging after abort on a new session starts empty
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): add the in-memory transactional adapter`

---

#### T16: Canonical commit order

**What**: `Commit` sorts `Payload` fragments by `CanonicalKey` ordinal, then appends `Manifest` fragments,
independent of staging order (ENG-23, ENG-27). A session commits exactly once; a second `Commit` throws.
**Where**: `src/Csharp2Md.Storage/InMemoryTransactionalStore.cs`
**Depends on**: T15
**Reuses**: `ArtifactRole.Manifest`
**Requirement**: ENG-22, ENG-23, ENG-27

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-test:assertion-quality`

**Done when**:

- [x] Staging a manifest then a payload still publishes payload then manifest
- [x] Two staging orders of the same payloads yield byte-identical `ArtifactsInPublicationOrder`
- [x] A second `Commit` on the same session is rejected
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): publish payloads then the manifest`

---

#### T17: Abort discards and preserves the previous publication

**What**: `Abort` drops the current session's fragments. A later successful `Commit` on the same key
replaces the publication; an abort after a prior commit leaves that publication unchanged (ENG-24, ENG-25).
**Where**: `src/Csharp2Md.Storage/InMemoryTransactionalStore.cs`
**Depends on**: T16
**Reuses**: `CommittedPublication`
**Requirement**: ENG-24, ENG-25

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Abort then commit of a new session does not contain aborted fragments
- [x] Commit, then abort a second session, then read: the first publication is unchanged
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): abort without replacing a prior publication`

---

#### T18: Storage does not interpret fragments

**What**: Assert Storage public types reference no Domain taxonomy type and that commit does not inspect
payload bytes beyond storing them (ENG-28).
**Where**: `tests/Csharp2Md.Storage.Tests/Isolation/StorageIsolationTests.cs`
**Depends on**: T17
**Reuses**: `DomainIsolationTests` surface walk
**Requirement**: ENG-28

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`, `dotnet-test:assertion-quality`

**Done when**:

- [x] No public Storage member exposes a `Csharp2Md.Domain` type
- [x] Arbitrary payload bytes round-trip unchanged
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then Full (Domain + Analysis + Storage + Projection tests)
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean over this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `test(storage): pin opaque fragment handling`

---

### Phase 4: Analysis facade contracts

#### T19: AnalysisRequest rejects empty and duplicate paths

**What**: Add `AnalysisRequest.Create` accepting one or more paths, rejecting a missing list and a duplicate
canonical full path by name (ENG-29, ENG-30, ENG-31).
**Where**: `src/Csharp2Md.Analysis/AnalysisRequest.cs`
**Depends on**: T18
**Reuses**: `Path.GetFullPath`; Domain `ArgumentException` naming style
**Requirement**: ENG-29, ENG-30, ENG-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] An empty or default array is rejected naming the missing input
- [x] `.\a.sln` and the absolute form of the same file are rejected as a duplicate naming that path
- [x] Two distinct paths are accepted
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): validate the analysis request`

---

#### T20: Analysis result types

**What**: Add `AnalysisResult`, `SolutionOutcome`, `StageReport` and `PublicationStatus` as design.md
specifies, with results ordered by logical relative path.
**Where**: `src/Csharp2Md.Analysis/AnalysisResult.cs`
**Depends on**: T19
**Reuses**: `ImmutableArray`
**Requirement**: ENG-10, ENG-34

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `HasUnpublishedSolution` is true iff any outcome is `Unpublished`
- [x] `StageReport` carries name and the three zeroable counts
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add analysis result types`

---

#### T21: Declare IAnalysisEngine

**What**: Add exactly one public analysis interface with `AnalyzeAsync(AnalysisRequest, CancellationToken)`
(ENG-10, ENG-17).
**Where**: `src/Csharp2Md.Analysis/IAnalysisEngine.cs`
**Depends on**: T20
**Reuses**: `AnalysisRequest`, `AnalysisResult`
**Requirement**: ENG-10, ENG-17

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] Reflection over Analysis public interfaces finds exactly `IAnalysisEngine`
- [x] The method accepts a `CancellationToken`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): declare the single analysis entry interface`

---

#### T22: Public surface allowlist

**What**: Fail if Analysis exposes a public type outside the design.md allowlist, and fail if any public type
is a pass, classifier, adapter or stage type (ENG-11, ENG-12).
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisPublicSurfaceTests.cs`
**Depends on**: T21
**Reuses**: `DomainIsolationTests` type walk
**Requirement**: ENG-11, ENG-12

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] An extra public type fails naming it
- [x] The allowlist matches design.md
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): pin the public surface allowlist`

---

#### T23: AnalysisEngine production constructor

**What**: Add public `AnalysisEngine(ITransactionalStore store)` implementing `IAnalysisEngine`. The
production constructor is enough to construct; `AnalyzeAsync` may still throw `NotImplementedException`
until Phase 5. Assert Analysis has no project reference to Storage, Projection, CLI or Domain, and that
no public member exposes Roslyn, MSBuild or `System.Text.Json` (ENG-03, ENG-05, ENG-07).
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T22
**Reuses**: `ITransactionalStore`
**Requirement**: ENG-03, ENG-05, ENG-07

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`, `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `typeof(AnalysisEngine)` is constructible with a fake store
- [x] Analysis csproj has no project reference to Storage, Projection, CLI or Domain
- [x] Domain still has no package or project reference
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then Full (Domain + Analysis + Storage + Projection tests)
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean over this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): add the constructible analysis facade`

---

### Phase 5: Eight-stage pipeline

#### T24: Internal stage contract and eight stubs

**What**: Add internal `IPipelineStage`, `StageResult` and eight stub stages with the architecture names, in
order. Persistence stubs a single Manifest fragment. No stage type is public (ENG-12, ENG-15).
**Where**: `src/Csharp2Md.Analysis/Pipeline/IPipelineStage.cs`
**Depends on**: T23
**Reuses**: `ArtifactRole.Manifest`
**Requirement**: ENG-12, ENG-13, ENG-15

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] The eight names equal Inventory through Batch Composition as design.md lists
- [x] Default stubs return zeros and no corruption
- [x] No public Analysis type is named `IPipelineStage` or a stub class
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add the eight internal pipeline stubs`

---

#### T25: Orchestrator executes declared order

**What**: Add `PipelineOrchestrator` that runs the eight stages in order and records `StageReport` names
(ENG-13). Construction rejects a different count or name order.
**Where**: `src/Csharp2Md.Analysis/Pipeline/PipelineOrchestrator.cs`
**Depends on**: T24
**Reuses**: `IPipelineStage`
**Requirement**: ENG-13

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A recording probe list of the eight names is observed in that order
- [x] A shuffled name list is rejected at orchestrator construction
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): run pipeline stages in declared order`

---

#### T26: Stage substitution without orchestrator change

**What**: Add `internal AnalysisEngine(ITransactionalStore, ImmutableArray<IPipelineStage>)`. A substitute
stage in one position is executed there with the same orchestrator type (ENG-14).
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T25
**Reuses**: `InternalsVisibleTo` Analysis.Tests
**Requirement**: ENG-14

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Replacing Persistence with a recording probe runs that probe in position 6
- [x] `PipelineOrchestrator`'s type is unchanged across substitution
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): allow internal pipeline stage substitution`

---

#### T27: Default pipeline reports zeros

**What**: Production `AnalyzeAsync` for one existing path runs all eight stubs and reports zero facts,
observations and relations on every `StageReport` (ENG-15).
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T26
**Reuses**: `InMemoryTransactionalStore` from tests via a test double or Storage reference in the test project
**Requirement**: ENG-15

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Every stage report has counts 0/0/0
- [x] Status is `Committed`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): run the stub pipeline to a committed zero result`

---

#### T28: Persistence stub stages the manifest

**What**: The default Persistence stub stages one `ArtifactRole.Manifest` fragment so commit has a last
artifact. Tests assert the committed publication's last artifact is Manifest.
**Where**: `src/Csharp2Md.Analysis/Pipeline/StubStages.cs`
**Depends on**: T27
**Reuses**: `IStoreSession.Stage`
**Requirement**: ENG-23

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A production run's `CommittedPublication` ends with a Manifest fragment
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): stage a stub manifest before commit`

---

#### T29: Observe cancellation between stages

**What**: Check `CancellationToken` before each stage. A cancelled token stops before the next stage and
does not commit (ENG-17, ENG-18).
**Where**: `src/Csharp2Md.Analysis/Pipeline/PipelineOrchestrator.cs`
**Depends on**: T28
**Reuses**: `CancellationToken`
**Requirement**: ENG-18

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-concurrency-patterns`

**Done when**:

- [x] Cancelling after stage 3 records stages 1-3 only and `Unpublished`
- [x] The store session is aborted
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): cancel the pipeline between stages`

---

#### T30: Stage failure skips the rest and names the stage

**What**: A throwing substitute stage aborts the session, sets `FailingStage` to that stage's name, skips
later stages, and leaves the solution `Unpublished` (ENG-19, ENG-25).
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T29
**Reuses**: internal substitution constructor
**Requirement**: ENG-19, ENG-25

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`, `dotnet-test:assertion-quality`

**Done when**:

- [x] The failing stage name is the substitute's `Name`
- [x] Later stage probes do not run
- [x] Aborted fragments are absent from any publication
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then Full (Domain + Analysis + Storage + Projection tests)
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean over this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): isolate a failed stage to one solution`

---

### Phase 6: Run semantics and isolation

#### T31: Commit exactly once on success

**What**: A successful single-solution run calls `Commit` exactly once (ENG-22). Prove with a counting store
spy.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/CommitOnceTests.cs`
**Depends on**: T30
**Reuses**: `ITransactionalStore` spy
**Requirement**: ENG-22

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] The spy's commit count is 1
- [x] Status is `Committed`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): commit a successful solution once`

---

#### T32: Structural corruption aborts and keeps the prior publication

**What**: A Validation substitute returning `StructuralCorruption` aborts, sets the flag, does not commit,
and leaves a previously committed publication for that key unchanged (ENG-24).
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T31
**Reuses**: `StageResult.StructuralCorruption`
**Requirement**: ENG-24

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] First run commits; second run with corruption leaves the first `CommittedPublication` equal
- [x] `SolutionOutcome.StructuralCorruption` is true and status is `Unpublished`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): abort publication on structural corruption`

---

#### T33: Unknowns still commit

**What**: A substitute that sets `HasUnknownsOrCandidatesOrFrontiers` without corruption still commits and
exposes the flag (ENG-26).
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T32
**Reuses**: `StageResult`
**Requirement**: ENG-26

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Status is `Committed` and the unknowns flag is true
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): commit runs that only produce unknowns`

---

#### T34: Pipeline staging order does not change committed bytes

**What**: A Persistence substitute that stages payload keys in two orders still yields identical
`CommittedPublication` bytes (ENG-27) because the adapter canonicalizes.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/StagingOrderTests.cs`
**Depends on**: T33
**Reuses**: T16 adapter behavior
**Requirement**: ENG-27

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Two substitute staging orders compare equal on `ArtifactsInPublicationOrder`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): committed content ignores staging order`

---

#### T35: Multi-solution isolation

**What**: Three solutions where the second's stage throws: the first and third commit, the second is
unpublished, and each solution used a distinct `PipelineContext` instance with no shared compilation or
graph type materialized (ENG-32, ENG-33, ENG-35, ENG-36).
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T34
**Reuses**: substitution constructor; `fixtures/SyntheticSolution` paths only as strings
**Requirement**: ENG-32, ENG-33, ENG-35, ENG-36

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Outcomes are three entries; middle `Unpublished` with `FailingStage` set
- [x] Context identity differs per solution
- [x] Reflection on the engine after the run finds no `Microsoft.CodeAnalysis.Compilation` instance field
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): isolate each solution in the batch`

---

#### T36: Canonical result order ignores input order

**What**: The same two paths supplied swapped yield identical per-solution outcomes in the same logical
relative path order (ENG-34).
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T35
**Reuses**: `AnalysisRequest.SolutionPaths`
**Requirement**: ENG-34

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Shuffled input: `Solutions` ordered by `/`-normalized supplied path, ordinal
- [x] Corresponding `SolutionOutcome` values are equal
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): order batch results independently of input order`

---

#### T37: A completed run writes no files

**What**: Snapshot `src/`, `tests/`, `contracts/` and `fixtures/` (excluding `bin/`, `obj/`, `TestResults/`)
around a production `AnalyzeAsync` and assert no path added, changed or deleted (ENG-16).
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/NoFilesystemWriteTests.cs`
**Depends on**: T36
**Reuses**: design.md ENG-16 mitigation (do not snapshot `Path.GetTempPath()`)
**Requirement**: ENG-16

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`, `dotnet-test:assertion-quality`

**Done when**:

- [x] File-set hash before and after is equal
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then Full (Domain + Analysis + Storage + Projection tests)
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean over this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `test(analysis): prove the stub run writes nothing`

---

### Phase 7: Provisional analyze CLI

#### T38: Add the CLI test project

**What**: Add `tests/Csharp2Md.Cli.Tests` referencing the CLI project, `InternalsVisibleTo` on the CLI
csproj, registered under `/tests/`.
**Where**: `tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
**Depends on**: T37
**Reuses**: Analysis.Tests csproj; existing `PackAsTool` metadata
**Requirement**: ENG-02

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:scaffold-dotnet-test-project`

**Done when**:

- [x] The test project references `Csharp2Md.Cli`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(cli): add the cli test project`

---

#### T39: Replace the root action with analyze --solution

**What**: Rewrite `Program.cs` to a required `analyze` subcommand and repeatable `--solution` with
`Required = true` and `Arity = ArgumentArity.OneOrMore`. Compose `InMemoryTransactionalStore` and
`AnalysisEngine`. Keep `PackAsTool` / `ToolCommandName` `csharp2md` (ENG-02, ENG-37, ENG-38).
**Where**: `src/Csharp2Md.Cli/Program.cs`
**Depends on**: T38
**Reuses**: System.CommandLine 2.0 `SetAction` / `GetValue` already in this file; design.md command tree
**Requirement**: ENG-02, ENG-37, ENG-38

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] The command tree exposes exactly one verb, `analyze`
- [ ] `--solution` is required and repeatable
- [ ] CLI project references Analysis, Storage and Projection, not Core or Domain
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cli): expose analyze with repeatable --solution`

---

#### T40: Invocation errors exit 1 naming the problem

**What**: Missing `--solution` and a non-existent path exit 1 with the missing option or path named on
stderr (ENG-39, ENG-40). Duplicate paths reaching the engine map to exit 1 naming the duplicate.
**Where**: `src/Csharp2Md.Cli/Program.cs`
**Depends on**: T39
**Reuses**: existing `Invalid` helper message shape `csharp2md: {message}`
**Requirement**: ENG-39, ENG-40

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] No `--solution` → exit 1, stderr names the option
- [ ] Missing path → exit 1, stderr names that path
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cli): reject invalid analyze invocations with exit 1`

---

#### T41: Successful analyze exits 0 with split streams

**What**: A valid `--solution` against an existing fixture path exits 0, writes the summary to stdout and
diagnostics (if any) to stderr (ENG-41, ENG-44).
**Where**: `src/Csharp2Md.Cli/Program.cs`
**Depends on**: T40
**Reuses**: `fixtures/SyntheticSolution` as an existing path; engine stub ignores contents
**Requirement**: ENG-41, ENG-44

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Exit code is 0
- [ ] Summary is on stdout; diagnostics are not on stdout
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cli): report a completed stub analysis on stdout`

---

#### T42: Unpublished solutions exit 2; unknowns stay 0

**What**: Map `HasUnpublishedSolution` to exit 2 (ENG-42). A run that only flags unknowns still exits 0
(ENG-43). Drive the latter via InternalsVisibleTo if needed, or a test host that injects the engine.
**Where**: `src/Csharp2Md.Cli/Program.cs`
**Depends on**: T41
**Reuses**: design.md exit-code table
**Requirement**: ENG-42, ENG-43

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Forced unpublished outcome → exit 2
- [ ] Unknowns without unpublished → exit 0
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cli): map unpublished solutions to exit 2`

---

#### T43: Remove the markdown-era option surface

**What**: Assert `analyze` has no option named `--topic`, `--domain`, `--manifest`, `--output`, `--trust`,
`--include-source-generators` or `--analysis-timeout` (ENG-45). Rewrite `launchSettings.json` to the new
verb.
**Where**: `src/Csharp2Md.Cli/Program.cs`
**Depends on**: T42
**Reuses**: System.CommandLine option enumeration
**Requirement**: ENG-45

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Each removed name is absent from `analyze.Options` and `RootCommand.Options`
- [ ] `Properties/launchSettings.json` uses `analyze --solution`
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cli): drop the legacy option surface`

---

#### T44: Pin CLI references and packaging

**What**: Assert CLI declares project references only to Analysis, Storage and Projection, not Domain, and
still packs as the `csharp2md` tool (ENG-02, ENG-08).
**Where**: `tests/Csharp2Md.Cli.Tests/Isolation/CliIsolationTests.cs`
**Depends on**: T43
**Reuses**: csproj XML walk from T12
**Requirement**: ENG-02, ENG-08

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] The project-reference set equals Analysis, Storage, Projection
- [ ] `PackAsTool` is true and `ToolCommandName` is `csharp2md`
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then Full including Cli.Tests
- [ ] Test count recorded (no silent deletions)
- [ ] `dotnet-skills:slopwatch` reports clean over this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `test(cli): pin composition-root references and tool packaging`

---

### Phase 8: Legacy excision

#### T45: Write the port ledger

**What**: Add `docs/architecture/legacy-port-ledger.md` with one row per removed area, including last commit
SHA from `git log -1 --format=%H` on that path, and named rows for the Roslyn sanitation probes and CLI
security-boundary tests (ENG-52, ENG-53).
**Where**: `docs/architecture/legacy-port-ledger.md`
**Depends on**: T44
**Reuses**: engine-bootstrap inventory; `tests/Csharp2Md.Core.Tests/Analysis/Viability/RoslynSanitationProbeTests.cs`
**Requirement**: ENG-52, ENG-53

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Every Core area listed in the design reuse tables has a row with a SHA
- [ ] The two named test suites appear with `Re-establish in` workstream 4 and 8
- [ ] A test reads the ledger and asserts those two names are present
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `docs(architecture): record the legacy port ledger before deletion`

---

#### T46: Delete Csharp2Md.Core

**What**: Remove `src/Csharp2Md.Core` from disk and from `csharp2md.slnx` (ENG-46).
**Where**: `csharp2md.slnx`
**Depends on**: T45
**Reuses**: nothing; CLI no longer references Core after T39
**Requirement**: ENG-46

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:

- [ ] `src/Csharp2Md.Core` does not exist
- [ ] slnx has no Core project path
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release`
- [ ] Test count recorded (no silent deletions)

**Tests**: none
**Gate**: build

**Commit**: `chore(engine): remove the legacy Core assembly`

---

#### T47: Delete Csharp2Md.Core.Tests

**What**: Remove `tests/Csharp2Md.Core.Tests` and its slnx entry (ENG-47). Delete
`DomainIsolationTests` members that load `Csharp2Md.Core.dll`.
**Where**: `csharp2md.slnx`
**Depends on**: T46
**Reuses**: `tests/Csharp2Md.Domain.Tests/Isolation/DomainIsolationTests.cs`
**Requirement**: ENG-47, ENG-05

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:

- [ ] `tests/Csharp2Md.Core.Tests` does not exist
- [ ] Domain isolation tests no longer load Core
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: solution

**Commit**: `chore(engine): remove the legacy Core test project`

---

#### T48: Delete benchmarks and schemas

**What**: Remove `benchmarks/Csharp2Md.RetrievalIndex.Benchmarks` and `schemas/` (ENG-48).
**Where**: `csharp2md.slnx`
**Depends on**: T47
**Reuses**: nothing
**Requirement**: ENG-48

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:

- [ ] Both directories are absent
- [ ] slnx has no benchmarks folder
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: none
**Gate**: solution

**Commit**: `chore(engine): remove legacy benchmarks and schemas`

---

#### T49: Restrict the solution project list

**What**: Assert `csharp2md.slnx` lists only Domain, Analysis, Storage, Projection, CLI and their test
projects (ENG-49).
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/SolutionTopologyTests.cs`
**Depends on**: T48
**Reuses**: T12 topology test, tightened to an allowlist
**Requirement**: ENG-49

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Any extra project path fails naming it
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: solution

**Commit**: `test(engine): allowlist the post-excision solution`

---

#### T50: Keep SyntheticSolution fixtures

**What**: Assert `fixtures/SyntheticSolution` still exists on disk (ENG-50).
**Where**: `tests/Csharp2Md.Cli.Tests/Isolation/FixtureRetentionTests.cs`
**Depends on**: T49
**Reuses**: `fixtures/SyntheticSolution`
**Requirement**: ENG-50

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] The directory exists and contains at least one `.slnx` or `.csproj`
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(cli): keep the synthetic solution fixtures`

---

#### T51: Prune unused central package versions

**What**: Drop `Microsoft.CodeAnalysis.CSharp.Workspaces`,
`Microsoft.CodeAnalysis.Workspaces.MSBuild` and `YamlDotNet` from `Directory.Packages.props`. Fail the
hygiene test if a version has no consumer in the solution or `fixtures/` (ENG-51, ENG-06).
**Where**: `Directory.Packages.props`
**Depends on**: T50
**Reuses**: fixture csproj package references
**Requirement**: ENG-51, ENG-06

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:package-management`

**Done when**:

- [ ] Roslyn and YamlDotNet versions are absent
- [ ] Fixture packages remain
- [ ] A leftover unused `PackageVersion` fails naming the package
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: solution

**Commit**: `chore(engine): drop unused central package versions`

---

#### T52: Green solution and ENG-09

**What**: `dotnet test csharp2md.slnx` reports zero failures (ENG-54). Build with warnings-as-errors
succeeds (ENG-09). Confirm ENG-01 assembly set one last time.
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/SolutionTopologyTests.cs`
**Depends on**: T51
**Reuses**: nothing
**Requirement**: ENG-09, ENG-54, ENG-01

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`, `dotnet-test:run-tests`

**Done when**:

- [ ] `dotnet test csharp2md.slnx` exit code is 0
- [ ] `dotnet build csharp2md.slnx -c Release` succeeds with `TreatWarningsAsErrors`
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)
- [ ] `dotnet-skills:slopwatch` reports clean over this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `test(engine): require a fully green post-excision solution`

---

## Phase Execution Map

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 7 → Phase 8

Phase 1:  T1 → T2 → T3 → T4 → T5
Phase 2:  T6 → T7 → T8 → T9 → T10 → T11 → T12
Phase 3:  T13 → T14 → T15 → T16 → T17 → T18
Phase 4:  T19 → T20 → T21 → T22 → T23
Phase 5:  T24 → T25 → T26 → T27 → T28 → T29 → T30
Phase 6:  T31 → T32 → T33 → T34 → T35 → T36 → T37
Phase 7:  T38 → T39 → T40 → T41 → T42 → T43 → T44
Phase 8:  T45 → T46 → T47 → T48 → T49 → T50 → T51 → T52
```

Execution is strictly sequential. Packing into ~7-task batches at Execute (whole phases, never split):

| Batch | Phases | Tasks |
| --- | --- | --- |
| 1 | Phase 1 | T1–T5 (5) |
| 2 | Phase 2 | T6–T12 (7) |
| 3 | Phase 3 | T13–T18 (6) |
| 4 | Phase 4 | T19–T23 (5) |
| 5 | Phase 5 | T24–T30 (7) |
| 6 | Phase 6 | T31–T37 (7) |
| 7 | Phase 7 | T38–T44 (7) |
| 8 | Phase 8 | T45–T52 (8) |

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: Evidence method on Create | 1 method + call-site updates | ✅ Granular |
| T2: Callable wiring | 1 method | ✅ Granular |
| T3: Target-shape wiring | 1 method | ✅ Granular |
| T4: Guard reachability test | 1 test file | ✅ Granular |
| T5: Drift + TAX rows | 1 spec file (drift is existing test) | ⚠️ 2 related artifacts, same closure |
| T6–T11: Projects | 1 csproj or 1 isolation test each | ✅ Granular |
| T12: Topology pin | 1 test file | ✅ Granular |
| T13–T14: Port types | 1 file each | ✅ Granular |
| T15–T17: Adapter methods | 1 class, sliced by behavior | ✅ Granular |
| T18: Storage isolation | 1 test file | ✅ Granular |
| T19–T23: Facade | 1 type or 1 test file each | ✅ Granular |
| T24–T30: Pipeline | 1 seam each | ✅ Granular |
| T31–T37: Run semantics | 1 invariant each | ✅ Granular |
| T38–T44: CLI | 1 behavior each | ✅ Granular |
| T45: Ledger | 1 file | ✅ Granular |
| T46–T48: Deletions | 1 tree each | ✅ Granular |
| T49–T52: Post-excision pins | 1 assertion set each | ✅ Granular |

**Granularity check**: no task owns unrelated components. T5 pairs the drift assertion with the TAX-row edit
because ENG-60 and ENG-61 are one closure of the Domain work.

---

## Diagram-Definition Cross-Check

Intra-phase `Depends on` is a linear predecessor chain matching each phase fence. Cross-phase: T6 depends on
T5, T13 on T12, T19 on T18, T24 on T23, T31 on T30, T38 on T37, T45 on T44.

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | (start) | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | (cross-phase) | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T7 | T7 → T8 | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T9 | T9 → T10 | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T12 | (cross-phase) | ✅ Match |
| T14 | T13 | T13 → T14 | ✅ Match |
| T15 | T14 | T14 → T15 | ✅ Match |
| T16 | T15 | T15 → T16 | ✅ Match |
| T17 | T16 | T16 → T17 | ✅ Match |
| T18 | T17 | T17 → T18 | ✅ Match |
| T19 | T18 | (cross-phase) | ✅ Match |
| T20 | T19 | T19 → T20 | ✅ Match |
| T21 | T20 | T20 → T21 | ✅ Match |
| T22 | T21 | T21 → T22 | ✅ Match |
| T23 | T22 | T22 → T23 | ✅ Match |
| T24 | T23 | (cross-phase) | ✅ Match |
| T25 | T24 | T24 → T25 | ✅ Match |
| T26 | T25 | T25 → T26 | ✅ Match |
| T27 | T26 | T26 → T27 | ✅ Match |
| T28 | T27 | T27 → T28 | ✅ Match |
| T29 | T28 | T28 → T29 | ✅ Match |
| T30 | T29 | T29 → T30 | ✅ Match |
| T31 | T30 | (cross-phase) | ✅ Match |
| T32 | T31 | T31 → T32 | ✅ Match |
| T33 | T32 | T32 → T33 | ✅ Match |
| T34 | T33 | T33 → T34 | ✅ Match |
| T35 | T34 | T34 → T35 | ✅ Match |
| T36 | T35 | T35 → T36 | ✅ Match |
| T37 | T36 | T36 → T37 | ✅ Match |
| T38 | T37 | (cross-phase) | ✅ Match |
| T39 | T38 | T38 → T39 | ✅ Match |
| T40 | T39 | T39 → T40 | ✅ Match |
| T41 | T40 | T40 → T41 | ✅ Match |
| T42 | T41 | T41 → T42 | ✅ Match |
| T43 | T42 | T42 → T43 | ✅ Match |
| T44 | T43 | T43 → T44 | ✅ Match |
| T45 | T44 | (cross-phase) | ✅ Match |
| T46 | T45 | T45 → T46 | ✅ Match |
| T47 | T46 | T46 → T47 | ✅ Match |
| T48 | T47 | T47 → T48 | ✅ Match |
| T49 | T48 | T48 → T49 | ✅ Match |
| T50 | T49 | T49 → T50 | ✅ Match |
| T51 | T50 | T50 → T51 | ✅ Match |
| T52 | T51 | T51 → T52 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Domain relation construction | unit | unit | ✅ OK |
| T2 | Domain relation construction | unit | unit | ✅ OK |
| T3 | Domain relation construction | unit | unit | ✅ OK |
| T4 | Domain relation construction | unit | unit | ✅ OK |
| T5 | Domain relation construction + spec artifact | unit | unit | ✅ OK |
| T6 | Project wiring | none | none | ✅ OK |
| T7 | Assembly isolation / test project | unit | unit | ✅ OK |
| T8 | Project wiring | none | none | ✅ OK |
| T9 | Test project smoke | unit | unit | ✅ OK |
| T10 | Project wiring | none | none | ✅ OK |
| T11 | Projection marker isolation | unit | unit | ✅ OK |
| T12 | Assembly isolation | unit | unit | ✅ OK |
| T13 | Storage port types | unit | unit | ✅ OK |
| T14 | Storage port | unit | unit | ✅ OK |
| T15 | In-memory adapter | unit | unit | ✅ OK |
| T16 | In-memory adapter | unit | unit | ✅ OK |
| T17 | In-memory adapter | unit | unit | ✅ OK |
| T18 | Storage isolation | unit | unit | ✅ OK |
| T19 | Analysis request | unit | unit | ✅ OK |
| T20 | Analysis result | unit | unit | ✅ OK |
| T21 | Analysis facade | unit | unit | ✅ OK |
| T22 | Public surface allowlist | unit | unit | ✅ OK |
| T23 | Analysis facade + isolation | unit | unit | ✅ OK |
| T24 | Pipeline stubs | unit | unit | ✅ OK |
| T25 | Pipeline orchestrator | unit | unit | ✅ OK |
| T26 | Pipeline substitution | unit | unit | ✅ OK |
| T27 | Pipeline stubs | unit | unit | ✅ OK |
| T28 | Pipeline stubs | unit | unit | ✅ OK |
| T29 | Pipeline orchestrator | unit | unit | ✅ OK |
| T30 | Pipeline failure | unit | unit | ✅ OK |
| T31 | Run semantics | unit | unit | ✅ OK |
| T32 | Run semantics | unit | unit | ✅ OK |
| T33 | Run semantics | unit | unit | ✅ OK |
| T34 | Run semantics | unit | unit | ✅ OK |
| T35 | Multi-solution isolation | unit | unit | ✅ OK |
| T36 | Determinism | unit | unit | ✅ OK |
| T37 | No filesystem write | unit | unit | ✅ OK |
| T38 | Project wiring + CLI tests | unit | unit | ✅ OK |
| T39 | CLI analyze verb | unit | unit | ✅ OK |
| T40 | CLI errors | unit | unit | ✅ OK |
| T41 | CLI success path | unit | unit | ✅ OK |
| T42 | CLI exit codes | unit | unit | ✅ OK |
| T43 | CLI options | unit | unit | ✅ OK |
| T44 | CLI isolation | unit | unit | ✅ OK |
| T45 | Port ledger | none (asserted by unit test in Isolation) | unit | ✅ OK |
| T46 | Project wiring (deletion) | none | none | ✅ OK |
| T47 | Isolation (Domain Core load) | unit | unit | ✅ OK |
| T48 | Project wiring (deletion) | none | none | ✅ OK |
| T49 | Assembly isolation | unit | unit | ✅ OK |
| T50 | Fixture retention | unit | unit | ✅ OK |
| T51 | Package hygiene | unit | unit | ✅ OK |
| T52 | Solution green | unit | unit | ✅ OK |
