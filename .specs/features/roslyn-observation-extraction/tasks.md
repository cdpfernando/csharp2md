# Roslyn Observation Extraction Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and
Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the
full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**User-confirmed deviation from the standard flow:** the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by standing user request, as it was for
`symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`,
`knowledge-taxonomy-contract`, `engine-bootstrap` and `factual-storage`. No mutants are injected at any
point. The Verifier's spec-anchored outcome check, per-AC `file:line` evidence, and `validation.md` report
still run as normal.

**Phase-end quality gate (user-confirmed, applies to every phase):** the last task of each phase - T6, T12,
T18, T24, T31, T37, T44, T51, T56, T63 - additionally runs `dotnet-skills:slopwatch` over that phase's
changes and reports clean before the phase is considered complete. This is in addition to each task's own
`Tools` list.

---

**Spec**: `.specs/features/roslyn-observation-extraction/spec.md`
**Context**: `.specs/features/roslyn-observation-extraction/context.md`
**Design**: `.specs/features/roslyn-observation-extraction/design.md`
**Status**: Approved — Execute in progress (phases 1–3)

**Scope of this task list**: the whole feature. All 64 requirements `ROSE-01` through `ROSE-64` are broken
down here; nothing is deferred to a later pass. The approved design is the implementation contract.

**Roslyn note (Execute):** consult Context7 `/dotnet/roslyn` before any `ISymbol`, `MSBuildWorkspace`,
`CSharpSyntaxWalker`, generator, or analyzer-reference call. Do not invent members. Open a workspace with
`MSBuildWorkspace.Create(properties)` then `OpenSolutionAsync(path, progress: null, cancellationToken)`
only. Do not call `MSBuildLocator.RegisterDefaults`. Do not use the `Microsoft.Build.Framework.ILogger`
overload. Sanitation probes use the members Context7 documents for empty analyzer references and absent
generators (`WithAnalyzerReferences` / `GetGenerators` / `GetGeneratorsForAllLanguages` as documented).

**Default-engine seam (Execute):** after T12, every test that feeds a fake solution path (`alpha.sln`,
`alpha/a.sln`, `zeta/b.sln`) must pass `StubStages.CreateDefault()` into the internal
`AnalysisEngine` constructor. Fixture tests keep the production constructor. `PersistenceManifestTests`
keeps asserting an empty package until T52, because Persistence still stages `FactualSnapshot.Empty` until
then.

**LocalCorpus (Verifier only):** after the feature Verifier, run
`tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs` when `fixtures/eShop` or
`fixtures/eShopOnContainers` exist; skip when they do not. That is not a task in this list.

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/Csharp2Md.Analysis.Tests/Pipeline/DefaultPipelineZerosTests.cs`,
> `CommitOnceTests.cs`, `NoFilesystemWriteTests.cs`, `CanonicalResultOrderTests.cs`,
> `Isolation/AnalysisPublicSurfaceTests.cs`, `PackageHygieneTests.cs`, `SolutionTopologyTests.cs`,
> `Storage/FactualSnapshotTests.cs`, `tests/Csharp2Md.Cli.Tests/AnalyzeOptionSurfaceTests.cs`,
> `AnalyzeSuccessTests.cs`, `LocalCorpusAnalyzeTests.cs`,
> `tests/Csharp2Md.Storage.Tests/Surface/RequirementCoverageTests.cs`) and project guidelines.
> Guidelines found: `AGENTS.md` and `CLAUDE.md` (they route test quality to the `dotnet-test:*` skills as
> post-hoc gates, declare no coverage threshold, skip the discrimination sensor, and name
> `fixtures/SyntheticSolution` as the only versioned analysis fixture), `Directory.Build.props:7`
> (`TreatWarningsAsErrors`), `.config/dotnet-tools.json` (slopwatch). No coverage-threshold tool config and
> no CI workflow exist in this repository, so strong defaults apply to the Coverage Expectation column.
> Trait IDs for new tests are `ROSE-nn`. Existing `ENG-*` / `STOR-*` tests that this feature supersedes are
> rewritten in the task that replaces their contract, not deleted silently.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Project wiring (`*.csproj` PackageReference, `Directory.Packages.props`) | none | Build gate only - declarative MSBuild; isolation tests in the row below catch forbidden references | `src/Csharp2Md.Analysis/Csharp2Md.Analysis.csproj`, `Directory.Packages.props` | build gate only |
| Fixture stand-in source (`fixtures/SyntheticSolution`) | none | Build gate only - versioned corpus; occurrence assertions live on the extractor tests | `fixtures/SyntheticSolution/Acme.Orders/**/*.cs` | build gate only |
| Snapshot, diagnostics, accumulator, unpublished abort | unit | All branches; 1:1 to ROSE-21, ROSE-56, AD-017; identity collision aborts; observation union prefers bound; `IdentityOrKey` is never an absolute path | `tests/Csharp2Md.Analysis.Tests/Storage/*Tests.cs`, `Pipeline/*Tests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| DomainMapper diagnostics envelope | unit | 1:1 to ROSE-58 mapper side; named codes from design.md; suspected-secret DTO has span + redacted excerpt and omits the secret | `tests/Csharp2Md.Storage.Tests/Mapping/*Tests.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Assembly isolation (public surface, no Roslyn types exported, no `Microsoft.Build.*` PackageReference, no Locator call) | unit | 1:1 to ROSE-26..31, ROSE-48, ROSE-62; each forbidden type, package or call named in the failure | `tests/Csharp2Md.*.Tests/Isolation/*Tests.cs` | matching `dotnet test tests/Csharp2Md.<X>.Tests/Csharp2Md.<X>.Tests.csproj` |
| Inventory (authorized root, solution file reader, path guard, structural facts) | unit | All branches; 1:1 to ROSE-01..13, ROSE-07, ROSE-11; symlink unpublished; missing project named; duplicate `SolutionId` rejected before Open | `tests/Csharp2Md.Analysis.Tests/Inventory/*Tests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Semantic Analysis (workspace factory, sanitizer, symbol emitter, variants) | unit | All branches; 1:1 to ROSE-12, ROSE-14..16, ROSE-22..25, ROSE-29, ROSE-30; sanitation probes empty; `Acme.Broken` commits; Open failure unpublished that solution only | `tests/Csharp2Md.Analysis.Tests/Semantics/*Tests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Observation extractors (walker, detectors, ordinals, secrets, contains) | unit | All branches; 1:1 to ROSE-32..51, ROSE-55; one named fixture occurrence per kind; registered-context kinds absent outside compiled contexts; empty payload except Configuration key and Route literal | `tests/Csharp2Md.Analysis.Tests/Extraction/*Tests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Filled pipeline / Persistence | unit | 1:1 to ROSE-58..60, ROSE-63, ROSE-64; Inventory/Semantic/Extraction counts > 0 on the fixture; later stubs 0/0/0; last valid package kept on abort; in-memory creates no files | `tests/Csharp2Md.Analysis.Tests/Pipeline/*Tests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Determinism, clone-path, secrets, absolute paths | unit | 1:1 to ROSE-52..56; two working directories, retry bytes, shuffled documents, planted `Password=secret` | `tests/Csharp2Md.Analysis.Tests/Pipeline/*Tests.cs`, `Extraction/*Tests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| CLI `analyze` surface | unit | 1:1 to ROSE-02 (named Detail), ROSE-08 (exit 1), ROSE-61, ROSE-62; `--trust` / `--include-source-generators` / `--analysis-timeout` absent | `tests/Csharp2Md.Cli.Tests/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj` |
| Requirement traceability | unit | Every ROSE-01..64 is carried by at least one `[Trait("Requirement", "ROSE-nn")]` | `tests/Csharp2Md.Analysis.Tests/Surface/RequirementCoverageTests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |

## Gate Check Commands

> Generated from the repository's own build and test entry points (`csharp2md.slnx`, `global.json`,
> `tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`). No CI workflow file exists. The test
> stack is xUnit 2.9.3 on `Microsoft.NET.Test.Sdk` (VSTest). `dotnet test csharp2md.slnx` is a valid green
> gate. No `.editorconfig` exists, so `dotnet format` enforces whitespace and import ordering only.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks whose tests live in one test project | `dotnet test tests/Csharp2Md.<X>.Tests/Csharp2Md.<X>.Tests.csproj` for the project the task names |
| Full | After tasks spanning more than one test project | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj` |
| Build | After phase completion and for project- or fixture-only tasks | `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command |
| Solution | After Persistence stages the accumulator (T52 onward) and at feature close | `dotnet test csharp2md.slnx` |

Every phase-end task (T6, T12, T18, T24, T31, T37, T44, T51, T56, T63) runs `dotnet-skills:slopwatch` over
the phase's changes per the protocol above. T45 also uses the build gate because it is fixture source with
no test layer; it does not run slopwatch. The tool is pinned locally in `.config/dotnet-tools.json`.

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a
phase execute in the listed order. Each phase diagram shows the intra-phase chain; `Depends on` in a task body
names its execution predecessor, and the first task of each phase depends on the last task of the phase before
it.

### Phase 1: Snapshot, diagnostics, unpublished abort

Named diagnostics reach the package. Stages can abort a solution without calling it structural corruption.
The accumulator is the only in-memory graph.

```
T1 → T2 → T3 → T4 → T5 → T6
```

### Phase 2: Roslyn packages and test seams

Analysis may restore Workspaces.MSBuild. Fake-path tests keep injecting stubs. Production `CreateDefault`
is the slot later phases fill.

```
T7 → T8 → T9 → T10 → T11 → T12
```

### Phase 3: Inventory primitives

Authorized root, solution file parse, symlink guard, Solution/Project facts, duplicate identity.

```
T13 → T14 → T15 → T16 → T17 → T18
```

### Phase 4: Inventory stage

Documents, unsupported files, missing projects, filled Inventory, symlink abort, CLI Detail.

```
T19 → T20 → T21 → T22 → T23 → T24
```

### Phase 5: Semantic workspace

MSBuildWorkspace per TFM, sanitation, open/SDK/compile failure policy, variants.

```
T25 → T26 → T27 → T28 → T29 → T30 → T31
```

### Phase 6: Symbol facts

BoundSolution lease, declared symbols, Callable only, no architecture facets, public surface still clean.

```
T32 → T33 → T34 → T35 → T36 → T37
```

### Phase 7: Observation extraction core

Drafts, ordinals, always-when-bindable kinds, locators, secrets, filled Extraction stage, identity union.

```
T38 → T39 → T40 → T41 → T42 → T43 → T44
```

### Phase 8: Fixture, registered-context detectors, contains

ROSE-49 occurrences, five detectors, `contains` only.

```
T45 → T46 → T47 → T48 → T49 → T50 → T51
```

### Phase 9: Persistence and package contract

Stage the accumulator. Later stubs stay zero. No retrieval files. In-memory writes nothing. CLI surface
unchanged.

```
T52 → T53 → T54 → T55 → T56
```

### Phase 10: Determinism, secrets, abort, traceability

Clone-path, retry bytes, shuffle, no absolute paths, planted secret, last package kept, ROSE trait coverage.

```
T57 → T58 → T59 → T60 → T61 → T62 → T63
```

---

## Task Breakdown

### Phase 1: Snapshot, diagnostics, unpublished abort

#### T1: Add DiagnosticRecord

**What**: Add public `DiagnosticRecord(string Code, string Message, string? IdentityOrKey)` on the Analysis
storage surface and allowlist the type. `IdentityOrKey` is a relative path or fact id, never an absolute
filesystem path.
**Where**: `src/Csharp2Md.Analysis/Storage/DiagnosticRecord.cs`
**Depends on**: None
**Reuses**: `src/Csharp2Md.Storage/Wire/EnvelopeDtos.cs` `DiagnosticRecordDto` field-for-field;
`tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisPublicSurfaceTests.cs` allowlist
**Requirement**: ROSE-56

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`, `dotnet-skills:csharp-nullable-reference-types`

**Done when**:

- [x] Type is public, allowlisted, and constructed with the three fields from design.md
- [x] A test rejects an `IdentityOrKey` that contains a drive prefix or `Path.IsPathRooted` value
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add named diagnostic records for the package envelope`

---

#### T2: Extend FactualSnapshot with diagnostics and secrets

**What**: Add trailing optional `Diagnostics` and `SuspectedSecrets` parameters defaulting to empty so
existing six-argument call sites still compile. `Empty` exposes empty arrays for both. Merge concatenates
them.
**Where**: `src/Csharp2Md.Analysis/Storage/FactualSnapshot.cs`
**Depends on**: T1
**Reuses**: Domain `SuspectedSecretEvidence`; `tests/Csharp2Md.Analysis.Tests/Storage/FactualSnapshotTests.cs`
**Requirement**: ROSE-58

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`, `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] `Empty` has length-zero `Diagnostics` and `SuspectedSecrets`
- [x] A six-argument `new FactualSnapshot(...)` still compiles; Merge concatenates the new arrays
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): carry diagnostics and suspected secrets on the snapshot`

---

#### T3: Map snapshot diagnostics into DiagnosticsEnvelope

**What**: Stop hardcoding `new DiagnosticsEnvelope([])`. Map `FactualSnapshot.Diagnostics` field-for-field
and flatten `SuspectedSecrets` to `DiagnosticRecordDto` with code `suspected-secret`, document id as
`IdentityOrKey`, and a message that includes the one-based span plus the redacted excerpt. The secret value
is absent from the DTO and from canonical JSON.
**Where**: `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`
**Depends on**: T2
**Reuses**: `DiagnosticRecordDto`; `tests/Csharp2Md.Storage.Tests/Mapping/`
**Requirement**: ROSE-58

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:serialization`, `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `ToWire` copies snapshot diagnostics in stable order
- [x] A suspected-secret snapshot produces a `suspected-secret` record whose JSON does not contain the raw
      secret and does contain `***` or `[REDACTED]`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): publish snapshot diagnostics into diagnostics.json`

---

#### T4: Add SolutionOutcome.Detail

**What**: Add optional `string? Detail` on `SolutionOutcome`, default null, so existing call sites compile.
CLI tests that construct outcomes still pass.
**Where**: `src/Csharp2Md.Analysis/AnalysisResult.cs`
**Depends on**: T3
**Reuses**: existing `SolutionOutcome` constructor; `tests/Csharp2Md.Analysis.Tests/AnalysisResultTests.cs`
**Requirement**: ROSE-02

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] `Detail` is readable; omitted argument is null
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): name unpublished solution failures on SolutionOutcome`

---

#### T5: Abort publication without structural corruption

**What**: Add `AbortPublication` to `StageResult`. The orchestrator maps it to unpublished with
`StructuralCorruption == false` and copies `PipelineContext` publication detail onto `SolutionOutcome.Detail`.
Do not treat this path as `PublicationRejectedException`.
**Where**: `src/Csharp2Md.Analysis/Pipeline/IPipelineStage.cs`
**Depends on**: T4
**Reuses**: `PipelineOrchestrator` Failed vs Corrupted split; `AnalysisEngine.CreateOutcome`
**Requirement**: ROSE-22

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A substitute stage with `AbortPublication: true` unpublished that solution, left
      `StructuralCorruption` false, and copied Detail
- [x] A `StructuralCorruption: true` result still takes the corruption path
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): abort a solution without calling it structural corruption`

---

#### T6: Add SnapshotAccumulator

**What**: Add internal `SnapshotAccumulator` on `PipelineContext`. `AddFact` of unequal facts with one
identity sets `StructuralCorruption`. `AddObservation` unions by identity and keeps `bound` over `unbound`.
`ToSnapshot()` is what Persistence will stage. Roslyn types do not appear on the accumulator.
**Where**: `src/Csharp2Md.Analysis/Pipeline/SnapshotAccumulator.cs`
**Depends on**: T5
**Reuses**: Domain `Observation` identity (span is not an axis); `FactualSnapshot`
**Requirement**: ROSE-21

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Unequal facts with one identity set `StructuralCorruption`
- [x] Two observations that differ only in span collapse to one identity; bound wins over unbound
- [x] `ToSnapshot()` includes facts, observations, relations, diagnostics and suspected secrets
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean on this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): accumulate the per-solution factual snapshot in memory`

---

### Phase 2: Roslyn packages and test seams

#### T7: Allow Analysis to reference Microsoft.CodeAnalysis packages

**What**: Change `SolutionTopologyTests.ForbiddenPackageCases` so Analysis may reference
`Microsoft.CodeAnalysis.*` while Storage and Projection still must not. Keep `Microsoft.Build` forbidden on
all three. Remove `Microsoft.CodeAnalysis.Workspaces.MSBuild` and
`Microsoft.CodeAnalysis.CSharp.Workspaces` from `PackageHygieneTests.DroppedPackageVersions`.
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/SolutionTopologyTests.cs`
**Depends on**: T6
**Reuses**: `PackageHygieneTests.DroppedPackageVersions`; design.md integration points
**Requirement**: ROSE-26

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Analysis + `Microsoft.CodeAnalysis` is no longer a forbidden case; Storage and Projection still are
- [x] The two Workspaces package ids are absent from the dropped list; YamlDotNet stays dropped
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): allow Workspaces packages on Analysis isolation tests`

---

#### T8: Reference Workspaces.MSBuild 5.6.0 from Analysis

**What**: Restore `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0 and
`Microsoft.CodeAnalysis.CSharp.Workspaces` 5.6.0 in `Directory.Packages.props` and add those
PackageReference items to Analysis. Do not add any `Microsoft.Build.*` PackageReference.
**Where**: `src/Csharp2Md.Analysis/Csharp2Md.Analysis.csproj`
**Depends on**: T7
**Reuses**: `Directory.Packages.props`; AD-003
**Requirement**: ROSE-26

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:package-management`, `dotnet-skills:project-structure`

**Done when**:

- [x] Both PackageVersion entries are 5.6.0 and Analysis consumes them
- [x] `DirectoryPackages_EveryPackageVersionHasAConsumer` stays green
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): restore MSBuild workspace 5.6.0 for semantic binding`

---

#### T9: Forbid Microsoft.Build PackageReference on Analysis

**What**: Assert Analysis.csproj contains no PackageReference whose id starts with `Microsoft.Build`.
Keep the existing `Microsoft.Build` forbidden case on Storage and Projection.
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisIsolationTests.cs`
**Depends on**: T8
**Reuses**: `SolutionTopologyTests.Csproj_DeclaresNoForbiddenPackage`
**Requirement**: ROSE-27

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] A test names any `Microsoft.Build*` PackageReference on Analysis if one appears
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): reject Microsoft.Build package references on Analysis`

---

#### T10: Forbid MSBuildLocator.RegisterDefaults in production

**What**: Scan production `src/**/*.cs` for `MSBuildLocator` and `RegisterDefaults`. The test fails by
naming the file if either appears.
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/MsBuildLocatorAbsenceTests.cs`
**Depends on**: T9
**Reuses**: existing file-scan isolation tests
**Requirement**: ROSE-28

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] The scanner reports zero production hits
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): forbid MSBuildLocator.RegisterDefaults in production source`

---

#### T11: Add PipelineStages.CreateDefault

**What**: Add `PipelineStages.CreateDefault()` as the production stage list (still the eight stubs at this
task). Switch `AnalysisEngine(ITransactionalStore)` to that factory. Keep `StubStages.CreateDefault()` as
the injectable empty walking skeleton.
**Where**: `src/Csharp2Md.Analysis/Pipeline/PipelineStages.cs`
**Depends on**: T10
**Reuses**: `StubStages.DeclaredNames` and the eight stub classes; `AnalysisEngine` internal constructor
**Requirement**: ROSE-59

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Production constructor uses `PipelineStages.CreateDefault()`
- [x] `StageSubstitutionTests` still constructs a substitute beside `StubStages.CreateDefault()`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): separate production pipeline wiring from the stub skeleton`

---

#### T12: Inject stubs into fake-path default-engine tests

**What**: Change every test that opens a fake solution path through the production constructor so it passes
`StubStages.CreateDefault()`. Named files: `CanonicalResultOrderTests`, `CommitOnceTests`, the first run in
`StructuralCorruptionTests`. Do not change fixture tests.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/CanonicalResultOrderTests.cs`
**Depends on**: T11
**Reuses**: internal `AnalysisEngine(ITransactionalStore, ImmutableArray<IPipelineStage>)`
**Requirement**: ROSE-59

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Fake-path tests no longer call `new AnalysisEngine(store)` without stages
- [x] `DefaultPipelineZerosTests` and `PersistenceManifestTests` still use the production constructor on
      `Acme.Orders.slnx`
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean on this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `test(analysis): keep stub stages on fake-path engine tests`

---

### Phase 3: Inventory primitives

#### T13: Add SecretRedactor

**What**: Add an internal heuristic that flags connection-string keys, `Password=`, token/bearer, and
certificate PEM. On match it returns a `RedactedExcerpt` containing `***` or `[REDACTED]` and does not hash
the secret value.
**Where**: `src/Csharp2Md.Analysis/Extraction/SecretRedactor.cs`
**Depends on**: T12
**Reuses**: Domain `SuspectedSecretEvidence`, `RedactedExcerpt.Create`
**Requirement**: ROSE-55

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `Password=secret` is flagged; the returned excerpt contains a mask and not `secret`
- [x] A non-secret literal such as `OrderStatus.Placed` is not flagged
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): redact suspected secrets before they enter payloads`

---

#### T14: Compute AuthorizedRoot

**What**: `AuthorizedRoot.Compute(solutionPath, existingProjectPaths)` returns the smallest directory that
contains the solution file and every listed project path that exists on disk. Missing listed paths do not
expand the root.
**Where**: `src/Csharp2Md.Analysis/Inventory/AuthorizedRoot.cs`
**Depends on**: T13
**Reuses**: `fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx` sibling projects
**Requirement**: ROSE-01

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] For `Acme.Orders.slnx` plus existing sibling projects the root is `fixtures/SyntheticSolution`
- [x] A missing `Acme.DoesNotExist` path does not change that root
- [x] A solution whose projects all sit under its own directory keeps that directory as root
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): compute the authorized root from the solution and existing projects`

---

#### T15: Read project paths from solution files

**What**: `SolutionFileReader.ReadProjectPaths` walks `.slnx` `Project/@Path` and `.sln` `Project(...)`
paths without MSBuild. Paths are returned as listed, not yet rooted.
**Where**: `src/Csharp2Md.Analysis/Inventory/SolutionFileReader.cs`
**Depends on**: T14
**Reuses**: `.slnx` XML walk in `PackageHygieneTests.ConsumerProjectPaths`
**Requirement**: ROSE-03

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `Acme.Orders.slnx` yields `Acme.Orders`, `Acme.Shared.Contracts`, `Acme.Broken`, `Acme.DoesNotExist`
- [x] A `.sln` fixture string with a `Project(...)` path yields that path
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): read inventoried project paths without MSBuild`

---

#### T16: Reject symlink escapes with PathGuard

**What**: `PathGuard.RejectEscapes(root, path)` resolves reparse points and aborts when the resolved target
lies outside the authorized root. It does not follow, hash, or copy the target.
**Where**: `src/Csharp2Md.Analysis/Inventory/PathGuard.cs`
**Depends on**: T15
**Reuses**: `AuthorizedRoot`; `File.CreateSymbolicLink` / `Directory.CreateSymbolicLink`
**Requirement**: ROSE-02

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A symlink inside the root that points at a file outside it is rejected and the symlink path is named
- [x] A regular file inside the root is accepted
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): reject inventory paths that escape the authorized root`

---

#### T17: Emit Solution and Project facts

**What**: Inventory constructs `WorkspaceIdentity.Create("default")`, a `Solution` whose logical path is
`Path.GetFileName` including extension, and one `Project` per existing listed path whose logical relative
path is relative to the authorized root with forward slashes. `SolutionOutcome.LogicalRelativePath` stays
the `--solution` token with `/` separators.
**Where**: `src/Csharp2Md.Analysis/Inventory/InventoryFacts.cs`
**Depends on**: T16
**Reuses**: Domain `Solution.Create`, `Project.Create`, `SolutionId`, `ProjectId`
**Requirement**: ROSE-09

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Fixture solution id uses workspace `default` and logical path `Acme.Orders.slnx`
- [x] `Acme.Shared.Contracts` logical path is relative to `fixtures/SyntheticSolution` with `/`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): emit clone-path-independent Solution and Project facts`

---

#### T18: Reject duplicate SolutionId before Open

**What**: `AnalyzeAsync` computes each requested path's `SolutionId` before `Open`. Two inputs that share a
filename (hence a `SolutionId`) throw `ArgumentException` naming both paths. CLI already maps that to exit
1.
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T17
**Reuses**: CLI `CommandFactory` `ArgumentException` → exit 1; `AnalysisRequest.Create`
**Requirement**: ROSE-08

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Two folders each containing `Acme.Orders.slnx` are rejected before any session `Open`
- [x] The exception message names both input paths
- [x] Distinct filenames in one request still proceed
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean on this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): reject duplicate solution identities before opening a store`

---

### Phase 4: Inventory stage

#### T19: Emit Document facts excluding build outputs

**What**: Inventory emits one `Document` per project item whose resolved path is inside the root, plus files
sitting in the project directory inside the root, excluding directory names `bin`, `obj`, `.git`, `.vs`.
Relative paths use forward slashes and contain no drive prefix.
**Where**: `src/Csharp2Md.Analysis/Inventory/DocumentInventory.cs`
**Depends on**: T18
**Reuses**: Domain `Document.Create`; ROSE-53 exclusion list in design.md
**Requirement**: ROSE-04

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `OrdersController.cs` is a Document with a `/`-separated relative path and no drive prefix
- [x] A planted `bin/Generated.cs` under the project directory is not inventoried
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): inventory documents without local build-output paths`

---

#### T20: Record unsupported-document for non-C# files

**What**: A non-C# inventoried file is still a `Document`. Inventory records diagnostic code
`unsupported-document` and does not enqueue it for a C# extractor.
**Where**: `src/Csharp2Md.Analysis/Inventory/DocumentInventory.cs`
**Depends on**: T19
**Reuses**: `DiagnosticRecord` code table in design.md
**Requirement**: ROSE-05

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A planted `.json` under the project directory becomes a Document plus `unsupported-document`
- [x] `.cs` files do not receive that diagnostic
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): keep non-C# documents without running C# extractors`

---

#### T21: Record missing-project and continue

**What**: A listed project path that does not exist on disk records `missing-project` naming that path, sets
unknowns, and does not abort the solution.
**Where**: `src/Csharp2Md.Analysis/Inventory/InventoryStage.cs`
**Depends on**: T20
**Reuses**: `Acme.DoesNotExist` in `Acme.Orders.slnx`
**Requirement**: ROSE-11

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Analyzing the fixture records `missing-project` for `Acme.DoesNotExist` and still returns success
      from Inventory
- [x] `HasUnknownsOrCandidatesOrFrontiers` is true
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): name missing listed projects and keep analyzing`

---

#### T22: Replace Inventory stub with InventoryStage

**What**: `InventoryStage.Name == "Inventory"`. It computes the root, guards paths, emits Solution / Project
/ Document facts, records declared `TargetFramework` / `TargetFrameworks` from csproj XML onto the context,
and reports a non-zero fact count on the fixture. `PipelineStages.CreateDefault()` uses it. Rewrite
`DefaultPipelineZerosTests` so Inventory is > 0 and later stages stay 0.
**Where**: `src/Csharp2Md.Analysis/Inventory/InventoryStage.cs`
**Depends on**: T21
**Reuses**: `StubStages.DeclaredNames[0]`; `PipelineStages.CreateDefault`
**Requirement**: ROSE-13

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Production default engine on `Acme.Orders.slnx` reports Inventory fact count > 0
- [x] Semantic Analysis through Batch Composition still report 0/0/0
- [x] `SkipUnrecognizedProjects` is not required yet; missing projects stay diagnostics
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): fill the Inventory pipeline stage`

---

#### T23: Abort on symlink escape and name the symlink

**What**: When PathGuard rejects an escape, Inventory sets publication Detail to the symlink path, returns
`AbortPublication`, and does not hash or copy the target. The last valid package for that solution stays
byte-identical.
**Where**: `src/Csharp2Md.Analysis/Inventory/InventoryStage.cs`
**Depends on**: T22
**Reuses**: T5 abort path; in-memory store prior publication
**Requirement**: ROSE-02

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A planted escape symlink unpublished that solution; Detail names the symlink path
- [x] `StructuralCorruption` is false; a prior committed publication's bytes are unchanged
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): unpublish a solution that follows a symlink out of root`

---

#### T24: Print SolutionOutcome.Detail from the CLI

**What**: `WriteDiagnostics` includes `outcome.Detail` on unpublished lines so symlink and MSBuild open
failures name the path on stderr.
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`
**Depends on**: T23
**Reuses**: `tests/Csharp2Md.Cli.Tests/AnalyzeExitCodeTests.cs`
**Requirement**: ROSE-02

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A fake unpublished outcome with Detail `link.cs` writes that string to stderr
- [x] Committed outcomes still print no `csharp2md:` diagnostic line
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean on this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `feat(cli): print unpublished solution detail on stderr`

---

### Phase 5: Semantic workspace

#### T25: Open MSBuildWorkspace per target framework

**What**: `MsBuildWorkspaceFactory.Open` creates `MSBuildWorkspace` with global properties `Configuration`
(default `Debug`) and `TargetFramework`, sets `SkipUnrecognizedProjects = true`, and calls
`OpenSolutionAsync(path, progress: null, cancellationToken)`. One lease per `(configuration, tfm)` pair.
**Where**: `src/Csharp2Md.Analysis/Semantics/MsBuildWorkspaceFactory.cs`
**Depends on**: T24
**Reuses**: Inventory-recorded TFMs; Context7 `/dotnet/roslyn` `MSBuildWorkspace`
**Requirement**: ROSE-24

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-concurrency-patterns`

**Done when**:

- [x] Opening `Acme.Orders.slnx` with `Debug` and the fixture TFM returns a disposable lease
- [x] `Acme.DoesNotExist` does not throw from Open
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): open an MSBuild workspace per analysis variant`

---

#### T26: Strip analyzer and generator assemblies

**What**: `CompilationSanitizer.Strip` returns a `Project` whose analyzer references are empty. Consult
Context7 for the exact `Project` / `Compilation` members; do not invent them.
**Where**: `src/Csharp2Md.Analysis/Semantics/CompilationSanitizer.cs`
**Depends on**: T25
**Reuses**: Context7 `/dotnet/roslyn`; AD-003
**Requirement**: ROSE-29

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] After Strip, `AnalyzerReferences` is empty on the returned project
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): strip analyzer and generator assemblies from compilations`

---

#### T27: Re-establish sanitation probes

**What**: Tests against `CompilationSanitizer` and against a compilation taken from the filled Semantic
Analysis internals assert analyzer references are empty and generator collections are empty. Re-home the
port-ledger probes under Analysis.Tests; do not edit the ledger's former-path row.
**Where**: `tests/Csharp2Md.Analysis.Tests/Semantics/CompilationSanitizerTests.cs`
**Depends on**: T26
**Reuses**: `InternalsVisibleTo` on Analysis; port ledger row "Roslyn sanitation probes"
**Requirement**: ROSE-30

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Probes fail by naming a remaining analyzer or generator if Strip is skipped
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): re-home Roslyn sanitation probes under Analysis.Tests`

---

#### T28: Abort only the failed solution when Open throws

**What**: If `OpenSolutionAsync` throws, Semantic Analysis sets Detail to the failure, returns
`AbortPublication`, and does not set `StructuralCorruption`. Remaining solutions in the request continue.
**Where**: `src/Csharp2Md.Analysis/Semantics/SemanticAnalysisStage.cs`
**Depends on**: T27
**Reuses**: T5 abort path; `MultiSolutionIsolationTests` pattern
**Requirement**: ROSE-22

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A test double whose Open throws unpublished that solution only; a second fixture solution still
      commits
- [x] `WorkspaceDiagnosticKind.Failure` alone does not abort
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): unpublish only the solution whose MSBuild open failed`

---

#### T29: Record unresolvable-sdk and continue

**What**: After a successful Open, workspace diagnostics that name a project SDK failure become
`unresolvable-sdk`. Analysis continues. Do not treat `WorkspaceDiagnosticKind.Failure` as a hard abort.
**Where**: `src/Csharp2Md.Analysis/Semantics/SemanticAnalysisStage.cs`
**Depends on**: T28
**Reuses**: `workspace.Diagnostics`; `Acme.Broken` if its failure is SDK-shaped, otherwise a test double
**Requirement**: ROSE-12

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] An SDK-shaped workspace diagnostic records `unresolvable-sdk` naming the project
- [x] The solution still proceeds to compilation of loadable projects
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): name unresolvable project SDKs without aborting the solution`

---

#### T30: Record compilation-error and extract what still binds

**What**: Compilation diagnostics of error severity record `compilation-error` naming the project, set
unknowns, and do not abort. Later extraction still sees occurrences that bind.
**Where**: `src/Csharp2Md.Analysis/Semantics/SemanticAnalysisStage.cs`
**Depends on**: T29
**Reuses**: `Acme.Broken`; `GetCompilationAsync` after Strip
**Requirement**: ROSE-23

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Analyzing `Acme.Orders.slnx` records `compilation-error` for `Acme.Broken` and still commits
- [x] Loadable projects in that solution still produce a compilation
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): keep bindable observations when a project has compile errors`

---

#### T31: Emit AnalysisVariantId with environment local

**What**: Each compiled TFM × configuration pair becomes `AnalysisVariantId.Create(tfm, configuration,
DefineConstants symbols, "local")`. Configuration is `Debug` when the request does not name one. No new
CLI flag.
**Where**: `src/Csharp2Md.Analysis/Semantics/AnalysisVariantFactory.cs`
**Depends on**: T30
**Reuses**: Domain `AnalysisVariantId`; context.md variant rules
**Requirement**: ROSE-25

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Fixture variants have `environment` `local` and configuration `Debug`
- [x] DefineConstants are sorted and taken from the compilation/project for that variant
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean on this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): identify each compilation as a local analysis variant`

---

### Phase 6: Symbol facts

#### T32: Add BoundSolution lease and SemanticAnalysisStage

**What**: Semantic Analysis puts a disposable `BoundSolution` lease on `PipelineContext` and replaces the
Semantic Analysis stub in `PipelineStages.CreateDefault()`. `AnalysisEngine` has no `Compilation` instance
field. Dispose happens in Observation Extraction (T43); this task only attaches the lease.
**Where**: `src/Csharp2Md.Analysis/Semantics/SemanticAnalysisStage.cs`
**Depends on**: T31
**Reuses**: `MsBuildWorkspaceLease`; `PipelineContext`
**Requirement**: ROSE-31

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] After Semantic Analysis on the fixture, context holds a non-null lease
- [x] Production constructor no longer uses `SemanticAnalysisStub`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): fill Semantic Analysis and lease the bound solution`

---

#### T33: Emit Symbol facts for declared C# symbols

**What**: `SymbolFactEmitter` emits one `Symbol` fact per declared type, method, property, field, event,
constructor, local function, and identifiable lambda. Identity is `(project, CanonicalSymbolSignature)`, so
multi-TFM does not duplicate symbols. Consult Context7 for `ISymbol` members used in the signature.
**Where**: `src/Csharp2Md.Analysis/Semantics/SymbolFactEmitter.cs`
**Depends on**: T32
**Reuses**: Domain `Symbol.Create`, `CanonicalSymbolSignature.Create`
**Requirement**: ROSE-14

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `OrderService.PlaceOrderAsync` is present as a Symbol on the fixture package-in-memory
- [x] Re-emitting the same symbol from a second TFM does not add a second fact identity
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): emit declared C# symbols as structural facts`

---

#### T34: Tag Callable on methods, constructors, local functions and lambdas

**What**: Those four shapes include `SymbolFacet.Callable`. Types, properties, fields and events do not.
**Where**: `src/Csharp2Md.Analysis/Semantics/SymbolFactEmitter.cs`
**Depends on**: T33
**Reuses**: Domain `SymbolFacetSet`
**Requirement**: ROSE-15

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `PlaceOrderAsync` facets contain `Callable`
- [x] `OrdersController` type facets do not contain `Callable`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): mark callable symbols without classifying architecture roles`

---

#### T35: Never tag Controller Handler Repository Client or Service

**What**: Symbol facets in this workstream never include `Controller`, `Handler`, `Repository`, `Client`, or
`Service`, including on `OrdersController` and `OrderRepository`.
**Where**: `src/Csharp2Md.Analysis/Semantics/SymbolFactEmitter.cs`
**Depends on**: T34
**Reuses**: `SymbolFacet` enum; AD-004
**Requirement**: ROSE-16

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] `OrdersController` is not tagged `Controller`
- [x] `OrderRepository` is not tagged `Repository`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): leave architecture symbol facets to later classifiers`

---

#### T36: Keep Roslyn types off the Analysis public surface

**What**: After Semantic internals exist, `AnalysisIsolationTests` still fails if any public member exposes
a `Microsoft.CodeAnalysis` type. Extractor/Stage type names stay off the exported surface.
`DiagnosticRecord` remains the only new allowlisted type from this feature.
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisIsolationTests.cs`
**Depends on**: T35
**Reuses**: `AnalysisPublicSurfaceTests`; `InternalsVisibleTo`
**Requirement**: ROSE-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] `GetExportedTypes` plus public member signatures contain no `Microsoft.CodeAnalysis` type
- [x] `InventoryStage`, `SemanticAnalysisStage`, `ObservationExtractor` are not exported
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): keep Roslyn and stage types off the public Analysis surface`

---

#### T37: Report non-zero Semantic Analysis counts on the fixture

**What**: Default engine on `Acme.Orders.slnx` reports Semantic Analysis fact count > 0. Rewrite
`DefaultPipelineZerosTests` accordingly. Observation Extraction through Batch Composition still 0/0/0.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/DefaultPipelineZerosTests.cs`
**Depends on**: T36
**Reuses**: `StubStages.DeclaredNames`
**Requirement**: ROSE-59

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Inventory and Semantic Analysis counts are each > 0 on the fixture
- [x] Observation Extraction and later stubs remain 0/0/0
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean on this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `test(analysis): expect non-zero Semantic Analysis counts on the fixture`

---

### Phase 7: Observation extraction core

#### T38: Add ObservationDraft and OccurrenceOrdinalAssigner

**What**: Drafts carry owner, kind, payload, locator, method, diagnostic and document hash. After a locator
sort (`relative path`, then span), assign occurrence ordinal 1..n per `(owner, kind, payload)`.
**Where**: `src/Csharp2Md.Analysis/Extraction/OccurrenceOrdinalAssigner.cs`
**Depends on**: T37
**Reuses**: Domain `Observation.Create` (ordinal is required); `EvidenceLocator`
**Requirement**: ROSE-43

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Two drafts that share owner/kind/payload get ordinals 1 and 2 in locator order
- [x] Shuffling the input list does not change assigned ordinals
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): assign observation ordinals after a locator sort`

---

#### T39: Extract always-when-bindable kinds

**What**: One `AlwaysWhenBindableWalker : CSharpSyntaxWalker` emits `Invocation`, `ObjectCreation`,
`TypeUsage`, `BaseType` and `AttributeUsage` for bindable occurrences. Payloads are empty
`NormalizedPayload` (TAX-79 has no method-name role). Owner is the containing callable, or the nearest
inventoried declared symbol.
**Where**: `src/Csharp2Md.Analysis/Extraction/AlwaysWhenBindableWalker.cs`
**Depends on**: T38
**Reuses**: Context7 `CSharpSyntaxWalker`; Domain `ObservationKind`; fixture `OrdersController : ControllerBase`
**Requirement**: ROSE-32

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Fixture yields at least one of each of the five kinds (AttributeUsage may wait on T45's `[HttpGet]`)
- [x] `OrdersController : ControllerBase` is a `BaseType` observation
- [x] Payloads contain only `StructuralLiteral` values (here: none)
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): extract always-when-bindable C# observations`

---

#### T40: Emit unbound always-when-bindable syntax without inventing a target

**What**: When the syntax is present but bind fails, still emit the kind with `EvidenceMethod.Syntactic`, a
binding diagnostic that names the failure, and no invented target fact id. Successful bind uses
`EvidenceMethod.Semantic` and `BindingDiagnostic("bound", "bound")`.
**Where**: `src/Csharp2Md.Analysis/Extraction/AlwaysWhenBindableWalker.cs`
**Depends on**: T39
**Reuses**: Domain `BindingDiagnostic`; ROSE-45
**Requirement**: ROSE-45

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] An unbound invocation observation exists, diagnostic code is not `bound`, payload has no target fact id
- [x] A bound invocation uses `Semantic` and diagnostic `bound`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): keep unbound syntax as observations with a binding diagnostic`

---

#### T41: Attach relative locators, SHA-256 hashes and ExtractorVersion 1

**What**: Every observation carries a `/`-separated relative path, a one-based span, the SHA-256 digest of
that document's file bytes, and `ExtractorVersion(1)`. `DocumentId` is the owning Document fact id, not a
filesystem path.
**Where**: `src/Csharp2Md.Analysis/Extraction/ObservationMaterializer.cs`
**Depends on**: T40
**Reuses**: Domain `EvidenceLocator`, `DocumentHash.Create`, `ExtractorVersion`
**Requirement**: ROSE-50

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Locator path has `/` and no drive prefix; span start line is >= 1
- [x] Hash matches SHA-256 of the file bytes independently of clone path
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): stamp observations with relative locators and document hashes`

---

#### T42: Redact secrets before they enter payloads

**What**: Before any string enters a payload, diagnostic message or fact, run `SecretRedactor`. On match,
omit the value and `AddSuspectedSecret` with document, span, whole-document hash and redacted excerpt.
**Where**: `src/Csharp2Md.Analysis/Extraction/ObservationMaterializer.cs`
**Depends on**: T41
**Reuses**: T13 `SecretRedactor`; accumulator `AddSuspectedSecret`
**Requirement**: ROSE-55

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A constructed occurrence whose literal is `Password=secret` never places `secret` on the observation
- [x] Accumulator contains one `SuspectedSecretEvidence` whose excerpt is masked
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): drop suspected secret values from observation payloads`

---

#### T43: Replace Observation Extraction stub

**What**: `ObservationExtractionStage` walks C# documents only, runs the walker, materializes observations,
disposes the `BoundSolution` lease, and reports observation count > 0 on the fixture. Non-C# documents are
not visited. No Roslyn type is public.
**Where**: `src/Csharp2Md.Analysis/Extraction/ObservationExtractionStage.cs`
**Depends on**: T42
**Reuses**: `PipelineStages.CreateDefault`; T20 unsupported documents
**Requirement**: ROSE-48

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Production default engine reports Observation Extraction count > 0 on `Acme.Orders.slnx`
- [x] A planted `.json` document produces no C# observations
- [x] The bound lease is disposed after the stage (test double records Dispose)
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): fill the Observation Extraction pipeline stage`

---

#### T44: Collapse span-only duplicates and ignore document order

**What**: Two drafts that share owner, kind, payload and assigned ordinal but differ only in span collapse
to one identity. Shuffling document enumeration does not change identities. Multi-TFM keeps `bound` over
`unbound`.
**Where**: `src/Csharp2Md.Analysis/Pipeline/SnapshotAccumulator.cs`
**Depends on**: T43
**Reuses**: T6 union rules; T38 ordinals
**Requirement**: ROSE-44

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Span-only pairs produce one observation identity
- [x] Two document visitation orders produce the same identity set
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean on this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): union observation identities without a span axis`

---

### Phase 8: Fixture, registered-context detectors, contains

#### T45: Add fixture occurrences for AttributeUsage, RouteDeclaration and Configuration

**What**: Add stand-in `[HttpGet("orders/{id}")]` on `GetOrderStatus` and a small `IConfiguration` stand-in
used as `configuration["Logging:Level"]`. Do not add `Controller` facets. `OrderRepository` stays a
negative for DataAccess.
**Where**: `fixtures/SyntheticSolution/Acme.Orders/Api/OrdersController.cs`
**Depends on**: T44
**Reuses**: existing ControllerBase stand-in; design.md fixture extensions
**Requirement**: ROSE-49

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `GetOrderStatus` has `[HttpGet("orders/{id}")]`
- [x] A C# file in the fixture reads `configuration["Logging:Level"]` through an `IConfiguration` stand-in
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes`
- [x] Test count recorded (no silent deletions)

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): add route, attribute and configuration occurrences for the ledger`

---

#### T46: Detect Assignment in DbSet or DbContext entity writes

**What**: `AssignmentDetector` emits `Assignment` only when a write targets a member of a type `T` that is
a `DbSet<T>` entity or a `DbContext`-owned entity in this compilation. `order.Status =` in the fixture is
the positive. Other assignments are not this kind (ROSE-42).
**Where**: `src/Csharp2Md.Analysis/Extraction/AssignmentDetector.cs`
**Depends on**: T45
**Reuses**: `IRegisteredContextDetector`; fixture `OrderService`
**Requirement**: ROSE-37

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `order.Status =` is an `Assignment` observation
- [x] A non-entity property write in the fixture is not an `Assignment`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): observe entity assignments only in compiled persistence context`

---

#### T47: Detect Configuration keys without values

**What**: `ConfigurationDetector` emits `Configuration` for `IConfiguration` indexer / `GetSection` /
`GetValue`, `IOptions<T>`, and `Configure<T>`. Payload is `LiteralRole.ConfigurationKey` only. Bound values
never enter the payload. Secret-shaped values go through T42.
**Where**: `src/Csharp2Md.Analysis/Extraction/ConfigurationDetector.cs`
**Depends on**: T46
**Reuses**: T45 `configuration["Logging:Level"]`; `StructuralLiteral.Create`
**Requirement**: ROSE-38

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Fixture key `Logging:Level` is present; no bound configuration value is present
- [x] A non-configuration indexer is not this kind
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): observe configuration keys and never their bound values`

---

#### T48: Detect RouteDeclaration

**What**: Emit `RouteDeclaration` for `[HttpGet]` `[HttpPost]` `[HttpPut]` `[HttpDelete]` `[HttpPatch]`
`[Route]`, or `MapGet`/`MapPost`/`MapPut`/`MapDelete`. Payload `LiteralRole.Route` when a route literal is
present.
**Where**: `src/Csharp2Md.Analysis/Extraction/RouteDeclarationDetector.cs`
**Depends on**: T47
**Reuses**: T45 `[HttpGet("orders/{id}")]`
**Requirement**: ROSE-39

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Fixture `[HttpGet("orders/{id}")]` is a `RouteDeclaration` whose payload contains `orders/{id}`
- [x] The same attribute is also an `AttributeUsage` from T39
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): observe HTTP route declarations from attributes and Map* calls`

---

#### T49: Detect MessageOperation

**What**: Emit `MessageOperation` for a bound invocation named `Publish`, `PublishAsync`, `Send`,
`SendAsync`, or `Subscribe`. Empty payload. Other method names are not this kind (ROSE-42).
**Where**: `src/Csharp2Md.Analysis/Extraction/MessageOperationDetector.cs`
**Depends on**: T48
**Reuses**: fixture `eventBus.PublishAsync`
**Requirement**: ROSE-40

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `PublishAsync` on the fixture event bus is a `MessageOperation`
- [x] A bound `Find` / `Add` invocation is not a `MessageOperation`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): observe publish, send and subscribe invocations`

---

#### T50: Detect DataAccess

**What**: Emit `DataAccess` for `DbSet<T>` member access; `SaveChanges` / `SaveChangesAsync` / `Add` /
`AddAsync` / `FromSqlRaw` / `FromSqlInterpolated` / `ExecuteSqlRaw` / `ExecuteSqlInterpolated` on a
`DbContext` or `DbSet<T>`; and LINQ whose source binds to a `DbSet<T>`. Empty payload. `OrderRepository`
is a negative. After this task the fixture has at least one positive of each of the ten kinds.
**Where**: `src/Csharp2Md.Analysis/Extraction/DataAccessDetector.cs`
**Depends on**: T49
**Reuses**: fixture `_context.SaveChanges`, `DbSet`, `Add`, LINQ
**Requirement**: ROSE-41

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `_context.SaveChanges` is a `DataAccess` observation
- [x] `OrderRepository` produces no `DataAccess`
- [x] Reading observations by kind from the fixture shows all ten kinds at least once
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): observe DbSet, SaveChanges and EF SQL invocations`

---

#### T51: Emit contains relations only

**What**: After extraction, for each C# document with at least one observation, emit `Project contains
Document` and `Document contains Symbol` for each symbol declared in that document. Classifier
`csharp2md.inventory.contains` version 1, `EvidenceMethod.Syntactic`, empty facets, non-empty
`derived_from`, `AnalysisVariants` the variants that produced them. No `Solution contains Project`. No
other confirmed kind.
**Where**: `src/Csharp2Md.Analysis/Extraction/ContainsRelationEmitter.cs`
**Depends on**: T50
**Reuses**: `ConfirmedRelation.Create`, `ClassifierIdentity.Create`, `FacetBinding.Create(..., [], [])`,
`EvidenceChain.Create`
**Requirement**: ROSE-17

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Fixture package-in-memory has `Project contains Document` and `Document contains Symbol`
- [x] Zero `Solution contains Project`; zero `invokes` / `belongs-to` / other confirmed kinds
- [x] `derived_from` is non-empty and evidence method is `Syntactic`
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command
- [x] Test count recorded (no silent deletions)
- [x] `dotnet-skills:slopwatch` reports clean on this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): emit inventory contains relations from extracted observations`

---

### Phase 9: Persistence and package contract

#### T52: Persistence stages the accumulator

**What**: `PersistenceStage` calls `Session.Stage(accumulator.ToSnapshot())` instead of
`FactualSnapshot.Empty`. StageResult counts may stay 0/0/0; the committed package is non-empty. Rewrite
`PersistenceManifestTests` so the empty-package assertion is replaced by non-zero structural facts and
observations.
**Where**: `src/Csharp2Md.Analysis/Pipeline/PersistenceStage.cs`
**Depends on**: T51
**Reuses**: existing commit/abort engine loop; `IStoreSession.Stage`
**Requirement**: ROSE-58

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] In-memory commit of `Acme.Orders.slnx` contains Solution, Project, Document, Symbol, observations and
      `contains`
- [x] `PersistenceManifestTests` no longer requires zero fact/observation artifact counts
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: solution

**Commit**: `feat(analysis): persist the extracted snapshot instead of an empty package`

---

#### T53: Keep later stub stages at zero counts

**What**: Classification and Promotion, Validation and Coverage, Retrieval Projection, and Batch
Composition still report 0 facts, 0 observations, and 0 relations. Inventory, Semantic Analysis, and
Observation Extraction stay non-zero on the fixture.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/DefaultPipelineZerosTests.cs`
**Depends on**: T52
**Reuses**: `StubStages.DeclaredNames`
**Requirement**: ROSE-60

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] The four later stubs are each 0/0/0 on the fixture default engine
- [x] The three filled stages each have a non-zero count in at least one of the three metrics
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): keep classifier, retrieval and composition stubs at zero`

---

#### T54: Forbid source-projection Markdown catalogs and postings

**What**: The committed package contains no source-projection files, Markdown pages, catalogs, or postings.
Assert against artifact canonical keys after a fixture commit.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/PackageContentsTests.cs`
**Depends on**: T53
**Reuses**: `CommittedPublication.ArtifactsInPublicationOrder`; STOR-46
**Requirement**: ROSE-57

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] No artifact key ends with `.md` or sits under a catalogs/postings/source-projection prefix
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): keep retrieval projections out of the workstream 4 package`

---

#### T55: Keep the in-memory adapter file-free

**What**: Rewrite `NoFilesystemWriteTests` so the filled default engine still hashes the working tree
unchanged when the store is in-memory. Structural facts and observations are present in the publication.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/NoFilesystemWriteTests.cs`
**Depends on**: T54
**Reuses**: existing file-set hash; `InMemoryTransactionalStore`
**Requirement**: ROSE-64

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Before/after working-tree hashes match
- [x] The in-memory publication has non-zero facts and observations
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): filled extraction still writes no files through the in-memory store`

---

#### T56: Keep the CLI option surface and CLI-Domain isolation

**What**: CLI still requires `--output` and at least one `--solution`. `--trust`,
`--include-source-generators`, and `--analysis-timeout` stay absent. `Csharp2Md.Cli` still has no project
reference to Domain.
**Where**: `tests/Csharp2Md.Cli.Tests/AnalyzeOptionSurfaceTests.cs`
**Depends on**: T55
**Reuses**: `CliIsolationTests`; STOR-52
**Requirement**: ROSE-61

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Analyze product options remain exactly `--output` and `--solution`
- [ ] CLI project references still exclude Domain
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command
- [ ] Test count recorded (no silent deletions)
- [ ] `dotnet-skills:slopwatch` reports clean on this phase's changes

**Tests**: unit
**Gate**: build

**Commit**: `test(cli): keep analyze options and Domain isolation unchanged`

---

### Phase 10: Determinism, secrets, abort, traceability

#### T57: Two clones produce equal identities

**What**: Copy the fixture tree to two working directories and analyze both. Every structural fact identity
and every observation identity is equal.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/ClonePathIndependenceTests.cs`
**Depends on**: T56
**Reuses**: `InMemoryTransactionalStore`; Domain identity equality
**Requirement**: ROSE-52

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Sorted fact ids and observation ids from clone A equal clone B
- [ ] Absolute clone paths do not appear in those ids
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): keep identities independent of the clone path`

---

#### T58: Retry produces byte-identical canonical payloads

**What**: Analyze the same solution twice into separate in-memory (or temp filesystem) packages. Every
canonical payload file is byte-identical.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/RetryCanonicalBytesTests.cs`
**Depends on**: T57
**Reuses**: Storage canonical JSON; manifest-last publication order
**Requirement**: ROSE-53

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Pairwise payload bytes match, including `diagnostics.json`
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): keep canonical package bytes stable across retries`

---

#### T59: Shuffled document order does not change identities

**What**: Drive extraction with two document visitation orders (test double or shuffled inventory list).
Structural fact identities and observation identities are unchanged.
**Where**: `tests/Csharp2Md.Analysis.Tests/Extraction/DocumentOrderIndependenceTests.cs`
**Depends on**: T58
**Reuses**: T38 locator sort; T44 union
**Requirement**: ROSE-54

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Identity sets are equal across the two orders
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): keep observation identities independent of document order`

---

#### T60: Canonical payloads contain no absolute path

**What**: After a fixture commit, no canonical payload UTF-8 text contains a drive prefix, `\\`, or the
clone's full path. `IdentityOrKey` values are relative or opaque ids.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/NoAbsolutePathTests.cs`
**Depends on**: T59
**Reuses**: ROSE-56 scanner idea from T1; Storage payload fragments
**Requirement**: ROSE-56

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] A scan of every payload fragment finds no rooted path
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): keep absolute filesystem paths out of canonical payloads`

---

#### T61: Planted secrets omit the value and record a redacted excerpt

**What**: In a temp copy of the fixture, plant `Password=secret` in a C# string that the configuration
context would otherwise capture. Analyze that copy. The value is absent from payload JSON; a redacted
excerpt is present. Do not edit the versioned fixture for this case.
**Where**: `tests/Csharp2Md.Analysis.Tests/Extraction/SuspectedSecretExtractionTests.cs`
**Depends on**: T60
**Reuses**: T13, T42, T47; T3 mapper shape
**Requirement**: ROSE-55

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Canonical JSON from the temp copy does not contain `secret`
- [ ] A `suspected-secret` diagnostic includes a masked excerpt and the document hash of the whole file,
      not a hash of the secret
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): omit planted secret values from the committed ledger`

---

#### T62: Unpublished abort leaves the last valid package byte-identical

**What**: Commit the fixture, then rerun with a symlink escape (or Open failure double) for that same
solution key. The stored publication bytes are unchanged. `StructuralCorruption` is false.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/AbortPreservesPackageTests.cs`
**Depends on**: T61
**Reuses**: T23; `StructuralCorruptionTests` prior-publication pattern; filesystem or in-memory store
**Requirement**: ROSE-63

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Payload bytes after abort equal the first commit
- [ ] Outcome is Unpublished, Detail is set, `StructuralCorruption` is false
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): keep the last valid package when a solution is unpublished`

---

#### T63: Cover every ROSE requirement with a test trait

**What**: Add `RequirementCoverageTests` that every `ROSE-01` through `ROSE-64` is carried by at least one
`[Trait("Requirement", "ROSE-nn")]` in Analysis or CLI tests, and that every carried ROSE trait is
well-formed. Mirror the STOR scanner's decoy for malformed ids.
**Where**: `tests/Csharp2Md.Analysis.Tests/Surface/RequirementCoverageTests.cs`
**Depends on**: T62
**Reuses**: `tests/Csharp2Md.Storage.Tests/Surface/RequirementCoverageTests.cs` scanner
**Requirement**: ROSE-64

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`, `dotnet-test:test-gap-analysis`

**Done when**:

- [ ] Uncovered ROSE id list is empty
- [ ] A decoy `ROSE-9` is flagged as malformed
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)
- [ ] `dotnet-skills:slopwatch` reports clean on this phase's changes

**Tests**: unit
**Gate**: solution

**Commit**: `test(analysis): close ROSE-01 through ROSE-64 trait traceability`

---

## Phase Execution Map

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 7 → Phase 8 → Phase 9 → Phase 10

Phase 1:  T1 → T2 → T3 → T4 → T5 → T6
Phase 2:  T7 → T8 → T9 → T10 → T11 → T12
Phase 3:  T13 → T14 → T15 → T16 → T17 → T18
Phase 4:  T19 → T20 → T21 → T22 → T23 → T24
Phase 5:  T25 → T26 → T27 → T28 → T29 → T30 → T31
Phase 6:  T32 → T33 → T34 → T35 → T36 → T37
Phase 7:  T38 → T39 → T40 → T41 → T42 → T43 → T44
Phase 8:  T45 → T46 → T47 → T48 → T49 → T50 → T51
Phase 9:  T52 → T53 → T54 → T55 → T56
Phase 10: T57 → T58 → T59 → T60 → T61 → T62 → T63
```

Execution is strictly sequential. There is no intra-phase parallelism.

At Execute, pack whole phases into ~7-task batches. This list is 63 tasks in 10 phases of 5–7 tasks, so
each phase is already one batch. Offer sub-agents before T1; do not auto-spawn.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: DiagnosticRecord | 1 type | ✅ Granular |
| T2: FactualSnapshot diagnostics | 1 record extension | ✅ Granular |
| T3: DomainMapper envelope | 1 mapping change | ✅ Granular |
| T6: SnapshotAccumulator | 1 component | ✅ Granular |
| T8: Analysis PackageReference | 1 project file | ✅ Granular |
| T11: PipelineStages factory | 1 wiring type | ✅ Granular |
| T12: Fake-path stub injection | 1 test seam (several files, one contract) | ⚠️ OK if cohesive |
| T22: InventoryStage | 1 stage | ✅ Granular |
| T25: MsBuildWorkspaceFactory | 1 function | ✅ Granular |
| T33: SymbolFactEmitter | 1 emitter | ✅ Granular |
| T39: AlwaysWhenBindableWalker | 1 walker | ✅ Granular |
| T45: Fixture occurrences | 1 fixture edit | ✅ Granular |
| T46–T50: Detectors | 1 detector each | ✅ Granular |
| T51: ContainsRelationEmitter | 1 emitter | ✅ Granular |
| T52: PersistenceStage | 1 stage | ✅ Granular |

T12 edits every fake-path production-constructor call site because that is one seam. Splitting it would
leave a red default engine the moment Inventory is filled. T28–T32 share `SemanticAnalysisStage.cs` by
additive behavior, one concern per task.

---

## Diagram-Definition Cross-Check

Intra-phase `Depends on` is a linear predecessor chain matching each phase fence. Cross-phase: T7 depends on
T6, T13 on T12, T19 on T18, T25 on T24, T32 on T31, T38 on T37, T45 on T44, T52 on T51, T57 on T56.

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | (start) | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | T5 → T6 | ✅ Match |
| T7 | T6 | (cross-phase) | ✅ Match |
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
| T24 | T23 | T23 → T24 | ✅ Match |
| T25 | T24 | (cross-phase) | ✅ Match |
| T26 | T25 | T25 → T26 | ✅ Match |
| T27 | T26 | T26 → T27 | ✅ Match |
| T28 | T27 | T27 → T28 | ✅ Match |
| T29 | T28 | T28 → T29 | ✅ Match |
| T30 | T29 | T29 → T30 | ✅ Match |
| T31 | T30 | T30 → T31 | ✅ Match |
| T32 | T31 | (cross-phase) | ✅ Match |
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
| T52 | T51 | (cross-phase) | ✅ Match |
| T53 | T52 | T52 → T53 | ✅ Match |
| T54 | T53 | T53 → T54 | ✅ Match |
| T55 | T54 | T54 → T55 | ✅ Match |
| T56 | T55 | T55 → T56 | ✅ Match |
| T57 | T56 | (cross-phase) | ✅ Match |
| T58 | T57 | T57 → T58 | ✅ Match |
| T59 | T58 | T58 → T59 | ✅ Match |
| T60 | T59 | T59 → T60 | ✅ Match |
| T61 | T60 | T60 → T61 | ✅ Match |
| T62 | T61 | T61 → T62 | ✅ Match |
| T63 | T62 | T62 → T63 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Snapshot / diagnostics | unit | unit | ✅ OK |
| T2 | Snapshot / diagnostics | unit | unit | ✅ OK |
| T3 | DomainMapper diagnostics envelope | unit | unit | ✅ OK |
| T4 | Snapshot / unpublished abort | unit | unit | ✅ OK |
| T5 | Snapshot / unpublished abort | unit | unit | ✅ OK |
| T6 | Snapshot / accumulator | unit | unit | ✅ OK |
| T7 | Assembly isolation | unit | unit | ✅ OK |
| T8 | Project wiring + isolation | unit | unit | ✅ OK |
| T9 | Assembly isolation | unit | unit | ✅ OK |
| T10 | Assembly isolation | unit | unit | ✅ OK |
| T11 | Filled pipeline wiring | unit | unit | ✅ OK |
| T12 | Filled pipeline / test seam | unit | unit | ✅ OK |
| T13 | Observation extractors (secrets) | unit | unit | ✅ OK |
| T14 | Inventory | unit | unit | ✅ OK |
| T15 | Inventory | unit | unit | ✅ OK |
| T16 | Inventory | unit | unit | ✅ OK |
| T17 | Inventory | unit | unit | ✅ OK |
| T18 | Inventory | unit | unit | ✅ OK |
| T19 | Inventory | unit | unit | ✅ OK |
| T20 | Inventory | unit | unit | ✅ OK |
| T21 | Inventory | unit | unit | ✅ OK |
| T22 | Inventory + filled pipeline | unit | unit | ✅ OK |
| T23 | Inventory | unit | unit | ✅ OK |
| T24 | CLI `analyze` surface | unit | unit | ✅ OK |
| T25 | Semantic Analysis | unit | unit | ✅ OK |
| T26 | Semantic Analysis | unit | unit | ✅ OK |
| T27 | Semantic Analysis | unit | unit | ✅ OK |
| T28 | Semantic Analysis | unit | unit | ✅ OK |
| T29 | Semantic Analysis | unit | unit | ✅ OK |
| T30 | Semantic Analysis | unit | unit | ✅ OK |
| T31 | Semantic Analysis | unit | unit | ✅ OK |
| T32 | Semantic Analysis | unit | unit | ✅ OK |
| T33 | Semantic Analysis | unit | unit | ✅ OK |
| T34 | Semantic Analysis | unit | unit | ✅ OK |
| T35 | Semantic Analysis | unit | unit | ✅ OK |
| T36 | Assembly isolation | unit | unit | ✅ OK |
| T37 | Filled pipeline | unit | unit | ✅ OK |
| T38 | Observation extractors | unit | unit | ✅ OK |
| T39 | Observation extractors | unit | unit | ✅ OK |
| T40 | Observation extractors | unit | unit | ✅ OK |
| T41 | Observation extractors | unit | unit | ✅ OK |
| T42 | Observation extractors | unit | unit | ✅ OK |
| T43 | Observation extractors + pipeline | unit | unit | ✅ OK |
| T44 | Observation extractors | unit | unit | ✅ OK |
| T45 | Fixture stand-in source | none | none | ✅ OK |
| T46 | Observation extractors | unit | unit | ✅ OK |
| T47 | Observation extractors | unit | unit | ✅ OK |
| T48 | Observation extractors | unit | unit | ✅ OK |
| T49 | Observation extractors | unit | unit | ✅ OK |
| T50 | Observation extractors | unit | unit | ✅ OK |
| T51 | Observation extractors | unit | unit | ✅ OK |
| T52 | Filled pipeline / Persistence | unit | unit | ✅ OK |
| T53 | Filled pipeline | unit | unit | ✅ OK |
| T54 | Filled pipeline | unit | unit | ✅ OK |
| T55 | Filled pipeline | unit | unit | ✅ OK |
| T56 | CLI `analyze` surface | unit | unit | ✅ OK |
| T57 | Determinism | unit | unit | ✅ OK |
| T58 | Determinism | unit | unit | ✅ OK |
| T59 | Determinism | unit | unit | ✅ OK |
| T60 | Determinism | unit | unit | ✅ OK |
| T61 | Determinism / secrets | unit | unit | ✅ OK |
| T62 | Filled pipeline | unit | unit | ✅ OK |
| T63 | Requirement traceability | unit | unit | ✅ OK |

T8 sets `Tests: unit` because isolation tests assert the PackageReference; the matrix `none` row is the
csproj XML itself. T45 is fixture source only; ROSE-49 occurrence assertions live in T50.
