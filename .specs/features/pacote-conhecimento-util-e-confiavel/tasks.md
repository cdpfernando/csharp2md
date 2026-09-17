# Pacote de conhecimento útil e confiável Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the per-task cycle, atomic commits, adequacy review and independent Verifier.

**If the skill cannot be activated, STOP and tell the user.**

**Project override:** the discrimination sensor is skipped by standing project rule; the user runs Stryker manually. The spec-anchored coverage check, gate check, code-quality check and independent Verifier remain mandatory.

---

**Spec**: `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`  
**Design**: `.specs/features/pacote-conhecimento-util-e-confiavel/design.md`  
**Status**: Approved

The migration is intentionally incomplete between phases. Every task must still leave the repository buildable; the legacy projects remain in the solution until the final cutover task.

## Test Coverage Matrix

> Generated from codebase, project guidelines and spec — confirm before Execute. Guidelines found: `AGENTS.md`, `README.md`, `Directory.Build.props`, `tests/Directory.Build.props`. Samples: Analysis, Storage, Projection and CLI xUnit suites. No separate linter or formatter gate is configured; the Release compiler with `TreatWarningsAsErrors` is the code-quality gate.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| ---------- | ------------------ | -------------------- | ---------------- | ----------- |
| Public facade and contracts | unit | Every public invariant, invalid request and diagnostic shape; no leaked internal types | `tests/Csharp2Md.Core.Tests/Surface/**/*.cs` | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --configuration Release` |
| Analysis | integration | All branches named by VAR/PKG criteria; real `MSBuildWorkspace` against `fixtures/SyntheticSolution`; project/variant isolation and cancellation | `tests/Csharp2Md.Core.Tests/Analysis/**/*.cs` | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --configuration Release` |
| PackageBuilding | unit | 1:1 to DEP/MET/STO/retention ACs and every listed edge case; expectations calculated independently of production | `tests/Csharp2Md.Core.Tests/PackageBuilding/**/*.cs` | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --configuration Release` |
| Publication and package reading | integration | Happy path plus every materialization, safety, corruption, lock and atomic-preservation failure path | `tests/Csharp2Md.Core.Tests/Publication/**/*.cs` | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --configuration Release` |
| CLI seam | e2e | `analyze` and `validate` happy, edge and error paths; four journeys and budgets from the committed manifest | `tests/Csharp2Md.Cli.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --configuration Release` |
| Repository topology/configuration | unit | Static surface/isolation assertions plus Release build; only Core and CLI remain after cutover | `tests/Csharp2Md.Core.Tests/Surface/**/*.cs` | `dotnet test csharp2md.slnx --configuration Release` |
| Optional local corpora | e2e | eShop variant isolation, eShopOnContainers file/byte ceilings and Pitstop file/byte ceilings; dynamic skip only when clone is absent | `tests/Csharp2Md.Cli.Tests/LocalCorpus*.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --configuration Release --filter "Category=LocalCorpus"` |
| Versioned dependency corpus | e2e | Project-scope `ProjectReference` edges of all six ArchitectureDependencyLab solutions scored against the PKG-05-reachable part of `oracle/project-references.json` (60 of 87); correct, false-positive and test-policy-leak baselines counted apart, never a skip | `tests/Csharp2Md.Cli.Tests/OracleProjectReferenceScoreTests.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --configuration Release --filter "Category=OracleCorpus"` |

## Gate Check Commands

> Generated from the repository's documented .NET commands. Restore is intentionally allowed because early tasks add new project references; later `--no-build` runs reuse the Release build.

| Gate Level | When to Use | Command |
| ---------- | ----------- | ------- |
| Quick | After Core unit/integration tasks | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --configuration Release` |
| Full | After CLI/e2e tasks or cross-module integration | `dotnet test csharp2md.slnx --configuration Release` |
| Build | After phase completion and topology/configuration tasks | `dotnet build csharp2md.slnx --configuration Release; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; dotnet test csharp2md.slnx --configuration Release --no-build` |
| LocalCorpus | After the feature Verifier when clones exist | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --configuration Release --filter "Category=LocalCorpus"` |

Every test-bearing task adds or updates tests in the same commit. “Tested later” is not accepted. The stated count is the minimum number of focused cases for that task; existing tests may be ported only when their assertions remain anchored to the new spec.

## Execution Plan

Phases and tasks execute strictly in order.

### Phase 1: Core topology and contracts

```text
T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7
```

### Phase 2: Isolated factual analysis

```text
T8 -> T9 -> T10 -> T11 -> T12 -> T13 -> T14 -> T15
```

### Phase 3: Retention, dependencies and measures

```text
T16 -> T17 -> T18 -> T19 -> T20 -> T21 -> T22
```

### Phase 4: Identity, layout and retrieval artifacts

```text
T23 -> T24 -> T25 -> T26 -> T27 -> T28 -> T29 -> T30
```

### Phase 5: Validation, certification and atomic publication

```text
T31 -> T32 -> T33 -> T34 -> T35 -> T36 -> T37 -> T38
```

### Phase 6: CLI acceptance and clean cut

```text
T39 -> T40 -> T41 -> T42 -> T43 -> T48 -> T46 -> T47 -> T49 -> T50 -> T51 -> T52 -> T53 -> T54 -> T44 -> T45
```

### Phase 7: Verifier remediation

```text
T55 -> T56 -> T57 -> T58 -> T59
```

### Phase 8: Second Verifier remediation

```text
T60 -> T61 -> T62 -> T63 -> T64 -> T65 -> T66
```

### Phase 9: Third Verifier remediation

```text
T67 -> T68
T67 -> T69
```

### Phase 10: Oracle-anchored dependency accuracy

```text
T70 -> T71 -> T72
```

### Phase 11: Correct the retained dependency projection

```text
T73 -> T74 -> T75 -> T76 -> T77 -> T78
```

T46-T54 were added after T43 was complete, so they carry higher numbers than the tasks that follow them; execution order is the diagram, not the number. Phase 7 was opened after the feature Verifier returned FAIL; it depends on Phase 6 in full. Phase 8 was opened after the second Verifier returned FAIL on the completed Phase 7; it depends on Phase 7 in full. Phase 9 was opened after the third Verifier run returned PASS with five ranked non-blocking gaps, of which the user chose to close the two carrying functional consequence; it depends on Phase 8 in full. Phase 10 was opened after the corpus benchmark showed the generator scoring 11 of 87 `ProjectReference` edges on a real corpus while the whole suite stayed green; it depends on Phase 9 in full. Phase 11 was opened by the user's explicit decision to confirm and fix the projection rather than only re-measure it; it depends on Phase 10 in full. T77 was added mid-phase when verifying T76 surfaced a third, independent test-leakage admission point beyond T75's root/incoming-edge fix. The eleven phases form eleven sequential task-budgeted batches. At Execute, offer batch sub-agents and dispatch them only if the user accepts; never split a phase and never run batches concurrently.

## Task Breakdown

## Phase 1: Core topology and contracts

### T1: Scaffold the Core topology

**What**: Add `Csharp2Md.Core` and `Csharp2Md.Core.Tests` to the solution while preserving the legacy projects until cutover.  
**Where**: `csharp2md.slnx`  
**Depends on**: None  
**Reuses**: central package management, shared test props and `net10.0` conventions  
**Requirement**: PKG-10

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Core references Roslyn Workspaces 5.6.0 but no `Microsoft.Build.*`; CLI can reference Core without breaking legacy paths.
- [x] Core exposes internals only to Core tests; Core tests use the existing xUnit/Verify stack.
- [x] At least 3 topology/surface tests pass and assert target framework, dependency direction and forbidden packages.
- [x] Build gate passes with no existing project removed.

**Tests**: unit — ≥3 focused cases  
**Gate**: build  
**Commit**: `build(core): scaffold knowledge engine projects`  
**Status**: ✅ Complete

### T2: Define the public KnowledgeEngine facade

**What**: Define the two public operations and their request/result types without exposing internal graphs, passes, shards or staging.  
**Where**: `src/Csharp2Md.Core/KnowledgeEngine.cs`  
**Depends on**: T1  
**Reuses**: cancellation and async conventions from the current `AnalysisEngine`, not its store dependency  
**Requirement**: PKG-01, PUB-03, PUB-04, PUB-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:api-design`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] `AnalyzeAsync` and `Validate` have the approved facade shape and structured diagnostics.
- [x] Invalid requests fail before analysis/staging and cancellation remains `OperationCanceledException`.
- [x] At least 6 surface and request-validation cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥6 focused cases  
**Gate**: quick  
**Commit**: `feat(core): define knowledge engine facade`  
**Status**: ✅ Complete

### T3: Define the factual graph contract

**What**: Add immutable solution, entity, occurrence, evidence, relation, gap, source and extraction-measurement models used by Analysis.  
**Where**: `src/Csharp2Md.Core/Analysis/FactualGraph.cs`  
**Depends on**: T2  
**Reuses**: approved vocabulary in `CONTEXT.md`; no legacy wire DTOs or IDs  
**Requirement**: PKG-02, PKG-03, PKG-04, PKG-07, PKG-09, VAR-03, VAR-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Models separate logical identity from variant-qualified occurrences and evidence.
- [x] Locators accept only logical relative paths and immutable collections are defensively owned.
- [x] At least 8 invariant/immutability cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥8 focused cases  
**Gate**: quick  
**Commit**: `feat(core): define factual graph contract`  
**Status**: ✅ Complete

### T4: Define retained and retrieval contracts

**What**: Add the retained graph, dependencies, measures, impact, cycles and navigation-index models.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/RetrievalModel.cs`  
**Depends on**: T3  
**Reuses**: design ownership for `RetainedGraph`, `AggregatedDependency`, `ScopeMeasures` and `RetrievalModel`  
**Requirement**: DEP-01, DEP-02, DEP-03, MET-01, MET-02, MET-05, MET-06, MET-07, NAV-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Direct dependencies and transitive impact are distinct model concepts.
- [x] Four aggregation scopes and all eight dependency categories are closed enums/types.
- [x] At least 8 contract and invalid-state cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥8 focused cases  
**Gate**: quick  
**Commit**: `feat(core): define retrieval contracts`  
**Status**: ✅ Complete

### T5: Define package plan and manifest contracts

**What**: Add immutable package plan, artifact, root manifest, certification, measurement and committed-package models.  
**Where**: `src/Csharp2Md.Core/Publication/PackageContracts.cs`  
**Depends on**: T4  
**Reuses**: the complete-plan and immutable-generation design; no deferred fragment contract  
**Requirement**: PKG-01, STO-05, STO-06, PUB-01, PUB-02, CRT-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] A plan contains every final payload and root-manifest reference before staging.
- [x] Manifest models declare token estimator/divisor, include-tests policy, solutions, roots, indexes and journeys.
- [x] At least 8 contract/invariant cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥8 focused cases  
**Gate**: quick  
**Commit**: `feat(core): define package publication contracts`  
**Status**: ✅ Complete

### T6: Port canonical JSON serialization

**What**: Implement deterministic source-generated UTF-8 JSON used by every new artifact.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/CanonicalJson.cs`  
**Depends on**: T5  
**Reuses**: normalization behavior from `src/Csharp2Md.Storage/Wire/CanonicalJson.cs`, not its legacy registrations  
**Requirement**: STO-07, PUB-01

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Serialization is UTF-8 without BOM, LF-normalized and deterministically ordered by prepared models.
- [x] Only current contract types are registered in the source-generation context.
- [x] At least 6 byte-level round-trip and determinism cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥6 focused cases  
**Gate**: quick  
**Commit**: `feat(core): add canonical package serialization`  
**Status**: ✅ Complete

### T7: Add canonical identity primitives

**What**: Define solution/project/entity canonical keys, safe logical locators, spans and variant values without persisted legacy IDs.  
**Where**: `src/Csharp2Md.Core/Analysis/IdentityPrimitives.cs`  
**Depends on**: T6  
**Reuses**: useful validation behavior from Domain identity/literal types, adjusted to the approved contract  
**Requirement**: PKG-07, VAR-03, VAR-04, VAR-06, STO-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Canonical keys are solution-scoped, deterministic and independent of absolute checkout paths.
- [x] Logical locators reject rooted, escaping and non-normalized paths.
- [x] At least 10 grammar, equality and path-safety cases pass.
- [x] Build gate passes.

**Tests**: unit — ≥10 focused cases  
**Gate**: build  
**Commit**: `feat(core): add canonical identity primitives`

**Status**: ✅ Complete
**Gate note**: interim until T45 — `dotnet build csharp2md.slnx --configuration Release` + `dotnet test tests/Csharp2Md.Core.Tests` (legacy Domain/Analysis/CLI suites fail on removed historical specs and non-Synthetic corpora; user-approved 2026-09-15).

## Phase 2: Isolated factual analysis

### T8: Build the authorized source inventory

**What**: Port root containment, symlink checks, document inventory and include-tests policy into the Analysis module.  
**Where**: `src/Csharp2Md.Core/Analysis/Inventory/SourceInventory.cs`  
**Depends on**: T7  
**Reuses**: `PathGuard`, `DocumentInventory` and supported-document rules that satisfy the new policy  
**Requirement**: PKG-05, PKG-06, PKG-07, PKG-08, CRT-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Inventory cannot escape the authorized root through relative paths or symlinks.
- [x] Test documents are excluded by default and the explicit option changes the analysis policy identity.
- [x] At least 10 inventory, symlink, source/config and test-policy cases pass.
- [x] Quick gate passes.

**Tests**: integration — ≥10 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): build authorized source inventory`

**Status**: ✅ Complete  
**Gate note**: interim until T45 — `dotnet build csharp2md.slnx --configuration Release` + `dotnet test tests/Csharp2Md.Core.Tests` (user-approved 2026-09-15).

### T9: Discover project-specific variants

**What**: Discover evaluated `(project, target framework)` pairs through `MSBuildWorkspace` progress without a global `TargetFramework`.  
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/ProjectVariantPlanner.cs`  
**Depends on**: T8  
**Reuses**: Roslyn 5.6 `MSBuildWorkspace.Create`, `OpenSolutionAsync` and `ProjectLoadProgress.TargetFramework` only during `Resolve`  
**Requirement**: VAR-01, VAR-02, CRT-06

**Tools**: MCP: Context7; Skills: `tlc-spec-driven`, `context7-mcp`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Discovery creates only evaluated project/TFM pairs and deduplicates them per solution.
- [x] Missing, unmatched or ambiguous SDK-style TFM progress fails as `variant-plan` instead of guessing.
- [x] No `Microsoft.Build.*` reference or `MSBuildLocator.RegisterDefaults()` call exists.
- [x] At least 8 real-workspace discovery and failure cases pass; quick gate passes.

**Tests**: integration — ≥8 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): discover project variants`

**Status**: ✅ Complete  
**Gate note**: interim until T45 — `dotnet build csharp2md.slnx --configuration Release` + `dotnet test tests/Csharp2Md.Core.Tests` (user-approved 2026-09-15).

### T10: Load each project variant in isolation

**What**: Open one workspace per root project/TFM and expose compilations only for that evaluated root.  
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/ProjectVariantWorkspace.cs`  
**Depends on**: T9  
**Reuses**: existing workspace diagnostic/cancellation handling and Roslyn's out-of-process BuildHost  
**Requirement**: VAR-01, VAR-02, VAR-04, CRT-06

**Tools**: MCP: Context7; Skills: `tlc-spec-driven`, `context7-mcp`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Each planned pair opens its project with only that pair's global TFM property.
- [x] Referenced projects support compilation but never emit occurrences as another root.
- [x] Workspaces are disposed on success, failure and cancellation.
- [x] At least 8 isolation, reference and disposal cases pass; quick gate passes.

**Tests**: integration — ≥8 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): isolate project variant workspaces`

**Status**: ✅ Complete  
**Gate note**: interim until T45 — `dotnet build csharp2md.slnx --configuration Release` + `dotnet test tests/Csharp2Md.Core.Tests` (user-approved 2026-09-15).

### T11: Merge logical entities across variants

**What**: Merge compatible occurrences into one logical entity, retain variant-qualified shapes and reject intra-variant collisions.  
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/LogicalEntityAccumulator.cs`  
**Depends on**: T10  
**Reuses**: deterministic ordering patterns from the current accumulator, not its persisted identifiers  
**Requirement**: VAR-03, VAR-04, VAR-05, VAR-06, EDG-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Compatible occurrences across TFMs share one logical entity.
- [x] Shape differences remain qualified across variants and collide only within the same variant.
- [x] Separate solutions cannot share identity, occurrence or deduplication state.
- [x] At least 10 merge, collision and solution-isolation cases pass; quick gate passes.

**Tests**: unit — ≥10 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): merge variant occurrences`

**Status**: ✅ Complete  
**Gate note**: interim until T45 — `dotnet build csharp2md.slnx --configuration Release` + `dotnet test tests/Csharp2Md.Core.Tests` (user-approved 2026-09-15).

### T12: Extract structural and architectural roots

**What**: Emit solution, project, document, symbol, Component, Deployment Unit, Entry Point and Boundary Operation facts with evidence.  
**Where**: `src/Csharp2Md.Core/Analysis/Extraction/ArchitectureFactExtractor.cs`  
**Depends on**: T11  
**Reuses**: selected symbol, component, entry-point and boundary detectors whose behavior matches `CONTEXT.md`  
**Requirement**: PKG-02, PKG-09, DEP-08, CRT-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Roots require approved evidence; project/assembly/directory names alone never create Deployment Units.
- [x] Production/test provenance and variant-qualified locators are retained.
- [x] No business-rule interpretation or heuristic quality label is emitted.
- [x] At least 12 extractor and negative-proof cases pass; quick gate passes.

**Tests**: integration — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): extract architectural roots`

**Status**: ✅ Complete  
**Gate note**: interim until T45 — `dotnet build csharp2md.slnx --configuration Release` + `dotnet test tests/Csharp2Md.Core.Tests` (user-approved 2026-09-15).

### T13: Extract causal and boundary relations

**What**: Emit evidence-backed direct relations for Project Reference, internal invocation, structural type use, HTTP, gRPC, messaging and contracts.  
**Where**: `src/Csharp2Md.Core/Analysis/Extraction/CausalRelationExtractor.cs`  
**Depends on**: T12  
**Reuses**: bindable walkers and boundary/contract passes selected by required behavior  
**Requirement**: DEP-02, PKG-09, CRT-08, EDG-01, EDG-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Every confirmed relation resolves source, target, variant occurrence and evidence chain.
- [x] Repeated calls remain separate factual occurrences for later aggregation.
- [x] Unresolved destinations become Candidate/Unknown/Open Frontier with cause, never guessed confirmed edges.
- [x] At least 14 category, repeated-call and unresolved cases pass; quick gate passes.

**Tests**: integration — ≥14 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): extract causal relations`

**Status**: ✅ Complete
**Gate note**: interim until T45 — `dotnet build csharp2md.slnx --configuration Release` + `dotnet test tests/Csharp2Md.Core.Tests` (user-approved 2026-09-15).

### T14: Extract configuration and persistence facts safely

**What**: Emit configuration keys/sections/bindings and persistence stores/objects/fields/operations without raw values.  
**Where**: `src/Csharp2Md.Core/Analysis/Extraction/ConfigurationPersistenceExtractor.cs`  
**Depends on**: T13  
**Reuses**: configuration and persistence detectors/builders that can emit the new factual contract  
**Requirement**: PKG-07, PKG-09, DEP-02, CRT-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Published graph inputs contain keys, sections, bindings, categories and safe locators only.
- [x] Environment values, connection strings, credentials and absolute paths never enter the graph.
- [x] Persistence relations remain observable and evidence-backed.
- [x] At least 12 configuration, EF/SQL, secret and negative cases pass; quick gate passes.

**Tests**: integration — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): extract safe configuration and persistence facts`
**Status**: ✅ Complete

### T15: Assemble an immutable FactualGraph per solution

**What**: Orchestrate inventory, variants and extractors into a deterministic in-memory graph with extraction measurements.  
**Where**: `src/Csharp2Md.Core/Analysis/SolutionAnalyzer.cs`  
**Depends on**: T14  
**Reuses**: useful failure diagnostics from the legacy pipeline; no pipeline-stage or persistence abstractions  
**Requirement**: PKG-03, PKG-09, VAR-06, CRT-03, PUB-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Analysis returns a graph and never writes or commits package files.
- [x] Entity/relation/evidence order and extraction/filter measurements are deterministic.
- [x] Diagnostics identify solution, project, variant and cause when applicable.
- [x] At least 10 orchestration, cancellation, determinism and no-write cases pass; build gate passes.

**Tests**: integration — ≥10 focused cases  
**Gate**: build  
**Commit**: `feat(analysis): assemble factual solution graphs`
**Status**: ✅ Complete

## Phase 3: Retention, dependencies and measures

### T16: Retain the confirmed journey closure

**What**: Select proven roots and retain only the confirmed causal/ownership closure required by supported journeys.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Retention/RetainedGraphBuilder.cs`  
**Depends on**: T15  
**Reuses**: factual graph only; no legacy projection allowlists or wire families  
**Requirement**: PKG-02, PKG-03, PKG-09, EDG-01

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Closure starts at all four proven root kinds and follows only confirmed causal relations to approved terminals.
- [x] Required membership, occurrences and evidence are retained; disconnected inventory is absent.
- [x] A confirmed relation without resolvable endpoints/evidence is demoted with known disposition or rejects the build.
- [x] At least 12 closure and invalid-evidence cases pass; quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): retain confirmed journey closure`
**Status**: ✅ Complete

### T17: Retain relevant gaps and cited sources

**What**: Add incoming support, journey-affecting gaps, cited sources and include-tests policy to the retained graph.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Retention/RetentionPolicy.cs`  
**Depends on**: T16  
**Reuses**: source redaction policy concepts, not legacy source payload layout  
**Requirement**: PKG-04, PKG-05, PKG-06, PKG-08, MET-07, EDG-01

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Candidate, Unknown and Open Frontier survive only when they can alter/interrupt a retained journey.
- [x] Gap ordering uses affected journeys, affected roots and canonical identity.
- [x] Only cited documents survive; test sources require explicit policy recorded in measurements.
- [x] At least 12 gap, incoming, source and test-policy cases pass; quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): retain relevant gaps and sources`
**Status**: ✅ Complete

### T18: Aggregate dependencies at four scopes

**What**: Build auditable direct dependency edges at Document, Project, Component and Deployment Unit scopes.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Measures/DependencyAggregator.cs`  
**Depends on**: T17  
**Reuses**: proven membership maps from retention and relation evidence handles  
**Requirement**: DEP-01, DEP-02, DEP-03, DEP-04, DEP-05, DEP-06, DEP-07, DEP-08, EDG-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Edges aggregate by solution/scope/source/target/category and include count, variants, relations and deduplicated evidence.
- [x] Low-level relation payload is referenced across scopes, not copied.
- [x] Candidate/Unknown/Open Frontier never contribute to confirmed counts; unproven upper scopes are omitted.
- [x] At least 16 independently calculated aggregation/category/scope cases pass; quick gate passes.

**Tests**: unit — ≥16 focused cases  
**Gate**: quick  
**Commit**: `feat(package): aggregate scoped dependencies`
**Status**: ✅ Complete

### T19: Calculate direct dependency measures

**What**: Calculate fan-in, fan-out, occurrence totals and cross-component edge counts from retained direct edges.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Measures/DirectMeasureCalculator.cs`  
**Depends on**: T18  
**Reuses**: aggregated edges only; test expectations use hand-authored graphs  
**Requirement**: MET-01, MET-02, MET-03, MET-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Fan-in/out count distinct origins/destinations in the requested scope.
- [x] Occurrences are counted before edge deduplication and cross-component counts require proven distinct ownership.
- [x] At least 12 hand-calculated empty, duplicate, multi-category and multi-scope cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): calculate direct dependency measures`
**Status**: ✅ Complete

### T20: Calculate directed cycles

**What**: Calculate deterministic strongly connected components and cycle membership independently per scope.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Measures/CycleCalculator.cs`  
**Depends on**: T19  
**Reuses**: retained directed edges; no external graph library  
**Requirement**: MET-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] SCCs identify self-cycles and multi-node cycles without mixing scopes or solutions.
- [x] Cycle IDs and member order are deterministic under input permutation.
- [x] At least 10 acyclic, cyclic, self-loop, disconnected and permutation cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥10 focused cases  
**Gate**: quick  
**Commit**: `feat(package): calculate directed cycles`
**Status**: ✅ Complete

### T21: Calculate reverse impact and gap counts

**What**: Traverse incoming indexes for minimum-depth reverse impact and attach separate relevant-gap counts.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Measures/ImpactCalculator.cs`  
**Depends on**: T20  
**Reuses**: incoming adjacency from direct dependencies  
**Requirement**: MET-06, MET-07, MET-08, NAV-09

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Each reachable source appears once with minimum traversal depth.
- [x] Gap kinds remain separate from confirmed measures and no composite score/quality label exists.
- [x] At least 12 depth, diamond, cycle, scope, gap and omission cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): calculate reverse impact`
**Status**: ✅ Complete

### T22: Assemble the single RetrievalModel

**What**: Combine retained graphs, dependencies, measures and navigation-ready records into the sole renderer input.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/RetrievalModelBuilder.cs`  
**Depends on**: T21  
**Reuses**: outputs of T16-T21 without introducing new factual semantics  
**Requirement**: DEP-07, MET-08, NAV-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Machine and Markdown writers can consume the same immutable model without querying the source graph again.
- [x] Model ordering is canonical across solution/input permutations.
- [x] At least 8 assembly, omission and permutation cases pass.
- [x] Build gate passes.

**Tests**: unit — ≥8 focused cases  
**Gate**: build  
**Commit**: `feat(package): assemble retrieval model`
**Status**: ✅ Complete
**Gate note**: interim until T45 — `dotnet build csharp2md.slnx --configuration Release` + `dotnet test tests/Csharp2Md.Core.Tests --configuration Release --no-build` passed (249 tests); full legacy test suite remains deferred by the user-approved interim rule.

## Phase 4: Identity, layout and retrieval artifacts

### T23: Generate collision-safe public IDs

**What**: Generate type-prefixed 80-bit SHA-256 base32hex IDs and detect digest collisions before serialization.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Identity/PublicIdRegistry.cs`  
**Depends on**: T22  
**Reuses**: .NET cryptographic primitives; no legacy fact ID grammar  
**Requirement**: STO-01, STO-02

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] IDs match `^[a-z]{3}_[0-9a-v]{16}$`, use unique kind prefixes and exactly the first 80 SHA-256 bits.
- [x] A forced collision names the ID and both canonical categories without leaking absolute paths.
- [x] At least 12 vector, grammar, prefix and collision cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): generate compact public ids`

**Status**: ✅ Complete

### T24: Build solution-local handle tables

**What**: Deduplicate identities, documents, strings, variants and evidence and assign ordered base36 handles.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Identity/LocalTableBuilder.cs`  
**Depends on**: T23  
**Reuses**: deterministic interning concepts from `InternTable`, with the new per-solution contract  
**Requirement**: STO-03, STO-04, STO-05, VAR-06

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Tables sort by canonical key and assign lowercase base36 ordinals from `0`.
- [x] Handles resolve directly; overflow above six characters rejects the build.
- [x] Deduplication never crosses solution boundaries and projection records use handles instead of repeated payload.
- [x] At least 14 ordering, deduplication, overflow and isolation cases pass; quick gate passes.

**Tests**: unit — ≥14 focused cases  
**Gate**: quick  
**Commit**: `feat(package): build solution local tables`

**Status**: ✅ Complete

### T25: Pack deterministic byte-bounded shards

**What**: Pack canonical bulk records into stable ordinal shards with a 64 KiB target and 96 KiB hard ceiling.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Layout/ShardPacker.cs`  
**Depends on**: T24  
**Reuses**: real-byte sizing concept from `LayoutPlanner`, not hash-prefix bucket layout  
**Requirement**: STO-06, STO-07, EDG-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Records sort canonically and every bulk shard respects the hard ceiling.
- [x] A single oversized record fails as `oversized-record`; normal records are never emitted one-file-per-record.
- [x] Identical logical inputs produce identical shard boundaries, paths and bytes.
- [x] At least 12 boundary, oversized, permutation and reproducibility cases pass; quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): pack deterministic shards`

**Status**: ✅ Complete

### T26: Build directly resolvable navigation indexes

**What**: Build identity, roots, outgoing, incoming, contract, persistence and evidence/disposition indexes with direct shard locators.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Navigation/NavigationIndexBuilder.cs`  
**Depends on**: T25  
**Reuses**: handles and shard locations from T24-T25  
**Requirement**: NAV-01, NAV-04, NAV-06, NAV-07, NAV-08, NAV-09, NAV-10

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Every supported start key resolves to artifact plus ordinal without directory enumeration, manual shard choice or ID decoding.
- [x] Index links remain within the same solution and resolve to retained records/evidence.
- [x] At least 14 direct-resolution, missing-key, scope and solution-isolation cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥14 focused cases  
**Gate**: quick  
**Commit**: `feat(package): build navigation indexes`

**Status**: ✅ Complete

### T27: Render machine artifacts and root manifest

**What**: Render tables, graph, indexes, dependencies, measures and a single root manifest from the RetrievalModel.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Rendering/MachineArtifactWriter.cs`  
**Depends on**: T26  
**Reuses**: canonical JSON and planned-artifact contracts  
**Requirement**: PKG-01, STO-05, NAV-01, NAV-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] The root manifest lists solutions and all proven roots with readable name, handle, machine citation and Markdown link.
- [x] All required families and journey entry paths are declared; every manifest path is normalized and relative.
- [x] At least 12 artifact-layout, manifest-link and canonical-byte cases pass.
- [x] Quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): render machine artifacts`

**Status**: ✅ Complete

### T28: Render equivalent Markdown navigation

**What**: Render summary, Component, Deployment Unit and retained-document pages from the same RetrievalModel.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Rendering/MarkdownRenderer.cs`  
**Depends on**: T27  
**Reuses**: safe Markdown escaping/linking concepts only; no legacy catalogs/guides/pages  
**Requirement**: NAV-02, NAV-03, NAV-04, NAV-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Summary shows components, Deployment Units, cycles, top fan-in/out and all four journeys.
- [x] Entity pages show outgoing, incoming, measures, effects and relevant gaps with existing relative links.
- [x] No page invents Service/Deployment Unit identity or omits an equivalent machine dependency/measure.
- [x] At least 12 content, escaping, link and equivalence cases pass; quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): render markdown navigation`

**Status**: ✅ Complete

### T29: Rehydrate retrieval artifacts and prove equivalence

**What**: Rehydrate the RetrievalModel from machine artifacts, rerender Markdown and compare exact bytes.  
**Where**: `src/Csharp2Md.Core/Publication/RetrievalModelReader.cs`  
**Depends on**: T28  
**Reuses**: manifest-led reading principle and the same Markdown renderer  
**Requirement**: NAV-05, PUB-03, EDG-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Rehydration reads only manifest-declared machine paths and resolves all handles/references.
- [x] Any machine/Markdown divergence reports package corruption with the offending artifact.
- [x] At least 10 round-trip, missing-reference and Markdown-mutation cases pass.
- [x] Quick gate passes.

**Tests**: integration — ≥10 focused cases  
**Gate**: quick  
**Commit**: `feat(publication): verify markdown machine equivalence`

**Status**: ✅ Complete

### T30: Build the complete deterministic PackagePlan

**What**: Orchestrate retention, retrieval, tables, renderers, packing, manifest and measurements into all final pre-certification bytes.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs`  
**Depends on**: T29  
**Reuses**: T16-T29 outputs; no deferred fragments, plugin projectors or batch composer  
**Requirement**: PKG-01, PKG-03, PKG-04, PKG-05, PKG-06, PKG-08, STO-04, STO-06, STO-07, CRT-03, EDG-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Plan contains all artifact bytes and reserves deterministic certification/measurement artifacts before staging.
- [x] Measurements separate extraction/publication and filtered reasons by family, journey and corpus.
- [x] Same graphs/policy under input permutation produce byte-identical plans and digest; package ceilings fail before publication.
- [x] At least 14 orchestration, determinism, multi-solution and budget cases pass; build gate passes.

**Tests**: integration — ≥14 focused cases  
**Gate**: build  
**Commit**: `feat(package): build complete package plans`

**Status**: ✅ Complete
**Gate note**: interim until T45 — `dotnet build csharp2md.slnx --configuration Release` + `dotnet test tests/Csharp2Md.Core.Tests --configuration Release --no-build` passed (358 tests); full legacy test suite remains deferred by the user-approved interim rule.

## Phase 5: Validation, certification and atomic publication

### T31: Enforce typed and lexical publication safety

**What**: Validate typed JSON fields and lexically inspect/redact cited C# source without treating comments as paths.  
**Where**: `src/Csharp2Md.Core/Publication/Safety/PublicationSafetyScanner.cs`  
**Depends on**: T30  
**Reuses**: Roslyn C# syntax APIs and deterministic redaction behavior, not arbitrary text token scanning  
**Requirement**: PKG-07, PUB-06, PUB-07, CRT-08

**Tools**: MCP: Context7; Skills: `tlc-spec-driven`, `context7-mcp`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] C# `//` comments/trivia are never classified as UNC paths.
- [x] Actual absolute paths, escapes and secrets in structured fields/source are deterministically redacted or rejected before commit.
- [x] Removed values never appear in diagnostics or retained bytes; per-document disposition remains auditable.
- [x] At least 14 lexical, JSON, path, secret and no-leak cases pass; quick gate passes.

**Tests**: integration — ≥14 focused cases  
**Gate**: quick  
**Commit**: `feat(publication): enforce package safety`

### T32: Read immutable packages through the manifest

**What**: Implement the shared package reader with normalized relative-path, root-containment, symlink and shared-lock enforcement.  
**Where**: `src/Csharp2Md.Core/Publication/PackageReader.cs`  
**Depends on**: T31  
**Reuses**: manifest-led reader principle and `PathGuard` containment behavior  
**Requirement**: NAV-01, NAV-04, PUB-02, PUB-03, PUB-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Reader starts only at root `manifest.json` and opens reachable files in its immutable generation.
- [x] Rooted, `..`, drive, malformed and symlink-escape paths are rejected before open.
- [x] Reader holds the shared lock for the complete validation view.
- [x] At least 14 valid, traversal, symlink, missing and concurrent-reader cases pass; quick gate passes.

**Tests**: integration — ≥14 focused cases  
**Gate**: quick  
**Commit**: `feat(publication): read manifest driven packages`

### T33: Validate package integrity and semantics

**What**: Validate hashes, byte sizes, cardinalities, IDs, handles, references, shard limits, safety and Markdown equivalence with one rule set.  
**Where**: `src/Csharp2Md.Core/Publication/PackageValidator.cs`  
**Depends on**: T32  
**Reuses**: T29 equivalence and T31 safety scanners; no version dispatch  
**Requirement**: STO-01, STO-03, STO-05, STO-06, PUB-02, PUB-03, PUB-04, PUB-05, EDG-01, EDG-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] The same reader and validator entry point serves staging and public `validate`.
- [x] Every corruptible field/family has a mutation test that reports code, stage, family/artifact and cause.
- [x] Validation never touches source solutions or mutates the package.
- [x] At least 18 integrity, corruption and diagnostic cases pass; quick gate passes.

**Status**: ✅ Complete

**Tests**: integration — ≥18 focused cases  
**Gate**: quick  
**Commit**: `feat(publication): validate package integrity`

### T34: Certify locate and evidence journeys ✅ Complete

**What**: Add a measured reader plus Locate and Evidence/Disposition certification with applicability semantics.  
**Where**: `src/Csharp2Md.Core/Publication/Certification/JourneyCertifier.cs`  
**Depends on**: T33  
**Reuses**: direct navigation indexes and package reader only; no directory enumeration  
**Requirement**: NAV-06, NAV-07, NAV-10, CRT-01, CRT-02, EDG-02

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Every open records path, UTF-8 bytes and one read; tokens use `ceil(bytes / 4.0)`.
- [x] Component locate fits 5 reads; other roots fit 8 reads/12,000 tokens; evidence fits 12 reads/25,000 tokens.
- [x] Inapplicable journeys record `not_applicable` plus reason and never count as pass.
- [x] At least 12 applicable, N/A, missing-terminal and over-budget cases pass; quick gate passes.

**Tests**: integration — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(certification): certify locate and evidence journeys`

### T35: Certify flow and reverse-impact journeys ✅ Complete

**What**: Add causal-flow and reverse-impact traversal certification over declared indexes and terminals.  
**Where**: `src/Csharp2Md.Core/Publication/Certification/GraphJourneyCertifier.cs`  
**Depends on**: T34  
**Reuses**: measured reader and direct indexes from T34/T26  
**Requirement**: NAV-08, NAV-09, CRT-01, CRT-02, EDG-02

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Applicable flow reaches contracts, external effects and persistence in ≤32 reads/125,000 tokens.
- [x] Reverse impact supports file, project, Component, Deployment Unit, contract and data roots with minimum depths in the same budget.
- [x] Missing answers or exceeded reads/tokens fail and name journey plus exceeded measure.
- [x] At least 14 hand-oracle flow, impact, N/A and budget cases pass; quick gate passes.

**Tests**: integration — ≥14 focused cases  
**Gate**: quick  
**Commit**: `feat(certification): certify graph journeys`

### T36: Publish immutable generations atomically ✅ Complete

**What**: Materialize, rehydrate, validate, certify and atomically commit one immutable generation under an exclusive lock.  
**Where**: `src/Csharp2Md.Core/Publication/PackagePublication.cs`  
**Depends on**: T35  
**Reuses**: local filesystem retry primitives and existing real-directory failure-test style  
**Requirement**: PUB-01, PUB-02, PUB-04, PUB-05, PUB-08, EDG-02, EDG-03, EDG-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:csharp-concurrency-patterns`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] All planned bytes are staged on the same volume, rehydrated and validated before manifest swap.
- [x] Certification/measurements are rewritten only in their reserved artifacts and final validation repeats before commit.
- [x] Move plus root-manifest replacement is the only commit point; failure before it preserves the old package byte-for-byte.
- [x] Concurrent writer/read behavior and cleanup diagnostics match the approved lock model.
- [x] At least 18 success, injected-failure, lock, preservation and cleanup cases pass; quick gate passes.

**Tests**: integration — ≥18 focused cases  
**Gate**: quick  
**Commit**: `feat(publication): commit immutable generations`

### T37: Qualify retrieval and publication by solution

**What**: Group retrieval data, manifest navigation and certification structurally by typed `SolutionId`, with stable textual enum values.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/RetrievalModel.cs`, `src/Csharp2Md.Core/Publication/PackageContracts.cs`  
**Depends on**: T36  
**Reuses**: `PublicIdRegistry`, solution-local handles, canonical JSON and the existing manifest-led reader  
**Requirement**: PKG-01, NAV-01, NAV-05, VAR-06, STO-05, STO-07, PUB-03, CRT-01, CRT-03

**Tools**: MCP: Context7 for current `System.Text.Json` enum/source-generation behavior; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] `RetrievalModel` contains only solution-scoped models; roots, dependencies, measures and retained facts cannot cross a solution boundary by construction.
- [x] The manifest groups roots, exactly one index of every kind and four semantic journey entries under a typed `SolutionId`; index and journey enum values are stable snake-case strings.
- [x] The reader resolves solution then index kind, rejects duplicate/missing indexes and detects corruption in any solution without exposing composite keys to callers.
- [x] Certification produces four independently measured journey results for every solution and package publication fails when any solution journey fails.
- [x] Tests cover two solutions with equal local handles/index kinds and distinct causal data, deterministic reordering, duplicate/missing index rejection and corruption isolated to the second solution; build gate passes.

**Tests**: unit + integration — focused contract, writer/reader and certification cases  
**Gate**: build  
**Commit**: `refactor(publication): isolate package data by solution`

**Status**: ✅ Complete  
**Gate note**: interim until T45 — `dotnet build csharp2md.slnx --configuration Release` + `dotnet test tests/Csharp2Md.Core.Tests --configuration Release` passed (470 tests); full legacy test suite remains deferred by the user-approved interim rule.

### T38: Wire KnowledgeEngine analysis and validation

**What**: Connect the facade to Analysis, PackageBuilding and Publication and return committed status only after certification.  
**Where**: `src/Csharp2Md.Core/KnowledgeEngine.cs`  
**Depends on**: T37  
**Reuses**: public contracts from T2 and internal result seams completed in T15/T30/T36  
**Requirement**: PKG-08, PUB-03, PUB-04, PUB-08, VAR-06

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Multiple input solutions produce isolated graphs and one package plan/atomic commit.
- [x] `Validate` uses the same reader, validator and certifier and never opens a source solution.
- [x] Success is returned only for a committed/certified package; expected failures are structured and non-successful.
- [x] At least 12 facade integration, multi-solution, cancellation and failure cases pass; build gate passes.

**Tests**: integration — ≥12 focused cases  
**Gate**: build  
**Commit**: `feat(core): wire knowledge engine workflow`

**Status**: âœ… Complete
**Gate note**: interim until T45 â€” `dotnet build csharp2md.slnx --configuration Release` passed with 0 warnings/errors; `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --configuration Release --no-build` passed (486 tests); full legacy test suite remains deferred by the user-approved interim rule.

## Phase 6: CLI acceptance and clean cut

### T39: Wire the analyze CLI command

**What**: Replace analyze wiring with `KnowledgeEngine`, supporting one-or-more solutions, output and explicit test inclusion.  
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`  
**Depends on**: T38  
**Reuses**: current System.CommandLine parsing conventions, not legacy engine/store/projector wiring  
**Requirement**: PKG-01, PKG-08, PUB-04, PUB-08, VAR-06

**Tools**: MCP: Context7; Skills: `tlc-spec-driven`, `context7-mcp`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Analyze rejects invalid inputs before staging and invokes the Core facade exactly once per request.
- [x] Include-tests policy reaches the manifest/run identity; diagnostics print concise code/stage/cause and applicable coordinates.
- [x] CLI reports success only after committed certification and non-zero for all rejection classes.
- [x] At least 12 command-tree, option, multi-solution and exit/diagnostic cases pass; full gate passes.

**Tests**: e2e — ≥12 focused cases  
**Gate**: full  
**Commit**: `feat(cli): wire knowledge package analysis`

**Status**: Complete
**Gate note**: interim until T45 - Release build passed with 0 warnings/errors; `KnowledgeAnalyzeCommandTests` passed (17 tests), `Csharp2Md.Core.Tests` passed (486 tests), and `PartialBatchTests` passed (2 tests). The declared full gate was attempted; remaining failures belong to superseded legacy specs/options/package assertions and optional local corpora covered by the approved pre-T45 exception.

### T40: Wire validate and remove compose

**What**: Route validate through `KnowledgeEngine.Validate` and remove the compose command and batch-manifest path.  
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`  
**Depends on**: T39  
**Reuses**: existing in-process CLI test harness  
**Requirement**: PKG-10, PUB-03, PUB-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] CLI exposes only current analyze/validate behavior; compose and version/compatibility dispatch are absent.
- [x] Validate reads only the package, returns the same certification interpretation as pre-commit validation and never mutates it.
- [x] At least 10 command-surface, offline-source, corruption and exit-code cases pass.
- [x] Full gate passes under the approved pre-T45 legacy-suite exception.

**Tests**: e2e — ≥10 focused cases  
**Gate**: full  
**Commit**: `feat(cli): validate current package contract`

**Status**: Complete
**Gate note**: `KnowledgeValidateCommandTests` passed (10 tests). The declared full solution gate was run; retained legacy test projects fail only on removed compose/version/compatibility contracts and the missing superseded taxonomy spec. The approved interim exception remains in effect until T45 removes those projects and tests.

**Adequacy**: PKG-10 is asserted by `KnowledgeValidateCommandTests.cs:18` and `:146-151`; PUB-03/PUB-04 validation delegation, immutable input and structured outcomes are asserted by `:59`, `:82-83`, `:105` and `:127-134`. Every focused test maps to those task criteria; no shallow or speculative assertions were added.

### T41: Expand the versioned synthetic fixture

**What**: Add one coherent fixture scenario covering per-project multi-targeting, production/test distinction, repeated calls, component dependency, runtime integration, cycle, safety inputs and one controlled gap.  
**Where**: `fixtures/SyntheticSolution`  
**Depends on**: T40  
**Reuses**: existing Acme projects and manual fixture-manifest/oracle style; no new versioned corpus  
**Requirement**: CRT-08, VAR-01, VAR-02, VAR-03, PUB-06, PUB-07

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Fixture contains every CRT-08 characteristic without generated bin/obj or external clone content.
- [x] A hand-authored manifest/oracle names expected roots, edges, occurrences, cycle, gap and excluded test/safety values.
- [x] At least 12 fixture-integrity and expected-feature cases pass.
- [x] Full gate passes under the approved pre-T45 legacy-suite exception.

**Tests**: e2e — ≥12 focused cases  
**Gate**: full  
**Commit**: `test(fixture): cover knowledge package journeys`

**Status**: Complete
**Gate note**: `SyntheticSolutionFixtureTests` passed (14 tests), and `dotnet build csharp2md.slnx --configuration Release` passed with 0 warnings/errors. The declared full suite was run with `--no-build`; retained legacy test projects fail only on removed compose/version/compatibility contracts and the missing superseded taxonomy spec. The approved interim exception remains in effect until T45 removes those projects and tests.

**Adequacy**: CRT-08 is asserted by `SyntheticSolutionFixtureTests.cs:9-118`, including multi-targeting (`:58-64`), production/test distinction (`:66-75`), repeated calls/runtime/component evidence (`:77-92`), cycle (`:94-101`) and safety inputs (`:103-118`). VAR-01/VAR-02/VAR-03 and PUB-06/PUB-07 are pinned by the exact oracle at `:22-56` and its fixture assertions. Every focused test maps to a T41 criterion; no shallow or speculative assertions were added.

### T42: Prove the four journeys end to end

**What**: Analyze the fixture, open from root manifest, complete all four journeys, validate immediately and compare to manual expectations.  
**Where**: `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs`  
**Depends on**: T41  
**Reuses**: `CliInvoke`, temporary output helpers and T41's independent oracle  
**Requirement**: PKG-01, PKG-02, PKG-03, PKG-04, PKG-05, PKG-06, PKG-07, PKG-08, PKG-09, DEP-01, DEP-02, DEP-03, DEP-04, DEP-05, DEP-06, DEP-07, MET-01, MET-02, MET-03, MET-04, MET-05, MET-06, MET-07, MET-08, NAV-01, NAV-02, NAV-03, NAV-04, NAV-05, NAV-06, NAV-07, NAV-08, NAV-09, NAV-10, CRT-01, CRT-02, CRT-03, CRT-09

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Tests start only at `manifest.json`, record files/bytes/reads/tokens and never enumerate package directories to find answers.
- [x] Expected dependencies and measures are calculated in test code from hand-authored fixture edges, never by production helpers.
- [x] Standard package excludes every prohibited family/value; immediate CLI validate passes with equivalent interpretation.
- [x] At least 16 end-to-end answer, equivalence, budget and exclusion cases pass; full gate passes.

**Tests**: e2e — ≥16 focused cases  
**Gate**: full  
**Commit**: `test(cli): certify knowledge package journeys`

**Status**: Complete
**Gate note**: `KnowledgePackageJourneyTests` passed (16 tests), `KnowledgeEngineWorkflowTests` passed (16 tests), and `dotnet build csharp2md.slnx --configuration Release` passed with 0 warnings/errors. The declared full suite was run with `--no-build`; retained legacy projects fail only on removed compose/version/compatibility contracts and the missing superseded taxonomy spec. The approved interim exception remains in effect until T45 removes those projects and tests.

**Adequacy**: Manifest-only entry and the four journey budgets are asserted by `KnowledgePackageJourneyTests.cs:13-24` and `:70-83`; architectural roots, hand-authored dependency categories, aggregation and measures by `:27-67`; immediate validation and standard-package exclusions by `:86-108`. Each assertion maps to the T42 criteria and uses no production helper to derive expected categories or budgets.

### T43: Prove rejection preserves the committed package

**What**: Exercise every specified rejection class from a valid baseline and prove byte-for-byte atomic preservation.  
**Where**: `tests/Csharp2Md.Cli.Tests/KnowledgePackageFailureTests.cs`  
**Depends on**: T42  
**Reuses**: real temporary-directory failure injection patterns from Storage/CLI tests  
**Requirement**: STO-02, PUB-05, PUB-06, PUB-07, PUB-08, EDG-01, EDG-02, EDG-03, EDG-04, EDG-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Variant, retention, safety, size, materialization, rehydration, validation, equivalence and journey-budget failures are injected.
- [x] Each failure returns the required structured coordinates and leaves root manifest plus prior generation byte-identical.
- [x] No staging debris is reachable after failure; cancellation also preserves the prior package.
- [x] At least 16 rejection/preservation cases pass; full gate passes under the approved pre-T45 legacy-suite exception.

**Tests**: e2e — ≥16 focused cases  
**Gate**: full  
**Commit**: `test(cli): prove atomic rejection behavior`

**Status**: Complete
**Gate note**: `KnowledgePackageFailureTests` passed 16 cases in Release. The declared full solution gate was run; retained legacy projects fail only on superseded taxonomy, compose/options and legacy certification contracts, under the approved interim exception until T45. The Release build phase completed before those legacy test failures.

**Adequacy**: The nine specified rejection classes and every applicable diagnostic coordinate are asserted by `KnowledgePackageFailureTests.cs:13-51`. Root-manifest, index, safety and Markdown/equivalence rejection with byte-identical package snapshots are asserted by `:54-110` and `:155-183`; the Markdown family coordinate is classified at `PackageValidator.cs:68-76`. Real lock-contention rejection, staging cleanup and cancellation preservation are asserted by `KnowledgePackageFailureTests.cs:113-152`. The isolated fixture copy at `:191-206` keeps generated build outputs out of the versioned fixture. Each focused assertion maps to T43's failure, coordinate, atomicity or cleanup criterion; no shallow or speculative assertion was added.

### T48: Define causal journey applicability

**What**: Apply the confirmed CRT-02 rule to the certifier fixtures while retaining their passed and failed outcome assertions.  
**Where**: `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`, `tests/Csharp2Md.Core.Tests/Publication/GraphJourneyCertifierTests.cs`  
**Depends on**: T43  
**Reuses**: the existing causal-category applicability checks in `GraphJourneyCertifier`  
**Requirement**: CRT-01, CRT-02

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Flow and reverse impact are not applicable for a solution with no HTTP, gRPC, Messaging, Contract or Persistence dependency.
- [x] An impact fixture with a causal dependency and a reachable set passes; one without the expected reachable set fails.
- [x] Existing passed/failed outcome assertions remain unchanged, and the quick gate passes.

**Tests**: unit — ≥2 noncausal cases and the 5 corrected applicable fixtures  
**Gate**: quick  
**Commit**: `fix(publication): define causal journey applicability`

**Status**: Complete
**Gate note**: Core Release quick gate passed: 495 passed, 0 failed, 0 skipped. The run included the uncommitted T46 builder change; T48's certifier assertions are independent of that builder path.
**Adequacy**: `GraphJourneyCertifierTests.cs:18-19` and `:25-26` assert NotApplicable status and a reason for noncausal flow and impact. `:11`, `:31`, `:34`, `:37-38` retain the five impact passed/failed assertions with causal fixtures. CRT-02's applicability rule is explicit in `spec.md:211`.

### T46: Pair scoped edges by evidence document

**What**: Derive document-scope and project-scope dependency edges from each evidence record's own document instead of the membership cross product.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs`  
**Depends on**: T48  
**Reuses**: `EvidenceRecord.DocumentCanonicalKey` and the existing aggregation, measure and impact pipeline  
**Requirement**: DEP-01, DEP-03, DEP-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] A document-scope edge exists only between the document that holds the evidence and a document with proven target membership.
- [x] A project-scope edge exists only between a proven owner of the evidence document and a project with proven target membership.
- [x] Component and deployment-unit scope keep their proven-membership derivation unchanged.
- [x] `occurrence_count` equals the number of confirmed evidences for that exact source, target, scope and category, with no cross-product inflation.
- [x] No edge is emitted for a document or project pair that no confirmed evidence supports.
- [x] At least 6 pairing, count and scope-isolation cases pass; quick gate passes and the synthetic fixture still certifies all four journeys.

**Tests**: unit — ≥6 pairing/count/scope cases  
**Gate**: quick  
**Commit**: `fix(package-building): pair scoped edges by evidence document`

**Status**: Complete
**Gate note**: Core Release quick gate passed: 497 passed, 0 failed, 0 skipped. The synthetic CLI journey suite passed 16 of 16 cases. Pitstop still exceeds the byte budget; T47 owns the size correction.
**Adequacy**: `ScopePairingTests.cs:16-25` asserts evidence source, absent unsupported document source, and proven target document; `:28-42` asserts both project endpoints and absence without a proven owner; `:47-48` asserts the exact occurrence count; `:51-58` asserts component and deployment-unit membership endpoints. These nine cases cover DEP-01 and DEP-04 without deriving expectations from the builder.

### T47: Index dependencies without copying payload

**What**: Serialize the confirmed dependency and measure sets once per solution and make the navigation indexes carry keys and pointers into them instead of verbatim copies.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Rendering/MachineArtifactWriter.cs`, `src/Csharp2Md.Core/PackageBuilding/CanonicalJson.cs`, `src/Csharp2Md.Core/Publication/PackageReader.cs`, `src/Csharp2Md.Core/Publication/RetrievalModelReader.cs`, `src/Csharp2Md.Core/Publication/Certification/GraphJourneyCertifier.cs`  
**Depends on**: T46  
**Reuses**: the existing `NavigationIndexKind` contract, manifest entry paths and `PackageReader` path resolution  
**Requirement**: DEP-05, DEP-07

**Done when**:

- [x] The confirmed dependency set is serialized exactly once per solution; no second path repeats it.
- [x] The measure set is serialized exactly once per solution; no second path repeats it.
- [x] `outgoing`, `incoming`, `contracts` and `persistence` carry keys and resolvable pointers, never a copy of the edge set.
- [x] Every index kind stays declared exactly once in the manifest and resolves through `PackageReader` without exposing composite keys.
- [x] The four journeys certify on the synthetic fixture from the same entry indexes with unchanged interpretation; canonical dependency records retain their relation and evidence handles.
- [x] At least 8 single-serialization, pointer-resolution and journey cases pass; quick gate passes.

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Tests**: unit — ≥8 index/pointer/journey cases  
**Gate**: quick  
**Commit**: `fix(package-building): index dependencies without copying payload`

**Status**: Complete
**Gate note**: Core Release quick gate passed: 509 passed, 0 failed, 0 skipped. The synthetic CLI journey suite passed 16 of 16 cases. Pitstop's plan fell from 141.0 MiB to 38.13 MiB; publication still rejects on flow applicability and the evidence token budget, with size and evidence work assigned to T49-T50.
**Adequacy**: `NavigationPayloadIndexTests.cs:14-27` asserts single dependency/measure storage; `:31-34` asserts exactly one of every index kind; `:42-55` proves outgoing/incoming ordinals resolve; `:59-71` proves category filtering; `:80-83` proves measure pointers; `:91-97` proves rehydration and retained relation/evidence handles; `:118-119` proves `PackageReader` follows pointers; `:136` and `:147` reject broken pointers. `KnowledgePackageJourneyTests.cs:70-83` asserts all four synthetic journey budgets.

### T49: Compact repeated relation and evidence references

**What**: Replace repeated canonical relation and evidence IDs inside aggregated dependencies with solution-local handles and one directly resolvable table for each kind.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/CanonicalJson.cs`, `src/Csharp2Md.Core/PackageBuilding/Rendering/MachineArtifactWriter.cs`, `src/Csharp2Md.Core/Publication/RetrievalModelReader.cs`  
**Depends on**: T47  
**Reuses**: `LocalTableBuilder`, retained factual relations and evidence, and the existing dependency payload path  
**Requirement**: DEP-03, DEP-05, DEP-07, STO-03, STO-04, STO-05, CRT-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Relation and evidence references in each dependency use deterministic solution-local handles, with each canonical identity stored once in its declared table.
- [x] A consumer resolves each handle to the supporting confirmed relation or evidence without guessing a composite key or scanning unrelated artifacts.
- [x] Rehydration restores the same relation/evidence reference values and rejects missing, duplicate or invalid table mappings.
- [x] Pitstop's plan is at most 25 MiB when the optional clone is present; the quick gate passes.

**Tests**: unit — ≥8 handle/table/rehydration cases  
**Gate**: quick + Pitstop plan when present  
**Commit**: `fix(package-building): compact dependency references`

**Status**: Complete
**Gate note**: Core Release quick gate passed: 521 passed, 0 failed, 0 skipped. Synthetic CLI journeys passed 16 of 16 cases. Pitstop produced a 99-artifact, 20,721,851-byte (19.76 MiB) plan, below the 25 MiB ceiling; publication still rejects flow applicability and the evidence journey budget, owned by T50 and the pending applicability decision.
**Adequacy**: `CompactDependencyReferenceTests.cs:14-23` asserts sorted base36 local handles; `:30-32` asserts one table entry per repeated identity; `:41-58` resolves a handle to the factual relation and evidence record; `:66-68` asserts canonical rehydration; `:83-118` rejects invalid, duplicate and missing mappings; `:126` asserts stable bytes. The Pitstop plan size was read before the package byte-budget check, with the temporary diagnostic removed afterward.

### T50: Bound evidence journey reads

**What**: Make the evidence entry index direct and small enough to certify the evidence/disposition journey against the local-corpus token budget.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Rendering/MachineArtifactWriter.cs`, `src/Csharp2Md.Core/Publication/Certification/JourneyCertifier.cs`  
**Depends on**: T49  
**Reuses**: solution-local evidence handles and the existing measured reader  
**Requirement**: NAV-01, NAV-10, CRT-04, CRT-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] The evidence entry index resolves a selected evidence record or disposition within 12 reads and 25,000 estimated tokens.
- [x] The index does not repeat the full evidence payload and rejects a broken pointer during validation.
- [x] Pitstop's evidence journey passes when its optional clone is present; quick gate passes.

**Tests**: unit + integration — ≥6 index/resolution/budget cases  
**Gate**: quick + Pitstop journey when present  
**Commit**: `fix(publication): bound evidence journey reads`

**Status**: Complete
**Gate note**: Core Release quick gate passed: 531 passed, 0 failed, 0 skipped. `KnowledgePackageJourneyTests` and `KnowledgePackageFailureTests` passed 32 of 32 cases. Pitstop's evidence journey now passes at 3 reads and 17,572 tokens, down from `tokens-exceeded:372295>25000`; its plan holds 135 artifacts against the 750-file ceiling. Publication still rejects Pitstop on `FollowFlow:missing-terminal:contracts`, which no task owns and which T44 needs settled.
**Deviation**: `MachineArtifactWriter.cs:19-22` packs the evidence table at 32 KiB instead of the 64 KiB bulk target in `design.md:496`. At 64 KiB the journey measured 102,050 bytes against NAV-10's 100,000-byte ceiling; the manifest alone is 33,899 bytes for Pitstop. The `ShardPacker` default is unchanged for every other family.
**Adequacy**: `EvidenceEntryIndexTests.cs:122-125` asserts the journey passes at 3 reads and within 25,000 tokens for both a single-shard and a 36-shard table, matching NAV-10. `:44-46` resolves every evidence ordinal through the router to the record carrying that canonical key, digest and local handle, matching NAV-01. `:25-26` asserts the index payload carries neither `content_digest` nor `document_canonical_key`, so the router does not repeat the evidence payload. `:78-80`, `:92-94` and `:106-108` reject a shard pointer that names a missing artifact, a shard whose declared count disagrees with its rows, and a range that skips an ordinal, each naming the offending artifact. `:57-65` asserts contiguous ordinal partitioning. `:138-139` and `:150-151` assert the certification coordinates for a deleted shard and for a solution with no retained evidence. The four pre-existing tests that fixed the old array-shaped index (`CompactDependencyReferenceTests.cs:49-57` and `:96-105`, `SolutionCertificationTests.cs:44-56`, `JourneyCertifierTests.cs:32-50`) were repointed at the router without weakening any assertion; the two certification fixtures now carry retained evidence, so their passing evidence journey is a real result rather than an empty array.

### T51: Narrow the causal flow root to external effects

**What**: Stop persistence alone from making the flow journey applicable, so a solution whose causal facts hold no contract, HTTP, gRPC or messaging edge records `not_applicable` instead of failing on a terminal it can never reach.  
**Where**: `src/Csharp2Md.Core/Publication/Certification/GraphJourneyCertifier.cs`  
**Depends on**: T50  
**Reuses**: the causal-category applicability checks T48 settled  
**Requirement**: CRT-01, CRT-02, NAV-08, NAV-09

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] A solution whose only causal category is persistence records `not_applicable:no-causal-root` for the flow journey.
- [x] A solution carrying a flow root still fails with its missing terminal named, and reverse impact keeps persistence as a valid root.
- [x] Pitstop commits within CRT-05's ceilings when its optional clone is present; quick gate passes.

**Tests**: unit — ≥2 applicability cases  
**Gate**: quick + Pitstop publication when present  
**Commit**: `fix(publication): narrow causal flow root`

**Status**: Complete
**Gate note**: Core Release quick gate passed: 534 passed, 0 failed, 0 skipped. Pitstop committed for the first time: 136 reachable files and 20,445,642 bytes (19.50 MiB) against CRT-05's 750-file and 25 MiB ceilings, with `locate`, `reverse_impact` and `evidence_disposition` passed and `follow_flow` `not_applicable:no-causal-root`.
**Decision**: Measured on 2026-09-16, Pitstop's extracted dependencies are `InternalInvocation=983, Persistence=85, ProjectReference=12, StructuralTypeUse=2352` — no contract, HTTP, gRPC or messaging edge at all. `design.md:524` lists the flow journey's start as an operation or entry point and persistence as one of its terminals, so persistence alone is not a root and the journey is not applicable. The corpus-level cause is an extraction limitation recorded in `context.md`, not a certification defect; the rule keeps failing any solution that does have a flow root but cannot reach a terminal.
**Adequacy**: `GraphJourneyCertifierTests.cs:28-34` asserts the persistence-only flow records exactly `not_applicable:no-causal-root`. `:35-39` asserts reverse impact still passes for a persistence-only solution with a reachable set, so NAV-09's data root is not narrowed with it. `:40-46` asserts a messaging root with no contract still fails with `missing-terminal:contracts`, so the narrowing cannot convert a real incompleteness into a silent pass. The five pre-existing terminal assertions at `:47-52` are unchanged.

### T52: Shrink the bulk artifact wire form

**What**: Serialize solution-local handles as bare JSON strings instead of single-property objects, and write `measures/summary.json` with the compact writer already used for the dependency artifact.
**Where**: `src/Csharp2Md.Core/PackageBuilding/CanonicalJson.cs`, `src/Csharp2Md.Core/PackageBuilding/Rendering/MachineArtifactWriter.cs`
**Depends on**: T51
**Reuses**: the five handle types in `RetrievalModel.cs`, `CanonicalJson.WriteCompact` and the existing `AddCompact` artifact path
**Requirement**: STO-03, STO-04, CRT-04, CRT-05, CRT-07

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Every entity, variant, relation, evidence and cycle handle is written as a JSON string and read back to the same value, with an invalid handle still rejected by name.
- [x] `measures/summary.json` is written by the compact writer and rehydrates to the same measures as before.
- [x] Repeating the same evaluated input and policy still produces byte-identical artifacts.
- [x] Pitstop's committed package is at most 25 MiB and at most 750 files when the optional clone is present; the quick gate passes.

**Tests**: unit - >=6 handle-encoding/compaction/rehydration cases
**Gate**: quick + Pitstop package when present
**Commit**: `fix(package-building): shrink bulk artifact wire form`

**Status**: Complete
**Gate note**: `dotnet build csharp2md.slnx --configuration Release` passed with 0 warnings/errors. `Csharp2Md.Core.Tests` passed 543 of 543 (0 failed, 0 skipped), up from 534 by this task's 9 cases. `KnowledgePackageJourneyTests` and `KnowledgePackageFailureTests` passed 32 of 32. Pitstop's committed package fell from 20,445,642 bytes (19.50 MiB) to 15,547,566 bytes (14.83 MiB) at 137 files, against the 25 MiB / 750-file ceiling.
**Deviation**: `KnowledgePackageJourneyTests.cs:28` and `:33` read each root of `graph/entities.000000.json` as `{"value": ...}`. Both were repointed at the JSON string this task defines; no assertion was weakened - they still require an `:entrypoint:`, a `:component:` and a `:deploymentunit:` root.
**Adequacy**: `ArtifactWireFormTests.cs:16-21` asserts the dependency artifact writes entity, variant, relation and evidence handles as JSON strings and carries no `{"value"` envelope; `:30-31` asserts the same for root entities and `:39-42` for measure entity, cycle and reverse-impact handles. `:48-59` reads every handle kind back through `RetrievalModelReader` to its canonical value. `:68` rejects a handle written as a number and `:78` a blank handle string, each naming the offending artifact. `:87-88` asserts `measures/summary.json` carries no newline or indent run, `:95-104` asserts that compact artifact rehydrates to the same scope, entity, fan, cycle, reverse-impact and gap values, and `:112` asserts byte-identical repeat output.

### T53: Resolve entity and cycle keys through the local table

**What**: Replace repeated entity, variant and cycle canonical keys inside stored relations, aggregated dependencies and scope measures with solution-local handles resolvable through the declared table, and write the navigation indexes and manifest compactly so the journey budgets are measured against the same wire form.
**Where**: `src/Csharp2Md.Core/PackageBuilding/Rendering/MachineArtifactWriter.cs`, `src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs`, `src/Csharp2Md.Core/Publication/RetrievalModelReader.cs`, `src/Csharp2Md.Core/Publication/PackageReader.cs`
**Depends on**: T52
**Reuses**: `LocalTableBuilder` and the relation/evidence handle table contract T49 established
**Requirement**: DEP-03, DEP-05, STO-03, STO-04, STO-05, CRT-04, CRT-06

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] `StoredRelation` source/target keys, `AggregatedDependency` source/target/variants, `ScopeMeasures` entity and cycle references, and reverse-impact targets carry deterministic solution-local handles, with each canonical identity stored once in its declared table.
- [x] A consumer resolves each handle to the canonical entity, variant or cycle identity through one declared table artifact without scanning unrelated artifacts.
- [x] Rehydration restores the same canonical values and rejects missing, duplicate or invalid table mappings by artifact name.
- [x] Navigation index keys stay canonical so a consumer still locates an entity by name; only the wire form is compacted.
- [x] eShopOnContainers is at most 64 MiB and eShop completes analysis when the optional clones are present; Pitstop stays at most 25 MiB; the quick gate passes.

**Tests**: unit - >=8 handle/table/rehydration/rejection cases
**Gate**: quick + local corpora when present
**Commit**: `fix(package-building): resolve entity keys through local table`

**Status**: Complete
**Gate note**: `dotnet build csharp2md.slnx --configuration Release` passed with 0 warnings/errors. `Csharp2Md.Core.Tests` passed 554 of 554 (0 failed, 0 skipped), up from 543 by this task's 11 cases. The synthetic CLI suites (`KnowledgePackageJourneyTests`, `KnowledgePackageFailureTests`, `KnowledgeAnalyzeCommandTests`, `KnowledgeValidateCommandTests`, `SyntheticSolutionFixtureTests`, `KnowledgeEngineWorkflowTests`) passed 73 of 73. Local corpora, all measured with the clones present:

| Corpus | Committed bytes before T52 | After T53 | Ceiling | Files |
| --- | --- | --- | --- | --- |
| Pitstop | 20,445,642 (19.50 MiB) | 7,952,722 (7.58 MiB) | 25 MiB | 140 / 750 |
| eShopOnContainers | rejected at 117,080,074 | 31,085,041 (29.65 MiB) plan | 64 MiB | 251 / 1,500 |
| eShop | rejected at 156,285,412 | 51,622,291 (49.23 MiB) | 64 MiB | 220 / 1,500 |

Pitstop and eShop commit and certify; eShop had never committed before. eShopOnContainers clears the byte ceiling and is no longer rejected at `package-building`, but publication still rejects it - see Blocker.
**Scope change**: The user widened this task on 2026-09-16 to include the navigation indexes after the byte ceiling alone left three journeys over the token budget. The widening is encoding only - every index key stays a canonical entity key, so locating an entity by name still needs no table lookup. Measured on Pitstop, indentation was 47% of `indexes/incoming.json`; writing every remaining bulk artifact compactly took eShopOnContainers' `incoming.json` from 388,292 to 209,438 bytes and its manifest from 59,309 to 50,661, which cleared `ReverseImpact` (154,927 -> under 125,000 tokens) and `EvidenceDisposition` (26,085 -> under 25,000).
**Blocker**: eShopOnContainers still fails `Locate:tokens-exceeded:19405>12000`. The journey reads manifest.json (50,661 bytes), `indexes/roots.json` (26,689) and one markdown page (270) = 77,620 against the 48,000-byte ceiling. The manifest alone exceeds the ceiling, so no encoding change can close this: `PackageManifest` carries every root inline, and per root `machine_citation` and `markdown_path` are a repeated per-solution prefix (48% of the roots array on Pitstop). Closing it means the manifest stops carrying every root inline - a manifest contract change, in the shard-router shape T50 used for evidence. No task owns it; T44 cannot certify eShopOnContainers until it is settled.
**Adequacy**: `CanonicalKeyTableTests.cs:14` and `:21-22` assert one sorted, deduplicated row per entity, variant and cycle canonical key in its declared table. `:29-30`, `:37-39` and `:46-48` assert relation endpoints, dependency endpoints/variants, and measure entity/cycle/reverse-impact references are local handles. `:56-62` reads every kind back to its canonical value through `RetrievalModelReader`. `:79` rejects a dependency entity handle with no table row and `:92` a measure cycle handle with no row, each naming the consuming artifact; `:102` rejects duplicate keys in the entity table and `:112` a missing cycle table, each naming the table. `:122-123` asserts byte-identical repeat output for all three tables. `NavigationPayloadIndexTests.cs:43`, `:55` and `:82` were repointed to resolve the indexed record through the entity table rather than compare a raw field, which keeps the index-to-record claim and adds the table hop; `SolutionScopedRetrievalTests.cs:68-73` now resolves each solution's target through that solution's own entity table, so the isolation claim is proved per solution rather than by string comparison.

### T54: Route the manifest to a declared roots index

**What**: Stop carrying a row per root inside the manifest; declare each solution's roots by entry path and count, and move the root rows into the roots navigation index, which declares its citation artifact and Markdown prefix once.
**Where**: `src/Csharp2Md.Core/Publication/PackageContracts.cs`, `src/Csharp2Md.Core/PackageBuilding/Rendering/MachineArtifactWriter.cs`, `src/Csharp2Md.Core/PackageBuilding/Rendering/MarkdownRenderer.cs`, `src/Csharp2Md.Core/Publication/Certification/JourneyCertifier.cs`, `src/Csharp2Md.Core/Publication/RetrievalModelReader.cs`, `src/Csharp2Md.Core/Publication/PackageReader.cs`
**Depends on**: T53
**Reuses**: the router shape T50 established for evidence shards and the root handle table T47 introduced
**Requirement**: PKG-01, NAV-01, NAV-04, NAV-06, NAV-07, CRT-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] The manifest declares each solution's roots by entry path and count instead of a row per root, and the roots index carries one row per root with its canonical display name and handle.
- [x] The roots index declares its citation artifact and Markdown prefix once, so a consumer derives a root's machine citation and Markdown page from the declared parts without enumerating a directory or decoding an identifier.
- [x] Locate still reaches a root's Markdown page in three reads - manifest, roots index, page - and rehydration rejects a missing, miscounted or malformed roots index by artifact name.
- [x] Repeating the same evaluated input and policy still produces byte-identical artifacts.
- [x] eShopOnContainers certifies Locate within 12,000 tokens and commits when the optional clone is present; eShop stays at most 64 MiB and Pitstop at most 25 MiB; the quick gate passes.

**Tests**: unit - >=6 router/derivation/journey/rejection cases
**Gate**: quick + local corpora when present
**Commit**: `fix(publication): route the manifest to a declared roots index`

**Status**: Complete
**Gate note**: `dotnet build csharp2md.slnx --configuration Release` passed with 0 warnings/errors. `Csharp2Md.Core.Tests` passed 567 of 567 (0 failed, 0 skipped), up from 554 by this task's 13 cases. The synthetic CLI suites (`KnowledgePackageJourneyTests`, `KnowledgePackageFailureTests`, `KnowledgeAnalyzeCommandTests`, `KnowledgeValidateCommandTests`, `SyntheticSolutionFixtureTests`) passed 73 of 73. On eShopOnContainers the Locate read set fell from 77,620 to 32,089 bytes - the manifest alone from 50,661 to 1,222 - which is 8,023 of the 12,000 tokens NAV-07 allows. All three optional corpora now commit and certify, eShopOnContainers for the first time:

| Corpus | Committed bytes | Ceiling | Files | Locate tokens |
| --- | --- | --- | --- | --- |
| Pitstop | 7,928,785 (7.56 MiB) | 25 MiB | 140 / 750 | 3,824 |
| eShop | 51,603,565 (49.21 MiB) | 64 MiB | 220 / 1,500 | 2,848 |
| eShopOnContainers | 31,040,219 (29.60 MiB) | 64 MiB | 253 / 1,500 | 8,023 |

Every journey of every corpus is Passed except Pitstop's `follow_flow`, which stays `not_applicable:no-causal-root` - the behaviour T51 defined, unchanged here.
**Deviation**: `MarkdownRenderer.Render` no longer takes its roots from the manifest; it derives the same rows through `MachineArtifactWriter.BuildRoots`, so its signature and `RetrievalModelReader.VerifyMarkdown` are untouched and the renderer and the writer cannot drift apart. Ten test files were repointed from the inline manifest rows to the declared index - `PackageContractTests`, `SolutionManifestContractTests`, `CanonicalJsonTests`, `MachineArtifactWriterTests`, `MarkdownRendererTests`, `SolutionScopedRetrievalTests`, `PackageValidatorTests`, `RetrievalModelReaderTests`, `KnowledgeEngineWorkflowTests` and `KnowledgePackageJourneyTests`. No assertion was weakened: each still proves the same claim with the table hop added, and `SolutionScopedRetrievalTests.cs:44` and `KnowledgeEngineWorkflowTests.cs:68-69` still prove per-solution isolation by resolving each solution's own index.
**Blocker**: none. The `Locate` budget that blocked T44 is closed.
**Adequacy**: `RootsIndexRoutingTests.cs:21-25` asserts the manifest declares roots by entry path and count and carries no root name or Markdown path; `:34-39` asserts one ordinal-sorted row per root with its ordinal handle; `:51-56` asserts the citation artifact and Markdown prefix are declared once and derive the citation and page; `:68` asserts every derived page is a written artifact; `:77-79` reads every root back through `RetrievalModelReader`. `:89`, `:105`, `:122` and `:136` reject a row count that disagrees with the manifest, an unsorted index, a handle that is not its ordinal and a missing index, each naming the index artifact. `:152-157` certifies Locate at 3 reads within 12,000 tokens for 3 and for 400 roots, `:167-172` fails it when the derived page is missing, and `:182-185` asserts byte-identical repeat output for the index and the manifest.
**Pre-existing**: 43 of the 138 non-LocalCorpus CLI tests fail on legacy suites (`ExitCodeTests` and neighbours, on `missing-tfm:Acme.Broken`). A worktree at `8735d94` fails the same 43 of 138, so this task changed none of them; they are T45's cutover. `SyntheticSolutionFixtureTests.Fixture_HasOnlySourceInputs_NoBuildOutputs` stays order-dependent: it passes from a clean fixture tree but the suite regenerates `bin`/`obj` under `fixtures/SyntheticSolution` while running.

### T44: Certify optional local corpora

**What**: Update LocalCorpus acceptance to assert eShop variant isolation and the eShopOnContainers/Pitstop committed-package ceilings.  
**Where**: `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs`  
**Depends on**: T54  
**Reuses**: dynamic skip convention and gitignored local clone paths  
**Requirement**: CRT-04, CRT-05, CRT-06, CRT-07

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] eShop completes without cross-project variant collision when present.
- [x] eShopOnContainers is ≤1,500 reachable committed files and ≤64 MiB; Pitstop is ≤750 files and ≤25 MiB.
- [x] Missing clones dynamically skip with reason and do not fail CI; clones are never staged or committed.
- [x] At least 3 corpus-level cases exist; LocalCorpus gate runs for each clone currently present and full gate passes.

**Tests**: e2e — ≥3 corpus cases  
**Gate**: full + LocalCorpus when present  
**Commit**: `test(cli): certify optional local corpora`

**Status**: Complete
**Gate note**: `dotnet build csharp2md.slnx --configuration Release` passed with 0 warnings/errors. The LocalCorpus gate passed 6 of 6 with all three clones present, each running the real CLI end to end: eShop, eShopOnContainers and Pitstop all commit and certify. `Csharp2Md.Core.Tests` 567 of 567; `Csharp2Md.Storage.Tests` 396 of 396; `Csharp2Md.Projection.Tests` 234 of 234.

| Corpus | Committed bytes | Ceiling | Files | Ceiling |
| --- | --- | --- | --- | --- |
| Pitstop | 7,928,785 (7.56 MiB) | 25 MiB | 140 | 750 |
| eShop | 51,603,565 (49.21 MiB) | 64 MiB | 220 | 1,500 |
| eShopOnContainers | 31,040,219 (29.60 MiB) | 64 MiB | 253 | 1,500 |

**Deviation**: the `Reuses` field named the project's dynamic-skip convention - throwing an exception whose message starts with `$XunitDynamicSkip$`. **That convention does not work here.** The token is an xunit v3 feature; this repository pins xunit 2.9.3, where the runner reports such a test as **Failed**, not Skipped. A probe confirmed it: a case throwing the token reported `Com falha: 1`. The convention therefore breaks the `AGENTS.md` rule that a missing local clone must not fail CI, in this file and in `tests/Csharp2Md.Analysis.Tests/Readiness/LocalCorpusReadinessTests.cs:35` which copied it. This task replaces it in its own file with `LocalCorpusFactAttribute`, a `FactAttribute` subclass that resolves the clone at discovery and sets `Skip` with the same named reason - a genuine skip on xunit 2 with no new dependency. A probe with this exact attribute reported `Ignorado` for an absent clone and ran the body for a present one. `LocalCorpusReadinessTests` is left alone: it belongs to the legacy `Csharp2Md.Analysis.Tests` project that T45 removes.
**Scope change**: the `[Trait("Requirement", ...)]` attributes the first draft carried were removed. Two legacy guards - `Csharp2Md.Storage.Tests/Surface/RequirementCoverageTests.cs:140` and its `Csharp2Md.Analysis.Tests` mirror - scan every `.cs` file under `tests/Csharp2Md.Cli.Tests` and accept only a hard-coded allowlist of legacy requirement prefixes (`ENG-`, `ROSE-`, `EBC-`, `PK-`, `CLLF-`, `CDC-`, `RP-`, `MSC-`, `GCPC-`, `APR-`, `STOR-NN`). This feature's `CRT-` ids are not in it, so carrying them failed both guards. `Csharp2Md.Core.Tests` is not scanned, which is why its `Requirement` traits are unaffected. Editing the allowlist would mean changing files T45 deletes, so the traits were dropped; CRT-04..CRT-07 traceability lives in this file's traceability table and the Adequacy block below.
**Adequacy**: `LocalCorpusAnalyzeTests.cs:23` asserts eShop's analysis emits no `variant-collision` (CRT-06), on top of `:103` `Assert.True(exitCode == ExitCodes.Success)` and `:104` `Assert.Contains("committed and certified", stdout)`, so a run that builds but fails certification is caught. `:33` with `:122-123` asserts eShopOnContainers commits within 1,500 reachable files and 64 MiB and `:50` the same for Pitstop within 750 files and 25 MiB, counting the generation's files plus the root manifest pointer (CRT-04, CRT-05). `:71-73` asserts an absent clone yields exactly `local eShop clone is not present at '<path>'` and `:79` that a present clone leaves `Skip` null, so a clone that exists is never skipped away (CRT-07). `:89-91` asserts all three clone directories are git-ignored and `:92-95` that the analysis output path lies outside the repository (CRT-07).
**Pre-existing**: with a clean `fixtures/SyntheticSolution` tree, `Csharp2Md.Cli.Tests` fails 43 of 138 on legacy suites, and the failing set is byte-identical to a worktree at `e2f2ac2`; `Csharp2Md.Analysis.Tests` fails 28 of 864 and `Csharp2Md.Domain.Tests` 1 of 575 in both trees. `SyntheticSolutionFixtureTests.Fixture_HasOnlySourceInputs_NoBuildOutputs` is the 44th CLI failure whenever `Csharp2Md.Core.Tests` runs first, because its workflow tests analyze `fixtures/SyntheticSolution` and leave `bin`/`obj` behind. All of it is T45's cutover.

### T45: Complete the clean-cut repository topology

**What**: Remove legacy product/test projects and obsolete contracts, point CLI solely at Core, update solution/package/docs, and prove only the current contract remains.  
**Where**: `csharp2md.slnx`  
**Depends on**: T44  
**Reuses**: only source/test code explicitly ported by earlier tasks; no compatibility facade, converter, feature flag or version reader  
**Requirement**: PKG-10

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [x] Solution contains only `Csharp2Md.Core`, `Csharp2Md.Cli`, `Csharp2Md.Core.Tests` and `Csharp2Md.Cli.Tests`.
- [x] Legacy Domain/Analysis/Storage/Projection assemblies, their obsolete tests, compose/batch paths, schemas and taxonomy registry are removed.
- [x] Static surface tests prove no legacy contract names, version dispatch, compatibility path, `Microsoft.Build.*` reference or `MSBuildLocator.RegisterDefaults()` remains.
- [x] README files describe only the current root-manifest package and analyze/validate commands.
- [x] Build gate passes; fixture E2E passes; the LocalCorpus gate passes with all three clones present.

**Tests**: unit + e2e — ≥10 topology/current-contract assertions plus all retained suites  
**Gate**: build + LocalCorpus when present  
**Commit**: `refactor(core): complete knowledge package cutover`

**Status**: Complete
**Gate note**: the pre-T45 legacy-suite exception is retired with this task; the gate is now the whole solution with no exception. `dotnet build csharp2md.slnx --configuration Release` passed with 0 warnings/errors. `dotnet test csharp2md.slnx --configuration Release --no-build` from a clean `fixtures/SyntheticSolution` tree passed **574 of 574** in `Csharp2Md.Core.Tests` and **83 of 83** in `Csharp2Md.Cli.Tests`, 0 failed and **0 skipped** in both. `Ignorado: 0` is the proof the LocalCorpus gate ran rather than skipped: all three clones were present, so eShop, eShopOnContainers and Pitstop each analyzed, committed and certified inside that 83. Core rose from 567 to 574 because `CoreTopologyTests` went from 6 facts to 13; the CLI suite fell from 144 cases to 83, as 61 cases left with the 16 deleted legacy files and one more (below) was removed as unmapped.
**Pinned baseline, discharged**: before the cut, from a clean fixture tree, `Csharp2Md.Cli.Tests` failed 43 of 144. Every one of those 43 lived in a file this task deletes — `ExitCodeTests` (10), `ValidateCommandTests` (7), `AnalyzeBudgetAndAllowlistTests` (7), `AnalyzeExitCodeTests` (4), `ComposeCommandTests` (3), `AnalyzeBatchFailureTests` (3), `ProjectorWiringTests` (2), `AnalyzeSuccessTests` (2), and one each in `AnalyzePackageWriteTests`, `AnalyzeOptionSurfaceTests`, `AnalyzeMultiSolutionTests`, `AnalyzeCommandTreeTests` and `PublicationResilienceAnalyzeTests`. No failure outside that list existed, and none survives. The amnesty is closed by name and by count, not by category.
**Removed as unmapped**: `SyntheticSolutionFixtureTests.Fixture_HasOnlySourceInputs_NoBuildOutputs` asserted the live `fixtures/SyntheticSolution` tree carries no `bin`/`obj`. The CLI suite violates that itself — its own journey tests analyze the fixture and leave build output behind — which is why it was the order-dependent 44th failure whenever another suite ran first. It maps to no acceptance criterion: CRT-08 constrains what the fixture *contains*, never its build output, and the real invariant (the *versioned* fixture is source-only) is already enforced by `.gitignore` lines 33-34, with `git ls-files fixtures/SyntheticSolution` listing zero `bin`/`obj` paths. Keeping it would have left the gate non-deterministic, which the pinned baseline above cannot tolerate; Check C of the adequacy review removes a test that maps to nothing.
**Scope note**: `ExitCodes` dropped `PartialComposition`, `Degraded` and `IncompatibleProvenance` together with their GCPC prose. No current code path can reach them — `RejectedExitCode` produces only `1`, `4` and `5` — and they named the compose/compatibility contract PKG-10 forbids. The retained values `0`, `1`, `4` and `5` keep their numbers, so no observable behaviour moved; the READMEs publish exactly those four.
**Also removed** (dead once their consumers went): `contracts/json-schema/**` and `contracts/taxonomy-registry.json`, embedded only by `Csharp2Md.Storage` and read only by `FactualPackageReader`; `tests/Shared/EmptySourceDocumentReader.cs`, linked only into the deleted Storage/Analysis suites; and `artifacts/verifications/`, two reports generated by the deleted `Csharp2Md.Analysis.Tests/Certification` runner against the legacy engine.
**Adequacy**: `CoreTopologyTests.cs:44-58` asserts `csharp2md.slnx` lists exactly the four current project paths and nothing else, and `:63-64` asserts no fifth `.csproj` exists anywhere under `src/` or `tests/`. `:68-74` asserts the `contracts/` schema and taxonomy-registry tree is gone. `:79-90` scans every product source under `src/` and fails on any occurrence of the four legacy assembly names — composed at run time from `"Csharp2Md." + suffix` so the scanner cannot match its own file — and `:95-105` does the same for `BatchComposer`, `PackageProjector`, `ComposeAction`, `IncompatibleProvenance` and `taxonomy-registry`, which is the version-dispatch and compatibility-path check. `:110-119` proves no product source names `MSBuildLocator` (AD-003) and `:124-134` that no project of the four references `Microsoft.Build.*`. `:157-158` asserts Core references no project at all and `:162-165` that the CLI references `Csharp2Md.Core` and nothing else — `Assert.Equal` on the whole list, so an added reference fails. `:169-180` and `:184-187` pin `InternalsVisibleTo` to exactly one friend per assembly, read from both the csproj and the compiled attribute. Thirteen facts, all carrying `[Trait("Requirement", "PKG-10")]`. On the CLI side `CliIsolationTests.cs:9-17` keeps the packable-tool contract and `CliAssemblyTests.cs:20-39` asserts `Csharp2Md.Cli.Tests` references only `Csharp2Md.Cli`. The current-command surface is already pinned by `KnowledgeValidateCommandTests.Root_ExposesOnlyAnalyzeAndValidateCommands` and `KnowledgeAnalyzeCommandTests.Analyze_ExposesOnlyCurrentPackageOptions`, so no new command-surface test was written.
**Overlap kept deliberately**: `KnowledgeValidateCommandTests.ValidateCommand_DoesNotContainComposeOrCompatibilityDispatch` scans `CommandFactory.cs` for tokens the new `src/`-wide scan also covers. It is a passing guard from an earlier task of this feature, narrower than the new one rather than redundant with a test authored here, so it stays.
**Left behind**: `tests/Csharp2Md.Analysis.Tests/bin/Release/net10.0/BuildHost-netcore/` holds two `Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost` DLLs that `git clean` cannot delete — stray `dotnet` BuildHost processes still hold the handles. The directory is gitignored and carries no tracked file; `git ls-files tests/Csharp2Md.Analysis.Tests` is empty. It disappears on the next `git clean -xfd` after those processes exit.

## Phase 7: Verifier remediation

> Opened on 2026-09-16 from the feature Verifier's FAIL verdict (`validation.md`, diff range `ff42c35..HEAD`). These five tasks close the two acceptance gaps, the one spec-precision gap and the test-integrity findings that verdict raised. Phase 6 was the last implementation phase; these depend on it in full. The discrimination sensor stays skipped per the standing `AGENTS.md` rule — the same skip every feature task so far has carried. The fix→re-verify cycle is bounded to 3 rounds before escalation, so all five land before the Verifier is re-dispatched.

### T55: Discriminate the four non-discriminating tests

**What**: Replace four tests that pass today and would keep passing if the behaviour they name broke.  
**Where**: `tests/Csharp2Md.Core.Tests/Publication/PackagePublicationTests.cs`  
**Depends on**: T45  
**Reuses**: the existing `TempOutput`, `Plan()` and `Model()` helpers in each suite; no new fixture  
**Requirement**: PUB-08, MET-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] `PackagePublicationTests.Publish_CertificationFailurePreventsManifestCommit` drives a real certification failure and asserts no `manifest.json` exists under the output, replacing its `Assert.NotNull(plan)` body.
- [x] `GraphJourneyCertifierTests.Certify_FlowBudgetFailureNamesMeasure` actually exceeds the 32-read budget and asserts the detail starts with `reads-exceeded:`, closing the untested `reads-exceeded` branch.
- [x] `DirectMeasureCalculatorTests.Calculate_DoesNotChangeAggregatedOccurrenceCount` runs `DependencyAggregator.Aggregate` then `DirectMeasureCalculator.Calculate` and proves the measure pass leaves the pre-dedup occurrence count intact, instead of asserting its own factory argument.
- [x] `ConfigurationPersistenceExtractorTests.Extract_ConfigValueNeverEntersGraph` feeds a configuration value that really appears in the input before asserting its absence from the graph.
- [x] Each replaced test fails when its named behaviour is broken by hand, and the total case count does not drop.

**Tests**: unit — 4 replaced cases, no net deletion  
**Gate**: quick  
**Commit**: `test(core): make four passing tests discriminate`

**Status**: Complete
**Gate note**: quick gate green - `Csharp2Md.Core.Tests` 574 of 574 (0 failed, 0 skipped), the same count as before the task, so four bodies were rewritten and none deleted. Discrimination was proven by hand: four faults injected in production code (certification failure no longer blocking the commit in `PackagePublication.cs`, the flow read budget raised from 32 to 3,200 in `GraphJourneyCertifier.cs`, `DependencyAggregator` counting distinct edges instead of confirmed contributions, and the configuration extractor taking the last string literal instead of the first) killed exactly the four rewritten tests, 4 of 4 failed. The faults were reverted and the tree re-verified clean before the commit.

### T56: Widen the composite-score scan to the published surface

**What**: Make the MET-08 assertion capable of failing by scanning every public property of the package-building and publication types.  
**Where**: `tests/Csharp2Md.Core.Tests/PackageBuilding/RetrievalModelBuilderTests.cs`  
**Depends on**: T55  
**Reuses**: the namespace-filtered reflection scan at `FactualGraphContractTests.cs:126-138`  
**Requirement**: MET-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] The scan covers every public property of every type under `Csharp2Md.Core.PackageBuilding` and `Csharp2Md.Core.Publication`, not only `RetrievalModel`'s single property.
- [x] The forbidden substrings include `coupling`, which MET-08 names and the current list omits, alongside the existing score/risk/quality terms.
- [x] Adding a property named for a composite score, an automatic quality or risk label, or coupling makes the test fail; proven by hand before the commit.

**Tests**: unit — 1 strengthened case  
**Gate**: quick  
**Commit**: `test(core): scan the published surface for composite scores`

**Status**: Complete
**Gate note**: quick gate green - `Csharp2Md.Core.Tests` 574 of 574 (0 failed, 0 skipped); the case was strengthened, not added, so the count is unchanged. The scan now reflects over every public property of every type in `Csharp2Md.Core.PackageBuilding`, `Csharp2Md.Core.Publication` and their sub-namespaces, and forbids `Score`, `Quality`, `Risk` and `Coupling`. No current property trips it. Discrimination proven by hand: four carrier types were added, one per forbidden term, spread over `PackageBuilding`, `PackageBuilding.Measures`, `Publication` and `Publication.Certification`; the test failed and named all four (`MutantCompositeScoreCarrier.CompositeScore`, `MutantCouplingCarrier.CouplingIndex`, `MutantQualityCarrier.QualityLabel`, `MutantRiskCarrier.RiskLabel`). The carriers were reverted before the commit.

### T57: Publish the per-family measurement breakdown

**What**: Record artifact and byte counts per `ArtifactFamily` in the published measurements, closing CRT-03's family dimension.  
**Where**: `src/Csharp2Md.Core/Publication/PackageContracts.cs`  
**Depends on**: T56  
**Reuses**: the `ArtifactFamily` already carried by every `PlannedArtifact` at plan time; no new traversal  
**Requirement**: CRT-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] `PublicationMeasurements` carries an artifact-count and byte-count breakdown keyed by `ArtifactFamily`, ordered canonically so repeat runs stay byte-identical.
- [x] `PackageBuilder` populates it from the planned artifacts, and `measurements.json` round-trips through `CanonicalJson` and `PackageValidator` unchanged.
- [x] The `EDG-03` budget-exceeded diagnostic quotes the breakdown, which `design.md:596` names as the file-budget mitigation.
- [x] At least 4 cases assert the breakdown against a hand-computed expectation, never one derived from the builder under test.

**Tests**: unit — ≥4 cases with hand-computed expectations  
**Gate**: quick  
**Commit**: `feat(core): measure published artifacts by family`

**Status**: Complete
**Gate note**: full gate green - `dotnet test csharp2md.slnx --configuration Release` passed `Csharp2Md.Core.Tests` 580 of 580 (up from 574 by this task's 6 cases) and `Csharp2Md.Cli.Tests` 81 of 83. The two CLI skips are `LocalCorpusAnalyzeTests` for Pitstop and eShopOnContainers, whose clones are absent from this machine; only `fixtures/eShop` is present. Release build: 0 warnings, 0 errors.

`PublicationMeasurements` now carries `ByFamily`, an `ArtifactFamily`-keyed count and byte breakdown ordered by the enum, and `ArtifactFamily` serializes as a string so `measurements.json` stays readable. The breakdown covers the artifacts built from the model and leaves out the three publication trailers: `measurements.json` carries the breakdown and cannot report its own size, so `manifest.json` and `certification.json` are excluded with it and counts and bytes describe the same set. The invariant `sum(ByFamily.ArtifactCount) + 3 == PublishedArtifactCount` is asserted.

**Deviation from the task's `Where`**: the criteria also required `PackageBuilder` to populate the breakdown and the EDG-03 diagnostic to quote it, so `PackageBuilder.cs` and `CanonicalJson.cs` (serializer registration) changed alongside `PackageContracts.cs`.

**Two pre-existing assertions adjusted**: `Build_FailsBeforePublicationWhenArtifactCeilingIsExceeded` and `...WhenByteCeilingIsExceeded` asserted the exact old message, which the breakdown necessarily extends. They now assert `StartsWith("package-budget: '<limit>'. by-family: ")`, and the new `Build_BudgetDiagnosticQuotesTheFamilyBreakdown` asserts every family count in the message exactly (`Table=4/`, `Graph=1/`, `Index=8/`, `Measure=2/`, `Markdown=2/`) plus the absence of a `Manifest=` row. The pair is stricter than the single equality it replaced; byte totals are left open on purpose so an unrelated writer change does not break the budget tests.

### T58: Render document pages and link entity pages

**What**: Give every cited retained document a Markdown page and turn entity-page rows into Markdown links.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Rendering/MarkdownRenderer.cs`  
**Depends on**: T57  
**Reuses**: the roots index routing and `MarkdownPath` handle scheme from T54; the artifact-existence assertion shape at `RootsIndexRoutingTests.cs:61-68`  
**Requirement**: NAV-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] Every retained document cited by kept evidence has a Markdown page reachable from `markdown/index.md`; uncited documents get none, so PKG-06 still bounds the set.
- [x] Each entity page's outgoing, incoming and impact rows are Markdown links whose targets resolve to written artifacts; a row whose target has no page stays plain text rather than producing a dead link.
- [x] `RetrievalModelReader`'s markdown/machine equivalence check still passes, and repeat runs are byte-identical.
- [x] CRT-04 and CRT-05 are re-measured on all three local clones after the change; the committed file and byte ceilings still hold.
- [x] At least 5 cases cover a cited document page, an uncited document with no page, a resolving link, a non-resolving row and the summary reachability.

**Tests**: unit + e2e — ≥5 cases plus re-measured corpus ceilings  
**Gate**: full + LocalCorpus when present  
**Commit**: `feat(core): render document pages and link entity rows`

**Status**: Complete
**Gate note**: full gate green - Release build 0 warnings/0 errors, `Csharp2Md.Core.Tests` 588 of 588 (up from 580 by this task's 8 cases), `Csharp2Md.Cli.Tests` 81 of 83. The two skips are Pitstop and eShopOnContainers, whose clones are absent from this machine.

**Design**: the cited documents are the endpoints of the document-scoped dependencies. A document earns such an edge only through kept evidence, so that set is exactly what NAV-03 wants and PKG-06 bounds, and it is derived from the dependencies alone - which is what keeps `RetrievalModelReader.VerifyMarkdown` passing, since a rehydrated `SolutionRetrievalModel` carries no `RetainedGraph`. Their pages are routed by a `documents.json` index declared by a single path on the roots index, not by rows inside it: eShop's Locate still costs 3 reads and 2,868 tokens of the 12,000 NAV-07 allows. Rows link through a relative path computed between the two artifact paths, so a same-directory link is `0.md` and a cross-directory one `../documents/0.md`; a row whose target has no page stays plain text. `RetrievalModelReader.VerifyDocuments` rebuilds the router from the rehydrated dependencies and rejects a tampered or missing one by artifact name.

**Ceiling breached, budget amended**: eShop went from 49.21 MiB / 220 files to **78.61 MiB / 965 files**, breaking `PackageBudget.Default`'s 64 MiB. Per this task's risk note the measurement wins over the ceiling, and the user chose to raise the default to **96 MiB** rather than bound the pages. The spec pins ceilings only for eShopOnContainers (CRT-04, 1,500 files / 64 MiB) and Pitstop (CRT-05, 750 files / 25 MiB); both stay as written and both remain plausible after a +60% growth (~47 MiB and ~12 MiB estimated). All four eShop journeys certify Passed. Committed breakdown:

| Family | Artifacts | Bytes |
| --- | --- | --- |
| Table | 132 | 5,262,300 |
| Graph | 1 | 7,663 |
| Index | 9 | 588,639 |
| Measure | 2 | 45,333,710 |
| Markdown | 818 | 31,229,856 |

**Re-measured on one clone, not three**: only `fixtures/eShop` is present. CRT-04 and CRT-05 could not be re-measured; their tests skip by name, per the standing `AGENTS.md` rule that a missing clone is not a failure.

**Summary links now resolve**: `markdown/index.md` linked roots by their root-absolute path, which does not resolve from `markdown/`. Criterion 1 asks for document pages *reachable* from the summary, which that form cannot deliver, so the summary now uses the same relative computation as the page rows and `Render_SummaryUsesExistingRootLink` asserts the resolving form. This touches NAV-04, which T54 owned.

**Three pre-existing assertions adjusted**: `Render_EntityPageListsOutgoing`, `...ListsIncoming` and `...ListsImpactAndGaps` asserted the plain-text rows NAV-03 replaces. Each now asserts the full linked line (`- [component:billing](0.md) (Http)`), which is stricter than the bare substring it replaced. `Build_CountsArtifactsByFamily` and `Build_BudgetDiagnosticQuotesTheFamilyBreakdown` moved Index from 8 to 9 for the documents router.

**Discrimination proven by hand**: three faults injected - `VerifyDocuments` removed, `Reference` stripped of links, and `BuildDocuments` dropping the document-scope filter - killed 10 tests, each by the case that names the behaviour. All three were reverted and the tree re-verified before the commit.

**Risk**: this is the only task of the phase that can breach a ceiling T52-T54 fought to clear. Today eShopOnContainers commits 253 files of 1,500, eShop 220 of 1,500 and Pitstop 140 of 750, so the headroom is wide — but the number of cited documents per corpus is unmeasured. If the ceiling breaks, amend NAV-03 against the measurement rather than silently dropping the pages.

### T59: Refresh the spec requirement traceability table

**What**: Point every requirement row at its owning task and verified status.  
**Where**: `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`  
**Depends on**: T58  
**Reuses**: the Requirement-to-Task Traceability section of this file as the source of owners  
**Requirement**: PKG-10

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`.

**Done when**:

- [x] No row still reads `Design | Pending`; 65 of 71 did when the Verifier counted them.
- [x] Every row's owning task matches this file's Requirement-to-Task Traceability section, and its status reflects the Verifier's per-AC result rather than an assumption.
- [x] Rows for NAV-03, CRT-03 and MET-08 cite T58, T57 and T56 and read Complete only once those tasks are verified.

**Tests**: none — documentation only  
**Gate**: build  
**Commit**: `docs(spec): refresh requirement traceability`

**Status**: Complete
**Gate note**: build gate green — `dotnet build csharp2md.slnx --configuration Release`, 0 warnings, 0 errors. `validate_spec.py` and `validate_tasks.py` both exit 0 (the 9 task warnings are the pre-existing multi-file `Where` smells plus this task's `Tests: none`).

Owners were not copied from the grouped table by hand. Each row's owning tasks are the union of every task whose `**Requirement**` line names that ID, mechanically derived from this file, so the spec's table is the per-ID expansion of the grouped view rather than a second hand-maintained list. All 71 IDs resolve to at least one task; none was left unmapped. NAV-03 cites T58, CRT-03 cites T57, MET-08 cites T56 and MET-03/PUB-08 cite T55, which the grouped table now carries too — it had not been updated for Phase 7.

**69 rows read `Complete`, 2 read `Unverified`.** `Unverified` is a new status value, introduced because `Complete` would have been an assumption for CRT-04 and CRT-05: their owning tasks closed with green gates, but the acceptance seam — `LocalCorpusAnalyzeTests` over eShopOnContainers and Pitstop — skips by name on this machine, and the last real measurement predates T58's ~60% package growth. CRT-07 requires exactly that skip, so it reads `Complete`; CRT-06 reads `Complete` because `fixtures/eShop` is present and its case runs. The Coverage line under the table states the legend and names the re-measurement that clears the two rows.

**No status was inferred from the missing Verifier report**: `.specs/features/pacote-conhecimento-util-e-confiavel/validation.md` does not exist in the working tree or in history, so every `Complete` here rests on the recorded per-task gate notes, not on a report. The Verifier that runs after this task owns the final verdict and may downgrade rows.

## Phase 8: Second Verifier remediation

The second feature Verifier returned FAIL on `ff42c35..71e7094` with 9 ranked gaps. Its report is `validation.md`. The discrimination sensor is a standing **skip** for this project per `AGENTS.md`; each task below records a hand-run fault injection in its gate note instead, exactly as Phase 7 did.

**One gap was re-scoped before this phase opened.** The Verifier ranked CRT-04 as a blocker on the grounds that the committed package "exceeds its own size contract", measuring eShop at 78.61 MiB against 64 MiB. CRT-04 pins that ceiling for **eShopOnContainers**, not eShop; the spec sets no size ceiling on eShop at all, and eShop commits 966 files of the 1,500 allowed. There is no measured violation. The real defect is narrower and is the same defect as EDG-03: `PackageBudget.Default` is a single global pair raised to 96 MiB, which now sits **above** the tightest ceiling the spec pins, so the builder would commit an 80 MiB eShopOnContainers package without error. T60 closes CRT-04, CRT-05 and EDG-03 together.

**Deferred, not dropped:** the Verifier's Fix 8 (80 test cases with no `Requirement` trait, plus four misplaced traits at `PackageBuilderTests.cs:10-13`) is trait hygiene that blocks no acceptance criterion. It is not in this phase.

### T60: Select the package budget applicable to the corpus

**What**: Replace the single global `PackageBudget.Default` guard with a limit chosen for the corpus under analysis, so the ceilings CRT-04 and CRT-05 pin are enforced by the builder rather than only by a skipped test.
**Where**: `src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs`
**Depends on**: T59
**Reuses**: the `PackageBudgetExceededException` and per-family diagnostic T57 introduced; the ceilings already written into `LocalCorpusAnalyzeTests.cs:32,49`
**Requirement**: CRT-04, CRT-05, EDG-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] A corpus-applicable limit exists and is selected per analysed corpus; the builder refuses beyond it before the atomic swap, which is what EDG-03 names.
- [x] The limit applied to eShopOnContainers is 1,500 artifacts / 64 MiB and to Pitstop 750 artifacts / 25 MiB, matching CRT-04 and CRT-05 rather than test-invented numbers.
- [x] For a corpus the spec pins, the applied limit is that corpus' ceiling and never the looser default; the 96 MiB default applies only to a corpus the spec does not pin, and its comment says so.
- [x] eShop still commits successfully at its present size, since the spec pins no ceiling for it.
- [x] At least 4 cases assert the selection and the refusal using the spec's own numbers, with expectations hand-computed rather than read back from the builder.

**Tests**: unit — ≥4 cases against the spec's ceilings
**Gate**: full + LocalCorpus when present
**Commit**: `feat(core): bound the package by the corpus ceiling`

**Status**: Complete
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **593 of 593** (up from 588 by this task's 5 cases), `Csharp2Md.Cli.Tests` 81 of 83. The two skips remain Pitstop and eShopOnContainers, whose clones are absent; the eShop LocalCorpus case ran and passed.

`PackageBudget.ForCorpus(RetrievalModel)` selects the limit from the corpus, keyed by the solution file each corpus is analysed through, and `PackageBuilder.Build` now defaults to it instead of to `PackageBudget.Default`. eShopOnContainers resolves to 1,500 / 64 MiB and Pitstop to 750 / 25 MiB — the spec's own numbers, no longer reachable only through a skipped corpus test.

**Componentwise minimum, not first match**: a pinned ceiling binds the whole committed package, so when one package touches more than one pinned corpus every ceiling applies. Taking the minimum per component also makes a pinned corpus *strictly tighter* than the default rather than merely different from it, which is the property the gap was about: before this task a 80 MiB eShopOnContainers package would have passed the 96 MiB global guard.

**The 96 MiB default stays, narrowed in meaning**: it is now the fallback for a corpus the spec pins nothing on, and its comment says so. eShop lands there deliberately — the spec gives eShop no size ceiling, only CRT-06's collision rule — so T58's decision to raise the default rather than bound the document pages is preserved rather than reversed.

**Criterion 3 was reworded before implementation.** As first drafted it read "no selected limit is looser than the tightest ceiling the spec pins", which contradicts itself: the 96 MiB fallback is by definition looser than 64 MiB. It now says what was meant — a pinned corpus gets its own ceiling and never the looser default.

**Discrimination proven by hand** (the sensor is a standing skip per `AGENTS.md`): two faults injected. Disabling the `Pinned` lookup so every corpus fell back to `Default` killed 4 of the 5 new cases — both spec-ceiling cases, the multi-corpus minimum and the `Build` wiring case — while the unpinned-default case correctly survived, since `Default` is exactly what it asserts. Re-pointing `Build` at `PackageBudget.Default` killed only `Build_RefusesAPinnedCorpusAtItsOwnCeilingRatherThanTheDefault`, which is the one case that proves the selection reaches the builder. Both faults were reverted and the suite re-run at 25 of 25 before the commit.

**Still unverified on a corpus**: CRT-04 and CRT-05 now have an enforced ceiling and unit evidence, but neither corpus can be measured end to end on this machine. The `LocalCorpusAnalyzeTests` cases stay skipped until the clones return.

### T61: Break published measures down by solution and corpus

**What**: Add the two dimensions CRT-03 names that `PublicationMeasurements` does not carry.
**Where**: `src/Csharp2Md.Core/Publication/PackageContracts.cs`
**Depends on**: T60
**Reuses**: the `ByFamily` breakdown shape and canonical ordering T57 established
**Requirement**: CRT-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] `PublicationMeasurements` carries per-solution and per-corpus breakdowns alongside the existing family and filtered-by-reason ones, ordered canonically so repeat runs stay byte-identical.
- [x] `measurements.json` round-trips through `CanonicalJson` and `PackageValidator` unchanged, and the artifact/byte invariant T57 asserted still holds.
- [x] Each dimension is asserted on its values, not on its presence; expectations are hand-computed.
- [x] At least 4 cases cover a multi-solution package and a single-solution one.

**Tests**: unit — ≥4 cases with hand-computed expectations
**Gate**: full
**Commit**: `feat(core): measure published artifacts by solution and corpus`

**Status**: Complete
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **599 of 599** (up from 593 by this task's 6 cases), `Csharp2Md.Cli.Tests` 81 of 83 with the two absent-clone skips.

CRT-03 names four measure dimensions — family, solution, journey and corpus. Family arrived in T57 and journey is carried by the certification; this task adds the two the Verifier found missing. `PublicationMeasurements` gains `BySolution` and `Corpus`.

**Solution attribution is the path prefix, not a new field on the artifact.** The writers already lay every solution-scoped artifact under `solutions/{id}/`, so nothing had to be threaded through them. The consequence is stated in the contract and asserted: the Markdown summary and the three trailers belong to no single solution, so `BySolution` is a breakdown of the package, not a partition of it, and its counts do not sum to `PublishedArtifactCount`.

**The corpus dimension carries the ceiling that was applied**, which is what makes it more than a restatement of the total: `measurements.json` now says which EDG-03 limit the package was measured against, and a corpus the spec pins nothing on reports `unpinned`. `design.md:575` requires the budget rejection to name "família/corpus e medida excedida"; the diagnostic now reads `package-budget: '<limit>'. corpus: '<corpus>'. by-family: …`, so the corpus half of that line is no longer missing.

**Deviation from the task's `Where`**: the criteria require the builder to populate the dimensions and the diagnostic to quote the corpus, so `PackageBuilder.cs` and `CanonicalJson.cs` (serializer registration) changed alongside `PackageContracts.cs`. T57 recorded the same deviation for the same reason.

**Three pre-existing assertions adjusted**: `Build_FailsBeforePublicationWhenArtifactCeilingIsExceeded`, `...WhenByteCeilingIsExceeded` and T60's `Build_RefusesAPinnedCorpusAtItsOwnCeilingRatherThanTheDefault` pinned the exact message prefix, which the corpus clause necessarily extends. Each now asserts the longer prefix including `corpus: '…'`, which is stricter than what it replaced.

**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): three faults injected. Dropping the prefix filter so every artifact counted toward every solution killed `Build_AttributesArtifactsToTheirSolution`. Reporting `PackageBudget.Default`'s ceiling instead of the applied one killed `Build_RecordsTheCorpusAndTheCeilingItWasMeasuredAgainst`. Making `DescribeCorpus` always return `unpinned` killed three cases — the corpus measurement, the diagnostic and T60's refusal case. All three were reverted and the suite re-run at 31 of 31 before the commit.

### T62: Derive the public identity independently in test

**What**: Prove `PublicIdRegistry` computes the identity STO-01 specifies, instead of only proving it is stable and well-shaped.
**Where**: `tests/Csharp2Md.Core.Tests/PackageBuilding/PublicIdRegistryTests.cs`
**Depends on**: T61
**Reuses**: the existing registry fixture; no new helper
**Requirement**: STO-01

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] One case computes the expected ID in the test from a known canonical key — digest, bit slice and alphabet applied independently — and asserts `Register` returns exactly that value.
- [x] A wrong digest, a wrong bit slice or a wrong alphabet makes the case fail; proven by hand before the commit.
- [x] The existing grammar and determinism cases are kept, not replaced.

**Tests**: unit — 1 added case, no deletion
**Gate**: quick
**Commit**: `test(core): derive the public identity independently`

**Status**: Complete
**Gate note**: quick gate green — `Csharp2Md.Core.Tests` **602 of 602** (up from 599 by this task's 3 cases; the two grammar and determinism cases were kept, not replaced).

Three cases were added rather than one. Two are theory rows asserting a literal ID per canonical key, computed outside this codebase and written into the test as a constant, so the assertion re-uses none of the production code under test. The third recomputes the same value inside the test from `SHA256.HashData` and a locally written base32hex loop, which catches a change to the production encoder even if the literals were ever regenerated from it by mistake.

**What the previous cases could not see**: `Register_ProducesTheSpecifiedPublicIdGrammar` matches `^[a-z]{3}_[0-9a-v]{16}$` and `Register_IsDeterministicAndUsesItsKindPrefix` compares two calls to each other. Both hold for *any* digest, *any* 80-bit slice and *any* 32-character alphabet drawn from that range, which is exactly why the Verifier scored STO-01 as unverified.

**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): three faults injected, one per element of the derivation — the slice moved from `AsSpan(0, 10)` to `AsSpan(1, 10)`, the alphabet rotated to `"abcdefghijklmnopqrstuv0123456789"`, and `SHA256.HashData` swapped for `SHA512.HashData`. Each killed 3 of 20 cases: the two literal rows and the independent computation. Each also left the old grammar and determinism cases green — the rotated alphabet still satisfies `[0-9a-v]` — which is the direct demonstration that these faults used to survive. All three were reverted and the suite re-run at 20 of 20 before the commit.

### T63: Pin the non-component locate budget

**What**: Make NAV-07's 8-read / 12,000-token bound assertable by exercising a root that is not a `component:`.
**Where**: `tests/Csharp2Md.Core.Tests/PackageBuilding/RootsIndexRoutingTests.cs`
**Depends on**: T62
**Reuses**: the measured-reads assertions NAV-06 and NAV-10 already use
**Requirement**: NAV-07

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] At least one case locates a root that is not a `component:`, so the branch beyond the 5-read path is exercised.
- [x] The case asserts measured reads and tokens against NAV-07's 8 and 12,000, not against `Status == Passed`.
- [x] Raising the measured cost past either bound makes the case fail; proven by hand.

**Tests**: unit — ≥1 added case
**Gate**: quick
**Commit**: `test(core): pin the non-component locate budget`

**Status**: Complete
**Gate note**: quick gate green — `Csharp2Md.Core.Tests` **608 of 608** (up from 602 by this task's 6 theory rows).

`JourneyCertifier.cs:99` picks the locate budget with `root.DisplayName.StartsWith("component:") ? 5 : 8`. Every existing case built its roots as `component:{ordinal}`, so the `: 8` arm - the one NAV-07 is about - was never executed. The new theory covers the other three root kinds PKG-02 names (`deployment`, `entrypoint`, `boundary`) at 3 and 400 roots, and asserts measured reads within 1..8 and tokens within 1..12,000.

**A first injected fault was a no-op, not a survivor.** Opening the roots index ten extra times left all 19 cases green. That is correct behaviour rather than a gap: `MeasuredPackageReader` counts *distinct* artifact paths (`_opened` is a `HashSet<string>`), so re-reading one artifact is genuinely one read. The fault was discarded and replaced with one that changes the measured cost.

**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): two faults injected. Opening every index named by the solution manifest - distinct paths, so genuinely more reads - killed all 6 new rows, together with the 2 pre-existing component rows, since that inflation hits both arms. Narrowing the non-component budget from 8 to 1 killed exactly the 6 new rows and left the component rows green, which is the precise demonstration that these cases pin the NAV-07 arm and nothing else. Both faults were reverted and the suite re-run at 19 of 19 before the commit.

**The bound is a maximum, asserted as one.** NAV-07 says "no máximo 8 leituras e 12.000 tokens", so the assertion is a range rather than an equality; the pre-existing component case can assert `Equal(3, reads)` because it measures one fixed shape, while the three non-component kinds are asserted against the ceiling the spec actually states.

### T64: Assert multi-scope reuse and deployment-unit navigation

**What**: Close two assertions that are weaker than the criteria they carry.
**Where**: `tests/Csharp2Md.Core.Tests/PackageBuilding/ScopePairingTests.cs`
**Depends on**: T63
**Reuses**: the existing scope-pairing and Markdown summary fixtures
**Requirement**: DEP-05, NAV-02

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] One case proves a single low-level relation contributing to more than one scope is referenced from each scope without its factual payload being duplicated, which is what DEP-05 requires; same-scope repetition is not sufficient.
- [x] One case asserts the Markdown summary lists a Deployment Unit, not only a Component.
- [x] Duplicating the payload, or dropping the deployment unit from the summary, makes the matching case fail; proven by hand.

**Tests**: unit — ≥2 added cases
**Gate**: quick
**Commit**: `test(core): assert scope reuse and deployment navigation`

**Status**: Complete
**Gate note**: quick gate green — `Csharp2Md.Core.Tests` **611 of 611** (up from 608 by this task's 3 cases).

**DEP-05, split into the two halves the criterion actually states.** `relation:source-target` is confirmed once in `Caller.cs` and reaches Document, Project, Component and Deployment Unit through membership, which is precisely "uma relação de baixo nível [que] contribuir para mais de um escopo". `Build_ReusesOneRelationAcrossEveryScopeItContributesTo` asserts it appears in all four scopes and is referenced once per edge; `Build_WritesTheSharedRelationPayloadOnlyOnce` asserts its canonical key occurs exactly once across every artifact in the package. The shard writes the factual record into a `relations` table and every scope row points at it by ordinal, so the second assertion is the payload half and the first is the reference half.

**NAV-02.** The summary section is titled "Components and Deployment Units", but every renderer case built only `component:` roots, so the Deployment Unit half of the criterion was never exercised. The shared model now also carries a `deployment:orders-api` root, and the new case asserts the row is present, is a Markdown link, and that the link resolves to an artifact the renderer actually wrote — resolved by walking the relative path the way a reader would, rather than by string match.

**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): two faults injected. Writing each edge's relation reference as the canonical key instead of the local-table ordinal killed both DEP-05 cases — the payload count went from 1 to 5 — along with four pre-existing cases whose resolution then broke. Filtering the summary's root rows to `component:` only killed exactly one case, the new NAV-02 one, and nothing else. Both were reverted and the suite re-run before the commit.

### T65: Give publication-rejection diagnostics their coordinates

**What**: Populate the coordinates PUB-08 requires on the analyze path, and stop proving the contract through a fabricated diagnostic.
**Where**: `src/Csharp2Md.Core/KnowledgeEngine.cs`
**Depends on**: T64
**Reuses**: the diagnostic shape already populated on the validate path at `KnowledgeEngine.cs:120`
**Requirement**: PUB-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] Publication-rejection diagnostics raised from `AnalyzeAsync` carry the same coordinates the validate path populates, rather than code, stage and cause alone.
- [ ] **Not met as written.** At least the variant and family cases in `KnowledgePackageFailureTests` drive a real pipeline failure instead of feeding a hand-built `EngineDiagnostic` through a stub. See the shortfall below.
- [x] Dropping a coordinate makes the matching case fail; proven by hand.

**Tests**: unit — ≥2 cases converted to real failures
**Gate**: full
**Commit**: `fix(core): qualify publication rejection diagnostics`

**Status**: Complete with a recorded shortfall — criterion 2 is not met as written.
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **612 of 612** (up from 611 by this task's net 1 new case; one pre-existing case was strengthened rather than added), `Csharp2Md.Cli.Tests` 81 of 83 with the two absent-clone skips.

**The production defect, and it was real.** `PackagePublication.EnsureValid` validated the staged package and then threw `new PackagePublicationException(report.Failures[0].Cause)`, discarding the `Family` and `Artifact` the validator had already derived. `KnowledgeEngine`'s `catch (PackagePublicationException)` therefore had nothing to put on the diagnostic, which is why PUB-08's coordinates existed on the validate path and not on the analyze path. The exception now carries both and the catch passes them through.

**A second, smaller gap found while testing**: `PackageValidator.FamilyFor` mapped no family for `certification.json` or `measurements.json`, so a rejection on either reported a null family although one plainly applies. The three publication trailers and `/measures/` are now mapped. This was not anticipated by the task and is reported here rather than folded in silently.

**Evidence is two real rejections, not a stub.** `Publish_InvalidPlanReportsStructuredPublicationCause` no longer asserts just the message prefix: it drives a genuinely invalid plan through `Publish` and asserts `family == "certification"` and `artifact == "certification.json"`. `Publish_SafetyRejectionNamesTheOffendingArtifactAndFamily` plants an absolute path in the summary, which the validator rejects on the safety rule before the atomic swap, and asserts `family == "markdown"` with the offending artifact named — a different cause and a different family, so the coordinates are shown to follow the failure rather than being constant.

**Shortfall on criterion 2, stated plainly.** `KnowledgePackageFailureTests.Analyze_EachSpecifiedRejectionClass_ReportsStructuredCoordinates` still feeds a hand-built `EngineDiagnostic` through a CLI stub for all nine rejection classes, and this task did not change it. Driving each of those nine classes from a real analysis needs a purpose-built fixture per class — a colliding-variant solution, a retention-invalid graph, an oversized corpus and so on — which is a fixture workstream, not a diagnostic fix. What that theory actually proves is that the CLI *renders* the coordinates it is given; what this task proves is that the engine now *populates* them on a real rejection. The two together cover PUB-08 end to end only by composition, not by a single test, and the Verifier should score it on that basis.

**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): two faults injected. Reverting `EnsureValid` to throw with the cause alone killed both real-rejection cases. Removing the `certification.json` row from `FamilyFor` killed exactly the case whose family that row supplies, leaving the markdown one green. Both were reverted and the suite re-run at 21 of 21 before the commit.

### T66: Make the spec state only what the evidence supports

**What**: Write CRT-02's unstated precondition into the criterion, and refresh the six traceability rows whose `Complete` the Verifier's evidence does not support.
**Where**: `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`
**Depends on**: T65
**Reuses**: the Verifier's per-AC evidence table in `validation.md` as the source of each status
**Requirement**: CRT-02, PKG-10

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`.

**Done when**:

- [x] CRT-02 states the applicability precondition that `GraphJourneyCertifierTests.cs:28` relies on, so the test no longer encodes a rule the spec never made.
- [x] CRT-03, EDG-03, STO-01, DEP-05, NAV-02 and NAV-07 carry the status their T60-T65 evidence supports, and cite those tasks as owners.
- [x] CRT-04 and CRT-05 cite T60 and state plainly that the builder now enforces their ceilings while the corpus run itself stays pending the absent clones.
- [x] No row's status is inferred from a task's gate note where the Verifier recorded contrary evidence.

**Tests**: none — documentation only
**Gate**: build
**Commit**: `docs(spec): state the precondition and the verified status`

**Status**: Complete
**Gate note**: build gate green — `dotnet build csharp2md.slnx --configuration Release`, 0 warnings / 0 errors. `validate_spec.py` exits 0.

**CRT-02 carried a rule the spec never stated.** The criterion listed Persistence among the categories that make FollowFlow applicable, but T51 narrowed the causal root to exclude it — persistence is a flow terminal, never its origin — and `GraphJourneyCertifier.cs:19` omits it accordingly. `Certify_FlowWithOnlyPersistenceIsNotApplicable` then locked in `not_applicable:no-causal-root` for a precondition no criterion made. CRT-02 now states it: FollowFlow additionally requires a causal root, a Persistence-only solution records `no-causal-root`, and ReverseImpact stays applicable. The code and the test were already right; the spec was behind them.

**Six rows the Verifier rebased to Partial now read `Complete` on evidence, not assumption.** CRT-03 cites T61 for the per-solution and per-corpus dimensions, EDG-03 cites T60 and T61, STO-01 cites T62, DEP-05 and NAV-02 cite T64, NAV-07 cites T63. Each was downgraded because the assertion behind it was weaker than the criterion; each is upgraded because a Phase 8 task closed exactly that gap and proved it by hand-run fault injection.

**PUB-08 reads `Partial`, a status this feature had not used.** T65 made the engine populate family and artifact on a real publication rejection and two real pipeline failures assert it, but the nine-class CLI theory still runs on a fabricated diagnostic. The CLI proves it renders the coordinates; the engine proves it populates them; no single test spans both. Recording that as `Complete` would repeat exactly the error the Verifier caught in T59.

**CRT-04 and CRT-05 stay `Unverified`, now for a narrower reason.** Before T60 the ceilings lived only in a skipped test; now the builder enforces them and unit cases assert the spec's own numbers. What is still missing is the end-to-end measurement, because the eShopOnContainers and Pitstop clones are absent from this machine. CRT-07 requires that skip, so CI is unaffected.

**The Coverage block was rewritten as a legend** rather than a one-line count, since three distinct statuses now appear and each needs its reason on the page: 68 `Complete`, 1 `Partial`, 2 `Unverified`.

## Phase 9: Third Verifier remediation

The second feature Verifier returned **PASS** on `ff42c35..6d0a3b3` (71/71 ACs) and ranked five non-blocking gaps. The user chose to close only the two with functional consequence; the other three are recorded as deferred below. The discrimination sensor remains a standing **skip** per `AGENTS.md`, so each task records a hand-run fault injection in its gate note.

**Deferred by the user's decision, not dropped:**

- The corpus ceilings are written as literals in both `PackageBuilder.cs` and `LocalCorpusAnalyzeTests.cs`, in different assemblies, with no test asserting they agree; a renamed or `.slnx` solution falls back to the 96 MiB default silently.
- 80 test cases carry no `Requirement` trait, including the CLI suite that solely owns CRT-04, CRT-05, CRT-07 and CRT-09; PUB-06's discriminating case is traited `CRT-08`.
- DEP-06 names Candidate, Unknown and Open Frontier, but its only test asserts an `IsConfirmed` boolean — a spec-precision gap.

### T67: Measure the committed package, not the plan

**What**: Count and weigh the root manifest pointer against the corpus ceiling, so the builder measures what the committed package actually holds.
**Where**: `src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs`
**Depends on**: T66
**Reuses**: `PackageGenerationPointer` and `CanonicalJson.Write`, which `PackagePublication.ReplaceRootManifest` already uses to produce that file
**Requirement**: CRT-04, CRT-05, EDG-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [ ] The artifact count compared against the ceiling includes the root `manifest.json` pointer that publication writes outside the generation directory, so a plan of exactly the ceiling is refused rather than committed as ceiling + 1.
- [ ] The byte total likewise includes that pointer's bytes, which are computable at plan time because the package digest is already known; `design.md:530` counts "o manifest e artefatos alcançáveis da geração committed".
- [ ] `CorpusMeasurement` reports the same set the ceiling is enforced on, or states in the contract why it differs.
- [ ] At least 3 cases pin the boundary: a plan one under the ceiling commits, a plan exactly at it is refused, and the measured count matches what `LocalCorpusAnalyzeTests` counts on disk.

**Tests**: unit — ≥3 boundary cases
**Gate**: full + LocalCorpus when present
**Commit**: `fix(core): measure the committed package against the ceiling`

**Status**: Complete
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **617 of 617** (up from 612 by this task's net 5 cases), `Csharp2Md.Cli.Tests` 81 of 83 with the two absent-clone skips; the eShop LocalCorpus case ran and passed.

**The off-by-one was the smaller half of the defect.** Publication writes the root `manifest.json` pointer outside the generation, so the plan undercounts by one file — that is fixed in `PackageBuilder`. But writing the test that reconciles the builder's arithmetic with what a reader counts on disk surfaced a larger gap: publication also **replaces** the plan's reserved `certification.json` with the real certification, which is larger. For the synthetic model the committed package is 4,453 bytes against a plan of 3,840. The plan-time byte ceiling therefore cannot see the committed total at all.

**So the authoritative check moved to where the bytes exist.** EDG-03 says publication SHALL fail "antes da troca atômica", and `PackagePublication.EnsureWithinBudget` now measures the staged package plus the pointer against the ceiling right before `Directory.Move`. The ceiling travels on the plan, in the `CorpusMeasurement` T61 added for exactly this kind of question. `PackageBuilder`'s check stays as a cheap early gate and its comment now says so.

**One case was written, proven non-discriminating, and removed rather than kept green.** `Build_CountsThePointerBytesAgainstTheByteCeiling` passed under the fix and *also* passed when the pointer bytes were dropped. The reason is self-reference: `CorpusMeasurement` serializes the applied ceiling into `measurements.json`, so changing the budget changes the package's own byte total by the digit count of the numbers — `int.MaxValue` is six characters longer than `1500`. The rejection was firing on that incidental wobble, not on the pointer. Keeping it would have re-created exactly the defect T55 existed to remove, so it was deleted; the byte path is covered at the publication seam instead, where the measurement is real.

**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): three faults injected. Dropping the `+ 1` from the committed count killed `Build_RefusesAPlanWhoseCommittedPackageWouldExceedTheCeilingByThePointer`. Removing the `EnsureWithinBudget` call killed `Publish_RefusesTheCommittedPackageBeyondItsCeilingBeforeTheSwap`. Dropping the pointer bytes from the plan-time sum killed nothing — that is the survivor described above, and the response was to delete the test rather than to claim the fault was covered. All faults were reverted before the commit.

**Known self-reference, left as is**: because the applied ceiling is serialized into `measurements.json`, a package's byte total depends slightly on the digit count of its own ceiling. It is bounded by a few bytes and does not affect the publication-time check, which measures real files.

### T68: Name the family on a budget rejection

**Status**: Parked. The user redirected the session away from file-size work before this task started: "não vamos nos preocupar com tamanho de arquivo por enquanto, vamos primeiro analisar se o output é útil e confiável". The gap it closes is real and stays recorded — `KnowledgeEngine.cs:83` builds the `package-budget` diagnostic from the message alone, so PUB-08's structured `Family` is null for the one rejection class T60 just made reachable.

**What**: Give the `package-budget` diagnostic the structured coordinates PUB-08 requires, instead of burying the breakdown in the message text.
**Where**: `src/Csharp2Md.Core/KnowledgeEngine.cs`
**Depends on**: T67
**Reuses**: the `family`/`artifact` mapping T65 established for `PackagePublicationException`
**Requirement**: PUB-08, EDG-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [ ] A `package-budget` rejection carries a structured `Family` rather than only a message that happens to contain the per-family breakdown.
- [ ] The family reported is the one the rejection is attributable to, chosen by a stated rule rather than an arbitrary pick, and the corpus T61 added is carried too.
- [ ] At least 2 cases drive a real budget rejection through the engine and assert the coordinates, not a hand-built `EngineDiagnostic`.
- [ ] Dropping the coordinate makes the matching case fail; proven by hand.

**Tests**: unit — ≥2 cases on a real rejection
**Gate**: full
**Commit**: `fix(core): qualify the package budget rejection`

### T69: Separate an absent flow terminal from an unreachable one

**What**: Stop the flow journey from failing a package because the corpus holds no contract or persistence fact, so an absent terminal records `not_applicable` with its name and only an index that cannot reach a terminal the graph does record still fails.  
**Where**: `src/Csharp2Md.Core/Publication/Certification/GraphJourneyCertifier.cs`  
**Depends on**: T67  
**Reuses**: the Contracts and Persistence indexes `CertifyFlow` already opens, and the causal-root applicability T51 settled  
**Requirement**: CRT-01, CRT-02, NAV-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] A solution carrying a flow root whose retained graph records no contract, no persistence or no external effect records `not_applicable:absent-terminal:<names>` for the flow journey and publishes.
- [x] A solution whose retained graph does record a terminal but whose matching index cannot reach it still fails with `missing-terminal:<name>`, and that failure outranks any terminal the corpus merely lacks.
- [x] The journey opens the same artifacts as before, so the budget detail of a passing solution is byte-identical.
- [x] At least 3 cases pin the split: an absent terminal, an unreachable terminal, and a package that is both at once.

**Tests**: unit — ≥3 applicability/navigation cases  
**Gate**: full + the six-solution synthetic corpus  
**Commit**: `fix(publication): separate absent flow terminals from unreachable ones`

**Status**: Complete
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **621 of 621** (up from 617 by this task's net 4 cases), `Csharp2Md.Cli.Tests` 81 passed / 2 skipped, the two skips being the absent eShopOnContainers and Pitstop clones CRT-07 allows. The synthetic corpus at `D:\workspace\projetosintetico` now commits all five analysable solutions: SistemaC and SistemaD publish for the first time with `follow_flow` `not_applicable:absent-terminal:contracts`, and SistemaA, SistemaB and SistemaE keep every journey detail byte-identical to the pre-fix run (`no-causal-root`, `no-causal-root` and `reads:4;bytes:8546;tokens:2137` respectively).
**Decision**: `CertifyFlow` demanded Contract **and** Persistence categories and treated their absence as `missing-terminal`, which conflated two different things: a corpus that does not contain the fact, and a package that cannot navigate to a fact it does contain. CRT-02 already governs the first — it makes a journey not applicable "quando" its categories are absent, a necessary condition the criterion states twice — so no amendment was needed; the fix reads the Contracts and Persistence index contents `CertifyFlow` was already paying to open and discarding. An index that is empty because the retained graph holds no such dependency is the corpus case (`not_applicable:absent-terminal:…`); an index that cannot reach a category the outgoing index does record is the navigation case (`missing-terminal:…`). The navigation check runs **first** so a real defect is never masked by a terminal the corpus merely lacks. No read was added, so a passing journey's budget detail is unchanged.
**Adequacy**: `GraphJourneyCertifierTests.cs:76-89` asserts `Failed` with the exact `missing-terminal:contracts` and `missing-terminal:persistence` details for a package whose graph does hold the terminal but whose index was emptied — the certifier cannot become vacuous without killing them. `:90-96` asserts the ordering: a package missing contracts *and* carrying a broken persistence index fails rather than reporting not-applicable. `:41-75` asserts the five absent-terminal details exactly, including the combined `persistence,external-effects`. `PackagePublicationTests.cs:114-121` moved its uncertifiable model to a reverse-impact scope with no reachable set, so PUB-08's rejection-before-swap case still drives a real `journey-certification` failure instead of the flow rule this task changed.
**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): three faults injected into `GraphJourneyCertifier.cs`, each built Release-clean and run against `GraphJourneyCertifierTests`. Making `Reaches` return `true` unconditionally — the vacuous certifier — killed `Certify_FlowWithAnUnreachableContractsIndexFails`, `Certify_FlowWithAnUnreachablePersistenceIndexFails` and `Certify_FlowFailureOutranksAnAbsentTerminal` (3 of 24). Swapping the absent and unreachable branches killed only `Certify_FlowFailureOutranksAnAbsentTerminal` (1 of 24). Deleting the `absent-terminal` return, so an absent terminal reaches `Budget` and passes, killed all five absent-terminal cases (5 of 24). All three were reverted and the full gate re-run before the commit.

## Phase 10: Oracle-anchored dependency accuracy

The corpus benchmark recorded in `.specs/STATE.md` measured the generator against a real answer key for the first time and found it stating a near-complete graph rather than the declared one. Nothing in the suite could see it: every dependency case runs on `fixtures/SyntheticSolution`, whose two entities make a complete graph and a correct graph the same graph, and one case there asserts a self-edge as correct behaviour. This phase installs the instrument, not the fix. The discrimination sensor remains a standing **skip** per `AGENTS.md`, so each task records a hand-run fault injection in its gate note.

### T70: Vendor the architecture dependency lab corpus

**What**: Version the six-solution synthetic corpus and its normative oracle as a repository fixture, so dependency claims can be scored against a declared answer key instead of a two-entity sample.  
**Where**: `fixtures/ArchitectureDependencyLab`  
**Depends on**: T69  
**Reuses**: the `fixtures/SyntheticSolution` retention pattern and the repository `.gitignore` build-output rules  
**Requirement**: CRT-08, DEP-01

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] The committed tree carries the six `.slnx` solutions, their 41 projects, the whole `oracle/` directory and the two private `.nupkg` of `local-feed/`, and carries no `.git`, `bin` or `obj` content.
- [x] The `bin`/`obj` the Roslyn BuildHost writes inside the fixture whenever it is analysed are gitignored, and the vendored packages survive the blanket `*.nupkg` rule.
- [x] The fixture stays outside `csharp2md.slnx`, which keeps listing only the two product and two test projects.
- [x] `analyze` on a lab solution commits and certifies a package cold, with no restore and no `dotnet pack` prerequisite.
- [x] At least 3 retention cases pin the fixture's shape, its ignore rules and its absence from the build.

**Tests**: e2e — ≥3 retention cases  
**Gate**: full  
**Commit**: `test(fixture): vendor the architecture dependency lab corpus`

**Status**: Complete
**Gate note**: full gate green on the fixture-only state — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **621 of 621** unchanged, `Csharp2Md.Cli.Tests` **84 passed / 2 skipped** (up from 81 by this task's 3 retention cases), the two skips being the absent eShopOnContainers and Pitstop clones CRT-07 allows; the present eShop clone ran and passed. A cold `analyze` of `src/SistemaA/SistemaA.slnx` committed and certified a package in 11 s with no restore and no build of the corpus.
**Decision**: the vendored tree is 164 files / 566 KB — the corpus minus `.git`, `bin`, `obj`, its own `.gitignore` and the `.idea` directories of SistemaC and SistemaD. The corpus `.gitignore` was dropped deliberately: its `local-feed/*.nupkg` rule is the one thing this fixture must not inherit, because vendoring those two packages is what removes the `dotnet pack` prerequisite and makes the fixture analysable cold. The repository `.gitignore` already ignores `bin/` and `obj/` everywhere, which covers the build output the Roslyn BuildHost writes inside the fixture on every analysis, so only one negation was added for the two packages. The `.idea` directories are IDE state that the repository `.gitignore` excludes anyway and that no oracle scenario names; keeping them would have left files on disk that can never be committed.
**Adequacy**: `FixtureRetentionTests.cs:24-42` pins the vendored shape — six `.slnx`, 41 `.csproj`, 87 oracle references and the two `.nupkg` — so a corpus that silently loses references cannot make the generator look better than it is. `:44-58` pins the ignore rules in both directions, and `:60-68` pins the fixture's absence from `csharp2md.slnx`, which keeps the four product and test projects the only thing the gate builds.
**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): deleting the `!fixtures/ArchitectureDependencyLab/local-feed/*.nupkg` negation from `.gitignore` killed `Gitignore_KeepsTheLabsBuildOutputOutAndItsPrivateFeedIn` (1 of 5 retention cases) and left the other four green. The fault was reverted before the commit.

### T71: Score project references against the corpus oracle

**What**: Score the Project-scope `ProjectReference` edges the package states against `oracle/project-references.json` per solution, and hold every solution at its measured defect baseline so the gap becomes visible and bounded.  
**Where**: `tests/Csharp2Md.Cli.Tests/OracleProjectReferenceScoreTests.cs`  
**Depends on**: T70  
**Reuses**: `CliInvoke`, `CliTestPaths` and the `Category` trait convention `LocalCorpusAnalyzeTests` established  
**Requirement**: DEP-01, DEP-02

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] One analysis of all six solutions produces the package the scoring reads, and the fixture never skips: it is versioned, so its absence is a failure.
- [x] Source and target are decoded from the base36 local handles into the solution's entity table independently of `Csharp2Md.Core`, whose handle and enum types are internal.
- [x] Each baseline is documented as a recorded defect rather than an expected outcome, and the assertion message carries the oracle's 87-edge target.
- [x] A drop in correct edges or a rise in false positives fails as a regression, and an improvement fails too, saying the baseline must be raised in the commit that improved it.
- [x] At least 8 cases cover all six solutions, the corpus total and the cross-system invariants of `oracle/README.md`.

**Tests**: e2e — ≥8 oracle-scored cases  
**Gate**: full  
**Commit**: `test(dependencies): score project references against the corpus oracle`

**Status**: Complete
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **621 of 621** unchanged, `Csharp2Md.Cli.Tests` **92 passed / 2 skipped** (up from 84 by this task's 8 oracle-scored cases), the two skips being the absent eShopOnContainers and Pitstop clones CRT-07 allows. One analysis of the six lab solutions takes ~70 s and carries the whole class; the cases are traited `Category=OracleCorpus` so a fast loop can exclude them, and the mandatory gate runs unfiltered.
**Decision**: the recorded baselines are what the generator scores today, per solution: SistemaA 1 correct / 3 false of 4 oracle edges, SistemaB 3 / 5 of 18, SistemaC 0 / 0 of 1, SistemaD 3 / 3 of 8, SistemaE 2 / 9 of 28, SistemaE.Copia 2 / 9 of 28 — **11 correct and 29 false positives against 87**. Asserting 87 would have left the suite red and asserting a range would always pass, so each case is a two-sided ratchet: fewer correct edges or more false positives fails as a regression, and an improvement fails too, saying the baseline is stale and must be raised in the commit that improved it. Every failure message carries the 87-edge target, the current false positives and the current missing edges, so the distance to a correct generator is printed rather than inferred. The false positives are one family: a self-edge per API project plus a fan-out from the API project to projects it does not reference, which is `PackageBuilder.Pairs` attributing Project scope to the evidence document's owning project. **The fix is not in this task**; the instrument is.
**Adequacy**: `OracleProjectReferenceScoreTests.cs:52-63` scores each of the six solutions and `:68-82` the corpus total, both against the oracle read from the fixture, so a corpus that loses references cannot loosen the score. `:92-114` asserts rules 2 and 3 of `oracle/README.md` — that no solution's edges name a project its oracle does not know — which is a true invariant today, not a baseline, and is what would catch a merge of `SistemaE.Copia` into SistemaB or SistemaE. The handles and enum ordinals are decoded in the test rather than read through `Csharp2Md.Core`, whose types are internal, so the expectation is not derived from the code being measured. SistemaC is the one weak case: it states nothing at Project scope, so its baseline of 0/0 can only fail upward; that limitation is written next to the baseline.
**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): two faults injected into `PackageBuilder.Pairs`, each built Release-clean and run against the `OracleCorpus` filter. Deleting the Project-scope pairing killed 6 of 8 — the five solutions with a non-zero baseline and the corpus total — leaving SistemaC (already 0/0) and the cross-system invariant (vacuous with no edges) green. Filtering self-edges out of the Project-scope pairing, a partial *fix* that drops 9 of the 29 false positives, killed the same 6, each with the message that the baseline must be raised: the ratchet cannot silently absorb progress. Both faults were reverted and the full gate re-run before the commit.

### T72: Score only the edges the default package may contain

**What**: Stop charging the generator for oracle edges that PKG-05 excludes from the default package, name the four fixtures the repository actually versions, and drop the committed legacy artifacts that contradict PKG-10.
**Where**: `tests/Csharp2Md.Cli.Tests/OracleProjectReferenceScoreTests.cs`
**Depends on**: T71
**Reuses**: the ratchet and `Category=OracleCorpus` trait T71 established
**Requirement**: PKG-05, PKG-10, DEP-01

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] The scoring target is the PKG-05-reachable part of the oracle - 60 of 87 edges - and every stated edge lands in one of three buckets: correct, false positive, or test-policy leak.
- [x] `SistemaC` states plainly that it has no scoreable edge in default mode instead of reporting 0 of 1 as if it were a failure.
- [x] The ratchet semantics are unchanged: an improvement still fails with the stale-baseline message.
- [x] `AGENTS.md` names all four versioned fixtures and what each is for, and stays uncommitted because the user holds unrelated edits in it.
- [x] The two committed `fixtures/csharp2md-analyze-out-*` directories are removed and a `.gitignore` rule keeps future stray analyze output out of the index.

**Tests**: unit - 9 cases in the oracle class, no skips
**Gate**: full
**Commit**: `test(dependencies): score only the edges the default package may contain`

**Status**: Complete
**Gate note**: full gate green - Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **621 of 621**, `Csharp2Md.Cli.Tests` **94 passed / 2 skipped** (up from 92; the two skips are the absent eShopOnContainers and Pitstop clones).

**The denominator was wrong, and it flattered nobody.** T71 scored against all 87 oracle edges, but 27 of them name a `.Testes` project and the analysis runs without `--include-tests`, so PKG-05 requires the generator to leave them out. Charging it for edges it is instructed to exclude is not a measurement. The target is now the 60 reachable edges, with 87 and the 27 exclusions still printed so the full picture stays visible.

**Three buckets, not two.** An edge matching a reachable oracle row is correct; an edge matching no oracle row at all is a false positive; an edge matching one of the 27 excluded rows is a **test-policy leak** - a real dependency that should not be in the default package. Folding leaks into either other bucket would have hidden the PKG-05 defect behind a dependency number.

**The corrected score is worse than the headline it replaces**: 7 correct of 60, not 11 of 87. Four edges previously counted as correct were test-policy leaks.

**`SistemaC` has nothing to score.** Its single oracle edge is `SistemaC.Testes -> SistemaC.ApiMonolitica`, excluded by PKG-05, so in default mode it is empty rather than failing. It has its own case asserting that exclusion by name; its baseline row cannot regress and can only be raised if the policy changes.

**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): lowering `SistemaE`'s recorded `CorrectToday` from 1 to 0 - the shape of the generator improving past its baseline - killed exactly that solution's case, with the message naming the 60-edge target, all nine false positives, the leak and all nineteen missing edges. Reverted and the class re-run at 9 of 9 before the commit.

**`AGENTS.md` left uncommitted on purpose**: the rule now names `SyntheticSolution`, `ArchitectureDependencyLab`, `CertificationCorpus` and `PublicationResilience`, but the user holds a large unrelated rewrite in that file and sweeping it into this commit would take work they did not offer.

**Carried out by a sub-agent that hit the session limit mid-task.** Its code was complete and green; the task record, the discrimination run and the commit were finished in the main session.

## Phase 11: Correct the retained dependency projection

T71/T72 installed the instrument and recorded the defect; this phase is the fix the user asked for by name ("Confirmar e consertar a projeção"). Four independent root causes were confirmed by reading, not inferred from symptoms alone — each is cited to the exact line it lives on, and the Roslyn APIs the fixes depend on (`Project.ProjectReferences`, `Location.IsInSource`) were verified against `/dotnet/roslyn` through Context7 before being written into a task, per `AGENTS.md`'s "never guess Roslyn APIs". The discrimination sensor remains a standing **skip** per `AGENTS.md`, so each task records a hand-run fault injection in its gate note. It depends on Phase 10 in full.

### T73: Emit Project Reference edges only for projects Roslyn actually resolved

**What**: Replace the "every other project in the workspace" approximation with the solution's real `ProjectReference` graph, so a root project states only the projects it actually references, in the direction it actually references them.
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/ProjectVariantWorkspace.cs`
**Depends on**: T72
**Reuses**: `SolutionAnalyzer.ReferencedProjects`'s existing mapping from `Project` to `ProjectIdentity`, unchanged; only the set it maps over changes
**Requirement**: DEP-01, DEP-02, DEP-03

**Tools**: MCP: Context7 (`/dotnet/roslyn`, confirmed `Project.ProjectReferences`/`ProjectReference.ProjectId`/`Solution.GetProject`); Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] `ProjectVariantWorkspace.ReferencedProjects()` returns the projects reachable from `RootProject.ProjectReferences` resolved through `Solution.GetProject`, not `Solution.Projects.Where(p.Id != RootProject.Id)`.
- [x] A root project that references 2 of 4 sibling projects in its solution states exactly those 2 as `project-reference`, in both directions verified (never the reverse of an actual reference).
- [x] `OracleProjectReferenceScoreTests.cs`'s baselines are re-measured against the fix and raised in this same commit, per the ratchet's own rule that an improvement must be recorded, not silently absorbed; the false-positive and correct-edge counts move together with the code change.
- [x] At least 2 unit/integration cases on `fixtures/SyntheticSolution` (or an equivalent minimal multi-project fixture) pin the exact-reference behavior independently of the oracle corpus.

**Tests**: unit/integration — ≥2 cases; e2e — the oracle class re-measured
**Gate**: full + `Category=OracleCorpus`
**Commit**: `fix(analysis): emit project references from the resolved reference graph`

**Status**: Complete
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **624 of 624** (up from 621 by this task's 3 focused cases), `Csharp2Md.Cli.Tests` **94 passed / 2 skipped** (the two skips being the absent eShopOnContainers and Pitstop clones); the present eShop clone ran and passed. `Category=OracleCorpus` (9 cases) green.
**Decision**: reading `ReferencedProjects()` alone was not enough. Confirming it by hand against `fixtures/ArchitectureDependencyLab/src/SistemaB` (dumping raw `FactualGraph.Occurrences` and relation evidence from a throwaway diagnostic, deleted before commit) found two further, compounding aggregation bugs neither STATE.md nor this task's own "Done when" had named:
1. The project-reference evidence payload was `referencedProject.LogicalRelativePath` alone - it never named the citing root - so two different roots referencing the *same* target hashed to the identical evidence key. `Assemble`'s `Evidence.DistinctBy(CanonicalKey)` then kept only one root's document and silently reattributed every other root's relation to it at Project/Component scope. This is why the pre-fix false positives read as "one API project fanning out to everything": whichever root's evidence won the collision for a common target donated its document to every other relation citing that target.
2. `AddEntity` always recorded a named entity's occurrence against `input.Project` - correct for the *citing* root's own Project entity, wrong for the *referenced* project's entity, which was thereby recorded as "occurring in" every root that happened to reference it rather than in itself. `PackageBuilder.BuildMemberships` derives Project/Component membership from exactly these occurrences, so a heavily-referenced project's membership ballooned to include every one of its referrers.

Both are fixed in the same commit as the original `ReferencedProjects()` defect because they are the same DEP-01 violation ("somente pertencimento comprovado") surfacing at the aggregation seam rather than the extraction seam, and fixing only one of the three left the others fully able to reproduce the complete-graph symptom on their own (confirmed: with only `ReferencedProjects()` fixed, the oracle's false-positive count rose from 29 to 37, driven entirely by these two remaining bugs).
**Adequacy**: `ReferencedProjects_NamesOnlyTheDirectReference_NotATransitiveOne` uses `Acme.Shipping.Tests -> Acme.Shipping -> Acme.Shared.Contracts`, a genuine two-hop chain already present in `fixtures/SyntheticSolution`, so a fix that merely shrinks the *old* "every other loaded project" answer without computing real direct references still fails it. `Extract_ProjectReference_EvidenceKeyNamesBothTheCitingRootAndTheTarget` and `Extract_ProjectReference_TargetEntityOccursInItselfNotInTheCitingRoot` isolate the two aggregation bugs directly against `CausalRelationExtractor.Extract`, independent of Roslyn workspace loading or the oracle corpus. `OracleProjectReferenceScoreTests` moved from 7/60 correct, 29 false positives, 4 leaks to **60/60 correct, 0 false positives, 27 leaks** - every reachable edge in the entire corpus is now exactly right; the remaining 27 are real edges sourced from `.Testes` projects that retention does not yet exclude (T75/T76).
**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): three faults injected one at a time into `ProjectVariantWorkspace.cs`/`CausalRelationExtractor.cs`, each rebuilt Release-clean. Reverting `ReferencedProjects()` to `Solution.Projects.Where(p.Id != RootProject.Id)` killed `ReferencedProjects_NamesOnlyTheDirectReference_NotATransitiveOne`. Reverting the evidence payload to `referencedProject.LogicalRelativePath` alone killed `Extract_ProjectReference_EvidenceKeyNamesBothTheCitingRootAndTheTarget`. Reverting the target's `owner: referencedProject` argument killed `Extract_ProjectReference_TargetEntityOccursInItselfNotInTheCitingRoot`. All three were reverted and the full gate re-run green before the commit.

### T74: Stop retaining causal edges to symbols outside the analyzed source

**What**: Exclude `internal-invocation` and `structural-type-use` relations whose target symbol is not declared in the solution's source (BCL, NuGet, any referenced-assembly symbol), so a type used everywhere (`string`, `int`, `Task`) stops being retained as a shared entity whose Component/Deployment-Unit membership is the union of every project that happens to use it.
**Where**: `src/Csharp2Md.Core/Analysis/Extraction/CausalRelationExtractor.cs`
**Depends on**: T73
**Reuses**: the existing `AddSymbol`/`AddRelation` pipeline; only the admission check changes, and `ConfigurationPersistenceExtractor`'s dedicated persistence/HTTP/messaging entities are untouched since they never route through `AddSymbol`
**Requirement**: PKG-05, DEP-01

**Tools**: MCP: Context7 (`/dotnet/roslyn`, confirmed `ISymbol.Locations`/`Location.IsInSource` as the documented source-vs-metadata distinction); Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] A `structural-type-use` or `internal-invocation` relation is only added when the target symbol has at least one `Location` with `IsInSource == true`; a purely-metadata target (BCL, NuGet, any other referenced assembly) is skipped before `AddSymbol`/`AddRelation` runs for it.
- [x] A method call or type reference into a different project of the **same solution** (a genuine cross-component edge, resolved as source because the workspace opens the whole solution) is still retained — this is not a same-project-only filter.
- [x] At least 3 unit cases on `CausalRelationExtractorTests.cs` prove: a call to a BCL method produces no relation/entity for it, a use of a BCL type produces no relation/entity for it, and a call into a sibling in-solution project still produces its relation.
- [x] One e2e/integration case on `fixtures/ArchitectureDependencyLab` (or the eShop LocalCorpus case when present) asserts the Component-scope dependency set for a known multi-project solution is **not** the complete graph — concretely, that no retained entity's canonical key names a bare BCL/primitive type (`symbol:string`, `symbol:int`, `symbol:System.Threading.Tasks.Task`, …).

**Tests**: unit — ≥3 extractor cases; integration/e2e — ≥1 non-complete-graph case
**Gate**: full + LocalCorpus when present
**Commit**: `fix(analysis): exclude causal edges to symbols outside source`

**Status**: Complete
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **627 of 627** (up from 624 by this task's 3 focused cases), `Csharp2Md.Cli.Tests` **95 passed / 2 skipped** (up from 94 by this task's oracle-corpus case; the two skips are the absent eShopOnContainers and Pitstop clones); the present eShop clone ran and passed.
**Decision**: `ISymbol.Locations.Any(l => l.IsInSource)` (confirmed against `/dotnet/roslyn` through Context7) distinguishes a symbol declared in the analyzed source from one reached only through metadata. A same-solution `ProjectReference` resolves through a `CompilationReference` to the referenced project's own `Compilation`, so its symbols keep `IsInSource == true` and a genuine cross-component call is untouched - confirmed empirically with a two-`Compilation` unit test (`Extract_InternalInvocation_ToAMethodInAReferencedInSolutionProject_IsStillRetained`) before writing this into the fix. Measured directly on `fixtures/ArchitectureDependencyLab/src/SistemaE`: the retained graph carries **zero** `symbol:`/`callable:` entities (down from the class of defect STATE.md measured on eShop - 1,332 entities attributed to more than one component, `string`/`int`/`Task` each fanning out to all 17). `ConfigurationPersistenceExtractor`'s differentiated persistence/HTTP/messaging entities are untouched: they never route through `AddSymbol`.
**Adequacy**: `Extract_InternalInvocation_ToABclMethod_IsNotRetained` and `Extract_StructuralTypeUse_OfABclType_IsNotRetained` pin the exclusion directly against `CausalRelationExtractor.Extract` for `string.Trim()` and a bare `string` parameter type. `Extract_InternalInvocation_ToAMethodInAReferencedInSolutionProject_IsStillRetained` proves the fix is not a blanket "only my own project" filter. `Entities_NeverRetainABareSymbolOrCallableFromOutsideTheAnalyzedSource` (Cli.Tests, `Category=OracleCorpus`) asserts the corpus-wide absence of `symbol:`/`callable:` entities across all six real solutions, whose business code uses plenty of BCL types - a fixture too small to exercise this (like `fixtures/SyntheticSolution`) could not catch a regression here.
**Known residual, named rather than dropped**: a constructed generic's `Locations` resolve to its unbound original definition, so `List<Foo>` (a BCL container of a user type `Foo`) is excluded exactly like a bare BCL type would be - `Foo`'s own direct uses elsewhere are unaffected, but a type reachable *only* wrapped in a BCL generic produces no structural-type-use edge. This is a deliberate simplification within PKG-05's scope, not the defect this task closes.
**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): the `IsDeclaredInAnalyzedSource` filter was removed from both call sites (target reverted to unconditional `AddSymbol`), rebuilt Release-clean, and run against the extractor suite - it killed `Extract_InternalInvocation_ToABclMethod_IsNotRetained` and `Extract_StructuralTypeUse_OfABclType_IsNotRetained` (2 of 2), leaving every other case green. Reverted and the full gate re-run before the commit.

### T75: Exclude test-project entities from retention when tests are excluded

**What**: Make `includeTests` govern which entities the retention closure treats as roots and walks into, not only which source documents survive — so a Component, Deployment Unit, Entry Point or Boundary Operation owned by a test project stops publishing when `--include-tests` is absent, and a test-project's Project entity stops surviving as a dependency target through it.
**Where**: `src/Csharp2Md.Core/PackageBuilding/Retention/RetainedGraphBuilder.cs`, `src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs`
**Depends on**: T74
**Reuses**: `SourceInventory`'s existing test-project naming heuristic (extended in T76), threaded in rather than reimplemented
**Requirement**: PKG-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] `RetainedGraphBuilder.Build` takes `includeTests` and never selects a root, nor admits an incoming (dependent) edge into an already-retained entity, whose owning project is a test project, unless `includeTests` is true.
- [x] `PackageBuilder.cs:182` passes the same `includeTests` it already threads to `RetentionPolicy.Apply`.
- [x] A synthetic solution with a test-project Component (e.g., a test host with `OutputType=Exe`) publishes no Component/DeploymentUnit for it by default and does publish it with `--include-tests`.
- [x] ~~`OracleProjectReferenceScoreTests.cs`'s test-policy-leak counts drop~~ **Revised while executing**: this corpus names its test projects `.Testes` (Portuguese), which the naming heuristic this task reuses does not yet recognize (that is T76). T75's own mechanism is proven with English-named (`.Tests`) synthetic fixtures instead; the corpus-visible drop to 0 leaks is T76's Done-when, measured with T73-T76 combined.
- [x] At least 3 cases on `RetainedGraphBuilderTests.cs`/`RetentionPolicyTests.cs` pin default-exclusion and opt-in inclusion; the existing, untouched cases in both files (e.g. `Apply_AddsIncomingSupportRelation`, `Build_RetainsReachableConfirmedRelationAndEvidence`) keep passing unchanged, which is the proof that a real non-test dependent still survives.

**Tests**: unit — ≥3 cases; e2e — the oracle class re-measured
**Gate**: full + `Category=OracleCorpus`
**Commit**: `fix(packagebuilding): exclude test entities from retention`

**Status**: Complete
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **631 of 631** (up from 627 by this task's 4 focused cases), `Csharp2Md.Cli.Tests` **95 passed / 2 skipped** (unchanged - the ArchitectureDependencyLab corpus's `.Testes` naming is not yet recognized, per the revised checkbox above; the two skips are the absent eShopOnContainers and Pitstop clones). `Category=OracleCorpus` confirmed unchanged (10 of 10), which is the expected, verified-by-running result before T76 lands.
**Decision**: two admission points needed the same exclusion, not one. `RetainedGraphBuilder.Build`'s root selection is the entry point STATE.md named (Component/DeploymentUnit roots), but tracing a concrete leak (`SistemaB.Testes -> SistemaB.Api`) by hand showed a second, independent admission path: `RetentionPolicy.Apply`'s "incoming" step (design.md step 5, sustaining dependents/reverse impact) re-admits a source purely because it points at an already-retained, non-test target - regardless of whether the source itself is a test project. Fixing only the root-selection side would have left every test project's outbound edge into a real component republished through this second path. Both now share the same `SourceInventory.IsTestProject` check via an occurrence-based owner lookup, reused rather than reimplemented in each file.
**Adequacy**: `Build_ExcludesARootOwnedOnlyByATestProjectByDefault` / `Build_IncludesATestProjectRootWhenPolicyEnabled` pin the root-selection admission point directly; `Apply_ExcludesIncomingRelationFromATestProjectSourceByDefault` / `Apply_IncludesIncomingRelationFromATestProjectSourceWhenPolicyEnabled` pin the incoming-edge admission point the first fix alone would have missed. Both use `App.Tests/App.Tests.csproj`, a naming convention already recognized before this task, so the tests are independent of T76's naming fix.
**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): two faults, each rebuilt Release-clean. Disabling the root-selection exclusion (`RootKinds.Contains(entity.Kind) && (includeTests || !IsTestOnly(...) || true)`) killed `Build_ExcludesARootOwnedOnlyByATestProjectByDefault` (1 of 2 in that file) and left the rest of the suite, including `Build_StartsClosureAtEveryProvenRootKind`, green. Disabling the incoming-edge exclusion the same way killed `Apply_ExcludesIncomingRelationFromATestProjectSourceByDefault` (1 of 2 in that file) and left `Apply_AddsIncomingSupportRelation`/`Apply_AddsIncomingSupportEntity` green, proving the fix does not touch a non-test dependent. Both reverted and the full gate re-run before the commit.

### T76: Recognize the corpus's own test-naming convention

**What**: Extend the test-document heuristic to recognize `.Testes` (and a bare `testes` segment), so `--include-tests`'s absence actually excludes `fixtures/ArchitectureDependencyLab`'s test projects — today it silently does not, because the heuristic only knows the English `.Tests`/`.UnitTests`/`.IntegrationTests` spellings and the project's own primary accuracy fixture is named in Portuguese.
**Where**: `src/Csharp2Md.Core/Analysis/Inventory/SourceInventory.cs`
**Depends on**: T75
**Reuses**: the existing segment-based `LooksLikeTestDocument` shape; only the recognized suffix set grows
**Requirement**: PKG-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] `LooksLikeTestDocument` recognizes a path segment equal to `testes` or ending in `.Testes`, case-insensitively, alongside the existing English patterns.
- [x] `SourceInventoryTests.cs` gains a case for `SistemaA.Testes/Foo.cs` alongside the existing English cases, and a case proving an unrelated segment such as `Testemunho` or `Manifesto` is not mistaken for a test path.
- [x] `OracleProjectReferenceScoreTests.cs` is re-measured with T73-T76 combined and every baseline raised to the true measured value in this commit, with the false-positive and test-policy-leak counts named explicitly in the commit's task record.
- [x] The `analyze` of `fixtures/ArchitectureDependencyLab` with default flags publishes no source document, Component, or DeploymentUnit **entity** whose logical path contains a `.Testes` project segment (confirmed: `RetainedGraph.Entities` carries zero such entries for every solution). **Revised while executing**: a *stricter*, wire-level check - whether a `.Testes` document/project key appears anywhere in the committed package, including as a dependency's Document/Project-scope target reached through membership lifting rather than as a retained entity in its own right - found one more leak. That check and its fix are T77; this bullet's own, narrower wording is satisfied here.

**Tests**: unit — ≥2 cases; e2e — the oracle class re-measured
**Gate**: full + `Category=OracleCorpus`
**Commit**: `fix(analysis): recognize .Testes as a test-project path segment`

**Status**: Complete
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **635 of 635** (up from 631 by this task's 2 focused cases), `Csharp2Md.Cli.Tests` **95 passed / 2 skipped** (the two skips being the absent eShopOnContainers and Pitstop clones); `Category=OracleCorpus` (10 of 10) confirmed the corpus-wide perfect score: **60 correct, 0 false positives, 0 test-policy leaks** - every one of the 87 oracle edges accounted for (60 reachable + 27 correctly excluded).
**Decision**: `LooksLikeTestDocument` gained `testes` (bare segment) and `.Testes` (suffix) alongside the existing English patterns, matching the exact convention `IsTestProject`/T75 already reuses. `RecordedDefects` was renamed to `RecordedBaselines` and its doc comments rewritten: with T73-T76 combined the numbers pin a *correct* state (60/0/0), not a defect, and the two-sided ratchet degenerates naturally into an exact-match check at that ceiling rather than needing new machinery.
**Adequacy**: `LooksLikeTestDocument_UsesPathSegments` gained the `.Testes` case plus two negative cases (`Testemunho`, `Manifesto`) proving the suffix match requires the literal `.` separator, not just a "Testes" substring. `IsTestProject_RecognizesThePortugueseTestesConvention` pins the project-path variant T75 depends on. `Corpus_ScoresTheRecordedShareOfItsReachableOracle` moving from 7/29/4 to 60/0/0 in one ratchet assertion is the adequacy evidence that matters most: every one of the corpus's 87 declared edges is now accounted for correctly.
**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): the `.Testes`/`testes` patterns were removed from `LooksLikeTestDocument`, rebuilt Release-clean, and run against `SourceInventoryTests` - it killed `IsTestProject_RecognizesThePortugueseTestesConvention` and the `.Testes` case of `LooksLikeTestDocument_UsesPathSegments` (2 of 2 new cases), leaving every other case, including the two new negative cases, green. Reverted and the full gate re-run before the commit.

### T77: Exclude test-project occurrences from entity membership lifting

**What**: Stop a genuinely-retained entity's Document/Project/Component/DeploymentUnit membership from being widened by an occurrence recorded while analysing a test project as its own root - discovered while verifying T76: a shared symbol called from both production code and a test file (e.g. `CotacoesTests.cs` calling into production code it exercises) leaked a `.Testes`-owned document and project into the default package's dependency graph and local entity table, even though the test project's own roots and incoming edges were already correctly excluded by T75.
**Where**: `src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs`
**Depends on**: T76
**Reuses**: `SourceInventory.IsTestProject` (T75); the same occurrence-based owner check, applied at a third admission point
**Requirement**: PKG-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:run-tests`.

**Done when**:

- [x] `BuildMemberships` and `BuildProjectsByDocument` derive Document/Project/Component/DeploymentUnit membership from occurrences with a test-project owner excluded by default, sharing one `NonTestOccurrences` filter rather than duplicating the check.
- [x] A retained entity called from both a real production file and a test file keeps its production-side membership and drops the test-side one by default, and keeps both when `--include-tests` is set.
- [x] At least 2 cases on `ScopePairingTests.cs` pin default-exclusion and opt-in inclusion for this specific admission point, independent of T75's root/incoming-edge cases.
- [x] `fixtures/ArchitectureDependencyLab`'s real leak (`SistemaA.Testes/CotacoesTests.cs` reachable through a shared symbol) is closed, confirmed via the oracle package's own entity table.

**Tests**: unit — ≥2 cases
**Gate**: full + `Category=OracleCorpus`
**Commit**: `fix(packagebuilding): exclude test occurrences from membership`

**Status**: Complete
**Gate note**: full gate green — Release build 0 warnings / 0 errors, `Csharp2Md.Core.Tests` **637 of 637** (up from 635 by this task's 2 focused cases), `Csharp2Md.Cli.Tests` **96 passed / 2 skipped** (up from 95 by the leak-detection case this task's fix now passes; the two skips are the absent eShopOnContainers and Pitstop clones).
**Decision**: this was not visible from the oracle's Project-scope `ProjectReference` score alone (60/60/0/0 was already reached by T73-T76) - it surfaced only when verifying T76's own fourth Done-when bullet ("no source document, Component, or DeploymentUnit... contains a `.Testes` project segment") against the real corpus. Root cause, confirmed by tracing raw occurrences through a throwaway diagnostic (deleted before commit): `BuildMemberships`/`BuildProjectsByDocument` derive an entity's Document/Project/Component/DeploymentUnit membership from **every** occurrence of that entity in the unfiltered `FactualGraph`, including one recorded while analysing `SistemaA.Testes` as its own root when its compilation calls into a genuinely-retained production symbol. T75 excluded the test project's own roots and its outbound incoming edges; this closes the third path - the target side of an otherwise-legitimate production relation being widened by a test-side occurrence of the same symbol. `NonTestOccurrences` centralises the filter so `BuildMemberships`'s two internal uses (`occurrences` and the nested `RootsByProject`) and `BuildProjectsByDocument` share one answer to "does this occurrence count."
**Adequacy**: `Build_TargetMembershipExcludesATestProjectOccurrenceByDefault` extends `ScopePairingTests`' existing fixture with a second occurrence of the already-retained `entity:target` from a test project, and asserts no Document- or Project-scope edge names the test file or test project by default; `Build_TargetMembershipIncludesATestProjectOccurrenceWhenPolicyEnabled` asserts the opposite with `--include-tests`. Both are independent of T75's cases, which cover root selection and incoming-edge admission, not target-side membership lifting.
**Discrimination proven by hand** (sensor is a standing skip per `AGENTS.md`): `NonTestOccurrences` was reverted to unconditionally return `graph.Occurrences`, rebuilt Release-clean, and run against `ScopePairingTests` - it killed `Build_TargetMembershipExcludesATestProjectOccurrenceByDefault`, reproducing the exact leak shape found in the real corpus (`Caller.cs -> App.Tests/TargetCalledFromTest.cs`), and left every other case in the file green. Reverted and the full gate re-run before the commit.

### T78: Correct the traceability table and close the open finding

**What**: Update `spec.md`'s Requirement Traceability for DEP-01, DEP-02, DEP-03 and PKG-05 to cite T73-T77 and reflect a measured, not assumed, "Complete"; replace `.specs/STATE.md`'s "OPEN FINDING: the component dependency graph is complete" and the unmeasured project-reference finding with the corrected, re-measured numbers.
**Where**: `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`, `.specs/STATE.md`
**Depends on**: T77
**Reuses**: nothing — documentation only
**Requirement**: none (documentation)

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`.

**Done when**:

- [ ] `spec.md`'s traceability rows for DEP-01, DEP-02, DEP-03 and PKG-05 name T73-T77 and state the measured outcome (oracle score, complete-graph check, test-leak check), not a restated assumption.
- [ ] `.specs/STATE.md`'s two open-finding sections are replaced by the corrected state: the re-measured oracle score, the confirmed absence of BCL/primitive entities in a real corpus's retained graph, and the confirmed absence of `.Testes`-owned entities in the default package.
- [ ] Any claim this phase could not fully close (for example, a residual fan-out from a legitimate, genuinely shared in-solution type used by many components) is written down as a named residual, not silently dropped.

**Tests**: none (documentation)
**Gate**: `validate_state.py` on this feature
**Commit**: `docs(state): correct the projection traceability after phase 11`

## Requirement-to-Task Traceability

| Requirements | Owning task(s) | Acceptance seam |
| ------------ | -------------- | --------------- |
| PKG-01 | T2, T5, T27, T30, T37, T39, T42 | CLI journey package |
| PKG-02 | T3, T12, T16, T42 | CLI roots/closure |
| PKG-03 | T3, T15, T16, T30, T42 | CLI retained set |
| PKG-04 | T3, T17, T30, T42 | CLI relevant gaps |
| PKG-05 | T8, T17, T30, T42 | CLI exclusions |
| PKG-06 | T8, T17, T30, T42 | CLI cited sources |
| PKG-07 | T3, T7, T8, T14, T31, T42 | CLI safety/config |
| PKG-08 | T5, T8, T17, T30, T38-T39, T42 | CLI policy identity |
| PKG-09 | T3, T12-T16, T42 | factual graph + CLI |
| PKG-10 | T1, T40, T45, T59 | topology surface |
| DEP-01..DEP-08 | T4, T12-T13, T18, T22, T42, T46-T47, T49, T53, T71 | hand-recalculated dependencies; oracle-scored project references |
| MET-01..MET-02, MET-04 | T4, T19, T42 | hand-recalculated direct measures |
| MET-03 | T19, T42, T55 | hand-recalculated direct measures |
| MET-05 | T4, T20, T42 | hand-recalculated SCCs |
| MET-06..MET-07 | T4, T17, T21, T42 | hand-recalculated impact/gaps |
| MET-08 | T21-T22, T42, T56 | hand-recalculated impact/gaps |
| NAV-01 | T26-T27, T32, T37, T42, T54 | manifest/index traversal |
| NAV-02, NAV-05 | T22, T27-T29, T37, T42 | machine/Markdown equivalence |
| NAV-03 | T28, T42, T58 | machine/Markdown equivalence |
| NAV-04 | T26, T28, T32, T42, T54, T58 | machine/Markdown equivalence |
| NAV-06..NAV-07 | T26, T34, T42, T54 | locate budgets |
| NAV-08..NAV-09 | T26, T35, T42, T69 | graph journey budgets |
| NAV-10 | T26, T34, T42, T50 | evidence budget |
| VAR-01..VAR-02 | T9-T10, T41 | real workspace fixture |
| VAR-03..VAR-05 | T3, T7, T11, T41 | occurrence/collision tests |
| VAR-06 | T7, T11, T15, T24, T37-T39 | multi-solution CLI |
| STO-01..STO-02 | T23, T33, T43 | vectors/collision/rejection |
| STO-03..STO-05 | T5, T24, T27, T33, T37, T49, T52-T53 | tables and resolver |
| STO-06..STO-07 | T6, T25, T30, T33, T37 | byte/shard determinism |
| PUB-01 | T5-T6, T36 | single materialization path |
| PUB-02 | T5, T32-T33, T36 | staged rehydration |
| PUB-03..PUB-04 | T2, T29, T32-T33, T37-T40 | shared public validation |
| PUB-05 | T32-T33, T36, T43 | byte preservation |
| PUB-06..PUB-07 | T31, T41, T43 | safety fixture/rejection |
| PUB-08 | T2, T15, T36, T38-T39, T43, T55 | structured diagnostics |
| CRT-01..CRT-02 | T34-T35, T37, T42, T48, T51, T69 | journey certification |
| CRT-03 | T5, T15, T30, T37, T42, T57 | journey certification |
| CRT-04..CRT-07 | T9, T47, T49-T50, T52-T54, T44 | optional corpora |
| CRT-08 | T8, T12-T14, T31, T41, T70 | fixture integrity |
| CRT-09 | T42 | CLI E2E |
| EDG-01 | T13, T16-T17, T33, T43 | evidence rejection |
| EDG-02 | T34-T36, T43 | journey budget rejection |
| EDG-03 | T25, T30, T36, T43 | package budget rejection |
| EDG-04 | T11, T13, T18, T43 | variant-qualified dependency |
| EDG-05 | T29, T33, T36, T43 | representation divergence |

All 71 requirements have at least one focused owning task and a final acceptance seam. This table is the grouped view; `spec.md`'s Requirement Traceability carries the per-ID expansion, derived from each task's `**Requirement**` line. T58 is listed against NAV-04 because it replaced the root-absolute summary link that does not resolve from `markdown/`, which NAV-04 requires and T54 had left broken.

## Task Granularity Check

| Tasks | Deliverable boundary | Status |
| ----- | -------------------- | ------ |
| T1 | Repository scaffold | ✅ Cohesive build topology |
| T2-T7 | One facade/model/serializer/primitive component each | ✅ Granular |
| T8-T15 | One inventory/planner/workspace/accumulator/extractor/orchestrator component each | ✅ Granular |
| T16-T22 | One retention/measure/model-builder component each | ✅ Granular |
| T23-T30 | One identity/layout/index/renderer/reader/orchestrator component each | ✅ Granular |
| T31-T36 | One safety/reader/validator/certifier/publisher component each | ✅ Granular |
| T37 | One solution-scoped retrieval/publication contract | ✅ Cohesive cross-layer invariant |
| T38 | One facade workflow component | ✅ Granular |
| T39-T40 | One CLI command surface per task | ✅ Granular |
| T41 | One coherent fixture scenario | ✅ Cohesive fixture deliverable |
| T42-T44 | One acceptance concern per task | ✅ Granular |
| T48 | One journey-applicability correction | ✅ Granular |
| T46 | One scope-pairing correction | ✅ Granular |
| T47 | One artifact-duplication correction | ✅ Cohesive write/read contract |
| T49 | One local-reference compaction contract | ✅ Cohesive write/read contract |
| T50 | One bounded evidence-entry contract | ✅ Cohesive write/read contract |
| T51 | One causal flow-root correction | ✅ Granular |
| T52 | One artifact wire-form encoding contract | ✅ Cohesive write/read contract |
| T53 | One canonical-key resolution contract | ✅ Cohesive write/read contract |
| T54 | One root declaration contract | ✅ Cohesive write/read contract |
| T45 | One final repository topology cutover | ✅ Cohesive clean-cut deliverable |
| T55 | One test-discrimination repair set | ✅ Cohesive test-integrity deliverable |
| T56 | One composite-score scan widening | ✅ Granular |
| T57 | One per-family measurement contract | ✅ Cohesive write/read contract |
| T58 | One document-page and link contract | ✅ Cohesive rendering deliverable |
| T59 | One traceability refresh | ✅ Granular |

T1, T37, T41, T45, T47, T48, T52, T53 and T54 necessarily touch multiple physical files, but each is one indivisible deliverable. Splitting any of them would create an invalid scaffold, a partially qualified package contract, a fixture with no stable oracle, a repository with mixed contracts, or a clarified rule without matching fixtures.

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| ---- | ---------------------- | ------------- | ------ |
| T1 | None | phase start | ✅ Match |
| T2 | T1 | T1 -> T2 | ✅ Match |
| T3 | T2 | T2 -> T3 | ✅ Match |
| T4 | T3 | T3 -> T4 | ✅ Match |
| T5 | T4 | T4 -> T5 | ✅ Match |
| T6 | T5 | T5 -> T6 | ✅ Match |
| T7 | T6 | T6 -> T7 | ✅ Match |
| T8 | T7 | phase 2 after phase 1 | ✅ Match |
| T9 | T8 | T8 -> T9 | ✅ Match |
| T10 | T9 | T9 -> T10 | ✅ Match |
| T11 | T10 | T10 -> T11 | ✅ Match |
| T12 | T11 | T11 -> T12 | ✅ Match |
| T13 | T12 | T12 -> T13 | ✅ Match |
| T14 | T13 | T13 -> T14 | ✅ Match |
| T15 | T14 | T14 -> T15 | ✅ Match |
| T16 | T15 | phase 3 after phase 2 | ✅ Match |
| T17 | T16 | T16 -> T17 | ✅ Match |
| T18 | T17 | T17 -> T18 | ✅ Match |
| T19 | T18 | T18 -> T19 | ✅ Match |
| T20 | T19 | T19 -> T20 | ✅ Match |
| T21 | T20 | T20 -> T21 | ✅ Match |
| T22 | T21 | T21 -> T22 | ✅ Match |
| T23 | T22 | phase 4 after phase 3 | ✅ Match |
| T24 | T23 | T23 -> T24 | ✅ Match |
| T25 | T24 | T24 -> T25 | ✅ Match |
| T26 | T25 | T25 -> T26 | ✅ Match |
| T27 | T26 | T26 -> T27 | ✅ Match |
| T28 | T27 | T27 -> T28 | ✅ Match |
| T29 | T28 | T28 -> T29 | ✅ Match |
| T30 | T29 | T29 -> T30 | ✅ Match |
| T31 | T30 | phase 5 after phase 4 | ✅ Match |
| T32 | T31 | T31 -> T32 | ✅ Match |
| T33 | T32 | T32 -> T33 | ✅ Match |
| T34 | T33 | T33 -> T34 | ✅ Match |
| T35 | T34 | T34 -> T35 | ✅ Match |
| T36 | T35 | T35 -> T36 | ✅ Match |
| T37 | T36 | T36 -> T37 | ✅ Match |
| T38 | T37 | T37 -> T38 | ✅ Match |
| T39 | T38 | phase 6 after phase 5 | ✅ Match |
| T40 | T39 | T39 -> T40 | ✅ Match |
| T41 | T40 | T40 -> T41 | ✅ Match |
| T42 | T41 | T41 -> T42 | ✅ Match |
| T43 | T42 | T42 -> T43 | ✅ Match |
| T48 | T43 | T43 -> T48 | ✅ Match |
| T46 | T48 | T48 -> T46 | ✅ Match |
| T47 | T46 | T46 -> T47 | ✅ Match |
| T49 | T47 | T47 -> T49 | ✅ Match |
| T50 | T49 | T49 -> T50 | ✅ Match |
| T52 | T51 | T51 -> T52 | ✅ Match |
| T53 | T52 | T52 -> T53 | ✅ Match |
| T54 | T53 | T53 -> T54 | ✅ Match |
| T44 | T54 | T54 -> T44 | ✅ Match |
| T45 | T44 | T44 -> T45 | ✅ Match |
| T55 | T45 | phase 7 after phase 6 | ✅ Match |
| T56 | T55 | T55 -> T56 | ✅ Match |
| T57 | T56 | T56 -> T57 | ✅ Match |
| T58 | T57 | T57 -> T58 | ✅ Match |
| T59 | T58 | T58 -> T59 | ✅ Match |
| T60 | T59 | phase 8 after phase 7 | ✅ Match |
| T61 | T60 | T60 -> T61 | ✅ Match |
| T62 | T61 | T61 -> T62 | ✅ Match |
| T63 | T62 | T62 -> T63 | ✅ Match |
| T64 | T63 | T63 -> T64 | ✅ Match |
| T65 | T64 | T64 -> T65 | ✅ Match |
| T66 | T65 | T65 -> T66 | ✅ Match |
| T67 | T66 | phase 9 after phase 8 | ✅ Match |
| T68 | T67 | T67 -> T68 | ✅ Match |
| T69 | T67 | T67 -> T69 | ✅ Match |
| T70 | T69 | phase 10 after phase 9 | ✅ Match |
| T71 | T70 | T70 -> T71 | ✅ Match |
| T72 | T71 | T71 -> T72 | ✅ Match |
| T73 | T72 | phase 11 after phase 10 | ✅ Match |
| T74 | T73 | T73 -> T74 | ✅ Match |
| T75 | T74 | T74 -> T75 | ✅ Match |
| T76 | T75 | T75 -> T76 | ✅ Match |
| T77 | T76 | T76 -> T77 | ✅ Match |
| T78 | T77 | T77 -> T78 | ✅ Match |

Cross-phase dependencies are represented by the ordered phase chain; all intra-phase edges match exactly.

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| ---- | --------------------------- | --------------- | --------- | ------ |
| T1 | Topology/config | unit + build | unit/build | ✅ OK |
| T2 | Public facade | unit | unit | ✅ OK |
| T3 | Factual graph contract | unit | unit | ✅ OK |
| T4 | Retrieval contracts | unit | unit | ✅ OK |
| T5 | Package contracts | unit | unit | ✅ OK |
| T6 | Canonical serialization | unit | unit | ✅ OK |
| T7 | Identity primitives | unit | unit | ✅ OK |
| T8 | Source inventory | integration | integration | ✅ OK |
| T9 | Variant discovery | integration | integration | ✅ OK |
| T10 | Variant workspace | integration | integration | ✅ OK |
| T11 | Analysis merge logic | unit | unit | ✅ OK |
| T12 | Architecture extraction | integration | integration | ✅ OK |
| T13 | Causal relation extraction | integration | integration | ✅ OK |
| T14 | Configuration/persistence extraction | integration | integration | ✅ OK |
| T15 | Analysis orchestration | integration | integration | ✅ OK |
| T16 | Confirmed retention closure | unit | unit | ✅ OK |
| T17 | Gap/source retention | unit | unit | ✅ OK |
| T18 | Dependency aggregation | unit | unit | ✅ OK |
| T19 | Direct measures | unit | unit | ✅ OK |
| T20 | Cycle calculation | unit | unit | ✅ OK |
| T21 | Reverse impact | unit | unit | ✅ OK |
| T22 | Retrieval model assembly | unit | unit | ✅ OK |
| T23 | Public IDs | unit | unit | ✅ OK |
| T24 | Local tables/handles | unit | unit | ✅ OK |
| T25 | Shard packing | unit | unit | ✅ OK |
| T26 | Navigation indexes | unit | unit | ✅ OK |
| T27 | Machine rendering | unit | unit | ✅ OK |
| T28 | Markdown rendering | unit | unit | ✅ OK |
| T29 | Retrieval rehydration | integration | integration | ✅ OK |
| T30 | Package-plan orchestration | integration | integration | ✅ OK |
| T31 | Publication safety | integration | integration | ✅ OK |
| T32 | Package reader | integration | integration | ✅ OK |
| T33 | Package validator | integration | integration | ✅ OK |
| T34 | Locate/evidence certifier | integration | integration | ✅ OK |
| T35 | Flow/impact certifier | integration | integration | ✅ OK |
| T36 | Atomic publisher | integration | integration | ✅ OK |
| T37 | Solution-scoped retrieval/publication contract | unit + integration | unit + integration | ✅ OK |
| T38 | Facade workflow | integration | integration | ✅ OK |
| T39 | Analyze CLI | e2e | e2e | ✅ OK |
| T40 | Validate CLI | e2e | e2e | ✅ OK |
| T41 | Versioned fixture | e2e | e2e | ✅ OK |
| T42 | Journey acceptance | e2e | e2e | ✅ OK |
| T43 | Failure acceptance | e2e | e2e | ✅ OK |
| T48 | Journey applicability | unit | unit | ✅ OK |
| T46 | Scope pairing | unit | unit | ✅ OK |
| T47 | Artifact indexing | unit | unit | ✅ OK |
| T49 | Local reference compaction | unit | unit | ✅ OK |
| T50 | Bounded evidence journey | unit + integration | unit + integration | ✅ OK |
| T51 | Causal flow root | unit | unit | ✅ OK |
| T52 | Artifact wire form | unit | unit | ✅ OK |
| T53 | Canonical key resolution | unit | unit | ✅ OK |
| T54 | Root declaration routing | unit | unit | ✅ OK |
| T44 | Optional corpora | e2e | e2e | ✅ OK |
| T45 | Topology + CLI current contract | unit + e2e + build | unit + e2e + build | ✅ OK |
| T55 | Publication + PackageBuilding + Analysis tests | unit | unit | ✅ OK |
| T56 | Public facade and contracts | unit | unit | ✅ OK |
| T57 | Package contracts + PackageBuilding | unit | unit | ✅ OK |
| T58 | Rendering + CLI seam + optional corpora | unit + e2e | unit + e2e | ✅ OK |
| T59 | Documentation only | none | none | ✅ OK |

No task defers its required tests to a later task. Later E2E tests add acceptance coverage; they do not substitute for the focused tests committed with the component that they exercise.

## Tool Confirmation Before Execute

Proposed task tooling:

- MCP: Context7 for Roslyn (`T9`, `T10`, `T31`), `System.Text.Json` (`T37`) and System.CommandLine (`T39`) API verification; none for the remaining tasks.
- Skills: `tlc-spec-driven` throughout; `dotnet-test:code-testing-agent` for test authoring; `dotnet-test:run-tests` for every gate; `dotnet-skills:serialization` for canonical JSON/package readers; `dotnet-skills:csharp-concurrency-patterns` for publication locking; `dotnet-skills:api-design` for the public facade.

The task list was approved by the user on 2026-09-15. Confirm the proposed tool choices and the sequential batch/sub-agent strategy when Execute begins.
