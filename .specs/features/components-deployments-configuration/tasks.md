# Components, Deployments and Configuration Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**Standing skip — discrimination sensor**: the user runs Stryker manually; do not run the sensor's fault-injection pass. Every other Verifier step (spec-anchored coverage check, gate check, code-quality check) still runs as documented.

---

**Design**: `.specs/features/components-deployments-configuration/design.md`
**Status**: Approved

---

## Test Coverage Matrix

> Generated from codebase sampling and project guidelines. Guidelines found: [`AGENTS.md`](AGENTS.md), [`CLAUDE.md`](CLAUDE.md) (retrieval-led reasoning, Roslyn API verification, multi-csproj test execution note, LocalCorpus fixture rule). Carried forward from the `persistence-knowledge` matrix, extended with the inventory layer this feature touches.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Fixture source (analysis input) | none | Build gate only; correctness is asserted by the tests that analyze it | `fixtures/SyntheticSolution/**` | build gate only |
| Pure readers / model records (no I/O) | unit | All branches; 1:1 to spec ACs; every listed edge case | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Inventory (document classification) | unit + integration | All branches; classification asserted at the stage boundary | `tests/Csharp2Md.Analysis.Tests/Inventory/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Observation emitters (extraction) | unit + integration | All branches; payload contract asserted at the ledger; fixture-backed | `tests/Csharp2Md.Analysis.Tests/Extraction/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Classification passes (classifier logic) | unit + integration | All branches; 1:1 to spec ACs; fixture-backed assertions | `tests/Csharp2Md.Analysis.Tests/Classification/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Pipeline stage / orchestrator | integration | Stage wiring, count reporting, composability | `tests/Csharp2Md.Analysis.Tests/Pipeline/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Storage mapping (classifier facts) | unit | Classifier-produced facts round-trip correctly | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| CLI surface | unit | No new flags; CLI→Domain isolation; package-level assertions | `tests/Csharp2Md.Cli.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj` |
| End-to-end (fixture analyze) | integration | Full pipeline with component, deployment and configuration assertions | `tests/Csharp2Md.Analysis.Tests/Classification/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |

## Gate Check Commands

> Generated from codebase — confirm before Execute. Multi-csproj `dotnet test` hits MSB1008; run each test project separately. `Category=LocalCorpus` is excluded from every gate; those tests run only after the Verifier and only when the local clones exist.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After unit-test-only tasks scoped to Analysis | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Full | After extraction / classifier / pipeline tasks | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Build | After fixture changes, phase completion, and every cross-assembly task | `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |

**Measured baseline**: 1211 tests, **1208 passing and 3 failing** — Domain 549/549, Analysis 460/461, Storage 170/171, Cli 26/27, Projection 3/3.

The three failures are pre-existing fallout from the 5B/5C merge and are unrelated to this feature. Phase 0 repairs them so that every later task has a green gate to pass; from T3 onward the expected total is **1211 passing**. Every task reports its new total; a drop means a silent deletion.

---

## Execution Plan

Phases are ordered and run sequentially — each phase completes before the next begins, and tasks within a phase execute in order.

### Phase 0: Baseline repair

Three pre-existing failures block every gate in this feature. They land first, alone, so the starting line is green and measured.

```
T1 → T2 → T3
```

### Phase 1: Fixture applications

`Acme.Orders` and `Acme.Payments` become application projects. Each `Main` lands before the `OutputType` flip that requires it, so no intermediate commit leaves the fixture uncompilable.

```
T4 → T5 → T6 → T7
```

### Phase 2: Worker project and configuration file

The second deployable, which is what makes `Acme.Shared.Contracts` shared in one solution and private in the other.

```
T8 → T9 → T10 → T11
```

### Phase 3: Inventory and pipeline plumbing

`appsettings*.json` stops being an unsupported document and reaches the pipeline.

```
T12 → T13 → T14
```

### Phase 4: Extraction emitters

Both evidence sources enter the immutable ledger.

```
T15 → T16 → T17
```

### Phase 5: Topology model, builder and emitter

The grouping rule, built from the ledger alone.

```
T18 → T19 → T20 → T21 → T22
```

### Phase 6: Component lookup migration

Every pass that resolves a symbol's component switches off the path lookup the new rule invalidates.

```
T23 → T24 → T25
```

### Phase 7: Configuration model and builder

Declared keys correlated with the facts that consume them.

```
T26 → T27 → T28 → T29
```

### Phase 8: Configuration emitter and pass

Bindings, `configured-by`, and the `targets` promotion.

```
T30 → T31 → T32 → T33
```

### Phase 9: Registration, coverage and end-to-end

The pass joins the default pipeline and the fixture ground truth is asserted end to end.

```
T34 → T35 → T36 → T37 → T38
```

### Phase 10: Invariants and reconciliation

Determinism, security, storage round-trip, and the full-suite reconciliation.

```
T39 → T40 → T41 → T42
```

---

## Task Breakdown

### T1: Allow foreign workstream prefixes in the Analysis requirement guard

**What**: Add `CLLF-` and `CDC-` to the foreign-prefix allowlist so 5B's and 5D's requirement traits stop being reported as malformed ROSE IDs.
**Where**: `tests/Csharp2Md.Analysis.Tests/Surface/RequirementCoverageTests.cs`
**Depends on**: None
**Reuses**: The existing `ENG-` / `STOR-` / `EBC-` / `PK-` allowlist at line 146
**Requirement**: none — pre-existing baseline repair

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:test-anti-patterns`

**Done when**:

- [x] `CLLF-` and `CDC-` join the allowlist; the ROSE format and range checks are otherwise untouched
- [x] `FormatScanner_FlagsATraitNamingAnIdOutsideTheValidFormat_ProvingItIsNotVacuous` still fails its decoy, proving the guard is not weakened into vacuity
- [x] `EveryCarriedRequirementTrait_IsAWellFormedInRangeRoseId` passes
- [x] Gate check passes: quick gate command
- [x] Test count: Analysis 461 pass (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `fix(tests): allow CLLF and CDC traits in the Analysis requirement guard`

---

### T2: Allow foreign workstream prefixes in the Storage requirement guard

**What**: Add `CLLF-` and `CDC-` to the same allowlist in the Storage guard.
**Where**: `tests/Csharp2Md.Storage.Tests/Surface/RequirementCoverageTests.cs`
**Depends on**: T1
**Reuses**: The existing `ENG-` / `ROSE-` / `EBC-` / `PK-` allowlist at line 144
**Requirement**: none — pre-existing baseline repair

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:test-anti-patterns`

**Done when**:

- [x] `CLLF-` and `CDC-` join the allowlist; the STOR format and range checks are otherwise untouched
- [x] The decoy test still flags `STOR-9`
- [x] `EveryCarriedRequirementTrait_IsAWellFormedInRangeStorId` passes
- [x] Gate check passes: build gate command
- [x] Test count: Storage 171 pass (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `fix(tests): allow CLLF and CDC traits in the Storage requirement guard`

---

### T3: Correct the CLI package assertion for post-5B open frontiers

**What**: Replace the stale `Frontiers.IsEmpty` assertion with the post-5B ground truth — the package carries open frontiers from `InvokesPass` — asserting what they are rather than that there are none.
**Where**: `tests/Csharp2Md.Cli.Tests/AnalyzePackageWriteTests.cs`
**Depends on**: T2
**Reuses**: The surrounding `FactualPackageReader` assertions in the same test
**Requirement**: none — pre-existing baseline repair

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Line 68's `Assert.True(result.Snapshot.Frontiers.IsEmpty)` is replaced by a positive assertion naming the frontier kind and cause 5B emits
- [x] The `targets` candidate and no-confirmed-`targets` assertions are left unchanged; T32 revises them when the promotion lands
- [x] The whole suite is green: 1211 passing
- [x] Gate check passes: build gate command
- [x] Test count: 1211 pass — Domain 549, Analysis 461, Storage 171, Cli 27, Projection 3 (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `fix(tests): assert post-5B open frontiers in the CLI package test`

---

### T4: Add an entry point to the Acme.Orders composition root

**What**: Add `public static void Main(string[] args)` to `Program`, delegating to the existing `ConfigureHost`, so the project can become an application without changing its observable surface.
**Where**: `fixtures/SyntheticSolution/Acme.Orders/Program.cs`
**Depends on**: T3
**Reuses**: The existing `ConfigureHost(string[], OrderDbContext?)` in the same file
**Requirement**: CDC-01, CDC-09, CDC-19

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `Main` delegates to `ConfigureHost` and adds no new configuration key, client name or persistence call
- [x] The file's header comment describes the current contract, including why the entry point exists
- [x] The project still compiles as a library at this point
- [x] Gate check passes: build gate command
- [x] Test count: 1211 pass; any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): add an entry point to the Acme.Orders composition root`

---

### T5: Make Acme.Orders an application project

**What**: Set `<OutputType>Exe</OutputType>` so the project's Roslyn `OutputKind` becomes `ConsoleApplication`.
**Where**: `fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.csproj`
**Depends on**: T4
**Reuses**: The existing `PropertyGroup` in the same file
**Requirement**: CDC-01, CDC-09, CDC-19

**Tools**:

- MCP: NONE
- Skill: `dotnet-msbuild:property-patterns`

**Done when**:

- [x] `OutputType` is `Exe`; `TargetFramework`, `ImplicitUsings`, `Nullable`, the project reference and the package reference are unchanged
- [x] The fixture solution loads through `MsBuildWorkspaceFactory` without a new workspace diagnostic
- [x] Gate check passes: build gate command
- [x] Test count: 1211 pass; any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): make Acme.Orders an application project`

---

### T6: Add an entry point to Acme.Payments

**What**: Add a `Program` type with `Main` that exercises the existing `PaymentsService` surface, so Payments can become an application.
**Where**: `fixtures/SyntheticSolution/Acme.Payments/Program.cs`
**Depends on**: T5
**Reuses**: The existing `PaymentsService` in the sibling file; the `Program.Main` shape from T4
**Requirement**: CDC-01, CDC-09, CDC-19

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `Main` adds no new gRPC surface, contract or boundary operation that would shift 5A assertions
- [x] A header comment states that the file exists to make the project an application for the 5D grouping rule
- [x] Gate check passes: build gate command
- [x] Test count: 1211 pass; any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): add an entry point to Acme.Payments`

---

### T7: Make Acme.Payments an application project

**What**: Set `<OutputType>Exe</OutputType>` so `Acme.Shared.Contracts` becomes privately used inside `Acme.Payments.slnx`.
**Where**: `fixtures/SyntheticSolution/Acme.Payments/Acme.Payments.csproj`
**Depends on**: T6
**Reuses**: The existing `PropertyGroup` in the same file
**Requirement**: CDC-01, CDC-10, CDC-19

**Tools**:

- MCP: NONE
- Skill: `dotnet-msbuild:property-patterns`

**Done when**:

- [x] `OutputType` is `Exe`; the `Protobuf` item, project reference and package reference are unchanged
- [x] `Acme.Payments.slnx` still loads and its gRPC assertions still hold
- [x] Gate check passes: build gate command
- [x] Test count: 1211 pass; any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): make Acme.Payments an application project`

---

### T8: Add the worker source for the second deployable

**What**: Create a worker type with `Main` that consumes `IEventBus` from `Acme.Shared.Contracts`, so its symbols carry real observations. The file is inert until T9 adds a project that compiles it.
**Where**: `fixtures/SyntheticSolution/Acme.Orders.Worker/OrderPlacedWorker.cs`
**Depends on**: T7
**Reuses**: `Acme.Shared.Contracts`'s `IEventBus` and event types
**Requirement**: CDC-09, CDC-11, CDC-19

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] The worker consumes `IEventBus` so at least one symbol in the project owns an observation
- [x] It declares no HTTP client, no `DbContext` and no route, so it adds no boundary, contract or persistence facts
- [x] A header comment states the file's purpose in the 5D grouping ground truth
- [x] Gate check passes: build gate command
- [x] Test count: 1211 pass; any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): add the Acme.Orders.Worker source`

---

### T9: Add the worker project file

**What**: Create the worker's `csproj` as an application referencing only `Acme.Shared.Contracts`.
**Where**: `fixtures/SyntheticSolution/Acme.Orders.Worker/Acme.Orders.Worker.csproj`
**Depends on**: T8
**Reuses**: The `Acme.Orders.csproj` shape — same `TargetFramework`, `ImplicitUsings` and `Nullable`
**Requirement**: CDC-02, CDC-11, CDC-19

**Tools**:

- MCP: NONE
- Skill: `dotnet-msbuild:property-patterns`

**Done when**:

- [x] `OutputType` is `Exe` and `TargetFramework` is `net10.0`, matching every sibling fixture project
- [x] The only `ProjectReference` is `Acme.Shared.Contracts`; there is no reference to `Acme.Orders`
- [x] The project compiles against the `Main` added in T8
- [x] Gate check passes: build gate command
- [x] Test count: 1211 pass; any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): add the Acme.Orders.Worker project`

---

### T10: Register the worker in the Orders solution

**What**: Add the worker project to `Acme.Orders.slnx`, which is what makes `Acme.Shared.Contracts` a shared component there.
**Where**: `fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx`
**Depends on**: T9
**Reuses**: The existing relative-path `Project` entries in the same file
**Requirement**: CDC-11, CDC-21

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] The worker is listed by relative path alongside the existing four entries
- [x] `Acme.Broken` and `Acme.DoesNotExist` entries are unchanged, so the compile-failure and missing-project paths still have coverage
- [x] The solution loads and the existing suite still passes
- [x] Gate check passes: build gate command
- [x] Test count: 1211 pass; any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): register Acme.Orders.Worker in the Orders solution`

---

### T11: Add a second configuration file to the fixture

**What**: Add `appsettings.Development.json` declaring one key already present in `appsettings.json` plus one new key, covering the `appsettings*.json` glob and the no-merge rule.
**Where**: `fixtures/SyntheticSolution/Acme.Orders/appsettings.Development.json`
**Depends on**: T10
**Reuses**: The `Services` and `ConnectionStrings` shape of the sibling `appsettings.json`
**Requirement**: CDC-25, CDC-26

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] One key path is identical to a key in `appsettings.json` and one is new
- [x] No credential, password, token or connection string appears in the new file; the existing `appsettings.json` password stays the single secret probe
- [x] Gate check passes: build gate command
- [x] Test count: 1211 pass; any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): add a second configuration file`

---

### T12: Classify appsettings files as configuration documents

**What**: Add a `ConfigurationDocuments` array to `InventoriedDocuments` and route `appsettings*.json` into it instead of emitting an `unsupported-document` diagnostic.
**Where**: `src/Csharp2Md.Analysis/Inventory/DocumentInventory.cs`
**Depends on**: T11
**Reuses**: The existing enumeration, exclusion and `ToRelativeDocumentPath` logic unchanged
**Requirement**: CDC-25

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Matching is on the file name with ordinal-ignore-case, so `appsettings.json` and `appsettings.Development.json` both match and `myappsettings.json` does not
- [x] A matched document appears in `Documents` and `ConfigurationDocuments` and produces no `unsupported-document` diagnostic
- [x] Every other non-C# document, including the `.csproj`, keeps today's diagnostic
- [x] Unit tests cover matched, near-miss and unmatched names, and assert the diagnostic is absent only for matches
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): classify appsettings files as configuration documents`

---

### T13: Add the authorized root and configuration documents to the pipeline context

**What**: Add `AuthorizedRoot` and `ConfigurationDocuments` slots alongside the existing `CSharpDocuments` slot.
**Where**: `src/Csharp2Md.Analysis/Pipeline/PipelineContext.cs`
**Depends on**: T12
**Reuses**: The existing settable-slot pattern (`DeclaredTargetFrameworks`, `CSharpDocuments`)
**Requirement**: CDC-25, CDC-34

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Both slots default to an empty/unset value so every existing construction path still compiles
- [x] No stage reads either slot yet, so behaviour is unchanged
- [x] Unit tests assert the defaults and that a set value round-trips
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add authorized root and configuration documents to the pipeline context`

---

### T14: Fill the new pipeline slots from the inventory stage

**What**: Populate `AuthorizedRoot` and `ConfigurationDocuments` from the values `InventoryStage` already computes.
**Where**: `src/Csharp2Md.Analysis/Inventory/InventoryStage.cs`
**Depends on**: T13
**Reuses**: The `root` local already computed by `AuthorizedRoot.Compute`; the per-project `inventoried` result
**Requirement**: CDC-25, CDC-34

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `AuthorizedRoot` holds the same root the path guard uses, so no second derivation exists
- [x] `ConfigurationDocuments` accumulates across every analyzed project, ordered by relative path
- [x] The stage's fact count, diagnostics and abort behaviour are unchanged
- [x] Integration tests assert both fixture `appsettings*.json` files reach the context and that no `unsupported-document` diagnostic names them
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: integration
**Gate**: full

**Commit**: `feat(analysis): publish the authorized root and configuration documents`

---

### T15: Emit project metadata observations

**What**: A new emitter that writes each compiled project's output kind and in-solution project references into the ledger as `Configuration` observations owned by `Project` facts.
**Where**: `src/Csharp2Md.Analysis/Extraction/ProjectMetadataEmitter.cs`
**Depends on**: T14
**Reuses**: `BoundSolution.Leases`, `ObservationDraft`, `ObservationMaterializer.HashFileBytes`, `SnapshotAccumulator`
**Requirement**: CDC-01, CDC-02, CDC-03, CDC-04, CDC-05, CDC-06, CDC-07

**Tools**:

- MCP: `context7` (verify `Project.CompilationOptions.OutputKind` and `Project.ProjectReferences` against current Roslyn docs before writing either call)
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `output-kind` is `application` for `ConsoleApplication` and `WindowsApplication`, `library` otherwise
- [x] Each in-solution `ProjectReference` yields one `project-reference` observation holding the referenced logical path
- [x] A reference to a project not analyzed in this solution yields an `unanalyzed-project-reference` diagnostic and no observation
- [x] A project with no compilation yields no observation and leaves existing diagnostics untouched
- [x] Ordinals are assigned across the project's whole metadata set — `output-kind` takes 1, references take 2..N over ordinal-sorted paths — so no two observations share an `owner:kind:ordinal` key
- [x] Every observation carries `EvidenceMethod.Configured` and locates to the project's own `.csproj` document
- [x] Analyzing under two target frameworks yields the same observation set as one
- [x] Unit tests cover each branch above, including the ordinal-collision guard
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): emit project metadata observations`

---

### T16: Read appsettings documents into the ledger

**What**: A new reader that turns each configuration document into one key-path `Configuration` observation per leaf, carrying no value.
**Where**: `src/Csharp2Md.Analysis/Extraction/ConfigurationDocumentReader.cs`
**Depends on**: T15
**Reuses**: `ObservationMaterializer.Redact` / `HashFileBytes`, `SecretRedactor`, `PathGuard.RejectEscapes`, `ObservationDraft`
**Requirement**: CDC-26, CDC-27, CDC-28, CDC-29, CDC-30, CDC-31, CDC-32, CDC-33, CDC-34

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:serialization`

**Done when**:

- [x] Key paths are colon-joined; array elements use their index as a segment
- [x] `resolution` is `dynamic` for `${VAR}`, `$VAR` and `%VAR%`, `literal` for any other non-empty value, `unknown` for empty or null
- [x] `address` is present only when the value is a well-formed absolute URI and is not a suspected secret
- [x] A secret-bearing value produces `SuspectedSecretEvidence` with a redacted excerpt and never reaches a payload entry
- [x] Malformed JSON produces a `malformed-configuration-document` diagnostic, no observations, and no abort
- [x] Ordinals are 1..N over ordinal-sorted key paths, so no two observations share an `owner:kind:ordinal` key
- [x] A path that escapes the authorized root is rejected through the existing guard
- [x] Unit tests cover each branch above against hand-written JSON, including the fixture's three `Services` shapes
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): read appsettings documents into the ledger`

---

### T17: Register both emitters in the extraction stage

**What**: Call `ProjectMetadataEmitter` and `ConfigurationDocumentReader` from the extraction stage and include their output in the stage's observation count.
**Where**: `src/Csharp2Md.Analysis/Extraction/ObservationExtractionStage.cs`
**Depends on**: T16
**Reuses**: The existing `AlwaysWhenBindableWalker` / `ContainsRelationEmitter` call shape and the `BoundSolution` disposal in `finally`
**Requirement**: CDC-01, CDC-26, CDC-32

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Both emitters run before the stage disposes `BoundSolution`
- [x] The stage's reported observation count includes both sources
- [x] The early return when `BoundSolution` is null still emits nothing and throws nothing
- [x] Integration tests analyze both fixture solutions and assert the project-metadata and configuration observations reach the accumulator with the expected owners, payloads and evidence methods
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: integration
**Gate**: full

**Commit**: `feat(analysis): register the project metadata and configuration emitters`

---

### T18: Define the topology model records

**What**: The Roslyn-free record model the grouping builder produces and the emitter consumes.
**Where**: `src/Csharp2Md.Analysis/Classification/Topology/TopologyModel.cs`
**Depends on**: T17
**Reuses**: The `PersistenceModel` record-model pattern
**Requirement**: CDC-09, CDC-10, CDC-11, CDC-12, CDC-19, CDC-20

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] `TopologyModel`, `ComponentGroup`, `GroupingEvidence`, `DeploymentNode`, `InclusionEdge`, `UnreachedComponent` and `TopologyCoverage` match the design's data model
- [x] No record references Roslyn, constructs a Domain fact or touches the filesystem
- [x] XML doc comments state what each record means and which requirement it serves
- [x] Unit tests assert record equality and default-construction guards
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): define the topology model records`

---

### T19: Build the project graph and application reach

**What**: The builder's first half — partition projects into applications and libraries from `output-kind`, build the reference graph from `project-reference`, and compute each project's reaching-application set transitively.
**Where**: `src/Csharp2Md.Analysis/Classification/Topology/TopologyModelBuilder.cs`
**Depends on**: T18
**Reuses**: `ClassifierContext.ObservationsByKind`, `PayloadReader`
**Requirement**: CDC-09, CDC-13, CDC-18

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Only `Configuration` observations owned by a `Project` fact are read; symbol-owned and document-owned ones are ignored
- [x] Reach is transitive: an application reaching A which references B reaches B
- [x] A reference cycle terminates without infinite recursion
- [x] An empty project-metadata set yields an empty model rather than throwing
- [x] Unit tests cover a chain, a diamond, a cycle, an isolated library and an empty ledger against a hand-built ledger
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): compute project graph and application reach`

---

### T20: Apply the grouping rule and derive deployments

**What**: The builder's second half — assign each project a component grouping, derive deployment units and inclusion edges, and record components no application reaches.
**Where**: `src/Csharp2Md.Analysis/Classification/Topology/TopologyModelBuilder.cs`
**Depends on**: T19
**Reuses**: The reach map from T19
**Requirement**: CDC-10, CDC-11, CDC-12, CDC-19, CDC-20, CDC-21, CDC-22, CDC-23, CDC-24

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] An application groups into its own component named by its logical path
- [x] A library reached by exactly one application groups into that application's component and produces no component of its own
- [x] A library reached by two or more applications gets its own component named by its own path
- [x] A library reached by none gets its own component and an unreached record, with no deployment unit
- [x] Deployment units are derived only from applications; a solution with no application produces none
- [x] Every emitted collection is ordinal-sorted, so observation order cannot change the model
- [x] Unit tests cover all four grouping states, the no-application solution, and order independence
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): apply the component grouping rule`

---

### T21: Emit components, deployment units and their relations

**What**: The emitter walk — `Component` and `DeploymentUnit` facts, confirmed `belongs-to` and `included-in` relations, and unresolved inclusion records.
**Where**: `src/Csharp2Md.Analysis/Classification/Topology/TopologyEmitter.cs`
**Depends on**: T20
**Reuses**: `Component.Create`, `DeploymentUnit.Create`, `ConfirmedRelation.Create` with `sourceFact`/`targetFact` per AD-015, `EvidenceChain.Create`, `UnresolvedRecord.Create`
**Requirement**: CDC-14, CDC-15, CDC-16, CDC-17, CDC-20, CDC-21, CDC-22

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `Component.Owners` holds exactly the symbols with at least one observation, ordinal-sorted by fact id
- [x] A symbol owning no observation appears in no `Owners` and produces no `belongs-to`
- [x] Each symbol is the source of at most one `belongs-to`, carrying `EvidenceMethod.Semantic` and that symbol's own observations
- [x] `included-in` carries `EvidenceMethod.Configured` and the `output-kind` plus `project-reference` observations that prove the reach
- [x] A shared component emits one `included-in` per reaching application
- [x] An unreached component emits an `UnresolvedRecord` and no confirmed relation
- [x] Unit tests assert emitted identities, relation counts and evidence chains for a hand-built model
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): emit components, deployment units and inclusion relations`

---

### T22: Rewrite ComponentPass onto the topology builder

**What**: Replace the per-project label rule with a call into the builder and emitter, keeping the pass name, position and `IClassifierPass` contract.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/ComponentPass.cs`
**Depends on**: T21
**Reuses**: `TopologyModelBuilder`, `TopologyEmitter`; the `PersistencePass` pass-shape
**Requirement**: CDC-18, CDC-55

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`

**Done when**:

- [x] The pass name stays `"Components"` and the classifier identity is `csharp2md.classifier.component-topology` version 1
- [x] The four candidate observation kinds and the local `TryLogicalPath` helper are gone, replaced by the shared helper
- [x] `ClassifierPassResult` reports fact, relation and unresolved counts from the emitter
- [x] An empty ledger produces a zero result without throwing
- [x] `ComponentPassTests` is rewritten against the new rule — deployable, private-use, shared, unreached — rather than loosened
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): rewrite ComponentPass onto the topology builder`

---

### T23: Add a symbol-to-component lookup on the classifier context

**What**: Build a `Symbol → Component` map during `Refresh` from `Component.Owners` and expose it as `ComponentForSymbol`.
**Where**: `src/Csharp2Md.Analysis/Classification/ClassifierContext.cs`
**Depends on**: T22
**Reuses**: The existing `Refresh` lookup-building pattern (`_observationsByOwner`, `_symbolsBySignature`)
**Requirement**: CDC-14

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] The map is rebuilt on every `Refresh`, so a pass sees components emitted by earlier passes
- [x] A symbol in no component returns null rather than throwing
- [x] Unit tests cover a symbol in a deployable component, one in a shared component, one in a private-use grouping, and one in none
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions (Analysis 532 pass)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add a symbol-to-component lookup`

---

### T24: Migrate EntryPointPass to the symbol lookup

**What**: Replace the `symbol → project → logical path → component` lookup with `ComponentForSymbol`.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/EntryPointPass.cs`
**Depends on**: T23
**Reuses**: `ClassifierContext.ComponentForSymbol`
**Requirement**: CDC-14

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`

**Done when**:

- [x] The `componentsByPath` dictionary and the local `TryLogicalPath` helper are removed
- [x] Entry points declared in a privately-used library still resolve their component
- [x] Every existing `EntryPointPassTests` assertion still holds, with a new test covering the private-use case
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions (Domain 549 + Analysis 533 = 1082 pass)

**Tests**: unit
**Gate**: full

**Commit**: `refactor(analysis): resolve entry-point components by symbol`

---

### T25: Migrate BoundaryPass to the symbol lookup

**What**: Replace both `componentsByPath` lookups with `ComponentForSymbol`, leaving the two id-based lookups alone.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/BoundaryPass.cs`
**Depends on**: T24
**Reuses**: `ClassifierContext.ComponentForSymbol`
**Requirement**: CDC-14

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`

**Done when**:

- [x] Both path lookups and the local `TryLogicalPath` helper are removed; the `componentsById` lookups at lines 63 and 300 are unchanged
- [x] An outbound HTTP operation and a messaging operation declared in a privately-used library both still resolve their component
- [x] Every existing `BoundaryPassTests` and `BoundaryIntegrationTests` assertion still holds, with a new test covering the private-use case
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions (Domain 549 + Analysis 535 = 1084 pass)

**Tests**: unit
**Gate**: full

**Commit**: `refactor(analysis): resolve boundary components by symbol`

---

### T26: Define the configuration model records

**What**: The Roslyn-free record model the configuration builder produces.
**Where**: `src/Csharp2Md.Analysis/Classification/Configuration/ConfigurationModel.cs`
**Depends on**: T25
**Reuses**: The `TopologyModel` record-model shape from T18
**Requirement**: CDC-35, CDC-43

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] `ConfigurationModel`, `DeclaredKey`, `KeyResolution`, `ConfiguredEdge`, `TargetDecision`, `TargetOutcome`, `UnboundKeyRead` and `ConfigurationCoverage` match the design's data model
- [x] No record references Roslyn, constructs a Domain fact or touches the filesystem
- [x] Unit tests assert record equality and default-construction guards
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions (Analysis 544 pass)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): define the configuration model records`

---

### T27: Build the declared key table

**What**: The builder's first step — read every document-owned `Configuration` observation into a key table and resolve each declaring document to its owning component.
**Where**: `src/Csharp2Md.Analysis/Classification/Configuration/ConfigurationModelBuilder.cs`
**Depends on**: T26
**Reuses**: `ClassifierContext.ObservationsByKind`, `ComponentForSymbol`'s sibling component lookup, `PayloadReader`
**Requirement**: CDC-35

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Only observations owned by a `Document` fact are read; project-owned and symbol-owned ones are ignored
- [x] Two documents declaring the same key produce two entries and one binding identity, with no merge or override
- [x] A document whose project maps to no component is skipped with a diagnostic rather than binding to nothing
- [x] Unit tests cover the single-file, two-file and no-component cases against a hand-built ledger
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions (Analysis 548 pass)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): build the declared configuration key table`

---

### T28: Match configuration keys to the facts that consume them

**What**: The builder's second step — the four `configured-by` correlations plus unbound key reads.
**Where**: `src/Csharp2Md.Analysis/Classification/Configuration/ConfigurationModelBuilder.cs`
**Depends on**: T27
**Reuses**: The key table from T27; `ClassifierContext.FactsByType<DataStore>` and `<BoundaryOperation>`
**Requirement**: CDC-36, CDC-37, CDC-38, CDC-39, CDC-40, CDC-41, CDC-42

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Component, symbol, data-store and boundary-operation edges are produced for the four registered triples
- [x] Matching is exact ordinal; a prefix, suffix or case-differing name produces no edge
- [x] A symbol-read key declared nowhere produces an unbound read, not an edge
- [x] A data store whose name matches no `ConnectionStrings` key produces no edge and no unresolved record
- [x] Unit tests cover each triple, each refusal shape and the unbound read against a hand-built ledger
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions (Analysis 552 pass)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): correlate configuration keys with consuming facts`

---

### T29: Decide the outcome of each candidate targets link

**What**: The builder's third step — classify every 5A `targets` candidate as promote, frontier or leave, based on the matching key's resolution.
**Where**: `src/Csharp2Md.Analysis/Classification/Configuration/ConfigurationModelBuilder.cs`
**Depends on**: T28
**Reuses**: The key table from T27; the snapshot's `Candidates`
**Requirement**: CDC-43, CDC-44, CDC-45, CDC-46, CDC-47, CDC-48

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A `literal` key with an address yields `Promote` with an evidence chain holding both the C# and the configuration observation
- [x] A `dynamic` key yields `Frontier`
- [x] No matching key yields `Leave` with no frontier
- [x] A `literal` key without an address yields `Leave`, not `Promote`
- [x] Matching is exact ordinal against the key's last segment; a prefix or case variant yields `Leave`
- [x] No decision creates an `ExternalSystem`
- [x] Unit tests cover all five outcomes above against a hand-built ledger
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions (Analysis 557 pass)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): decide targets promotion from configuration`

---

### T30: Allow a promoted candidate to be removed from the accumulator

**What**: Add `RemoveCandidate` so a candidate superseded by a confirmed relation leaves the package.
**Where**: `src/Csharp2Md.Analysis/Pipeline/SnapshotAccumulator.cs`
**Depends on**: T29
**Reuses**: The existing `_candidates` list and `AddCandidate` shape
**Requirement**: CDC-43

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `RemoveCandidate` removes every structurally equal candidate and reports whether it removed any
- [x] Removing a candidate that was never added is a no-op returning false, not a throw
- [x] Removal does not set `StructuralCorruption` and does not touch facts, relations, frontiers or unresolved records
- [x] Unit tests cover removal, absent removal and the untouched-neighbours case
- [x] Gate check passes: quick gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): allow a superseded candidate to be removed`

---

### T31: Emit configuration bindings and configured-by relations

**What**: The emitter's first walk — `ConfigurationBinding` facts and the four confirmed `configured-by` relations, plus unresolved records for unbound reads.
**Where**: `src/Csharp2Md.Analysis/Classification/Configuration/ConfigurationEmitter.cs`
**Depends on**: T30
**Reuses**: `ConfigurationBinding.Create`, `ConfirmedRelation.Create`, `UnresolvedRecord.Create`, `StructuralLiteral.Create` with `LiteralRole.ConfigurationKey`
**Requirement**: CDC-35, CDC-36, CDC-37, CDC-38, CDC-39, CDC-40, CDC-42

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] One binding exists per (component, key) pair, deduplicating through `AddFact` without flagging corruption
- [x] Every `configured-by` relation carries `EvidenceMethod.Configured` and the declaring observation in its evidence chain
- [x] Facts and relations are emitted in one ordinal-sorted walk
- [x] Unit tests assert emitted identities, relation counts and evidence chains for a hand-built model
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): emit configuration bindings and configured-by relations`

---

### T32: Emit the targets promotion, frontier and candidate removal

**What**: The emitter's second walk — confirmed `targets` relations, open frontiers, and removal of promoted candidates.
**Where**: `src/Csharp2Md.Analysis/Classification/Configuration/ConfigurationEmitter.cs`
**Depends on**: T31
**Reuses**: `ConfirmedRelation.Create`, `OpenFrontier.Create`, `SnapshotAccumulator.RemoveCandidate`
**Requirement**: CDC-43, CDC-44, CDC-45, CDC-46, CDC-47

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A promoted candidate produces a confirmed `targets` relation with `EvidenceMethod.Configured` and is removed in the same walk
- [x] A frontier decision keeps the candidate and adds an `OpenFrontier` on the originating occurrence
- [x] A leave decision changes nothing
- [x] No `ExternalSystem` fact is created by this walk
- [x] `AnalyzePackageWriteTests`' `targets` assertions are revised to the new ground truth — a confirmed relation now exists — rather than loosened
- [x] Unit tests assert all three outcomes and the candidate-removal side effect
- [x] Gate check passes: build gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): promote targets relations proven by configuration`

---

### T33: Add the configuration classifier pass

**What**: The pass that runs the configuration builder and emitter.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/ConfigurationPass.cs`
**Depends on**: T32
**Reuses**: `ConfigurationModelBuilder`, `ConfigurationEmitter`; the `PersistencePass` pass-shape
**Requirement**: CDC-55

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`

**Done when**:

- [x] The pass name is `"Configuration"` and the classifier identity is `csharp2md.classifier.configuration` version 1
- [x] `ClassifierPassResult` reports fact, relation, candidate and unresolved counts from the emitter
- [x] An empty ledger produces a zero result without throwing
- [x] Unit tests assert the pass's counts and emitted records for a hand-built ledger
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): add the configuration classifier pass`

---

### T34: Register the configuration pass in the default pipeline

**What**: Insert `ConfigurationPass` after `PersistencePass` and before `RelationPass`.
**Where**: `src/Csharp2Md.Analysis/Pipeline/PipelineStages.cs`
**Depends on**: T33
**Reuses**: The existing `ClassificationAndPromotionStage` pass array
**Requirement**: CDC-55

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] The pass order is `Components`, `Entry points`, `Boundaries`, `Contracts`, `Persistence`, `Configuration`, `Relations`, `Invokes`, `Executes`
- [x] The stage's aggregate counts include the new pass's output
- [x] `ComposabilityTests`' pass-name assertion is updated to the nine-pass order rather than loosened
- [x] Integration tests assert the order and the aggregate counts
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: integration
**Gate**: full

**Commit**: `feat(analysis): register the configuration pass`

---

### T35: Publish component coverage into the diagnostics envelope

**What**: Emit a component-coverage `DiagnosticRecord` holding projects grouped, applications found and components without a deployment unit.
**Where**: `src/Csharp2Md.Analysis/Classification/Topology/TopologyEmitter.cs`
**Depends on**: T34
**Reuses**: `SnapshotAccumulator.AddDiagnostic`; the 5C persistence-coverage diagnostic shape
**Requirement**: CDC-56, CDC-58

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] The record carries the three counts and each unreached component's id
- [x] No percentage or recall figure appears in the payload
- [x] The record is emitted once per run, after the fact walk
- [x] Unit tests assert the counts for a hand-built model and the absence of `%`
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): publish component coverage counts`

---

### T36: Publish configuration coverage into the diagnostics envelope

**What**: Emit a configuration-coverage `DiagnosticRecord` holding keys declared, keys bound and keys read but not declared.
**Where**: `src/Csharp2Md.Analysis/Classification/Configuration/ConfigurationEmitter.cs`
**Depends on**: T35
**Reuses**: `SnapshotAccumulator.AddDiagnostic`; the coverage shape from T35
**Requirement**: CDC-57, CDC-58

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] The record carries the three counts and each unbound read's owner id
- [x] No percentage or recall figure appears in the payload
- [x] The record is emitted once per run, after both emitter walks
- [x] Unit tests assert the counts for a hand-built model and the absence of `%`
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): publish configuration coverage counts`

---

### T37: Assert the Orders solution ground truth end to end

**What**: A fixture integration test analyzing `Acme.Orders.slnx` and asserting the full component, deployment, binding and client picture.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/TopologyIntegrationTests.cs`
**Depends on**: T36
**Reuses**: The `PersistenceIntegrationTests` analyze-and-assert harness
**Requirement**: CDC-09, CDC-11, CDC-19, CDC-20, CDC-21, CDC-35, CDC-36, CDC-37, CDC-38, CDC-39, CDC-43, CDC-44, CDC-45

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Exactly three components exist: `Acme.Orders`, `Acme.Orders.Worker` and a shared `Acme.Shared.Contracts`
- [x] Exactly two deployment units exist, and the shared component is `included-in` both
- [x] All four `configured-by` triples are present, each naming the expected key
- [x] `PaymentService` has a confirmed `targets` relation and no surviving candidate; `NotificationService` has a candidate plus an open frontier; `ShippingService` is confirmed `targets` (CDC-43; `appsettings.Development.json` declares the key)
- [x] `appsettings.json` produces no `unsupported-document` diagnostic
- [x] Gate check passes: full gate command
- [x] Test count: Domain 549, Analysis 579 (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `test(analysis): assert the Orders solution topology end to end`

---

### T38: Assert per-solution grouping isolation

**What**: A fixture integration test analyzing `Acme.Payments.slnx` and asserting `Acme.Shared.Contracts` groups into the Payments component there, proving AD-008 isolation.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/TopologyIsolationTests.cs`
**Depends on**: T37
**Reuses**: The harness from T37
**Requirement**: CDC-10, CDC-13

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Exactly one component and one deployment unit exist
- [x] A symbol declared in `Acme.Shared.Contracts` has a `belongs-to` relation targeting the `Acme.Payments` component
- [x] No component named for `Acme.Shared.Contracts` exists in this solution
- [x] The same project's shared grouping in `Acme.Orders.slnx` is unaffected, asserted by referencing T37's expectations
- [x] Gate check passes: full gate command
- [x] Test count: Domain 549, Analysis 580 (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `test(analysis): assert per-solution grouping isolation`

---

### T39: Assert determinism and clone-path independence

**What**: Tests proving the new output is byte-identical across clone paths and idempotent across repeated classification.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/TopologyDeterminismTests.cs`
**Depends on**: T38
**Reuses**: The `ClonePathIndependenceTests` and `PersistenceDeterminismTests` harnesses
**Requirement**: CDC-49, CDC-51

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Two clone paths produce identical components, deployment units, bindings, relations, candidates, frontiers and unresolved records, and byte-identical canonical payloads
- [x] Running the classifier twice over the same ledger produces identical output
- [x] A test asserts no two new observations share an `owner:kind:ordinal` ordering key
- [x] Gate check passes: full gate command
- [x] Test count reported; no silent deletions

**Tests**: integration
**Gate**: full

**Commit**: `test(analysis): assert topology and configuration determinism`

---

### T40: Assert security and isolation invariants

**What**: Tests proving no credential and no absolute path reach the package, no classifier references Roslyn, and the registry is unchanged.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/ConfigurationIsolationTests.cs`
**Depends on**: T39
**Reuses**: The `PersistenceIsolationTests` and `NoAbsolutePathTests` harnesses
**Requirement**: CDC-08, CDC-50, CDC-52, CDC-53, CDC-54

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`

**Done when**:

- [x] The fixture connection-string password appears in no fact, observation, relation, candidate, frontier or diagnostic
- [x] `SuspectedSecretEvidence` for that value is present with a redacted excerpt
- [x] No emitted record contains an absolute path or drive-letter prefix
- [x] No type under `Csharp2Md.Analysis.Classification` references `Microsoft.CodeAnalysis`
- [x] `contracts/taxonomy-registry.json` is byte-identical and the drift gate passes
- [x] Gate check passes: build gate command
- [x] Test count reported; no silent deletions

**Tests**: integration
**Gate**: build

**Commit**: `test(analysis): assert configuration security and isolation invariants`

---

### T41: Round-trip deployment and configuration facts through Storage

**What**: Tests proving `DeploymentUnit` and `ConfigurationBinding` facts and their relations survive stage, commit and read with identity intact.
**Where**: `tests/Csharp2Md.Storage.Tests/Mapping/TopologyRecordRoundTripTests.cs`
**Depends on**: T40
**Reuses**: The `InvokesRecordRoundTripTests` round-trip harness
**Requirement**: CDC-53

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] `DeploymentUnit` and `ConfigurationBinding` facts round-trip with identical identities and fields
- [x] `belongs-to`, `included-in` and `configured-by` relations round-trip with kind, source, target and evidence method preserved
- [x] The `ConfigurationFactsShard` is written and read back non-empty
- [x] The manifest reports non-zero counts for the configuration artifact
- [x] Gate check passes: build gate command
- [x] Test count reported; no silent deletions

**Tests**: unit
**Gate**: build

**Commit**: `test(storage): round-trip deployment and configuration facts`

---

### T42: Reconcile fixture-driven assertions across the suite

**What**: Update every remaining assertion the fixture extension and the grouping rule shifted, to the new ground truth rather than to a looser check.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/BoundaryIntegrationTests.cs`
**Depends on**: T41
**Reuses**: The measured baseline and each test's original intent
**Requirement**: CDC-49, CDC-55

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:test-anti-patterns`

**Done when**:

- [x] Every count assertion shifted by the two new `Main` methods, the worker project and the second configuration file is updated to the exact new value, never widened to a range or a `> 0`
- [x] `BoundaryIntegrationTests`' three-candidate `targets` assertion becomes two candidates plus one confirmed relation
- [x] Any sibling test file needing the same reconciliation is updated in this task and named in the commit body
- [x] No test is skipped, weakened or deleted; the commit body explains every count change
- [x] The full suite is green
- [x] Gate check passes: build gate command
- [x] Test count reported and reconciled against the baseline; no silent deletions

**Tests**: integration
**Gate**: build

**Commit**: `test(analysis): reconcile fixture-driven assertions with the 5D ground truth`

---

## Phase Execution Map

Each phase line repeats the previous phase's final task as its head, so the phase-boundary dependency is visible rather than implied.

```
Phase 0:          T1 --→ T2 --→ T3
Phase 1:    T3 --→ T4 --→ T5 --→ T6 --→ T7
Phase 2:    T7 --→ T8 --→ T9 --→ T10 --→ T11
Phase 3:   T11 --→ T12 --→ T13 --→ T14
Phase 4:   T14 --→ T15 --→ T16 --→ T17
Phase 5:   T17 --→ T18 --→ T19 --→ T20 --→ T21 --→ T22
Phase 6:   T22 --→ T23 --→ T24 --→ T25
Phase 7:   T25 --→ T26 --→ T27 --→ T28 --→ T29
Phase 8:   T29 --→ T30 --→ T31 --→ T32 --→ T33
Phase 9:   T33 --→ T34 --→ T35 --→ T36 --→ T37 --→ T38
Phase 10:  T38 --→ T39 --→ T40 --→ T41 --→ T42
```

Execution is strictly sequential — there is no intra-phase parallelism. A single agent (or batch worker) works one task at a time, in order.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2 | 1 allowlist condition each | ✅ Granular |
| T3 | 1 assertion | ✅ Granular |
| T4, T6, T8 | 1 fixture source file each | ✅ Granular |
| T5, T7, T9, T10 | 1 project/solution file each | ✅ Granular |
| T11 | 1 configuration file | ✅ Granular |
| T12, T13, T14 | 1 file each | ✅ Granular |
| T15, T16 | 1 new emitter each | ✅ Granular |
| T17 | 1 stage wiring | ✅ Granular |
| T18, T26 | 1 record-model file each | ✅ Granular |
| T19, T20 | 2 cohesive steps in 1 builder file | ⚠️ OK — same file, distinct rule sets, separate tests |
| T21 | 1 emitter | ✅ Granular |
| T22, T33 | 1 pass each | ✅ Granular |
| T23 | 1 lookup | ✅ Granular |
| T24, T25 | 1 pass migration each | ✅ Granular |
| T27, T28, T29 | 3 cohesive steps in 1 builder file | ⚠️ OK — same file, distinct rule sets, separate tests |
| T30 | 1 method | ✅ Granular |
| T31, T32 | 2 walks in 1 emitter file | ⚠️ OK — same file, distinct outputs, separate tests |
| T34 | 1 registration | ✅ Granular |
| T35, T36 | 1 diagnostic each | ✅ Granular |
| T37–T41 | 1 test file each | ✅ Granular |
| T42 | 1 test file plus named siblings | ⚠️ OK — reconciliation is inherently cross-file; siblings named in the commit body |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | phase head | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | T5 → T6 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T7 | T7 → T8 | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T9 | T9 → T10 | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T12 | T12 → T13 | ✅ Match |
| T14 | T13 | T13 → T14 | ✅ Match |
| T15 | T14 | T14 → T15 | ✅ Match |
| T16 | T15 | T15 → T16 | ✅ Match |
| T17 | T16 | T16 → T17 | ✅ Match |
| T18 | T17 | T17 → T18 | ✅ Match |
| T19 | T18 | T18 → T19 | ✅ Match |
| T20 | T19 | T19 → T20 | ✅ Match |
| T21 | T20 | T20 → T21 | ✅ Match |
| T22 | T21 | T21 → T22 | ✅ Match |
| T23 | T22 | T22 → T23 | ✅ Match |
| T24 | T23 | T23 → T24 | ✅ Match |
| T25 | T24 | T24 → T25 | ✅ Match |
| T26 | T25 | T25 → T26 | ✅ Match |
| T27 | T26 | T26 → T27 | ✅ Match |
| T28 | T27 | T27 → T28 | ✅ Match |
| T29 | T28 | T28 → T29 | ✅ Match |
| T30 | T29 | T29 → T30 | ✅ Match |
| T31 | T30 | T30 → T31 | ✅ Match |
| T32 | T31 | T31 → T32 | ✅ Match |
| T33 | T32 | T32 → T33 | ✅ Match |
| T34 | T33 | T33 → T34 | ✅ Match |
| T35 | T34 | T34 → T35 | ✅ Match |
| T36 | T35 | T35 → T36 | ✅ Match |
| T37 | T36 | T36 → T37 | ✅ Match |
| T38 | T37 | T37 → T38 | ✅ Match |
| T39 | T38 | T38 → T39 | ✅ Match |
| T40 | T39 | T39 → T40 | ✅ Match |
| T41 | T40 | T40 → T41 | ✅ Match |
| T42 | T41 | T41 → T42 | ✅ Match |

No task depends on a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1, T2 | Test-suite guard (test code) | unit | unit | ✅ OK |
| T3 | CLI surface (test code) | unit | unit | ✅ OK |
| T4–T11 | Fixture source (analysis input) | none | none | ✅ OK |
| T12 | Inventory | unit + integration | unit | ✅ OK — integration lands in T14, which is the earliest point the stage can exercise it |
| T13 | Pipeline stage | integration | unit | ✅ OK — the slot has no stage behaviour until T14 fills it |
| T14 | Pipeline stage | integration | integration | ✅ OK |
| T15, T16 | Observation emitters | unit + integration | unit | ✅ OK — integration lands in T17, the earliest point the stage runs them |
| T17 | Pipeline stage | integration | integration | ✅ OK |
| T18, T26 | Pure model records | unit | unit | ✅ OK |
| T19, T20 | Pure readers (builder) | unit | unit | ✅ OK |
| T21 | Classification | unit + integration | unit | ✅ OK — integration lands in T37 |
| T22 | Classification pass | unit + integration | unit | ✅ OK — integration lands in T37 |
| T23 | Classification | unit | unit | ✅ OK |
| T24, T25 | Classification passes | unit + integration | unit | ✅ OK — existing fixture integration tests already cover these passes and must stay green |
| T27, T28, T29 | Pure readers (builder) | unit | unit | ✅ OK |
| T30 | Pipeline (accumulator) | unit | unit | ✅ OK |
| T31, T32 | Classification | unit + integration | unit | ✅ OK — integration lands in T37 |
| T33 | Classification pass | unit + integration | unit | ✅ OK — integration lands in T34 and T37 |
| T34 | Pipeline stage | integration | integration | ✅ OK |
| T35, T36 | Classification | unit + integration | unit | ✅ OK — integration lands in T37 |
| T37, T38 | End-to-end (fixture analyze) | integration | integration | ✅ OK |
| T39 | End-to-end | integration | integration | ✅ OK |
| T40 | End-to-end | integration | integration | ✅ OK |
| T41 | Storage mapping | unit | unit | ✅ OK |
| T42 | End-to-end | integration | integration | ✅ OK |

No `Tests: none` outside the fixture layer, where the matrix says `none`.
