# Generator CLI, Projections and Certification Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**Standing skip — discrimination sensor**: the user runs Stryker manually; do not run the sensor's fault-injection pass. Every other Verifier step (spec-anchored coverage check, gate check, code-quality check) still runs as documented.

---

**Spec**: `.specs/features/generator-cli-projections-certification/spec.md`
**Design**: `.specs/features/generator-cli-projections-certification/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase sampling and project guidelines. Guidelines found: [`CLAUDE.md`](CLAUDE.md) / [`AGENTS.md`](AGENTS.md) (retrieval-led reasoning, Roslyn API verification, multi-csproj test execution note, LocalCorpus fixture rule, quality-gate routing), [`Directory.Build.props`](Directory.Build.props) (`TreatWarningsAsErrors`). No CI workflow exists. Carried forward from the `multi-solution-composition` matrix and extended with the certification, layout and CLI layers this feature creates.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain facets and registry | unit | All branches; registry projection asserted byte-equal to the committed artifact | `tests/Csharp2Md.Domain.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Inventory and document policy | unit | All branches; 1:1 to spec ACs; every accepted and every excluded category asserted by category, not by one sample | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Classifier passes | unit | All branches; 1:1 to spec ACs; every listed edge case; each disposition asserted with its cause, and each negative case asserted as a non-promotion | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Coverage and certification stage | unit | All branches; every status transition asserted; denominators re-derived independently in the test, never read back from the producer | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Storage wire mapping and intern tables | unit | All branches; round-trip; ordering keys asserted total; interned form asserted byte-identical in meaning to the inlined form | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Layout planner and sharding | unit | All branches; split and unsplit both asserted; shard assignment asserted stable across two runs; every citation asserted to resolve after the split | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Package validation and manifest | unit + integration | All branches; every rejection path asserted with its reason code and offending value | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Projection (pure, no I/O) | unit | All branches; 1:1 to spec ACs; every label asserted against the artifact and ordinal it cites | `tests/Csharp2Md.Projection.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |
| Retrieval scenario runner | integration | Every documented scenario executed end to end; both artifact sources asserted to produce the same report | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Pipeline stage / orchestrator | integration | Stage wiring, engine wiring, accumulator lifecycle | `tests/Csharp2Md.Analysis.Tests/Pipeline/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| End-to-end (fixture analyze) | integration | Full pipeline over a real fixture; determinism; isolation; every regression case asserted on the published package, never on an in-memory DTO | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| CLI surface | unit | Every subcommand; exit-code mapping asserted per outcome class; CLI→Domain isolation | `tests/Csharp2Md.Cli.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"` |
| Analysis fixtures (`fixtures/CertificationCorpus`) | integration | Each fixture is proven, in the task that adds it, to produce the observations and facts the later tasks assert against | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Labeled corpora and engine certification | integration | Every certified area measured against ground truth; a flipped label asserted to fail certification | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Documentation and decision log | none | - (build gate only; no executable behaviour) | `.specs/**`, `docs/**` | build gate only |

## Gate Check Commands

> Generated from codebase — confirm before Execute. Multi-csproj `dotnet test` hits MSB1008; run each test project separately. `Category=LocalCorpus` is excluded from every gate; those tests run only after the Verifier and only when the local clones exist.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After unit-only tasks scoped to a single assembly | the matching project's command from the matrix above |
| Full | After tasks that cross Storage and Projection or touch the wire contract | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |
| Build | After phase completion, port changes, and every task touching Analysis, Domain or the CLI | `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |

**Measured baseline (2026-08-27, `524d73e`)**: `dotnet build -c Debug` clean, 0 warnings, 0 errors. **1723 tests, 1723 passing, 0 failing** — Domain 555, Analysis 662, Storage 289, Cli 33, Projection 184. No baseline repair phase is needed. Every task reports its new total; a drop means a silent deletion.

**Expected baseline movement**: Phase 6 changes the wire encoding and Phase 7 changes the manifest shape. Storage and Projection assertions that pin the current inlined form will need rewriting inside those tasks — that is a rewrite, never a deletion, and each task states the count it lands on.

---

## Tooling

Confirmed at task approval: the routing in [`CLAUDE.md`](CLAUDE.md) applies as written. No skill is invoked speculatively outside these routes.

| When | Skill |
| --- | --- |
| A task creates or changes a C# type | `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance` |
| A task changes a public surface consumed across assemblies | `dotnet-skills:csharp-api-design` |
| A task touches serialization or the wire contract | `dotnet-skills:serialization` |
| After a phase lands substantial new code | `dotnet-skills:slopwatch` |
| Before declaring the suite done | `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns` |
| Before the Verifier runs | `dotnet-test:test-gap-analysis` |
| Running gates | `dotnet-test:run-tests` |
| Diagnosing a build failure | `dotnet-msbuild:binlog-failure-analysis` |

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a phase execute in order.

### Phase 1: Certification corpus

Every later phase asserts against this tree. `fixtures/SyntheticSolution` stays byte-identical (AD-026).

```
T1 -> T2
T1 -> T3
T1 -> T4
T1 -> T5
T1 -> T6
```

### Phase 2: Supported-document policy and inventory correction

Shrinks the inventory before anything measures it, and fixes the two diagnostic false-positive classes.

```
T8 -> T9 -> T12
T6 -> T10
T9 -> T10 -> T11
T5 -> T13
T5 -> T14
```

### Phase 3: Entry capability

The registry move lands once, early. Closes audit blocker B2.

```
T15 -> T16 -> T17 -> T18
T2 -> T17
```

### Phase 4: Classifier corrections

Scoped evidence and full invocation accounting. Closes audit blocker B3 and removes 96.3% of the largest payload.

```
T19 -> T20 -> T21
T22 -> T23
T3 -> T23
```

### Phase 5: Coverage and certification

Needs Phase 4's disposition ledger to have real denominators. Closes audit blocker B1.

```
T25 -> T26 -> T27 -> T28 -> T30 -> T31
T23 -> T27
T18 -> T27
T27 -> T29 -> T30
T11 -> T29
```

### Phase 6: Wire v2 encoding and layout plan

The largest breakage, isolated. Nothing writes bytes until the plan exists (AD-023).

```
T21 -> T32 -> T34 -> T35 -> T36
T33 -> T35
```

### Phase 7: Publication, manifest and reader

Writes from the plan, publishes real cardinality and provenance, and makes the reader manifest-driven.

```
T36 -> T37 -> T38 -> T39 -> T41
T31 -> T39
T38 -> T40 -> T41
```

### Phase 8: Projections

Consumes the planner's shard-aware citations. Closes audit blockers B4 and B5 and finding I1.

```
T36 -> T42 -> T43 -> T45
T42 -> T44 -> T45
T43 -> T46 -> T47
T40 -> T47
```

### Phase 9: CLI

Needs a finished package shape to re-validate (AD-025).

```
T41 -> T48 -> T49
T47 -> T48 -> T50
T41 -> T51
T12 -> T52
T33 -> T52
```

### Phase 10: Engine certification

Independent of the rest; last so the classifiers are final.

```
T2 -> T53
T3 -> T53
T4 -> T53 -> T54 -> T55 -> T56
```

### Phase 11: Security, fidelity and contract recovery

Every new projection surface is a new place a redacted value could resurface, and contract recovery is what makes the messaging accounting usable.

```
T43 -> T57
T4 -> T57
T45 -> T58
```

### Phase 12: Determinism, isolation and batch certification

Shards, labels and provenance are all new inputs to the determinism guarantee. Batch certification needs `compose` to exist.

```
T47 -> T60 -> T61 -> T62
T51 -> T62
```

### Phase 13: Completion gate

Closes the roadmap.

```
T35 -> T63
T50 -> T63 -> T66
T50 -> T64
T56 -> T64 -> T65 -> T66
T62 -> T66
```

---

## Task Breakdown

### T1: Create the certification corpus solution skeleton

**What**: A new `.slnx` and the empty project tree the later fixture tasks fill, with no analysis content of its own.
**Where**: `fixtures/CertificationCorpus/CertificationCorpus.slnx`
**Depends on**: None
**Reuses**: `fixtures/SyntheticSolution` project layout and `Directory.Build.props` conventions
**Requirement**: GCPC-117

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:

- [x] The solution restores and builds under `net10.0`
- [x] A test analyzes the corpus end to end and asserts a committed package with a manifest
- [x] `fixtures/SyntheticSolution` is untouched
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 663 pass; total 1724 (Domain 555, Analysis 663, Storage 289, Cli 33, Projection 184)

**Tests**: integration
**Gate**: build

**Commit**: `test(fixtures): add the certification corpus solution skeleton`

---

### T2: Add the entry-point regression fixture

**What**: One controller carrying a routed action, a conventional action with no route attribute, and a private helper equivalent to `CatalogController.ChangeUriPlaceholder`.
**Where**: `fixtures/CertificationCorpus/Certification.Api/EntryPointShapes.cs`
**Depends on**: T1
**Reuses**: the controller shapes already exercised in `fixtures/SyntheticSolution`
**Requirement**: GCPC-025

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A test analyzes the corpus and asserts all three callables are inventoried as `Symbol` facts
- [x] The test records the current (defective) entry-point count as the documented pre-fix baseline, so T17 has something to invert
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 665 pass; total 1726 (Domain 555, Analysis 665, Storage 289, Cli 33, Projection 184)

**Tests**: integration
**Gate**: quick

**Commit**: `test(fixtures): add the private-helper entry-point regression`

---

### T3: Add the invocation regression fixture

**What**: A controller action calling an injected interface method whose single concrete implementation lives in another project of the same solution, plus two framework calls equivalent to `Ok` and `NotFound`.
**Where**: `fixtures/CertificationCorpus/Certification.Api/InvocationChain.cs`
**Depends on**: T1
**Reuses**: the cross-project call shape in `fixtures/SyntheticSolution`'s `ReceiverShapes`
**Requirement**: GCPC-018

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A test asserts three `Invocation` observations exist for the action
- [x] The test asserts the interface symbol and the concrete implementation are both inventoried, in different projects
- [x] The test records the current disposition set as the documented pre-fix baseline for T23
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 668 pass; total 1729 (Domain 555, Analysis 668, Storage 289, Cli 33, Projection 184)

**Tests**: integration
**Gate**: quick

**Commit**: `test(fixtures): add the interface-to-implementation invocation regression`

---

### T4: Add the contract regression fixture

**What**: A published event with a handler in the same solution, a published event with no handler, and two same-named payload types in unrelated projects.
**Where**: `fixtures/CertificationCorpus/Certification.Messaging/ContractShapes.cs`
**Depends on**: T1
**Reuses**: the `IEventBus` / `IIntegrationEventHandler` shapes already classified by `ContractPass`
**Requirement**: GCPC-087, GCPC-090

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A test asserts `MessageOperation` observations exist for both published events
- [x] The test asserts the two same-named payload types are distinct `Symbol` facts in different projects
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 670 pass; total 1731 (Domain 555, Analysis 670, Storage 289, Cli 33, Projection 184)

**Tests**: integration
**Gate**: quick

**Commit**: `test(fixtures): add the contract and message-operation regression`

---

### T5: Add the configuration and inventory regression fixture

**What**: A `.sln` carrying a solution folder and a genuinely missing project reference, plus `appsettings` documents covering comments, a trailing comma, a BOM, an unterminated object and a duplicate key.
**Where**: `fixtures/CertificationCorpus/ConfigurationShapes/`
**Depends on**: T1
**Reuses**: the `appsettings` handling already exercised by `ConfigurationDocumentReader`
**Requirement**: GCPC-103, GCPC-105

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A test asserts the `.sln` parses and names the solution folder entry separately from the missing project entry
- [x] The test records the current diagnostic set as the documented pre-fix baseline for T13 and T14
- [x] The BOM and duplicate-key documents are byte-exact, committed with their encoding preserved
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 677 pass; total 1738 (Domain 555, Analysis 677, Storage 289, Cli 33, Projection 184)

**Tests**: integration
**Gate**: quick

**Commit**: `test(fixtures): add the solution-folder and configuration-parsing regression`

---

### T6: Add the excluded-asset fixture

**What**: A C#-only project carrying, alongside its sources, a `.ts`, a `.js` with a `.map`, an image, a `.zip`, a `package-lock.json` and a `.pfx`.
**Where**: `fixtures/CertificationCorpus/Certification.WebAssets/`
**Depends on**: T1
**Reuses**: nothing; this is new fixture content
**Requirement**: GCPC-028, GCPC-032

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A test asserts every one of the six assets is currently inventoried as a `Document`, recording the pre-fix baseline T10 inverts
- [x] The `.pfx` is a self-signed throwaway carrying no real key material
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 678 pass; total 1739 (Domain 555, Analysis 678, Storage 289, Cli 33, Projection 184)

**Tests**: integration
**Gate**: quick

**Commit**: `test(fixtures): add the excluded-asset regression`

---

### T7: Add the SyntheticSolution immutability guard

**What**: A test that hashes the `fixtures/SyntheticSolution` tree and fails when it changes, so no later task silently re-baselines a prior workstream.
**Where**: `tests/Csharp2Md.Analysis.Tests/Fixtures/SyntheticSolutionImmutabilityTests.cs`
**Depends on**: None
**Reuses**: the deterministic tree-walk ordering used by the existing determinism tests
**Requirement**: GCPC-117

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] The test computes an order-independent digest over every tracked file under `fixtures/SyntheticSolution`
- [x] The expected digest is committed, and the failure message names the files that differ
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 679 pass; total 1740 (Domain 555, Analysis 679, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: quick

**Commit**: `test(fixtures): guard fixtures/SyntheticSolution against silent change`

---

### T8: Create ClassifierCapabilityRegistry

**What**: A registry letting each active classifier pass declare the document extensions it consumes, so the conditionally supported set is derived rather than hand-listed.
**Where**: `src/Csharp2Md.Analysis/Classification/ClassifierCapabilityRegistry.cs`
**Depends on**: None
**Reuses**: the `IClassifierPass` registration in `ClassificationAndPromotionStage.CreateDefault`
**Requirement**: GCPC-029

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] Each registered pass declares its consumed extensions; a pass declaring none contributes none
- [x] The registry's result is asserted to change when a pass is added or removed, not hardcoded
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 685 pass; total 1746 (Domain 555, Analysis 685, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): derive consumed document extensions from active classifiers`

---

### T9: Create SupportedDocumentPolicy

**What**: The accept-or-exclude decision with its category, covering the default supported set, the conditional set and the default excluded set.
**Where**: `src/Csharp2Md.Analysis/Inventory/SupportedDocumentPolicy.cs`
**Depends on**: T8
**Reuses**: `ClassifierCapabilityRegistry` from T8; the extension tests in `DocumentInventory.IsCSharpDocument` and `IsConfigurationDocument`
**Requirement**: GCPC-026, GCPC-029, GCPC-030

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Every accepted category and every excluded category has its own assertion, by category rather than by one sample
- [x] A conditional extension is asserted accepted only when a classifier declares it, and excluded when none does
- [x] The policy carries a version string used later by provenance
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 702 pass; total 1763 (Domain 555, Analysis 702, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add the supported-document policy`

---

### T10: Apply the policy in DocumentInventory

**What**: Excluded documents stop producing a `Document` fact and stop producing a per-document `unsupported-document` diagnostic; a single aggregated diagnostic replaces them.
**Where**: `src/Csharp2Md.Analysis/Inventory/DocumentInventory.cs`
**Depends on**: T9, T6
**Reuses**: `SupportedDocumentPolicy` from T9; the enumeration and authorized-root guard above `DocumentInventory.cs:98` are untouched
**Requirement**: GCPC-027, GCPC-028, GCPC-031, GCPC-033, GCPC-035

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Analyzing the T6 fixture asserts none of the six excluded assets appears in the manifest, in `facts/structural.json` or under `source/`
- [x] Exactly one aggregated diagnostic is published, naming the excluded count and every excluded extension
- [x] No individual `unsupported-document` diagnostic remains
- [x] The project's `.cs`, `.csproj` and `appsettings.json` are asserted still present with their configuration facts intact
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; Analysis 706 pass; total 1767 (Domain 555, Analysis 706, Storage 289, Cli 33, Projection 184)

**Tests**: integration
**Gate**: build

**Commit**: `feat(analysis): exclude unsupported documents from the inventory`

---

### T11: Publish the document policy report

**What**: Accepted and excluded counts and byte totals per policy category, carried on the snapshot to the package.
**Where**: `src/Csharp2Md.Analysis/Inventory/DocumentPolicyReport.cs`
**Depends on**: T10
**Reuses**: the `FactualSnapshot` diagnostics slot pattern established by AD-017
**Requirement**: GCPC-034

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] Counts and bytes are asserted per category against the T6 fixture's known contents
- [x] Accepted plus excluded equals the total enumerated document count
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; Analysis 712 pass; total 1773 (Domain 555, Analysis 712, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): publish accepted and excluded document totals per category`

---

### T12: Plumb the document allowlist

**What**: An explicit caller-supplied allowlist that re-includes named documents without admitting any other excluded class.
**Where**: `src/Csharp2Md.Analysis/AnalysisRequest.cs`
**Depends on**: T9
**Reuses**: `AnalysisRequest.Create` validation shape
**Requirement**: GCPC-030

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] An allowlisted `.ts` file is asserted inventoried while the sibling `.js` stays excluded
- [x] An allowlist entry naming a path outside the authorized root is rejected
- [x] An empty allowlist is asserted to change nothing
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 715 pass; total 1776 (Domain 555, Analysis 715, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): accept an explicit document allowlist`

---

### T13: Stop diagnosing solution folders as missing projects

**What**: Filter solution-folder entries by project type GUID when reading a `.sln`, so only real project references reach the missing-project branch.
**Where**: `src/Csharp2Md.Analysis/Inventory/SolutionFileReader.cs`
**Depends on**: T5
**Reuses**: the existing `SlnProjectPathPattern` regex at `SolutionFileReader.cs:50`, extended to capture the type GUID
**Requirement**: GCPC-103, GCPC-104

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] The T5 fixture asserts no diagnostic names the solution folder
- [x] The same fixture asserts the genuinely missing project is still diagnosed, naming the referencing solution and the missing path
- [x] A `.slnx` regression asserts folder elements were never affected
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 717 pass; total 1778 (Domain 555, Analysis 717, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: build

**Commit**: `fix(analysis): stop reporting solution folders as missing projects`

---

### T14: Apply the configuration parsing policy

**What**: Accept the syntax the .NET configuration provider accepts — line and block comments, trailing commas and a UTF-8 BOM — and reject duplicate keys, which the provider also rejects.
**Where**: `src/Csharp2Md.Analysis/Extraction/ConfigurationDocumentReader.cs`
**Depends on**: T5
**Reuses**: the existing parse-and-diagnose branch at `ConfigurationDocumentReader.cs:47`
**Requirement**: GCPC-105, GCPC-106, GCPC-107

**Tools**:

- MCP: `context7`
- Skill: `dotnet-skills:serialization`

**Done when**:

- [x] Comments, trailing comma and BOM documents each yield configuration bindings and no malformed diagnostic
- [x] The unterminated document is still diagnosed and yields no binding
- [x] The duplicate-key document is diagnosed and yields no binding, matching the provider's own rejection
- [x] The accepted syntax is documented as a stated policy string carried into provenance
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 724 pass; total 1785 (Domain 555, Analysis 724, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: build

**Commit**: `fix(analysis): align configuration parsing with the .NET provider`

---

### T15: Add the ExternallyReachable symbol facet

**What**: One new `SymbolFacet` value plus the regenerated `contracts/taxonomy-registry.json`, moving `taxonomy_version` to 2.
**Where**: `src/Csharp2Md.Domain/Facets/SymbolFacets.cs`
**Depends on**: None
**Reuses**: the `SymbolFacet.Abstract` precedent from AD-018 exactly
**Requirement**: GCPC-019

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] The `symbol-facet` axis publishes the new value and the registry projection is asserted byte-equal to the committed artifact
- [x] The AD-013 drift gate passes with declaration and artifact regenerated in this one commit
- [x] `taxonomy_version` is asserted to be 2 and the other four axes are asserted unchanged by this task
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count reported; Domain 560 pass; total 1790 (Domain 560, Analysis 724, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: build

**Commit**: `feat(domain): add the externally-reachable symbol facet`

---

### T16: Emit ExternallyReachable from the symbol emitter

**What**: Populate the new facet from the symbol's effective accessibility, accounting for containing types.
**Where**: `src/Csharp2Md.Analysis/Semantics/SymbolFactEmitter.cs`
**Depends on**: T15
**Reuses**: the existing `Facets(symbol)` construction path
**Requirement**: GCPC-020

**Tools**:

- MCP: `context7`
- Skill: NONE

**Done when**:

- [x] A public method on a public type carries the facet; a private method does not
- [x] A public method on an internal or private nested type does not carry it, proving effective rather than declared accessibility
- [x] Protected and internal cases are each asserted explicitly rather than folded together
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 730 pass; total 1796 (Domain 560, Analysis 730, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): emit effective external reachability on symbols`

---

### T17: Require proven entry capability in EntryPointPass

**What**: A callable becomes an `EntryPoint` only with positive entry-capability evidence; one that cannot be determined becomes a candidate or unresolved record instead.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/EntryPointPass.cs`
**Depends on**: T16, T2
**Reuses**: the existing controller and handler type detection; only the promotion predicate changes
**Requirement**: GCPC-020, GCPC-021, GCPC-022, GCPC-024

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] The T2 fixture asserts exactly two `EntryPoint` facts and that neither names the private helper
- [x] The conventional action with no route attribute is asserted still published, with its missing-route diagnostic preserved
- [x] A callable with undeterminable capability is asserted published as candidate or unresolved, never as a confirmed entry point
- [x] `fixtures/SyntheticSolution`'s entry-point expectations are asserted unchanged
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 733 pass; total 1799 (Domain 560, Analysis 733, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: build

**Commit**: `fix(analysis): require proven entry capability before promoting an entry point`

---

### T18: Publish entry-point capability evidence

**What**: Each published `EntryPoint` cites the evidence that proved its entry capability, by artifact key and ordinal.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/EntryPointPass.cs` (modify)
**Depends on**: T17
**Reuses**: the `derived_from` evidence-chain construction already used by `implements-operation`
**Requirement**: GCPC-023

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Every published entry point in the corpus cites a resolvable evidence record
- [x] The cited evidence is asserted present in the artifact at the cited ordinal, read from the published package
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; Analysis 734 pass; Storage 289 pass; total 1800 (Domain 560, Analysis 734, Storage 289, Cli 33, Projection 184)

**Tests**: integration
**Gate**: build

**Commit**: `feat(analysis): cite the evidence that proves an entry point`

---

### T19: Create EvidenceScope

**What**: A helper returning only the observations that justify a given promotion, replacing document-wide evidence chains.
**Where**: `src/Csharp2Md.Analysis/Classification/EvidenceScope.cs`
**Depends on**: None
**Reuses**: `EvidenceChain.Create`
**Requirement**: GCPC-039

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] A structural relation's scope is asserted to contain the declaration evidence and no `invocation` or `data-access` observation
- [x] A causal relation's scope is asserted to contain exactly the occurrence that produced it
- [x] An empty scope is rejected, since the taxonomy requires a non-empty `derived_from`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 739 pass; total 1805 (Domain 560, Analysis 739, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): scope evidence chains to the promotion they justify`

---

### T20: Scope the contains relation evidence

**What**: Replace the single document-wide chain reused for every `contains` edge with a per-edge scoped chain.
**Where**: `src/Csharp2Md.Analysis/Extraction/ContainsRelationEmitter.cs`
**Depends on**: T19
**Reuses**: `EvidenceScope` from T19, replacing the shared chain built at `ContainsRelationEmitter.cs:49`
**Requirement**: GCPC-039, GCPC-044

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A `Document contains Symbol` edge is asserted to cite the symbol's own declaration evidence and no unrelated observation
- [x] A `Project contains Document` edge is asserted to cite different evidence from the edges beneath it
- [x] The published `contains` payload's byte size for the corpus is recorded, and asserted to be a large reduction against the pre-fix figure recorded in the test
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; Analysis 742 pass; Storage 289 pass; total 1808 (Domain 560, Analysis 742, Storage 289, Cli 33, Projection 184). Corpus `contains.json`: 952,956 bytes pre-fix -> 390,576 bytes post-fix (~59% reduction).

**Tests**: integration
**Gate**: build

**Commit**: `fix(analysis): scope contains-relation evidence to each edge`

---

### T21: Scope the remaining passes' evidence

**What**: Adopt `EvidenceScope` in the classifier passes that still attach broader evidence than their promotion justifies.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/RelationPass.cs`
**Depends on**: T20
**Reuses**: `EvidenceScope` from T19
**Requirement**: GCPC-039

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Each adopting pass asserts its relation cites only justifying evidence, with one negative assertion naming an observation that must not appear
- [x] `belongs-to`'s published byte size is recorded and asserted reduced against the pre-fix figure
- [x] No relation is left with an empty `derived_from`
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; Analysis 745 pass; Storage 289 pass; total 1811 (Domain 560, Analysis 745, Storage 289, Cli 33, Projection 184). Corpus `belongs-to.json`: 110,874 bytes pre-fix -> 100,409 bytes post-fix.
- Deviation: the only remaining unscoped `EvidenceChain.Create(context.ObservationsByOwner(...))` call was `TopologyEmitter.cs`'s `belongs-to` construction, not `RelationPass.cs` (whose own broad-lookup fallbacks are protocol-scoped via `IsEvidenceKind` and legitimately need `Invocation`/`DataAccess` for outbound-HTTP/messaging evidence, so narrowing them would be a regression, not a fix). Fixed in `TopologyEmitter.cs` instead; `RelationPass.cs` is unchanged.

**Tests**: unit
**Gate**: build

**Commit**: `fix(analysis): scope the remaining classifier evidence chains`

---

### T22: Create the invocation disposition ledger

**What**: A record type and accumulator slot capturing one explicit disposition per recognized invocation occurrence.
**Where**: `src/Csharp2Md.Analysis/Classification/InvocationDisposition.cs`
**Depends on**: None
**Reuses**: the `SnapshotAccumulator` add-and-collect pattern used for candidates and unresolved records
**Requirement**: GCPC-011, GCPC-012

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] Every disposition kind is representable: confirmed, candidate, unresolved, open frontier and a categorized exclusion
- [x] Adding two dispositions for one occurrence is asserted to be detectable rather than silently overwriting
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 750 pass; total 1816 (Domain 560, Analysis 750, Storage 289, Cli 33, Projection 184)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add the invocation disposition ledger`

---

### T23: Give every InvokesPass branch an explicit disposition

**What**: Each of the five branches that currently `continue` without emitting a record instead records a disposition, so no occurrence disappears.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/InvokesPass.cs`
**Depends on**: T22, T3
**Reuses**: every existing branch; the silent `continue` statements at `InvokesPass.cs` lines 64, 91, 107, 162 and 168 gain a disposition before continuing
**Requirement**: GCPC-011, GCPC-013, GCPC-016, GCPC-017, GCPC-018

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] The T3 fixture asserts exactly one candidate link from the action to the concrete implementation, and no confirmed `invokes` to the interface member
- [x] The two framework calls are asserted present as counted exclusions carrying the declared category
- [x] The line-91 branch is asserted to emit a disposition rather than nothing, on a fixture case that reaches it
- [x] Every recognized occurrence in the corpus is asserted to carry exactly one disposition
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 753 pass; total 1819 (Domain 560, Analysis 753, Storage 289, Cli 33, Projection 184)
- Finding (not fixed, out of T23's scope): `ConcreteImplementors` matches by metadata name + parameter shape + arity only, with no exclusion of the calling method's own declaring type or check of return type. For the T3 fixture this produces a second, spurious self-referencing candidate (`OrderQueriesController.GetOrderStatus` -> itself) alongside the correct one to `Certification.Queries.OrderQueries.GetOrderStatus`. GCPC-018's own wording ("one candidate link per concrete implementing symbol") is technically satisfied since the code treats both as matches; the imprecision is in match *detection*, not disposition. Tests were scoped to assert "exactly one candidate to the concrete implementation" rather than "exactly one candidate total" to avoid silently accepting the bug while not expanding this task's surface to fix unrelated matching logic.

**Tests**: unit
**Gate**: build

**Commit**: `fix(analysis): give every recognized invocation an explicit disposition`

---

### T24: Publish inbound HTTP method and route

**What**: Fill `HttpMethod` and `Route` on an inbound HTTP boundary operation when both are proven, instead of leaving them for `protocol_operation_key` alone.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/BoundaryPass.cs`
**Depends on**: None
**Reuses**: `BoundaryOperation.HttpMethod` and `Route`, already populated on the outbound path by `CreateOutboundHttp`
**Requirement**: GCPC-099, GCPC-100, GCPC-102

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] An inbound action with an explicit verb attribute and route template publishes both fields
- [x] An action whose route is conventional only publishes the verb and records the route as unresolved
- [x] `protocol_operation_key` is asserted still published, and asserted no longer to be the only source of the verb
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Domain 563 pass; Analysis 756 pass; total 1825 (Domain 563, Analysis 756, Storage 289, Cli 33, Projection 184)
- Deviation: `BoundaryOperation.Create`'s `CreateInbound` factory (`src/Csharp2Md.Domain/Facts/Architecture/BoundaryFacts.cs`) discarded the `httpMethod`/`route` parameters entirely (hardcoded `null`) for every inbound operation, regardless of what `BoundaryPass.cs` passed in. `BoundaryPass.cs` alone could not have closed GCPC-099/102 without this Domain-level fix, so `CreateInbound` was changed to accept, validate (route must carry the `Route` literal role) and store both. Also extended the fix symmetrically to the reverse of the stated case (route proven, verb not, e.g. a verb-agnostic `[Route]` attribute) since GCPC-102's text covers either unproven part.

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): publish verb and route on inbound HTTP operations`

---

### T25: Define the coverage and certification envelopes

**What**: The v2 envelope shapes carrying evaluation state, per-reason counts, the status vocabulary and the accounting totals.
**Where**: `src/Csharp2Md.Storage/Wire/EnvelopeDtos.cs`
**Depends on**: None
**Reuses**: the existing `CoverageMetricDto` and `RunCertificationEnvelope` shapes, extended rather than replaced
**Requirement**: GCPC-002, GCPC-004, GCPC-009

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:serialization`

**Done when**:

- [x] `not_evaluated` is absent from the status vocabulary and a test asserts it cannot be serialized
- [x] Round-trip is asserted for every envelope, including the `not_applicable` metric form
- [x] The JSON Schemas under `contracts/json-schema/envelopes/` are updated and asserted to accept the new shapes and reject the old
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Storage 304 pass; Projection 184 pass; total 1840 (Domain 563, Analysis 756, Storage 304, Cli 33, Projection 184)
- Deviation: `CoverageMetricDto`/`RunCertificationEnvelope` are consumed outside `EnvelopeDtos.cs` (`DomainMapper.cs`, `FactualPackageReader.cs`, `PackageValidator.cs`, and two test files), so the new validated shapes forced minimal, compile-preserving call-site fixes in those files too — a hardcoded `not_evaluated`/zero-coverage placeholder becomes a valid `"degraded"` interim status until T31 wires in the real computation. `ProjectorPublicationTests.cs` needed the same array-vs-record-equality fix `ImmutableArray<T>`'s reference-based `Equals` already required elsewhere in this codebase.

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): define the coverage and certification envelope contracts`

---

### T26: Carry the envelopes on FactualSnapshot

**What**: The snapshot transports coverage, certification and the accounting ledgers from Analysis to Storage, the route AD-017 opened for diagnostics.
**Where**: `src/Csharp2Md.Analysis/Storage/FactualSnapshot.cs`
**Depends on**: T25
**Reuses**: the existing optional-slot construction pattern for `Diagnostics` and `SuspectedSecrets`
**Requirement**: GCPC-001

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] `Merge` is asserted to combine the new slots without double counting
- [x] A snapshot constructed without the new slots is asserted to keep its existing behaviour
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; Analysis 762 pass; Storage 304 pass; total 1846 (Domain 563, Analysis 762, Storage 304, Cli 33, Projection 184)
- Deviation: also touched `src/Csharp2Md.Analysis/Pipeline/SnapshotAccumulator.cs` (the same set-and-collect companion T11 already needed for `DocumentPolicyReport`, not newly introduced here) and added the four new report type names to `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisPublicSurfaceTests.cs`'s allowlist -- an existing gate that fails closed on any new public Analysis type, so it had to be updated in the same commit as the type it allowlists. Bundled all four new slots (coverage, certification, invocation accounting, contract accounting) in one pass per AD-024's own wording ("coverage, certification and the accounting ledgers ... travel on FactualSnapshot"), so T29/T30 only add computation, not further FactualSnapshot surgery.

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): carry coverage and certification on the snapshot`

---

### T27: Compute entry-point and linked-call coverage

**What**: A real Validation and Coverage stage at pipeline index 4 producing the first two metrics with enumerable denominators.
**Where**: `src/Csharp2Md.Analysis/Pipeline/ValidationAndCoverageStage.cs`
**Depends on**: T26, T23, T18
**Reuses**: replaces `ValidationAndCoverageStub`; registered the way `PipelineStages.CreateDefault` already replaces indices 0, 1, 2, 3 and 5
**Requirement**: GCPC-002, GCPC-003, GCPC-005

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] The test re-derives each denominator independently from the published facts and observations and asserts it equals the published value
- [x] Numerator plus exclusions plus unknowns is asserted never to exceed the denominator
- [x] No recall or precision percentage is published; a test asserts the envelope carries no such field
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 770 pass; total 1854 (Domain 563, Analysis 770, Storage 304, Cli 33, Projection 184)
- Deviation: none. Resumed and completed a prior worker's interrupted (uncommitted) draft of this file after verifying it matched the spec's denominator definitions in `docs/architecture/quality-and-security.md`; the only fix needed was a missing `using Csharp2Md.Analysis.Pipeline;` in the test file.

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): compute entry-point and linked-call coverage`

---

### T28: Compute contract and persistence coverage

**What**: The remaining two mandatory metrics, over the populations named in `quality-and-security.md`.
**Where**: `src/Csharp2Md.Analysis/Pipeline/ValidationAndCoverageStage.cs` (modify)
**Depends on**: T27
**Reuses**: the denominator-enumeration shape established by T27
**Requirement**: GCPC-002, GCPC-003

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Both denominators are independently re-derived in the test and asserted equal
- [x] The T4 fixture's unhandled event is asserted to sit in the contract denominator and not in its numerator
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 776 pass; total 1860 (Domain 563, Analysis 776, Storage 304, Cli 33, Projection 184)
- Deviation: none functional. Contract coverage's denominator is scoped to messaging boundary operations only (`BoundaryOperation.Protocol is Messaging`) -- HTTP boundary operations carry no mechanically recognized payload type anywhere in this codebase today, so they are not yet an enumerable population per GCPC-003. Persistence coverage is tested against `fixtures/SyntheticSolution/Acme.Orders` rather than the certification corpus, because the certification corpus contains no persistence fixture at all (its persistence_coverage there is legitimately `not_applicable`); Acme.Orders already exercises a resolved read/write/flush and one occurrence that must stay unresolved (`OrderSqlQueries.SelectAllFrom`'s run-time table name), so it is the only fixture that lets the "unknowns > 0" bullet be tested honestly.

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): compute contract and persistence coverage`

---

### T29: Publish the invocation and contract accounting ledgers

**What**: Per-disposition and per-outcome totals that sum exactly to the recognized occurrence and slot counts.
**Where**: `src/Csharp2Md.Analysis/Pipeline/InvocationAccounting.cs`
**Depends on**: T27, T11
**Reuses**: the disposition ledger from T22 and the document policy report from T11
**Requirement**: GCPC-012, GCPC-014, GCPC-015, GCPC-088, GCPC-089

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] The per-disposition totals are asserted to sum to the recognized invocation-occurrence count
- [x] An occurrence holding both an unresolved record and an open frontier is asserted counted exactly once
- [x] Message operations and payload slots are asserted to sum to their per-outcome counts
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 781 pass; total 1865 (Domain 563, Analysis 781, Storage 304, Cli 33, Projection 184)
- Deviation: `InvokesPass`'s disposition ledger is transient to one `Execute` call and an excluded occurrence (`external-framework-callable`, `duplicate-edge`) leaves no other published trace anywhere in the codebase today, so the ledger itself is the only place that split is still recoverable -- it cannot be re-derived from public facts the way T27/T28's coverage metrics are. `ClassificationAndPromotionStage.cs` (not named in this task's `Where`) was therefore touched to special-case capturing `InvokesPass`'s ledger via its internal `Execute(..., out ledger)` overload (already exposed for tests since T22) immediately after it runs, and to publish `ContractAccounting` (fully re-derivable from public facts, no ledger needed) once all passes finish. `InvocationAccounting.cs` holds both the `InvocationAccounting` and `ContractAccounting` static builders, since both are the accounting-ledger half of AD-024 this task publishes.

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): publish the invocation and contract accounting ledgers`

---

### T30: Compute the run certification status

**What**: The passed, degraded and failed rules, including the `not_applicable` metric case that makes certifying a zero-denominator package impossible.
**Where**: `src/Csharp2Md.Analysis/Pipeline/RunCertifier.cs`
**Depends on**: T28, T29
**Reuses**: the metrics from T27 and T28 and the ledgers from T29
**Requirement**: GCPC-001, GCPC-006, GCPC-007, GCPC-008, GCPC-009, GCPC-010, GCPC-013

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Each of the three statuses is asserted on an input that produces only that status
- [x] A library-only solution with no entry points is asserted `degraded` with `entry_point_coverage` `not_applicable` and a stated reason, never `passed`
- [x] An unaccounted occurrence is asserted to force `failed` and to be named
- [x] A quarantined derived fact is asserted to force `failed`
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Analysis 788 pass; total 1872 (Domain 563, Analysis 788, Storage 304, Cli 33, Projection 184)
- Deviation: "a classifier conflict affects a covered area" (GCPC-006/007/008) has no dedicated published record anywhere in this codebase -- taxonomy.md's "the conflict is published" is prose with no backing type. The only fact-level conflict this pipeline actually detects and publishes today is an identity collision (`SnapshotAccumulator.StructuralCorruption`/`CollidingIdentity`), so `RunCertifier` treats that single signal as covering both "a derived fact is quarantined" and "a classifier conflict affects a covered area" -- documented in `RunCertifier`'s own remarks rather than inventing a new conflict-tracking type. `ValidationAndCoverageStage.ExecuteAsync` (not named in this task's `Where`) was touched to call `RunCertifier.Certify` and `SetCertification` once coverage is computed, reusing the same `context.Accumulator.StructuralCorruption`/`CollidingIdentity` and `snapshot.InvocationAccounting` T29 already publishes.

**Tests**: unit
**Gate**: build

**Commit**: `feat(analysis): compute the run certification status`

---

### T31: Map the computed envelopes in DomainMapper

**What**: Replace the hardcoded zero coverage and `not_evaluated` status with the values the stage produced.
**Where**: `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`
**Depends on**: T30
**Reuses**: the existing envelope mapping path at `DomainMapper.cs:155-156`
**Requirement**: GCPC-001, GCPC-002

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A committed package's `coverage.json` and `run-certification.json` are read from disk and asserted to carry the computed values
- [x] `ZeroCoverage` and the `not_evaluated` literal are asserted absent from the assembly
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [x] Test count reported; Storage 306 pass; Analysis 788 pass; total 1874 (Domain 563, Analysis 788, Storage 306, Cli 33, Projection 184)
- Deviation: none beyond `DomainMapper.cs` itself (the `UnevaluatedCoverage` field is removed and replaced with `MapCoverage`/`MapCertification`, which fall back to a per-field `not_applicable`/`degraded` reason only when `snapshot.Coverage`/`.Certification` are literally absent -- a hand-built snapshot in an unrelated test, never a genuine analysis run). Note for the traceability record: GCPC-001 and GCPC-002 were marked Verified after T30 and T28 respectively on the strength of `ValidationAndCoverageStage`'s in-memory computation, but both ACs are phrased as "WHEN the system publishes ... coverage" / "WHEN a run publishes a package" -- until this task, `DomainMapper` still overwrote every computed value with the old hardcoded placeholder on the way to `coverage.json`/`run-certification.json`, so the published package did not actually carry them. This task is what makes those two verifications true end-to-end; no status change was needed since both rows already read Verified, but the gap is recorded here rather than left implicit.

**Tests**: integration
**Gate**: build

**Commit**: `fix(storage): publish computed coverage and certification`

---

### T32: Create the intern table and interned wire encoding

**What**: A per-artifact preamble of interned strings that records reference by index, removing the repeated identity, variant, classifier and facet strings.
**Where**: `src/Csharp2Md.Storage/Wire/InternTable.cs`
**Depends on**: T21
**Reuses**: `CanonicalJson.Write` for deterministic bytes
**Requirement**: GCPC-039, GCPC-044

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:serialization`

**Done when**:

- [x] An artifact's interned form is asserted to round-trip to the same records as the inlined form
- [x] Table ordering is asserted deterministic and independent of record order
- [x] The corpus's `contains` payload byte size is recorded and asserted reduced against the T20 figure
- [x] An artifact with no repeats is asserted to publish no wasteful table
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Storage 311 pass; Projection 184 pass; total 1879 (Domain 563, Analysis 788, Storage 311, Cli 33, Projection 184)
- Deviation: none. `InternTable` is a standalone, generic JSON-array transform (works on any repeated string leaf, not only the four named families) proven against a hand-built round-trip fixture and the real certification-corpus `contains.json`; it is not yet wired into `PackagePublisher` or `LayoutPlanner` — that wiring is out of this task's named `Where` and no T33-T41 "Done when" bullet requires it, so GCPC-039 stays partially closed (canonical/wire payload families only, catalogs/postings are Phase 8) and GCPC-044 stays Pending per the traceability note (the scale input hasn't run yet).

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): intern repeated identities in the wire encoding`

---

### T33: Create the ceiling calculator

**What**: Derive the per-artifact byte ceiling from the declared per-scenario reading budget and the measured bytes-per-token ratio, and publish the calculation with its inputs.
**Where**: `src/Csharp2Md.Storage/Mapping/CeilingCalculator.cs`
**Depends on**: None
**Reuses**: `ShardWriter.DefaultCeilingBytes` as the value being replaced
**Requirement**: GCPC-036, GCPC-037

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] The derivation is asserted reproducible: same budget and ratio give the same ceiling
- [x] A different declared budget is asserted to move the ceiling, proving it is derived and not a constant
- [x] The token estimator is a declared deterministic function and its identifier is published
- [x] The published calculation names its inputs, so a consumer can re-derive the number
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; Storage 316 pass; total 1884 (Domain 563, Analysis 788, Storage 316, Cli 33, Projection 184)
- Deviation: none. `CeilingCalculator` is a standalone, tested derivation; it is not yet consumed by `ShardWriter`/`LayoutPlanner` or written into the manifest -- that wiring is T35's and T39's own named `Where`, not this task's. At the declared defaults (100,000-token budget, 25 file reads, the measured 8.192 bytes/token ratio for this project's indented canonical JSON) it derives to exactly 32 KiB, matching design.md's Tech Decisions table.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): derive the per-artifact byte ceiling from the reading budget`

---

### T34: Plan artifact keys and record ordinals

**What**: The layout planner computes every artifact key and every record's ordinal from the validated wire document, before any byte is written.
**Where**: `src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs`
**Depends on**: T32
**Reuses**: the slot enumeration currently in `PublishedPackageView.From`
**Requirement**: GCPC-040, GCPC-041

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] Every fact and relation in a document is asserted to have exactly one planned location
- [x] The plan is asserted identical across two runs over the same document
- [x] The plan is asserted to be computed without writing or reading any file
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count reported; Storage 320 pass; total 1888 (Domain 563, Analysis 788, Storage 320, Cli 33, Projection 184)
- Deviation: none beyond scope. `LayoutPlan`/`LayoutPlanner.Plan(WireDocument)` plans every family (compound fact bundles get one unsplit artifact each; confirmed relations, candidates, unresolved records, open frontiers and observations get a real per-record `PlannedRecord` list) but this task does not yet split anything -- that is T35's own named `Where`. GCPC-040 and GCPC-041 are both conditioned on "WHEN an artifact IS split", which cannot happen yet, so neither is marked Verified here; GCPC-040 is revisited at T35 (which actually produces splits) and GCPC-041 at T36 (whose own "Done when" is its literal text), even though this task's `Requirement` field names both.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): plan artifact keys and ordinals before publication`

---

### T35: Add adaptive bucket-depth sharding to the plan

**What**: Extend the bucket prefix until every bucket fits the derived ceiling, and route canonical payloads through the same rule as catalogs and postings.
**Where**: `src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs` (modify)
**Depends on**: T33, T34
**Reuses**: `ShardWriter.BucketKey`, generalized from a fixed one-byte prefix to adaptive depth
**Requirement**: GCPC-038, GCPC-039, GCPC-042, GCPC-043

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] An over-ceiling payload is asserted split, and every shard asserted within the ceiling
- [x] Bucket assignment is asserted derived from the fact id and never from a display name
- [x] Two runs are asserted to assign every record to the same shard
- [x] A single record larger than the ceiling is asserted published in its own shard with a recorded degradation reason, never truncated
- [x] No directory is named after a fact identity
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Storage 325 pass; Projection 184 pass; total 1893 (Domain 563, Analysis 788, Storage 325, Cli 33, Projection 184)
- Deviation: scope is confined to the flat record-array families (confirmed relations per kind, candidates, unresolved records, open frontiers, observations per kind) -- the compound fact-family bundles (`facts/structural.json` etc.) are not split by this task, staying single unsharded artifacts regardless of ceiling, because splitting them would change their JSON shape from an object of sub-arrays to a flat array, a wire-schema break outside this task's named `Where`. GCPC-042 and GCPC-043 are fully satisfied for every family this planner shards (deterministic SHA-256-of-identity bucketing, no new directory, only a filename suffix mirroring `ShardWriter`'s existing pattern) and are marked Verified. GCPC-038 stays Pending: the degradation-reason escape hatch is the documented exception for an irreducible single record (design.md's Risks & Concerns table), but the compound fact-family bundles remain wholly unenforced against the ceiling, so "no artifact exceeds the ceiling" is not yet universally true. GCPC-039 stays Pending per the batch's own partial-closure note.

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): shard canonical payloads with adaptive bucket depth`

---

### T36: Rebuild PublishedPackageView over the plan

**What**: `TryLocate` and `TryLocateRelation` return shard-aware citations naming the shard key and the ordinal within it.
**Where**: `src/Csharp2Md.Storage/Mapping/PublishedPackageView.cs`
**Depends on**: T35
**Reuses**: the existing `TryLocate` signature and `ArtifactCitation` shape, unchanged for callers
**Requirement**: GCPC-041

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Every citation is asserted to resolve to the record it claims, on both a split and an unsplit payload
- [x] Existing projector callers are asserted to compile and behave unchanged against the new view
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Storage 328 pass; Projection 184 pass; total 1896 (Domain 563, Analysis 788, Storage 328, Cli 33, Projection 184)
- Deviation: `LayoutPlanner.cs` (not this task's named `Where`) needed a fix, not only `PublishedPackageView.cs`. T34/T35's `LayoutPlan` never carried the envelope artifacts (`contracts/taxonomy-registry.json`, `coverage.json`, `diagnostics.json`, `measurements.json`, `run-certification.json`) or the final alphabetical sort that the pre-T36 `PublishedPackageView.From` applied to its whole slot list -- rebuilding the view straight off `Plan.Slots` without this would have silently dropped those five artifacts from every publication and reordered the rest, breaking `PackagePublisher`/`ManifestBuilder` the moment T36 landed, before T37 even runs. Fixed minimally in `LayoutPlanner.Plan` (add the five envelope artifacts unconditionally, sort the complete list ordinally) rather than reintroducing them in `PublishedPackageView` itself, so the plan stays the single source of the artifact list AD-023 calls for. `PublishedPackageView.From(WireDocument)` (the existing single-argument overload every current caller uses) now delegates to `LayoutPlanner.Plan(document, int.MaxValue)` -- an effectively unbounded ceiling -- so it keeps producing the exact unsplit shape those callers already depend on; a new `From(WireDocument, LayoutPlan)` overload lets a caller (T37) supply an already-computed, possibly-sharded plan so the writer and its citations are always built from the same one plan object. Verified by running the full existing Storage (328), Projection (184) and Analysis (788, informal extra check beyond this task's own gate) suites unmodified and green. Traceability: GCPC-041 is this task's own field and is now Verified; GCPC-040 (attached to T34's field, deferred there since nothing split yet) is also flipped Verified here, since its "WHEN an artifact is split" text needs both T35's preservation-under-split and this task's citation-resolvability-under-split together, and both now exist.

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): make package citations shard-aware`

---

### T37: Write payloads from the plan

**What**: `PackagePublisher` emits each planned artifact instead of writing whole families by fixed key.
**Where**: `src/Csharp2Md.Storage/Mapping/PackagePublisher.cs`
**Depends on**: T36
**Reuses**: the existing per-key `Write` switch, driven by the plan rather than by `view.Slots`
**Requirement**: GCPC-038, GCPC-040

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Every planned artifact is asserted written exactly once, and nothing outside the plan is written
- [x] Manifest-last publication order is asserted preserved
- [x] An abort is asserted to leave the prior package byte-identical
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Storage 331 pass; Projection 184 pass; total 1899 (Domain 563, Analysis 788, Storage 331, Cli 33, Projection 184)
- Deviation (significant, three files beyond this task's named `Where`): making `PackagePublisher.ToPublicationOrder` plan-driven required threading the *same* `LayoutPlan` through `PublicationPipeline.cs` and fixing `ProjectionValidator.cs`, or the change would have silently broken every existing projector. Specifics: (1) `PublicationPipeline.Publish` built the projector/composer `view` from `PublishedPackageView.From(report.Document)` *separately* from what `PackagePublisher.ToPublicationOrder` would plan, so a citation minted for the view could point to a shard `ToPublicationOrder` never wrote -- fixed by computing one `LayoutPlan` in `Publish` and passing it to both. (2) `ProjectionValidator.AuthoritativeText` re-derived a citation's text via `PackagePublisher.Write(document, citation.ArtifactKey)`, which only knows the never-split compound-family keys; a shard key (e.g. a hypothetical `relations/confirmed/contains.a1.json`) would throw. Fixed by reading straight from `view.Plan`'s `PlannedRecord` when the artifact is one of the plan's record-array families, falling back to `Write` only for the compound families that stay whole. (3) Discovered by running the fuller regression net beyond this task's own gate (Analysis, Cli, Domain -- 12 initial failures): `LayoutPlanner.PlanFamily`'s *unsplit* branch was sorting every flat family by record identity before writing, when the pre-existing `Write()` wrote them in the document's own order; `CatalogProjector.AddUnknowns` and `PostingProjector.AddIndexed` address `relations/unresolved.json`/`relations/frontiers.json` by that natural ordinal directly, bypassing the citation lookup, so the reorder silently pointed existing projections at the wrong record. Fixed in `LayoutPlanner.cs` (already this task's dependency, touched again here): the unsplit case now keeps natural document order byte-for-byte identical to the pre-plan writer; only an actually-split family sorts within its buckets, which is new and has no caller depending on the old order. (4) Also reverted `ToPublicationOrder`'s and `PublicationPipeline.Publish`'s own single-arg call sites from the derived real ceiling back to an effectively unbounded one (`int.MaxValue`) -- turning on the real ~32 KiB ceiling for every live analysis broke a wide swath of Analysis-layer tests relying on families staying under one un-split key, which is out of this batch's scope (baseline note: "Domain/Analysis/Cli should not move"). The sharding mechanism itself stays fully implemented and tested against an explicit ceiling (T35's own tests); activating it for the live `analyze` pipeline by default is left to whichever later task (CLI budget option, or similar) deliberately wires `CeilingCalculator`'s value in. Re-ran the full pre-existing Analysis (788), Cli (33) and Domain (563) suites after each fix and confirmed zero regressions and unchanged counts, beyond this task's own required gate. GCPC-038 stays Pending (same reasoning as T35, reinforced: the live default pipeline does not even attempt real ceiling enforcement in this batch).

**Tests**: integration
**Gate**: full

**Commit**: `feat(storage): publish payloads from the planned layout`

---

### T38: Publish real cardinality in the manifest

**What**: Each manifest entry carries the artifact's actual top-level entry count and byte size, including projection artifacts.
**Where**: `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs`
**Depends on**: T37
**Reuses**: the entry construction at `ManifestBuilder.cs:30`, taking the count from the plan rather than a slot lookup that misses projections
**Requirement**: GCPC-061

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] For every manifest entry, the cited artifact is opened and its real entry count and byte size are asserted equal to the manifest's values
- [x] A projection artifact holding records is asserted to carry a non-zero count
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Storage 333 pass; Projection 184 pass; total 1901 (Domain 563, Analysis 788, Storage 333, Cli 33, Projection 184)
- Deviation: `ManifestEntry` (`src/Csharp2Md.Storage/Wire/EnvelopeDtos.cs`, not this task's named `Where`) had no byte-size field at all, so publishing one required adding `ByteSize` there -- exactly the shape design.md's own DTO sketch already specifies (`ManifestEntry(string CanonicalKey, string Role, int Count, long ByteSize, string Path)`), so this is filling in an already-planned field, not inventing one. That in turn required regenerating the committed `contracts/json-schema/envelopes/manifest.json` (added `byte_size` to `properties` and `required`) to keep `JsonSchemaDriftTests` green -- the same "regenerate both in one commit" pattern T15 used for the taxonomy registry. Count is now derived per-fragment rather than from a `view.Slots` lookup: a plan-known artifact (including a split family) keeps the plan's own accurate count; an artifact the plan never saw (a catalog, posting, label or other projection) gets its real count by parsing its own bytes (a JSON array's length, or a JSON object's own array properties summed when at least one exists, one otherwise). A deferred fragment (`SourceProjector`'s raw source copies) is deliberately excluded from this byte-parsing path -- `StagedFragment.ReadPayload()` can be materialized only once, and the transactional store still owns that one read for writing the file to disk; calling it again here to measure size would throw. Its count falls back to the plan (zero, since it is not a plan artifact) and its byte size is published as zero, a known, narrow limitation called out here rather than silently accepted: these are raw text blobs anyway, not JSON record sets, so GCPC-061's "top-level entry count" has no natural meaning for them. Did NOT bump `schema_version` (still 1) despite design.md's version-axis table attributing that bump partly to this exact change -- `TaxonomyVersions.Initial` is a Domain-layer constant with its own hardcoded-`1` tests and a registry drift gate wired to it; bumping it is out of this batch's Storage/Projection scope (baseline note: "Domain/Analysis/Cli should not move") and is left to whichever later task owns that axis.

**Tests**: unit
**Gate**: full

**Commit**: `fix(storage): publish real counts and byte sizes in the manifest`

---

### T39: Publish generator provenance

**What**: The manifest carries generator version, build identity, all five version axes and the deterministic parameters that shaped the package.
**Where**: `src/Csharp2Md.Storage/Wire/ProvenanceDto.cs`
**Depends on**: T38, T31
**Reuses**: `TaxonomyVersions.Initial` for the version axes and the policy version string from T9 and T14
**Requirement**: GCPC-056, GCPC-057, GCPC-058, GCPC-059, GCPC-060

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:serialization`

**Done when**:

- [x] Two runs of the same build over the same input are asserted to publish byte-identical provenance
- [x] No timestamp or duration appears in provenance; a test asserts they remain only in `measurements.json`
- [x] All five version axes are asserted present with the values this feature moved them to
- [x] The derived ceiling, document-policy version and allowlist digest are asserted present
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Storage 341 pass; Projection 184 pass; total 1909 (Domain 563, Analysis 788, Storage 341, Cli 33, Projection 184)
- Deviation (two files beyond this task's named `Where`): `ManifestEnvelope` (`EnvelopeDtos.cs`) had nowhere to carry a provenance block, so it gained an optional trailing `ProvenanceDto? Provenance = null` (default keeps the two pre-existing positional `ManifestEnvelope(...)` call sites -- `DomainMapper.cs`'s placeholder and a Projection test fixture -- compiling unchanged), and `ManifestBuilder.From` (already this task's dependency) now calls `ProvenanceDto.Current()`. Also regenerated the committed `contracts/json-schema/envelopes/manifest.json` for the new `provenance` property (same "regenerate both together" pattern as T38's `byte_size`). `GeneratorVersion` comes from the loaded assembly's own version (`Directory.Build.props`'s `<Version>`); `BuildIdentity` from its module version id (MVID), reproducible because the .NET SDK's deterministic-build default gives identical source the identical MVID -- no git/SourceLink dependency needed, and both are trivially byte-identical across two calls in the same process, which is what the "two runs" test can actually exercise. Version axes are read straight from `TaxonomyTables.Default.Versions` (whatever they currently are) rather than hardcoded expected numbers, so the test holds regardless of whether every axis was already bumped by earlier phases; two of the five (`extractor_set_version`, `classifier_set_version`) are still `1` today even though design.md's version-axis table says this feature moves them to `2` -- that bump belongs to `Csharp2Md.Domain`'s `TaxonomyVersions.Initial`, out of this Storage/Projection-scoped batch, and is not made here. `DocumentPolicyVersion` and `AllowlistDigest` are the two fields not fully wired to a real per-run value: `SupportedDocumentPolicy.Version` (Analysis-internal, T9) has no public channel onto `WireDocument` yet, so `ProvenanceDto` carries a hand-kept-in-step string constant instead of referencing it; the allowlist an `analyze` run actually used (T12) similarly never reaches Storage today (there is no CLI entry point for it in this batch either -- that is T52's job), so every publication reports `ProvenanceDto.EmptyAllowlistDigest`, a real, deterministic digest, but only correct for the always-empty-allowlist case. Both fields are "present" as GCPC-058 requires, but not yet driven by the real analysis; GCPC-058 is marked Pending rather than Verified for this reason.

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): publish generator provenance in the manifest`

---

### T40: Make the package reader manifest-driven

**What**: `FactualPackageReader` enumerates the manifest instead of reading fixed keys, and additionally returns the projection fragments it finds.
**Where**: `src/Csharp2Md.Storage/FactualPackageReader.cs`
**Depends on**: T38
**Reuses**: the existing `ReadOptionalObject` and `ReadOptionalArray` helpers, driven by the manifest
**Requirement**: GCPC-064

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A sharded package is asserted to read back to the same document as its unsharded equivalent
- [x] The reader is asserted to open no file the manifest does not list
- [x] Projection fragments are returned alongside the document
- [x] The `not_evaluated` and zero-coverage read defaults are removed
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count reported; Storage 346 pass; Projection 184 pass; total 1914 (Domain 563, Analysis 788, Storage 346, Cli 33, Projection 184)
- Deviation: `PackageReadResult` (`src/Csharp2Md.Storage/PackageReadResult.cs`, not this task's named `Where`) needed a new `Projections` field to carry the returned fragments -- there was nowhere else to put them, and its only construction site is `FactualPackageReader.Read` itself, so the change is source-compatible everywhere else. The reader no longer trusts `File.Exists` as a proxy for "the manifest lists this": every read (`ReadOptionalObject`/`ReadRequiredObject`/`ReadShardedArray`) now checks manifest membership first and only then opens the file, and the corpus of every artifact actually opened is exactly the manifest's own `Artifacts` list (proved by the stray-file test: an undeclared, deliberately malformed JSON file sitting in the package directory is never touched). A record-array family is merged from every shard the manifest lists for it (its base key plus any `base.<bucket>.json` alongside it, concatenated in path order) -- proven equivalent to the unsplit case by content (relation target ids), not by position, since sharding is free to reorder records across files. `coverage.json` and `run-certification.json` are now required (`ReadRequiredObject`, throwing `PublicationRejectedException("missing-artifact", path)` if the manifest omits them) rather than silently defaulting to the removed `not_evaluated`/all-zero placeholder -- every package this pipeline actually publishes always carries both (T31/T35), so this only changes behavior for a hand-corrupted package, which is exactly what T41's own validation work will formalize. Any manifest-listed artifact the core document does not consume (a catalog, a posting, a label, a source copy, or anything Projection ever adds) is returned in `PackageReadResult.Projections`, generically -- Storage does not need to know Projection's key names to do this. GCPC-064 stays Pending: this closes the reader half only; the `validate` CLI verb that would let a caller invoke it without a solution present is Phase 9's own task (T48), per the traceability note.

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): drive the package reader from the manifest`

---

### T41: Add manifest and provenance checks to PackageValidator

**What**: Manifest counts and sizes agree with real content, every file is reachable and declared, and provenance compatibility is checked — asserted at publication and on re-validation alike.
**Where**: `src/Csharp2Md.Storage/Validation/PackageValidator.cs`
**Depends on**: T39, T40
**Reuses**: the existing `Validate` entry point and rejection-reason shape
**Requirement**: GCPC-061, GCPC-062, GCPC-065, GCPC-071

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A manifest count that disagrees with its artifact is rejected, naming the artifact and both values
- [ ] A file present but undeclared, and a file declared but absent, are each rejected
- [ ] A provenance version newer than the running generator is rejected with its own reason code
- [ ] Every new rejection is asserted to leave a previously committed package byte-identical
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): validate manifest cardinality and provenance compatibility`

---

### T42: Create LabelProjector

**What**: Compact labels for component, type, method, protocol, verb and route, each derived from a proven payload value and citing where it came from.
**Where**: `src/Csharp2Md.Projection/Labels/LabelProjector.cs`
**Depends on**: T36
**Reuses**: `PublishedPackageView.TryLocate` for citations and `RedactionEnvelope` for the safety check
**Requirement**: GCPC-093, GCPC-094, GCPC-098, GCPC-084

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Every emitted label is asserted equal to the value at the artifact key and ordinal it cites
- [ ] An unproven value is asserted to yield no label rather than an inferred one
- [ ] A value inside a declared redacted span is asserted to yield no label
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): derive compact labels from the factual authority`

---

### T43: Carry labels in the catalogs

**What**: `CatalogEntryDto` gains its label collection and the catalog projector fills it.
**Where**: `src/Csharp2Md.Projection/Catalogs/CatalogProjector.cs`
**Depends on**: T42
**Reuses**: `LabelProjector` from T42; the existing catalog ordering and shard rules
**Requirement**: GCPC-093, GCPC-095

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Every catalog entry's labels are asserted resolvable to the artifact and ordinal they cite
- [ ] Catalog ordering is asserted unchanged, still keyed on the fact id
- [ ] The canonical id is asserted still present as the identity
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): carry compact labels in the catalogs`

---

### T44: Title Markdown pages by their label

**What**: A page leads with its compact label and keeps the canonical id as an identity line, so no page is titled by fact type plus an encoded id.
**Where**: `src/Csharp2Md.Projection/Markdown/MarkdownProjector.cs`
**Depends on**: T42
**Reuses**: the existing page assembly; only the heading at `MarkdownProjector.cs:112` and the identity line change
**Requirement**: GCPC-096, GCPC-101

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] No published page's title consists solely of a fact type plus an encoded identity
- [ ] An HTTP boundary operation page is asserted to show the published verb and route, and asserted not to synthesize either
- [ ] Page bytes are asserted identical across two runs
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): title pages by their proven label`

---

### T45: Validate labels against the payload

**What**: A label disagreeing with the authoritative payload aborts publication and names the page and value.
**Where**: `src/Csharp2Md.Storage/Validation/ProjectionValidator.cs`
**Depends on**: T43, T44
**Reuses**: the existing `EnsureValueMatches` path, extended to the label fields
**Requirement**: GCPC-097

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A label altered by one character is asserted to abort publication, naming the offender
- [ ] A label citing a missing key, and one citing an out-of-range ordinal, are each asserted to abort
- [ ] The prior package is asserted byte-identical after each abort
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): fail publication on a label that contradicts the payload`

---

### T46: Rewrite the retrieval guide

**What**: A guide that starts at catalogs, teaches bucket selection, covers all seven relation kinds and all three proof states in their own artifacts, and states the five stopping rules.
**Where**: `src/Csharp2Md.Projection/Guides/RetrievalGuideProjector.cs`
**Depends on**: T43
**Reuses**: the existing scenario enumeration, repointed from payloads to catalogs and postings
**Requirement**: GCPC-046, GCPC-047, GCPC-048, GCPC-049, GCPC-050, GCPC-051, GCPC-055

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Each scenario's first step is asserted to name a catalog, and asserted never to name a canonical payload artifact
- [ ] Each of `executes`, `implements-operation`, `invokes`, `uses-contract`, `accesses-data`, `operates-on` and `targets` is asserted to have its own documented path naming the artifact that holds it
- [ ] Candidates, unresolved records and open frontiers each have their own documented path
- [ ] All five stopping rules are documented
- [ ] A guide naming an artifact key absent from the publication is asserted to abort
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): rewrite the retrieval guide around catalogs and postings`

---

### T47: Execute and measure the documented scenarios

**What**: One runner over two artifact sources — staged fragments during `analyze`, the on-disk package during `validate` — reporting files, hops, bytes, tokens, relevant facts and noise.
**Where**: `src/Csharp2Md.Storage/Retrieval/RetrievalScenarioRunner.cs`
**Depends on**: T46, T40
**Reuses**: `FactualPackageReader` from T40 as the on-disk source
**Requirement**: GCPC-052, GCPC-053, GCPC-054

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Every scenario documented in the published guide is executed and asserted to reach its stated endpoint
- [ ] Each scenario's measured reads, bytes and tokens are asserted within the declared budget and published to `measurements.json`
- [ ] Both artifact sources are asserted to produce the same report for the same package
- [ ] A documented path that does not resolve is asserted to fail the run
- [ ] A scenario with no instance in the input is asserted reported not exercised, never passed
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: full

**Commit**: `feat(storage): execute and measure the documented retrieval scenarios`

---

### T48: Add the validate subcommand

**What**: `validate --package` re-hydrates a published package and re-runs the publication validators, touching no solution.
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`
**Depends on**: T41, T47
**Reuses**: `FactualPackageReader`, `PackageValidator` and `ProjectionValidator` — no new validator is introduced (AD-025)
**Requirement**: GCPC-063, GCPC-064, GCPC-066, GCPC-067

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] `validate` runs to completion in a process with no access to the original solution directory
- [ ] No Roslyn or MSBuild type is loaded during `validate`; a test asserts it
- [ ] The published certification status is reported and asserted not recomputed from a solution
- [ ] A directory with no manifest is rejected and left unchanged
- [ ] `Csharp2Md.Cli` is asserted to still declare no `Csharp2Md.Domain` reference
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: unit
**Gate**: build

**Commit**: `feat(cli): add the validate subcommand`

---

### T49: Add the corrupted-package corpus

**What**: One deliberately corrupted package per defect class, so detection strength is proven by inputs rather than by a second implementation.
**Where**: `tests/Csharp2Md.Storage.Tests/Corruption/CorruptedPackageFactory.cs`
**Depends on**: T48
**Reuses**: a committed corpus package as the clean baseline each corruption mutates
**Requirement**: GCPC-065, GCPC-066

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Seven corruptions exist: bad hash, wrong count, dangling reference, out-of-range ordinal, out-of-bounds locator, broken link, mismatched provenance
- [ ] Each is asserted rejected, naming the artifact key, the defect class and the offending value
- [ ] Each corruption is asserted to differ from the clean package in exactly the intended way
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: full

**Commit**: `test(storage): add one corrupted package per defect class`

---

### T50: Map the outcome classes onto exit codes

**What**: The stable exit-code space across all three subcommands.
**Where**: `src/Csharp2Md.Cli/ExitCodes.cs`
**Depends on**: T48
**Reuses**: the existing `0`, `1` and `2` meanings, extended rather than renumbered
**Requirement**: GCPC-069, GCPC-070, GCPC-071, GCPC-072, GCPC-073

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Each of `0`, `1`, `2`, `3`, `4`, `5` and `6` is asserted on an invocation that produces only that outcome
- [ ] The existing `0`, `1` and `2` meanings are asserted unchanged against the current CLI tests
- [ ] An invalid invocation is asserted to publish nothing
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: unit
**Gate**: build

**Commit**: `feat(cli): map outcome classes onto stable exit codes`

---

### T51: Add the compose subcommand

**What**: `compose` rebuilds contributions from published packages and produces the batch artifacts with no solution present.
**Where**: `src/Csharp2Md.Storage/ContributionReader.cs`
**Depends on**: T41
**Reuses**: `SolutionContribution` and `BatchComposer.Compose` unchanged; only the contribution source changes
**Requirement**: GCPC-068

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] `compose` over published packages is asserted to produce byte-identical batch artifacts to those `analyze` produced
- [ ] No solution file is opened; a test asserts it
- [ ] A package directory missing from the output root is asserted to yield an incomplete-scope batch
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: build

**Commit**: `feat(cli): add the compose subcommand over published packages`

---

### T52: Expose the allowlist and budget options on analyze

**What**: `analyze` accepts the document allowlist and the declared reading budget, and rejects malformed values before any analysis begins.
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs` (modify)
**Depends on**: T12, T33
**Reuses**: the allowlist from T12 and the budget from T33; the existing `Invalid` helper for argument rejection
**Requirement**: GCPC-030, GCPC-036

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] An allowlist path outside the authorized root is rejected with exit `1` and nothing is published
- [ ] A non-positive budget is rejected with exit `1`
- [ ] The supplied budget is asserted to reach the published provenance
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: unit
**Gate**: build

**Commit**: `feat(cli): accept the document allowlist and reading budget`

---

### T53: Author the labeled corpora

**What**: Ground-truth data files recording, per item, its source, its expected present, absent or unresolved state and the rationale — authored against the source, never from classifier output.
**Where**: `fixtures/CertificationCorpus/labels/`
**Depends on**: T2, T3, T4
**Reuses**: the regression fixtures as the labeled subjects
**Requirement**: GCPC-074, GCPC-075, GCPC-076

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Positives, negatives and lookalikes exist for each certified classifier
- [ ] Every label carries a source reference and a written rationale
- [ ] A test asserts each labeled item's construct still exists in the fixture, so a stale label fails rather than silently dropping
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: quick

**Commit**: `test(certification): author the labeled corpora`

---

### T54: Measure precision and recall per certified area

**What**: The engine-certification runner comparing classifier output to ground truth for each area.
**Where**: `tests/Csharp2Md.Analysis.Tests/Certification/EngineCertificationRunner.cs`
**Depends on**: T53
**Reuses**: the labeled corpora from T53 and the existing fixture analyze harness
**Requirement**: GCPC-077, GCPC-080

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Precision and recall are computed per area from the label files, not from classifier output
- [ ] A test asserts no precision or recall value is written into any run's package
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: quick

**Commit**: `test(certification): measure precision and recall against ground truth`

---

### T55: Apply the normative engine thresholds

**What**: Entry points 99 and 95, linked calls 99 and 90, contracts 99 and 95, persistence 99 and 90 — with failing items named.
**Where**: `tests/Csharp2Md.Analysis.Tests/Certification/EngineThresholdTests.cs`
**Depends on**: T54
**Reuses**: the runner from T54
**Requirement**: GCPC-078, GCPC-079

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Each area is asserted at or above its normative threshold
- [ ] A deliberately flipped ground-truth label is asserted to fail certification and to name that item, proving the measurement reads ground truth
- [ ] A failing area names the measured value alongside the threshold
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: quick

**Commit**: `test(certification): enforce the normative precision and recall thresholds`

---

### T56: Publish the engine certification report

**What**: A versioned repository artifact recording per-area precision, recall and thresholds — not a CLI subcommand.
**Where**: `artifacts/verifications/engine-certification.md`
**Depends on**: T55
**Reuses**: the measurements from T54 and T55
**Requirement**: GCPC-081

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] The report is regenerated by the suite and asserted to match the measured values
- [ ] A test asserts no `certify` subcommand and no engine-certification flag exists on the CLI
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: build

**Commit**: `docs(certification): publish the engine certification report`

---

### T57: Make contract producers and consumers recoverable in one hop

**What**: Postings that answer "who publishes this contract" and "who consumes it" from the contract identity, without scanning a relation payload.
**Where**: `src/Csharp2Md.Projection/Postings/PostingProjector.cs`
**Depends on**: T43, T4
**Reuses**: the existing contract producer and consumer posting groups, repointed at the shard-aware citations
**Requirement**: GCPC-089, GCPC-091, GCPC-092

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] The T4 fixture's handled event is asserted to reach both its producer and its consumer in one posting hop from the contract identity
- [ ] The unhandled event is asserted to publish an explicit unresolved or excluded outcome and no contract
- [ ] A payload slot whose contract identity is unproven is asserted published as candidate or unresolved, never as a contract
- [ ] The published diagnostics and catalogs are asserted never to state that messaging is absent when only the contract is
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): recover contract producers and consumers in one hop`

---

### T58: Sweep every published byte for secrets

**What**: A whole-package sweep asserting no secret literal and no individual secret hash appears anywhere, labels and shards included, with the redaction model intact.
**Where**: `tests/Csharp2Md.Projection.Tests/Security/SecretAbsenceTests.cs`
**Depends on**: T45
**Reuses**: `SecretRedactor` and `RedactionEnvelope`; the corpus configuration document carrying a secret
**Requirement**: GCPC-082, GCPC-083, GCPC-086

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] The secret's literal value is asserted absent from every published byte of every artifact, scanned by walking the package rather than a listed subset
- [ ] The secret's individual hash is asserted absent from every published byte
- [ ] The sidecar is asserted to still declare `redacted`, the ordinal-sorted spans, the original hash and the published hash
- [ ] A corpus secret whose value would otherwise become a label is asserted to produce no label
- [ ] The coverage, provenance and accounting envelopes are asserted to carry no credential, connection string, token, certificate or authorization value
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: full

**Commit**: `test(projection): sweep every published byte for secrets`

---

### T59: Guard the analysis trust boundary

**What**: A regression guard asserting source generators still require separate consent and diagnostic analyzers never run (AD-003).
**Where**: `tests/Csharp2Md.Analysis.Tests/Semantics/TrustBoundaryTests.cs`
**Depends on**: None
**Reuses**: the existing workspace construction in `MsBuildWorkspaceFactory` and `CompilationSanitizer`
**Requirement**: GCPC-085

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Analyzing without explicit consent is asserted to run no source generator
- [ ] Diagnostic analyzers are asserted never to execute, on a fixture project that declares one
- [ ] The guard fails if the consent flag is removed or defaulted on
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: build

**Commit**: `test(analysis): guard the source-generator and analyzer trust boundary`

---

### T60: Prove whole-package determinism

**What**: Two runs, two absolute paths and reversed input order produce byte-identical factual, projection, provenance and composition artifacts.
**Where**: `tests/Csharp2Md.Analysis.Tests/Determinism/WholePackageDeterminismTests.cs`
**Depends on**: T47
**Reuses**: the existing determinism harness, extended to shards, labels and provenance
**Requirement**: GCPC-108, GCPC-109, GCPC-110

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Two runs over the same input are asserted byte-identical across every published artifact, compared file by file rather than by count
- [ ] The same repository analyzed from two different absolute paths is asserted byte-identical
- [ ] Reversed `--solution` order and shuffled document enumeration order are each asserted byte-identical
- [ ] Shard assignment, label content and provenance are each explicitly included in the comparison
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: build

**Commit**: `test(analysis): prove whole-package determinism across runs, paths and order`

---

### T61: Prove solution isolation in a batch

**What**: A multi-solution batch keeps each solution's semantics local; no solution's facts enter another's package.
**Where**: `tests/Csharp2Md.Analysis.Tests/Determinism/BatchIsolationTests.cs`
**Depends on**: T60
**Reuses**: the two-solution batch harness from `multi-solution-composition`
**Requirement**: GCPC-111

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Every fact in each package is asserted to carry its own solution's identity, with no identity from the sibling solution present
- [ ] A single-solution run and the same solution inside a batch are asserted to produce byte-identical package contents
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: build

**Commit**: `test(analysis): prove semantic isolation between batched solutions`

---

### T62: Certify the batch or declare incomplete scope

**What**: A batch is certified only when every required solution is published, provenance-compatible and individually certifiable; otherwise it names the reason and is not certified.
**Where**: `src/Csharp2Md.Storage/Validation/BatchValidator.cs`
**Depends on**: T61, T51
**Reuses**: the existing `BatchView.Complete` and `IncompleteScopeReason` shape, extended with the certification and provenance conditions
**Requirement**: GCPC-112, GCPC-113, GCPC-114

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A batch whose solutions are all published, compatible and certifiable is asserted certified
- [ ] An unpublished solution, a provenance-incompatible solution and a non-certifiable solution are each asserted to produce an uncertified batch with its own named reason
- [ ] Every committed per-solution package is asserted byte-identical to the fully successful run after an incomplete batch
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: build

**Commit**: `feat(storage): certify a batch only when every required solution qualifies`

---

### T63: Generate the over-ceiling scale input

**What**: A deterministic generator producing, at test time, an input large enough that the three named payload kinds each exceed the derived ceiling.
**Where**: `tests/Csharp2Md.Storage.Tests/Scale/ScaleInputGenerator.cs`
**Depends on**: T35, T50
**Reuses**: the layout planner from T35; no generated output is committed
**Requirement**: GCPC-038, GCPC-043, GCPC-044, GCPC-045

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] `contains`, `belongs-to` and the invocation observations are each asserted split
- [ ] Every file in the resulting package is asserted within the published ceiling
- [ ] No directory is named after a fact identity
- [ ] Every catalog and posting citation is asserted still to resolve after the split
- [ ] A re-run is asserted to assign every record to the same shard
- [ ] The largest artifact per role, with its byte size and token estimate, is asserted published
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: full

**Commit**: `test(storage): prove sharding and budgets on a generated scale input`

---

### T64: Build the LLM-readiness checklist evaluator

**What**: An objective evaluator whose criteria correspond to the audit's readiness matrix, runnable against any package.
**Where**: `tests/Csharp2Md.Analysis.Tests/Readiness/LlmReadinessChecklist.cs`
**Depends on**: T50, T56
**Reuses**: the published package as its only input, the way the original audit worked
**Requirement**: GCPC-115, GCPC-116, GCPC-120

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Every criterion the audit rated PARTIAL or FAIL — semantic legibility, scale, factual coverage, run certification, classification reliability, overall readiness — reports PASS on a package built from the certification corpus
- [ ] Each criterion's verdict cites the evidence that produced it
- [ ] The evaluator runs with the eShop clones absent
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: build

**Commit**: `test(readiness): evaluate the LLM-readiness checklist on the corpus`

---

### T65: Wire the optional LocalCorpus run

**What**: The same checklist runs against an eShop clone when its expected solution file exists, and reports a named skip when it does not.
**Where**: `tests/Csharp2Md.Analysis.Tests/Readiness/LocalCorpusReadinessTests.cs`
**Depends on**: T64
**Reuses**: the `Category=LocalCorpus` convention and the existing clone-presence check
**Requirement**: GCPC-118, GCPC-119

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] The test is asserted excluded by `Category!=LocalCorpus` and therefore absent from every gate
- [ ] With the clone absent, the skip is asserted reported with the missing path named, never silently assumed
- [ ] No mandatory gate is asserted to depend on the clone
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; total ≥ previous task's total

**Tests**: integration
**Gate**: build

**Commit**: `test(readiness): run the checklist on the local corpus when present`

---

### T66: Record the decisions and close the roadmap

**What**: AD-023 through AD-027 in `.specs/STATE.md`, the amended versioned-fixture constraint, and the roadmap's final row marked complete.
**Where**: `.specs/STATE.md`
**Depends on**: T63, T65, T62
**Reuses**: the existing `AD-NNN` entry format and the Handoff snapshot shape
**Requirement**: GCPC-120

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] AD-023 through AD-027 are recorded with decision, reason, trade-off, scope, date and status
- [ ] The standing single-fixture constraint is amended to admit `fixtures/CertificationCorpus`
- [ ] `architecture-knowledge-engine-roadmap.md` marks workstream 8 complete and the completion conditions checked
- [ ] `python3 .claude/skills/tlc-spec-driven/scripts/validate_state.py generator-cli-projections-certification` exits clean
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Full suite reported green with its final total

**Tests**: none
**Gate**: build

**Commit**: `docs(specs): record the certification decisions and close the roadmap`

---

## Phase Execution Map

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 7 → Phase 8 → Phase 9 → Phase 10 → Phase 11 → Phase 12 → Phase 13

Phase 1:   T1 -> T2
           T1 -> T3
           T1 -> T4
           T1 -> T5
           T1 -> T6

Phase 2:   T8 -> T9 -> T12
           T6 -> T10
           T9 -> T10 -> T11
           T5 -> T13
           T5 -> T14

Phase 3:   T15 -> T16 -> T17 -> T18
           T2 -> T17

Phase 4:   T19 -> T20 -> T21
           T22 -> T23
           T3 -> T23

Phase 5:   T25 -> T26 -> T27 -> T28 -> T30 -> T31
           T23 -> T27
           T18 -> T27
           T27 -> T29 -> T30
           T11 -> T29

Phase 6:   T21 -> T32 -> T34 -> T35 -> T36
           T33 -> T35

Phase 7:   T36 -> T37 -> T38 -> T39 -> T41
           T31 -> T39
           T38 -> T40 -> T41

Phase 8:   T36 -> T42 -> T43 -> T45
           T42 -> T44 -> T45
           T43 -> T46 -> T47
           T40 -> T47

Phase 9:   T41 -> T48 -> T49
           T47 -> T48 -> T50
           T41 -> T51
           T12 -> T52
           T33 -> T52

Phase 10:  T2 -> T53
           T3 -> T53
           T4 -> T53 -> T54 -> T55 -> T56

Phase 11:  T43 -> T57
           T4 -> T57
           T45 -> T58

Phase 12:  T47 -> T60 -> T61 -> T62
           T51 -> T62

Phase 13:  T35 -> T63
           T50 -> T63 -> T66
           T56 -> T64 -> T65 -> T66
           T50 -> T64
           T62 -> T66
```

Execution is strictly sequential - there is no intra-phase parallelism. A single agent (or batch worker) works one task at a time, in order.

T7 and T24 carry no dependency and no incoming arrow: T7 is a standalone guard test, and T24 modifies a pass no earlier task in this feature touches.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1–T7 | 1 fixture tree or 1 test file each | ✅ Granular |
| T8, T9, T11, T19, T22, T25 | 1 new type each | ✅ Granular |
| T10, T12–T18, T20, T21, T23, T24 | 1 file modified each | ✅ Granular |
| T26–T31 | 1 type or 1 metric pair each | ✅ Granular |
| T32–T36 | 1 encoding, 1 calculator, 2 planner concerns, 1 view | ✅ Granular |
| T37–T41 | 1 file each | ✅ Granular |
| T42–T47 | 1 projector or validator concern each | ✅ Granular |
| T48–T52 | 1 subcommand, 1 corpus, 1 exit map, 1 reader, 1 option pair | ✅ Granular |
| T53–T56 | 1 corpus, 1 runner, 1 threshold suite, 1 report | ✅ Granular |
| T57–T59 | 1 posting concern, 1 sweep suite, 1 guard suite | ✅ Granular |
| T60–T62 | 1 determinism suite, 1 isolation suite, 1 validator concern | ✅ Granular |
| T63–T66 | 1 generator, 1 evaluator, 1 optional suite, 1 decision log | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | none | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T1 | T1 → T3 | ✅ Match |
| T4 | T1 | T1 → T4 | ✅ Match |
| T5 | T1 | T1 → T5 | ✅ Match |
| T6 | T1 | T1 → T6 | ✅ Match |
| T7 | None | none | ✅ Match |
| T8 | None | none | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T9, T6 | T9 → T10, T6 → T10 | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T9 | T9 → T12 | ✅ Match |
| T13 | T5 | T5 → T13 | ✅ Match |
| T14 | T5 | T5 → T14 | ✅ Match |
| T15 | None | none | ✅ Match |
| T16 | T15 | T15 → T16 | ✅ Match |
| T17 | T16, T2 | T16 → T17, T2 → T17 | ✅ Match |
| T18 | T17 | T17 → T18 | ✅ Match |
| T19 | None | none | ✅ Match |
| T20 | T19 | T19 → T20 | ✅ Match |
| T21 | T20 | T20 → T21 | ✅ Match |
| T22 | None | none | ✅ Match |
| T23 | T22, T3 | T22 → T23, T3 → T23 | ✅ Match |
| T24 | None | none | ✅ Match |
| T25 | None | none | ✅ Match |
| T26 | T25 | T25 → T26 | ✅ Match |
| T27 | T26, T23, T18 | T26 → T27, T23 → T27, T18 → T27 | ✅ Match |
| T28 | T27 | T27 → T28 | ✅ Match |
| T29 | T27, T11 | T27 → T29, T11 → T29 | ✅ Match |
| T30 | T28, T29 | T28 → T30, T29 → T30 | ✅ Match |
| T31 | T30 | T30 → T31 | ✅ Match |
| T32 | T21 | T21 → T32 | ✅ Match |
| T33 | None | none | ✅ Match |
| T34 | T32 | T32 → T34 | ✅ Match |
| T35 | T33, T34 | T33 → T35, T34 → T35 | ✅ Match |
| T36 | T35 | T35 → T36 | ✅ Match |
| T37 | T36 | T36 → T37 | ✅ Match |
| T38 | T37 | T37 → T38 | ✅ Match |
| T39 | T38, T31 | T38 → T39, T31 → T39 | ✅ Match |
| T40 | T38 | T38 → T40 | ✅ Match |
| T41 | T39, T40 | T39 → T41, T40 → T41 | ✅ Match |
| T42 | T36 | T36 → T42 | ✅ Match |
| T43 | T42 | T42 → T43 | ✅ Match |
| T44 | T42 | T42 → T44 | ✅ Match |
| T45 | T43, T44 | T43 → T45, T44 → T45 | ✅ Match |
| T46 | T43 | T43 → T46 | ✅ Match |
| T47 | T46, T40 | T46 → T47, T40 → T47 | ✅ Match |
| T48 | T41, T47 | T41 → T48, T47 → T48 | ✅ Match |
| T49 | T48 | T48 → T49 | ✅ Match |
| T50 | T48 | T48 → T50 | ✅ Match |
| T51 | T41 | T41 → T51 | ✅ Match |
| T52 | T12, T33 | T12 → T52, T33 → T52 | ✅ Match |
| T53 | T2, T3, T4 | T2 → T53, T3 → T53, T4 → T53 | ✅ Match |
| T54 | T53 | T53 → T54 | ✅ Match |
| T55 | T54 | T54 → T55 | ✅ Match |
| T56 | T55 | T55 → T56 | ✅ Match |
| T57 | T43, T4 | T43 → T57, T4 → T57 | ✅ Match |
| T58 | T45 | T45 → T58 | ✅ Match |
| T59 | None | none | ✅ Match |
| T60 | T47 | T47 → T60 | ✅ Match |
| T61 | T60 | T60 → T61 | ✅ Match |
| T62 | T61, T51 | T61 → T62, T51 → T62 | ✅ Match |
| T63 | T35, T50 | T35 → T63, T50 → T63 | ✅ Match |
| T64 | T50, T56 | T50 → T64, T56 → T64 | ✅ Match |
| T65 | T64 | T64 → T65 | ✅ Match |
| T66 | T63, T65, T62 | T63 → T66, T65 → T66, T62 → T66 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1–T6 | Analysis fixtures | integration | integration | ✅ OK |
| T7 | Inventory and document policy | unit | unit | ✅ OK |
| T8, T9, T12 | Inventory and document policy | unit | unit | ✅ OK |
| T10, T11 | Inventory and document policy | unit / integration | integration, unit | ✅ OK |
| T13, T14 | Inventory and document policy | unit | unit | ✅ OK |
| T15 | Domain facets and registry | unit | unit | ✅ OK |
| T16, T17 | Classifier passes | unit | unit | ✅ OK |
| T18 | End-to-end (fixture analyze) | integration | integration | ✅ OK |
| T19–T24 | Classifier passes | unit | unit, integration (T20) | ✅ OK |
| T25 | Storage wire mapping | unit | unit | ✅ OK |
| T26–T30 | Coverage and certification stage | unit | unit | ✅ OK |
| T31 | End-to-end (fixture analyze) | integration | integration | ✅ OK |
| T32 | Storage wire mapping and intern tables | unit | unit | ✅ OK |
| T33–T36 | Layout planner and sharding | unit | unit | ✅ OK |
| T37 | Package validation and manifest | unit + integration | integration | ✅ OK |
| T38–T41 | Package validation and manifest | unit + integration | unit | ✅ OK |
| T42–T46 | Projection (pure, no I/O) | unit | unit | ✅ OK |
| T47 | Retrieval scenario runner | integration | integration | ✅ OK |
| T48, T50, T52 | CLI surface | unit | unit | ✅ OK |
| T49 | Package validation and manifest | unit + integration | integration | ✅ OK |
| T51 | CLI surface + package validation | integration | integration | ✅ OK |
| T53–T56 | Labeled corpora and engine certification | integration | integration | ✅ OK |
| T57 | Projection (pure, no I/O) | unit | unit | ✅ OK |
| T58 | Projection + package validation | integration | integration | ✅ OK |
| T59 | Pipeline stage / orchestrator | integration | integration | ✅ OK |
| T60, T61 | End-to-end (fixture analyze) | integration | integration | ✅ OK |
| T62 | Package validation and manifest | unit + integration | integration | ✅ OK |
| T63 | Layout planner and sharding | unit + integration | integration | ✅ OK |
| T64, T65 | End-to-end (fixture analyze) | integration | integration | ✅ OK |
| T66 | Documentation and decision log | none | none | ✅ OK — matrix says none for this layer |
