# Multi-Solution Composition Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**Standing skip — discrimination sensor**: the user runs Stryker manually; do not run the sensor's fault-injection pass. Every other Verifier step (spec-anchored coverage check, gate check, code-quality check) still runs as documented.

---

**Design**: `.specs/features/multi-solution-composition/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase sampling and project guidelines. Guidelines found: [`CLAUDE.md`](CLAUDE.md) (retrieval-led reasoning, Roslyn API verification, multi-csproj test execution note, LocalCorpus fixture rule), [`Directory.Build.props`](Directory.Build.props) (`TreatWarningsAsErrors`). No CI workflow exists. Carried forward from the `retrieval-projections` matrix and extended with the composition layer this feature creates.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Analysis write port and coordinates | unit | All branches; 1:1 to spec ACs; identity rule asserted on a path with spaces and a non-ASCII name | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Storage package layout and locking | unit + integration | All branches; every abort path asserted with its reason code; directory names asserted against the identity, never a recomputed path hash | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Storage wire mapping and manifests | unit | All branches; ordering keys asserted total; round-trip | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Storage batch validation | unit | All branches; every rejection path asserted with its reason code | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Projection composition (pure, no I/O) | unit | All branches; 1:1 to spec ACs; every listed edge case; both the match and the deliberate non-match | `tests/Csharp2Md.Projection.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |
| Projection contract shape (MSC-25) | unit | Reflection over the closure of `SolutionContribution` property types; forbidden types enumerated explicitly | `tests/Csharp2Md.Projection.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |
| Pipeline stage / orchestrator | integration | Stage wiring, engine wiring, accumulator lifecycle | `tests/Csharp2Md.Analysis.Tests/Pipeline/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| End-to-end (fixture analyze) | integration | Full pipeline over a real multi-solution batch; determinism; isolation; partial batch | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| CLI surface | unit | No new flags; exit-code mapping asserted; CLI→Domain isolation | `tests/Csharp2Md.Cli.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"` |
| Analysis fixture (`fixtures/SyntheticSolution`) | integration | A new fixture solution is proven to classify the operations the later tests depend on, in the task that adds it | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |

## Gate Check Commands

> Generated from codebase — confirm before Execute. Multi-csproj `dotnet test` hits MSB1008; run each test project separately. `Category=LocalCorpus` is excluded from every gate; those tests run only after the Verifier and only when the local clones exist.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After unit-only tasks scoped to a single assembly | the matching project's command from the matrix above |
| Full | After tasks that cross Storage and Projection or touch the wire contract | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |
| Build | After phase completion, port changes, and every task touching Analysis or the CLI | `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |

**Measured baseline (2026-08-27, `594e659`)**: `dotnet build` clean, 0 warnings, 0 errors. **1617 tests, 1617 passing, 0 failing** — Domain 555, Analysis 635, Storage 246, Cli 29, Projection 152. No baseline repair phase is needed. Every task reports its new total; a drop means a silent deletion.

---

## Tooling

Confirmed at task approval: the routing in [`CLAUDE.md`](CLAUDE.md) applies as written. No skill is invoked speculatively outside these routes.

| When | Skill |
| --- | --- |
| A task creates or changes a C# type | `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance` |
| After a phase lands substantial new code | `dotnet-skills:slopwatch` |
| Before declaring the suite done | `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns` |
| Before the Verifier runs | `dotnet-test:test-gap-analysis` |
| Running gates | `dotnet-test:run-tests` |

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a phase execute in order.

### Phase 1: Solution identity and package layout

Removes the absolute path from every identity and layout decision. Nothing downstream is reproducible until this lands.

```
T1 -> T2
T1 -> T3
T1 -> T4 -> T5
T4 -> T6
```

### Phase 2: Contribution seam

The bounded data composition is allowed to read, and the accumulator that collects it.

```
T6 -> T7 -> T9
T4 -> T8 -> T9 -> T10 -> T11
```

### Phase 3: Composition derivations

One matcher per artifact, then the assembly that orders, shards and suppresses empties.

```
T11 -> T12 -> T17
T11 -> T13 -> T17
T11 -> T14 -> T17
T11 -> T15 -> T17
T11 -> T16 -> T17
```

### Phase 4: Batch publication

Validation, manifest, both stores, the engine and the CLI exit code.

```
T17 -> T18 -> T20
T17 -> T19 -> T20 -> T22 -> T23
T18 -> T21
T19 -> T21 -> T22
```

### Phase 5: Fixture and end-to-end

The fixture that makes the positive path real, then the four end-to-end guarantees.

```
T23 -> T24 -> T25
T24 -> T26
T24 -> T27
T24 -> T28
```

---

## Task Breakdown

### T1: Create SolutionCoordinate

**What**: A readonly record struct carrying a solution's `SolutionId` and file name, with a `For(string solutionPath)` factory holding the single copy of the identity rule.
**Where**: `src/Csharp2Md.Analysis/Storage/SolutionCoordinate.cs`
**Depends on**: None
**Reuses**: `SolutionId.Create`, `WorkspaceIdentity.Create` — the exact expression duplicated at `AnalysisEngine.cs:119` and `InventoryFacts.cs:20`
**Requirement**: MSC-02

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] `SolutionCoordinate.For` returns the same identity for the same file name reached through two different absolute paths
- [x] `For` rejects a null, empty or whitespace path with `ArgumentException`
- [x] Identity asserted for a path containing spaces and for a non-ASCII solution name
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 641 pass; total 1623 (Domain 555, Analysis 641, Storage 246, Cli 29, Projection 152)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add SolutionCoordinate as the single solution identity rule`

---

### T2: Repoint AnalysisEngine onto SolutionCoordinate

**What**: Replace the inline identity expression in `RejectDuplicateSolutionIdentities` and `AnalyzeSolutionAsync` with `SolutionCoordinate.For`.
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T1
**Reuses**: `SolutionCoordinate.For` from T1
**Requirement**: MSC-12

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] No `WorkspaceIdentity.Create` call remains in `AnalysisEngine.cs`
- [x] Two solutions with the same file name from different directories are still rejected before any analysis starts, with a message naming both requested paths
- [x] Existing `DuplicateSolutionIdTests` pass unchanged
- [x] Gate check passes: build gate
- [x] Test count reported; total 1623 (Domain 555, Analysis 641, Storage 246, Cli 29, Projection 152)

**Tests**: unit
**Gate**: build

**Commit**: `refactor(analysis): derive engine solution identity from SolutionCoordinate`

---

### T3: Repoint InventoryFacts onto SolutionCoordinate

**What**: Replace the second copy of the identity expression in the inventory stage with `SolutionCoordinate.For`.
**Where**: `src/Csharp2Md.Analysis/Inventory/InventoryFacts.cs`
**Depends on**: T1
**Reuses**: `SolutionCoordinate.For` from T1
**Requirement**: MSC-02

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] No `WorkspaceIdentity.Create` call remains in `InventoryFacts.cs`
- [x] The `Solution` structural fact keeps the identity it had before this task, asserted against a fixture analysis
- [x] Gate check passes: build gate
- [x] Test count reported; total 1623 (Domain 555, Analysis 641, Storage 246, Cli 29, Projection 152)

**Tests**: unit
**Gate**: build

**Commit**: `refactor(analysis): derive inventory solution identity from SolutionCoordinate`

---

### T4: Switch the store port to SolutionCoordinate

**What**: `ITransactionalStore.Open` takes a `SolutionCoordinate`, with a `string` overload delegating through `SolutionCoordinate.For` so existing call sites keep compiling; both implementations follow the new signature.
**Where**: `src/Csharp2Md.Analysis/Storage/ITransactionalStore.cs` (plus the two implementations it breaks)
**Depends on**: T1
**Reuses**: `SolutionCoordinate.For` from T1; existing lock and session construction unchanged
**Requirement**: MSC-02

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`, `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Both overloads present; the `string` overload is proven to delegate, not duplicate, the identity rule
- [x] All 89 existing `Open(` call sites compile without edits
- [x] Behaviour is unchanged this task: the directory is still derived exactly as before
- [x] Gate check passes: build gate
- [x] Test count reported; total 1625 (Domain 555, Analysis 642, Storage 247, Cli 29, Projection 152)

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): accept a solution coordinate on the transactional store port`

---

### T5: Derive the package directory from the solution identity

**What**: Hash `coordinate.Identity.Value` instead of the absolute path for the `s-<hex>` directory and its `.lock` and `.staging` siblings.
**Where**: `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs`
**Depends on**: T4
**Reuses**: The existing SHA-256/`Convert.ToHexStringLower` expression, repointed at a different input
**Requirement**: MSC-02, MSC-05

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] The same solution file reached through two different parent directories produces one directory name
- [x] `FilesystemTestPaths` and the four tests that recompute the hash (`PipelineCancellationTests`, `CitationResolutionTests`, `SourceSecretAbsenceTests`, `SourceIntegrityTests`) derive the name from the identity, not from a copy of the path rule
- [x] `.lock` and `.staging` follow the same stem, asserted directly
- [x] Gate check passes: build gate
- [x] Test count reported; total 1627 (Domain 555, Analysis 642, Storage 249, Cli 29, Projection 152)

**Tests**: unit + integration
**Gate**: build

**Commit**: `feat(storage): derive the package directory from the solution identity`

---

### T6: Publish solution_key as the solution identity

**What**: Both stores put `coordinate.Identity.Value` in `ManifestContext`, and `InMemoryTransactionalStore.TryGetPublication` is rekeyed by identity.
**Where**: `src/Csharp2Md.Storage/InMemoryTransactionalStore.cs` (plus the matching manifest-context construction in the filesystem store)
**Depends on**: T4
**Reuses**: `ManifestContext`, `ManifestBuilder` unchanged
**Requirement**: MSC-07

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] One test runs the same snapshot through both stores and asserts an identical `solution_key`
- [x] No published artifact contains an absolute path, asserted by scanning the committed bytes of a fixture run
- [x] The 57 `TryGetPublication` call sites resolve through the identity
- [x] Gate check passes: build gate
- [x] Test count reported; total 1629 (Domain 555, Analysis 643, Storage 250, Cli 29, Projection 152)

**Tests**: unit
**Gate**: build

**Commit**: `fix(storage): publish the solution identity as solution_key in both stores`

---

### T7: Define SolutionContribution

**What**: The bounded record set composition may read — `SolutionContribution` plus `ContributedBoundaryOperation`, `ContributedIdentity` and `ContributedNamedIdentity`.
**Where**: `src/Csharp2Md.Storage/SolutionContribution.cs`
**Depends on**: T6
**Reuses**: Field names copied from `BoundaryOperationDto`, `ComponentDto`, `DeploymentUnitDto`, `ExternalSystemDto`, `ContractDto`
**Requirement**: MSC-25

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A reflection test walks the closure of every property type reachable from `SolutionContribution` and asserts none is `WireDocument`, `PublishedPackageView`, `ObservationDto`, `SymbolDto`, `ConfirmedRelation`, `CandidateLink` or a byte sequence
- [x] The forbidden-type list is declared explicitly in the test, so adding a member of a new forbidden type fails
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; total 1633 (Domain 555, Analysis 643, Storage 254, Cli 29, Projection 152)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): define the bounded solution contribution contract`

---

### T8: Add PublishBatch to the store port

**What**: `BatchSolutionRecord` on the Analysis port and an `ITransactionalStore.PublishBatch` member, implemented in both stores as a recording no-op this task fills in later.
**Where**: `src/Csharp2Md.Analysis/Storage/BatchSolutionRecord.cs` (plus the port and the two implementations it breaks)
**Depends on**: T4
**Reuses**: `PublicationStatus` from `AnalysisResult.cs`
**Requirement**: MSC-03

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] `BatchSolutionRecord` carries identity, file name, status and failing stage — and an API-shape test asserts it exposes no artifact key, ordinal or fact type
- [x] `PublishBatch` rejects a default or empty record array with `ArgumentException`
- [x] Gate check passes: build gate
- [x] Test count reported; total 1640 (Domain 555, Analysis 650, Storage 254, Cli 29, Projection 152)

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): add batch publication to the transactional store port`

---

### T9: Declare the batch composer port

**What**: `IBatchComposer` with `Contribute` and `Compose`, plus the `BatchView` record it consumes.
**Where**: `src/Csharp2Md.Storage/IBatchComposer.cs`
**Depends on**: T7, T8
**Reuses**: The `IPackageProjector` port shape established by AD-019
**Requirement**: MSC-25

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] `BatchView` derives `Complete` and `IncompleteScopeReason` from its records rather than accepting them from a caller
- [x] `Complete` is `false` with reason `solution-unpublished` when any record is unpublished, `true` with a null reason otherwise
- [x] `Compose` accepts only `BatchView`, asserted by an API-shape test
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; total 1645 (Domain 555, Analysis 650, Storage 259, Cli 29, Projection 152)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): declare the batch composer port`

---

### T10: Extract a contribution from the published view

**What**: `BatchComposer.Contribute` reads `PublishedPackageView` and emits one `SolutionContribution`.
**Where**: `src/Csharp2Md.Projection/Composition/BatchComposer.cs`
**Depends on**: T9
**Reuses**: `PublishedPackageView.TryLocate` for every artifact key and ordinal
**Requirement**: MSC-25

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Every boundary operation, contract, component, deployment unit and external system in the view appears exactly once
- [x] Every entry's artifact key and ordinal equal what `TryLocate` returns for that fact id
- [x] A fact the view cannot locate is omitted rather than emitted with a placeholder ordinal
- [x] External-system names read `Name.Value`; component and deployment-unit names read `Name`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; total 1651 (Domain 555, Analysis 650, Storage 259, Cli 29, Projection 158)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): extract a bounded contribution from the published view`

---

### T11: Register contributions after a successful replace

**What**: `PublicationPipeline.Publish` returns the contribution alongside the fragments, and each session registers it with its store only after the package directory is in place.
**Where**: `src/Csharp2Md.Storage/Mapping/PublicationPipeline.cs` (plus the accumulator on both stores)
**Depends on**: T10
**Reuses**: The existing `PublishedPackageView` already built inside `Publish`
**Requirement**: MSC-39

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A solution whose staging or replacement fails leaves no contribution in the accumulator
- [x] An aborted session leaves no contribution in the accumulator
- [x] Two sequential batches on one store instance do not share contributions
- [x] The view is built once per commit, not twice, asserted by a counting double
- [x] Gate check passes: build gate
- [x] Test count reported; total 1658 (Domain 555, Analysis 650, Storage 266, Cli 29, Projection 158)

**Tests**: unit + integration
**Gate**: build

**Commit**: `feat(storage): accumulate solution contributions after a successful replace`

---

### T12: Match proven cross-solution messaging relations

**What**: The matcher pairing outbound and inbound messaging operations by equal protocol operation key across different solutions.
**Where**: `src/Csharp2Md.Projection/Composition/MessagingCorrelator.cs`
**Depends on**: T11
**Reuses**: `FacetAxes.WireValue` for `outbound`, `inbound` and `messaging`
**Requirement**: MSC-16, MSC-17, MSC-18, MSC-19, MSC-20, MSC-38

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A matched pair produces one entry of kind `targets` carrying both fact identities, both solution identities, both artifact keys and ordinals, and the matched key
- [x] One outbound matching inbound operations in two other solutions produces two entries
- [x] An outbound and an inbound operation in the same solution produce no entry
- [x] An outbound operation matching nothing produces neither an entry nor a candidate
- [x] Direction and protocol literals come from `FacetAxes.WireValue`, asserted by a test that would fail on a hard-coded string
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Projection 163 pass; total 1663 (previous 1658)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): correlate cross-solution messaging operations`

---

### T13: Match shared contracts across solutions

**What**: The matcher grouping identical contract identities appearing in two or more solutions.
**Where**: `src/Csharp2Md.Projection/Composition/ContractCorrelator.cs`
**Depends on**: T11
**Reuses**: `ContributedIdentity` from T7
**Requirement**: MSC-21, MSC-27, MSC-37

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A contract identity present in two solutions produces one entry, not two
- [x] The entry carries, per owning solution, the solution identity, artifact key and ordinal
- [x] Owning solutions inside an entry are ordered by solution identity, ordinal ascending
- [x] A contract identity present in only one solution produces no entry
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Projection 167 pass; total 1667 (previous 1663)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): correlate contracts shared across solutions`

---

### T14: Match HTTP correlation candidates

**What**: The matcher pairing an outbound HTTP operation's method-and-route join against an inbound HTTP operation's protocol operation key in a different solution, always as a candidate.
**Where**: `src/Csharp2Md.Projection/Composition/HttpCorrelator.cs`
**Depends on**: T11
**Reuses**: `FacetAxes.WireValue` for `outbound`, `inbound` and `http`
**Requirement**: MSC-22, MSC-23

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A match produces a candidate carrying both identities, both solution identities, the matched key and the outbound destination scope
- [x] No HTTP pairing ever reaches the cross-solution relations artifact, asserted directly
- [x] An outbound operation with a null HTTP method or a null route produces no candidate
- [x] An inbound operation whose key is a bare template with no method produces no candidate, asserted as deliberate with a comment citing MSC-22
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Projection 172 pass; total 1672 (previous 1667)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): correlate cross-solution HTTP operations as candidates`

---

### T15: Group components and deployment units globally

**What**: The grouped, unmerged global catalog of components and deployment units.
**Where**: `src/Csharp2Md.Projection/Composition/GlobalComponentCatalog.cs`
**Depends on**: T11
**Reuses**: `ContributedNamedIdentity` from T7
**Requirement**: MSC-30, MSC-31, MSC-32, MSC-33

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Every component and deployment unit from every contribution appears with its fact identity, owning solution identity, artifact key and ordinal
- [x] Groups are ordered by canonical name; entries inside a group by solution identity, ordinal ascending
- [x] A name carried by two solutions yields one group with `shared_identity` set to `not-proven`
- [x] A name carried by one solution yields a group without a shared-identity claim
- [x] No two identities are ever merged into one, asserted by counting entries against the input
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Projection 177 pass; total 1677 (previous 1672)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): publish a grouped global component catalog`

---

### T16: Group external systems globally

**What**: The same grouped, unmerged form for external systems, reading the name through `Name.Value`.
**Where**: `src/Csharp2Md.Projection/Composition/GlobalExternalSystemCatalog.cs`
**Depends on**: T11
**Reuses**: The grouping helper introduced by T15
**Requirement**: MSC-34

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Output shape is identical to the component catalog, asserted against the same grouping helper
- [x] Two solutions naming the same external system yield one group with `shared_identity` set to `not-proven`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Projection 179 pass; total 1679 (previous 1677)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): publish a grouped global external system catalog`

---

### T17: Assemble, order and shard the composition

**What**: `BatchComposer.Compose` turning the five matchers' output into staged fragments, with composite sort keys, shard splitting and empty-artifact suppression.
**Where**: `src/Csharp2Md.Projection/Composition/BatchComposer.cs` (modify)
**Depends on**: T12, T13, T14, T15, T16
**Reuses**: `ShardWriter.Write` — it sorts and buckets by whatever key it is given; `CanonicalJson.Write`
**Requirement**: MSC-26, MSC-28, MSC-29, MSC-36

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Relations and candidates are ordered by source solution, source fact, target solution, target fact
- [x] An artifact exceeding a configured byte ceiling splits into ordinal-suffixed shards using `ShardWriter`
- [x] An artifact with zero entries is not emitted at all
- [x] A batch whose contributions carry no component, deployment unit, external system, contract or boundary operation emits no composition fragment
- [x] Reordering the contributions produces byte-identical fragments
- [x] Gate check passes: full gate
- [x] Test count reported; Storage 266 + Projection 184; total 1684 (previous 1679)

**Tests**: unit
**Gate**: full

**Commit**: `feat(projection): assemble, order and shard the batch composition`

---

### T18: Validate composition entries before publication

**What**: `BatchValidator` proving every entry resolves against the batch before anything is written.
**Where**: `src/Csharp2Md.Storage/Validation/BatchValidator.cs`
**Depends on**: T17
**Reuses**: `ProjectionValidator`'s structure and `PublicationRejectedException`
**Requirement**: MSC-15, MSC-17

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] An entry citing a solution identity absent from the batch is rejected with reason `batch-composition`
- [x] An entry citing an artifact key or ordinal absent from that solution's contribution is rejected
- [x] An entry pairing a solution with itself is rejected
- [x] A published empty artifact is rejected
- [x] A valid composition passes without throwing
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; Storage 272 pass; total 1690 (previous 1684)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): validate batch composition entries before publication`

---

### T19: Build the batch manifest

**What**: `BatchManifestBuilder` and its envelope DTO, emitting solutions and artifacts in canonical order with the completeness declaration.
**Where**: `src/Csharp2Md.Storage/Mapping/BatchManifestBuilder.cs`
**Depends on**: T17
**Reuses**: `ManifestBuilder`'s stem and entry shape; `CanonicalJson.Write`
**Requirement**: MSC-03, MSC-04, MSC-10, MSC-11

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:serialization`

**Done when**:

- [x] Each solution entry carries identity, file name, package directory and status
- [x] An unpublished entry also carries its failing stage
- [x] Solutions are ordered by identity, artifacts by canonical key, ordinal ascending
- [x] A batch with any unpublished solution emits `complete: false` and `incomplete_scope_reason: solution-unpublished`
- [x] A fully committed batch emits `complete: true` and no `incomplete_scope_reason` key
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; Storage 277 pass; total 1695 (previous 1690)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): build the batch manifest`

---

### T20: Publish the batch from the filesystem store

**What**: `FilesystemTransactionalStore.PublishBatch` — root lock, compose, validate, stage, replace, clear the accumulator.
**Where**: `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs` (modify)
**Depends on**: T18, T19
**Reuses**: The existing lock, staging and `FilesystemIo` retry machinery
**Requirement**: MSC-01, MSC-13, MSC-14, MSC-15, MSC-40

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `batch-manifest.json` and `composition/` appear at the output root after a successful batch
- [x] An earlier run's `batch-manifest.json` is replaced, not merged
- [x] A package directory no requested solution maps to is left byte-identical and is not referenced
- [x] A validation or I/O failure leaves every committed package directory byte-identical and no batch manifest on disk
- [x] A concurrently held root lock raises `PublicationRejectedException` with reason `lock`
- [x] A missing output root is created rather than rejected
- [x] The accumulator is empty after the call
- [x] Gate check passes: build gate
- [x] Test count reported; Storage 285 pass; total 1703 (previous 1695)

**Tests**: unit + integration
**Gate**: build

**Commit**: `feat(storage): publish the batch manifest and composition from the filesystem store`

---

### T21: Publish the batch from the in-memory store

**What**: `InMemoryTransactionalStore.PublishBatch` with a readable batch result, keeping the two stores observationally identical.
**Where**: `src/Csharp2Md.Storage/InMemoryTransactionalStore.cs` (modify)
**Depends on**: T18, T19
**Reuses**: The same compose-then-validate sequence as T20
**Requirement**: MSC-01

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] One test runs the same batch through both stores and asserts byte-identical manifest and composition fragments
- [x] The accumulator is empty after the call
- [x] A validation failure surfaces the same reason code as the filesystem store
- [x] Gate check passes: full gate
- [x] Test count reported; Storage 288 pass; total 1706 (previous 1703)

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): publish the batch from the in-memory store`

---

### T22: Drive the batch from AnalysisEngine

**What**: After the per-solution loop, build one `BatchSolutionRecord` per requested solution and call `PublishBatch`; surface a batch failure on `AnalysisResult`.
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs` (modify)
**Depends on**: T20, T21
**Reuses**: The existing outcome list and `SolutionOutcome.FailingStage`
**Requirement**: MSC-03, MSC-09

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Every requested solution yields a record, committed or not
- [x] An unpublished solution's record carries its failing stage and its package directory is left unmodified
- [x] A batch with every solution unpublished still publishes a manifest and no composition artifact
- [x] A `PublicationRejectedException` from `PublishBatch` is captured on `AnalysisResult` rather than escaping the engine
- [x] Gate check passes: build gate
- [x] Test count reported; Analysis 654 pass; total 1710 (previous 1706)

**Tests**: integration
**Gate**: build

**Commit**: `feat(analysis): publish a batch after the per-solution loop`

---

### T23: Report batch failure from the CLI

**What**: Map a batch publication failure onto the existing non-zero exit and the stderr diagnostic format, adding no new option.
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`
**Depends on**: T22
**Reuses**: `WriteDiagnostics` and the existing `HasUnpublishedSolution ? 2 : 0` mapping
**Requirement**: MSC-15

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A batch failure returns a non-zero exit code and writes a `csharp2md:` diagnostic naming the reason
- [x] A fully committed batch still returns 0
- [x] A batch with an unpublished solution still returns 2
- [x] No new CLI option exists, asserted against the parsed root command
- [x] Gate check passes: build gate
- [x] Test count reported; Cli 33 pass; total 1714 (previous 1710)

**Tests**: unit
**Gate**: build

**Commit**: `feat(cli): report batch publication failure`

---

### T24: Add the Acme.Shipping fixture solution

**What**: A restorable fixture solution that handles `OrderPlaced` through `IIntegrationEventHandler.HandleAsync` and exposes `POST shipments`, closing the `ShippingService` client call `Acme.Orders` already makes.
**Where**: `fixtures/SyntheticSolution/Acme.Shipping/`
**Depends on**: T23
**Reuses**: The local attribute stand-in pattern from `Acme.Orders/Api/OrdersController.cs:16` and the handler shape from `Acme.Orders/Events/OrderPlacedEventHandler.cs:23`; `Acme.Shared.Contracts` for the event type
**Requirement**: MSC-16, MSC-22

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:

- [x] The solution restores and builds without new NuGet dependencies
- [x] An integration test proves it classifies an inbound messaging operation whose protocol operation key equals the one `Acme.Orders` publishes
- [x] The same test proves it classifies an inbound HTTP operation whose key equals `POST shipments`
- [x] `Acme.Payments` is byte-unchanged, so its deliberate unrestored state still serves the existing tests
- [x] `fixtures/SyntheticSolution/README.md` records the new solution and why it exists
- [x] Gate check passes: build gate
- [x] Test count reported; Analysis 655 pass; total 1715 (previous 1714)

**Tests**: integration
**Gate**: build

**Commit**: `test(fixtures): add the Acme.Shipping solution for cross-solution correlation`

---

### T25: Prove cross-solution correlations end to end

**What**: An end-to-end test analyzing `Acme.Orders` and `Acme.Shipping` as one batch and asserting the three composition outcomes over real Roslyn-derived facts.
**Where**: `tests/Csharp2Md.Analysis.Tests/Composition/CrossSolutionCorrelationTests.cs`
**Depends on**: T24
**Reuses**: The fixture analysis helpers already used by `CitationResolutionTests`
**Requirement**: MSC-16, MSC-21, MSC-22

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Exactly one `targets` cross-solution relation is published, for the `OrderPlaced` pair
- [x] Exactly one HTTP correlation candidate is published, for the `POST shipments` pair
- [x] Both entries' artifact keys and ordinals resolve to the named operation inside each package in one read
- [x] Contracts shared through `Acme.Shared.Contracts` appear in `composition/shared-contracts.json`
- [x] `PaymentProcessed` produces no entry, asserted as the unpaired-publish case
- [x] Gate check passes: build gate
- [x] Test count reported; Analysis 656 pass; total 1716 (previous 1715)

**Tests**: integration
**Gate**: build

**Commit**: `test(analysis): prove cross-solution correlations over the fixture batch`

---

### T26: Prove batch determinism

**What**: End-to-end determinism across clone location and argument order.
**Where**: `tests/Csharp2Md.Analysis.Tests/Composition/BatchDeterminismTests.cs`
**Depends on**: T24
**Reuses**: `FilesystemTestPaths.SnapshotFiles` for byte comparison
**Requirement**: MSC-05, MSC-06

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] The same two solutions copied under a second parent directory produce identical package directory names
- [x] Both runs produce byte-identical `batch-manifest.json` and byte-identical composition artifacts
- [x] Reversing the `--solution` order produces byte-identical batch and composition artifacts
- [x] The comparison reads published bytes, never a value recomputed by the test
- [x] Gate check passes: build gate
- [x] Test count reported; Analysis 658 pass; total 1718 (previous 1716)

**Tests**: integration
**Gate**: build

**Commit**: `test(analysis): prove batch output is clone-path and argument-order independent`

---

### T27: Prove per-solution packages are untouched by batching

**What**: End-to-end proof that composition writes nothing into a solution's own package, and that a single-solution batch still publishes the batch layer.
**Where**: `tests/Csharp2Md.Analysis.Tests/Composition/BatchIsolationTests.cs`
**Depends on**: T24
**Reuses**: `FilesystemTestPaths.SnapshotFiles`
**Requirement**: MSC-08, MSC-24

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] `Acme.Orders` analyzed alone produces a package byte-identical to the same package inside an `Acme.Orders` plus `Acme.Shipping` batch
- [ ] No per-solution package gains a fact, observation, confirmed relation, candidate or quarantine record from composition
- [ ] A single-solution batch publishes `batch-manifest.json` with one entry and no cross-solution relation, shared-contract or correlation-candidate artifact
- [ ] Gate check passes: build gate
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: build

**Commit**: `test(analysis): prove batching leaves per-solution packages untouched`

---

### T28: Prove partial and fully failed batches

**What**: End-to-end proof of the incomplete-scope declaration and of committed packages surviving a sibling's failure.
**Where**: `tests/Csharp2Md.Analysis.Tests/Composition/PartialBatchTests.cs`
**Depends on**: T24
**Reuses**: The `Acme.Broken` failure path already exercised by the solution-loader tests
**Requirement**: MSC-09, MSC-10, MSC-35

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] With one solution failing, the committed package is byte-identical to the same run without the failing sibling
- [ ] The manifest declares `complete: false`, `incomplete_scope_reason: solution-unpublished`, and the failing entry carries its stage
- [ ] Composition still covers the committed solutions
- [ ] With every solution failing, a manifest is published with zero committed entries and no composition artifact
- [ ] The process exit code is 2 in both cases
- [ ] Gate check passes: build gate
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: build

**Commit**: `test(analysis): prove partial and fully failed batch behaviour`

---

## Phase Execution Map

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5

Phase 1:  T1 -> T2
          T1 -> T3
          T1 -> T4 -> T5
          T4 -> T6

Phase 2:  T6 -> T7 -> T9
          T4 -> T8 -> T9 -> T10 -> T11

Phase 3:  T11 -> T12 -> T17
          T11 -> T13 -> T17
          T11 -> T14 -> T17
          T11 -> T15 -> T17
          T11 -> T16 -> T17

Phase 4:  T17 -> T18 -> T20
          T17 -> T19 -> T20 -> T22 -> T23
          T18 -> T21
          T19 -> T21 -> T22

Phase 5:  T23 -> T24 -> T25
          T24 -> T26
          T24 -> T27
          T24 -> T28
```

Execution is strictly sequential - there is no intra-phase parallelism. A single agent (or batch worker) works one task at a time, in order.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1 | 1 type | ✅ Granular |
| T2 | 1 file, 1 refactor | ✅ Granular |
| T3 | 1 file, 1 refactor | ✅ Granular |
| T4 | 1 port + its only 2 implementers | ⚠️ Cohesive — one signature change; splitting would leave the build broken between tasks |
| T5 | 1 file, 1 derivation rule | ✅ Granular |
| T6 | 1 field in 2 stores | ⚠️ Cohesive — one contract, two implementers |
| T7 | 1 record set | ✅ Granular |
| T8 | 1 port member + its 2 implementers | ⚠️ Cohesive — same reason as T4 |
| T9 | 1 port | ✅ Granular |
| T10 | 1 method | ✅ Granular |
| T11 | 1 pipeline seam + accumulator | ⚠️ Cohesive — the return value and its only consumer |
| T12 | 1 matcher | ✅ Granular |
| T13 | 1 matcher | ✅ Granular |
| T14 | 1 matcher | ✅ Granular |
| T15 | 1 catalog | ✅ Granular |
| T16 | 1 catalog | ✅ Granular |
| T17 | 1 method | ✅ Granular |
| T18 | 1 validator | ✅ Granular |
| T19 | 1 builder | ✅ Granular |
| T20 | 1 method | ✅ Granular |
| T21 | 1 method | ✅ Granular |
| T22 | 1 file | ✅ Granular |
| T23 | 1 file | ✅ Granular |
| T24 | 1 fixture solution | ✅ Granular |
| T25 | 1 test file | ✅ Granular |
| T26 | 1 test file | ✅ Granular |
| T27 | 1 test file | ✅ Granular |
| T28 | 1 test file | ✅ Granular |

The four ⚠️ rows are the skill's "2-3 related things, cohesive" case: a port and its only implementers cannot be split without leaving a task whose gate cannot pass.

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | — | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T1 | T1 → T3 | ✅ Match |
| T4 | T1 | T1 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T4 | T4 → T6 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T4 | T4 → T8 | ✅ Match |
| T9 | T7, T8 | T7 → T9, T8 → T9 | ✅ Match |
| T10 | T9 | T9 → T10 | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T11 | T11 → T13 | ✅ Match |
| T14 | T11 | T11 → T14 | ✅ Match |
| T15 | T11 | T11 → T15 | ✅ Match |
| T16 | T11 | T11 → T16 | ✅ Match |
| T17 | T12, T13, T14, T15, T16 | five arrows into T17 | ✅ Match |
| T18 | T17 | T17 → T18 | ✅ Match |
| T19 | T17 | T17 → T19 | ✅ Match |
| T20 | T18, T19 | T18 → T20, T19 → T20 | ✅ Match |
| T21 | T18, T19 | T18 → T21, T19 → T21 | ✅ Match |
| T22 | T20, T21 | T20 → T22, T21 → T22 | ✅ Match |
| T23 | T22 | T22 → T23 | ✅ Match |
| T24 | T23 | T23 → T24 | ✅ Match |
| T25 | T24 | T24 → T25 | ✅ Match |
| T26 | T24 | T24 → T26 | ✅ Match |
| T27 | T24 | T24 → T27 | ✅ Match |
| T28 | T24 | T24 → T28 | ✅ Match |

Every dependency, including the cross-phase ones, is drawn in the phase diagrams. No task depends on a task in a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Analysis write port and coordinates | unit | unit | ✅ OK |
| T2 | Analysis write port and coordinates | unit | unit | ✅ OK |
| T3 | Analysis write port and coordinates | unit | unit | ✅ OK |
| T4 | Analysis write port and coordinates | unit | unit | ✅ OK |
| T5 | Storage package layout and locking | unit + integration | unit + integration | ✅ OK |
| T6 | Storage wire mapping and manifests | unit | unit | ✅ OK |
| T7 | Projection contract shape | unit | unit | ✅ OK |
| T8 | Analysis write port | unit | unit | ✅ OK |
| T9 | Storage batch validation | unit | unit | ✅ OK |
| T10 | Projection composition | unit | unit | ✅ OK |
| T11 | Pipeline stage / orchestrator | integration | unit + integration | ✅ OK |
| T12 | Projection composition | unit | unit | ✅ OK |
| T13 | Projection composition | unit | unit | ✅ OK |
| T14 | Projection composition | unit | unit | ✅ OK |
| T15 | Projection composition | unit | unit | ✅ OK |
| T16 | Projection composition | unit | unit | ✅ OK |
| T17 | Projection composition | unit | unit | ✅ OK |
| T18 | Storage batch validation | unit | unit | ✅ OK |
| T19 | Storage wire mapping and manifests | unit | unit | ✅ OK |
| T20 | Storage package layout and locking | unit + integration | unit + integration | ✅ OK |
| T21 | Storage package layout and locking | unit + integration | unit | ✅ OK — no filesystem path exists in this store; the byte-equality test against T20 supplies the integration half |
| T22 | Pipeline stage / orchestrator | integration | integration | ✅ OK |
| T23 | CLI surface | unit | unit | ✅ OK |
| T24 | Analysis fixture | integration | integration | ✅ OK |
| T25 | End-to-end | integration | integration | ✅ OK |
| T26 | End-to-end | integration | integration | ✅ OK |
| T27 | End-to-end | integration | integration | ✅ OK |
| T28 | End-to-end | integration | integration | ✅ OK |

No task carries `Tests: none`.

---

## Requirement Coverage

All 40 requirement IDs map to at least one task.

| Requirement | Task | Requirement | Task |
| --- | --- | --- | --- |
| MSC-01 | T20, T21 | MSC-21 | T13 |
| MSC-02 | T1, T3, T4, T5 | MSC-22 | T14, T24 |
| MSC-03 | T8, T19, T22 | MSC-23 | T14 |
| MSC-04 | T19 | MSC-24 | T27 |
| MSC-05 | T5, T26 | MSC-25 | T7, T9, T10 |
| MSC-06 | T26 | MSC-26 | T17 |
| MSC-07 | T6 | MSC-27 | T13 |
| MSC-08 | T27 | MSC-28 | T17 |
| MSC-09 | T22, T28 | MSC-29 | T17 |
| MSC-10 | T19, T28 | MSC-30 | T15 |
| MSC-11 | T19 | MSC-31 | T15 |
| MSC-12 | T2 | MSC-32 | T15 |
| MSC-13 | T20 | MSC-33 | T15 |
| MSC-14 | T20 | MSC-34 | T16 |
| MSC-15 | T18, T20, T23 | MSC-35 | T28 |
| MSC-16 | T12, T24, T25 | MSC-36 | T17 |
| MSC-17 | T12, T18 | MSC-37 | T13 |
| MSC-18 | T12 | MSC-38 | T12 |
| MSC-19 | T12 | MSC-39 | T11 |
| MSC-20 | T12 | MSC-40 | T20 |
