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
T39 -> T40 -> T41 -> T42 -> T43 -> T48 -> T46 -> T47 -> T49 -> T50 -> T51 -> T52 -> T53 -> T44 -> T45
```

T46-T53 were added after T43 was complete, so they carry higher numbers than the tasks that follow them; execution order is the diagram, not the number. The six phases form six sequential task-budgeted batches. At Execute, offer batch sub-agents and dispatch them only if the user accepts; never split a phase and never run batches concurrently.

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

**What**: Replace repeated entity, variant and cycle canonical keys inside stored relations, aggregated dependencies and scope measures with solution-local handles resolvable through the declared table.
**Where**: `src/Csharp2Md.Core/PackageBuilding/Rendering/MachineArtifactWriter.cs`, `src/Csharp2Md.Core/PackageBuilding/RetrievalModel.cs`, `src/Csharp2Md.Core/Publication/RetrievalModelReader.cs`
**Depends on**: T52
**Reuses**: `LocalTableBuilder` and the relation/evidence handle table contract T49 established
**Requirement**: DEP-03, DEP-05, STO-03, STO-04, STO-05, CRT-04, CRT-06

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] `StoredRelation` source/target keys, `AggregatedDependency` source/target/variants, `ScopeMeasures` entity and cycle references, and reverse-impact targets carry deterministic solution-local handles, with each canonical identity stored once in its declared table.
- [ ] A consumer resolves each handle to the canonical entity, variant or cycle identity through one declared table artifact without scanning unrelated artifacts.
- [ ] Rehydration restores the same canonical values and rejects missing, duplicate or invalid table mappings by artifact name.
- [ ] eShopOnContainers is at most 64 MiB and eShop completes analysis when the optional clones are present; Pitstop stays at most 25 MiB; the quick gate passes.

**Tests**: unit - >=8 handle/table/rehydration/rejection cases
**Gate**: quick + local corpora when present
**Commit**: `fix(package-building): resolve entity keys through local table`

### T44: Certify optional local corpora

**What**: Update LocalCorpus acceptance to assert eShop variant isolation and the eShopOnContainers/Pitstop committed-package ceilings.  
**Where**: `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs`  
**Depends on**: T53  
**Reuses**: dynamic skip convention and gitignored local clone paths  
**Requirement**: CRT-04, CRT-05, CRT-06, CRT-07

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] eShop completes without cross-project variant collision when present.
- [ ] eShopOnContainers is ≤1,500 reachable committed files and ≤64 MiB; Pitstop is ≤750 files and ≤25 MiB.
- [ ] Missing clones dynamically skip with reason and do not fail CI; clones are never staged or committed.
- [ ] At least 3 corpus-level cases exist; LocalCorpus gate runs for each clone currently present and full gate passes.

**Tests**: e2e — ≥3 corpus cases  
**Gate**: full + LocalCorpus when present  
**Commit**: `test(cli): certify optional local corpora`

### T45: Complete the clean-cut repository topology

**What**: Remove legacy product/test projects and obsolete contracts, point CLI solely at Core, update solution/package/docs, and prove only the current contract remains.  
**Where**: `csharp2md.slnx`  
**Depends on**: T44  
**Reuses**: only source/test code explicitly ported by earlier tasks; no compatibility facade, converter, feature flag or version reader  
**Requirement**: PKG-10

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Solution contains only `Csharp2Md.Core`, `Csharp2Md.Cli`, `Csharp2Md.Core.Tests` and `Csharp2Md.Cli.Tests`.
- [ ] Legacy Domain/Analysis/Storage/Projection assemblies, their obsolete tests, compose/batch paths, schemas and taxonomy registry are removed.
- [ ] Static surface tests prove no legacy contract names, version dispatch, compatibility path, `Microsoft.Build.*` reference or `MSBuildLocator.RegisterDefaults()` remains.
- [ ] README files describe only the current root-manifest package and analyze/validate commands.
- [ ] Build gate passes; fixture E2E passes; any present LocalCorpus gate passes.

**Tests**: unit + e2e — ≥10 topology/current-contract assertions plus all retained suites  
**Gate**: build + LocalCorpus when present  
**Commit**: `refactor(core): complete knowledge package cutover`

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
| PKG-10 | T1, T40, T45 | topology surface |
| DEP-01..DEP-08 | T4, T12-T13, T18, T22, T42, T46-T47, T49, T53 | hand-recalculated dependencies |
| MET-01..MET-04 | T4, T19, T42 | hand-recalculated direct measures |
| MET-05 | T4, T20, T42 | hand-recalculated SCCs |
| MET-06..MET-08 | T4, T17, T21-T22, T42 | hand-recalculated impact/gaps |
| NAV-01 | T26-T27, T32, T37, T42 | manifest/index traversal |
| NAV-02..NAV-05 | T22, T27-T29, T37, T42 | machine/Markdown equivalence |
| NAV-06..NAV-07 | T26, T34, T42 | locate budgets |
| NAV-08..NAV-09 | T26, T35, T42 | graph journey budgets |
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
| PUB-08 | T2, T15, T36, T38-T39, T43 | structured diagnostics |
| CRT-01..CRT-03 | T15, T30, T34-T37, T42, T48 | journey certification |
| CRT-04..CRT-07 | T9, T47, T49-T50, T52-T53, T44 | optional corpora |
| CRT-08 | T8, T12-T14, T31, T41 | fixture integrity |
| CRT-09 | T42 | CLI E2E |
| EDG-01 | T13, T16-T17, T33, T43 | evidence rejection |
| EDG-02 | T34-T36, T43 | journey budget rejection |
| EDG-03 | T25, T30, T36, T43 | package budget rejection |
| EDG-04 | T11, T13, T18, T43 | variant-qualified dependency |
| EDG-05 | T29, T33, T36, T43 | representation divergence |

All 71 requirements have at least one focused owning task and a final acceptance seam.

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
| T45 | One final repository topology cutover | ✅ Cohesive clean-cut deliverable |

T1, T37, T41, T45, T47, T48, T52 and T53 necessarily touch multiple physical files, but each is one indivisible deliverable. Splitting any of them would create an invalid scaffold, a partially qualified package contract, a fixture with no stable oracle, a repository with mixed contracts, or a clarified rule without matching fixtures.

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
| T44 | T53 | T53 -> T44 | ✅ Match |
| T45 | T44 | T44 -> T45 | ✅ Match |

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
| T44 | Optional corpora | e2e | e2e | ✅ OK |
| T45 | Topology + CLI current contract | unit + e2e + build | unit + e2e + build | ✅ OK |

No task defers its required tests to a later task. Later E2E tests add acceptance coverage; they do not substitute for the focused tests committed with the component that they exercise.

## Tool Confirmation Before Execute

Proposed task tooling:

- MCP: Context7 for Roslyn (`T9`, `T10`, `T31`), `System.Text.Json` (`T37`) and System.CommandLine (`T39`) API verification; none for the remaining tasks.
- Skills: `tlc-spec-driven` throughout; `dotnet-test:code-testing-agent` for test authoring; `dotnet-test:run-tests` for every gate; `dotnet-skills:serialization` for canonical JSON/package readers; `dotnet-skills:csharp-concurrency-patterns` for publication locking; `dotnet-skills:api-design` for the public facade.

The task list was approved by the user on 2026-09-15. Confirm the proposed tool choices and the sequential batch/sub-agent strategy when Execute begins.
