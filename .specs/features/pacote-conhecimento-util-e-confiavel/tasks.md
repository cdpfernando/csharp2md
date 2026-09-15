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
T31 -> T32 -> T33 -> T34 -> T35 -> T36 -> T37
```

### Phase 6: CLI acceptance and clean cut

```text
T38 -> T39 -> T40 -> T41 -> T42 -> T43 -> T44
```

The six phases form six sequential task-budgeted batches. At Execute, offer batch sub-agents and dispatch them only if the user accepts; never split a phase and never run batches concurrently.

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

- [ ] Serialization is UTF-8 without BOM, LF-normalized and deterministically ordered by prepared models.
- [ ] Only current contract types are registered in the source-generation context.
- [ ] At least 6 byte-level round-trip and determinism cases pass.
- [ ] Quick gate passes.

**Tests**: unit — ≥6 focused cases  
**Gate**: quick  
**Commit**: `feat(core): add canonical package serialization`

### T7: Add canonical identity primitives

**What**: Define solution/project/entity canonical keys, safe logical locators, spans and variant values without persisted legacy IDs.  
**Where**: `src/Csharp2Md.Core/Analysis/IdentityPrimitives.cs`  
**Depends on**: T6  
**Reuses**: useful validation behavior from Domain identity/literal types, adjusted to the approved contract  
**Requirement**: PKG-07, VAR-03, VAR-04, VAR-06, STO-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Canonical keys are solution-scoped, deterministic and independent of absolute checkout paths.
- [ ] Logical locators reject rooted, escaping and non-normalized paths.
- [ ] At least 10 grammar, equality and path-safety cases pass.
- [ ] Build gate passes.

**Tests**: unit — ≥10 focused cases  
**Gate**: build  
**Commit**: `feat(core): add canonical identity primitives`

## Phase 2: Isolated factual analysis

### T8: Build the authorized source inventory

**What**: Port root containment, symlink checks, document inventory and include-tests policy into the Analysis module.  
**Where**: `src/Csharp2Md.Core/Analysis/Inventory/SourceInventory.cs`  
**Depends on**: T7  
**Reuses**: `PathGuard`, `DocumentInventory` and supported-document rules that satisfy the new policy  
**Requirement**: PKG-05, PKG-06, PKG-07, PKG-08, CRT-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Inventory cannot escape the authorized root through relative paths or symlinks.
- [ ] Test documents are excluded by default and the explicit option changes the analysis policy identity.
- [ ] At least 10 inventory, symlink, source/config and test-policy cases pass.
- [ ] Quick gate passes.

**Tests**: integration — ≥10 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): build authorized source inventory`

### T9: Discover project-specific variants

**What**: Discover evaluated `(project, target framework)` pairs through `MSBuildWorkspace` progress without a global `TargetFramework`.  
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/ProjectVariantPlanner.cs`  
**Depends on**: T8  
**Reuses**: Roslyn 5.6 `MSBuildWorkspace.Create`, `OpenSolutionAsync` and `ProjectLoadProgress.TargetFramework` only during `Resolve`  
**Requirement**: VAR-01, VAR-02, CRT-06

**Tools**: MCP: Context7; Skills: `tlc-spec-driven`, `context7-mcp`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Discovery creates only evaluated project/TFM pairs and deduplicates them per solution.
- [ ] Missing, unmatched or ambiguous SDK-style TFM progress fails as `variant-plan` instead of guessing.
- [ ] No `Microsoft.Build.*` reference or `MSBuildLocator.RegisterDefaults()` call exists.
- [ ] At least 8 real-workspace discovery and failure cases pass; quick gate passes.

**Tests**: integration — ≥8 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): discover project variants`

### T10: Load each project variant in isolation

**What**: Open one workspace per root project/TFM and expose compilations only for that evaluated root.  
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/ProjectVariantWorkspace.cs`  
**Depends on**: T9  
**Reuses**: existing workspace diagnostic/cancellation handling and Roslyn's out-of-process BuildHost  
**Requirement**: VAR-01, VAR-02, VAR-04, CRT-06

**Tools**: MCP: Context7; Skills: `tlc-spec-driven`, `context7-mcp`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Each planned pair opens its project with only that pair's global TFM property.
- [ ] Referenced projects support compilation but never emit occurrences as another root.
- [ ] Workspaces are disposed on success, failure and cancellation.
- [ ] At least 8 isolation, reference and disposal cases pass; quick gate passes.

**Tests**: integration — ≥8 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): isolate project variant workspaces`

### T11: Merge logical entities across variants

**What**: Merge compatible occurrences into one logical entity, retain variant-qualified shapes and reject intra-variant collisions.  
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/LogicalEntityAccumulator.cs`  
**Depends on**: T10  
**Reuses**: deterministic ordering patterns from the current accumulator, not its persisted identifiers  
**Requirement**: VAR-03, VAR-04, VAR-05, VAR-06, EDG-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Compatible occurrences across TFMs share one logical entity.
- [ ] Shape differences remain qualified across variants and collide only within the same variant.
- [ ] Separate solutions cannot share identity, occurrence or deduplication state.
- [ ] At least 10 merge, collision and solution-isolation cases pass; quick gate passes.

**Tests**: unit — ≥10 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): merge variant occurrences`

### T12: Extract structural and architectural roots

**What**: Emit solution, project, document, symbol, Component, Deployment Unit, Entry Point and Boundary Operation facts with evidence.  
**Where**: `src/Csharp2Md.Core/Analysis/Extraction/ArchitectureFactExtractor.cs`  
**Depends on**: T11  
**Reuses**: selected symbol, component, entry-point and boundary detectors whose behavior matches `CONTEXT.md`  
**Requirement**: PKG-02, PKG-09, DEP-08, CRT-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Roots require approved evidence; project/assembly/directory names alone never create Deployment Units.
- [ ] Production/test provenance and variant-qualified locators are retained.
- [ ] No business-rule interpretation or heuristic quality label is emitted.
- [ ] At least 12 extractor and negative-proof cases pass; quick gate passes.

**Tests**: integration — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): extract architectural roots`

### T13: Extract causal and boundary relations

**What**: Emit evidence-backed direct relations for Project Reference, internal invocation, structural type use, HTTP, gRPC, messaging and contracts.  
**Where**: `src/Csharp2Md.Core/Analysis/Extraction/CausalRelationExtractor.cs`  
**Depends on**: T12  
**Reuses**: bindable walkers and boundary/contract passes selected by required behavior  
**Requirement**: DEP-02, PKG-09, CRT-08, EDG-01, EDG-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Every confirmed relation resolves source, target, variant occurrence and evidence chain.
- [ ] Repeated calls remain separate factual occurrences for later aggregation.
- [ ] Unresolved destinations become Candidate/Unknown/Open Frontier with cause, never guessed confirmed edges.
- [ ] At least 14 category, repeated-call and unresolved cases pass; quick gate passes.

**Tests**: integration — ≥14 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): extract causal relations`

### T14: Extract configuration and persistence facts safely

**What**: Emit configuration keys/sections/bindings and persistence stores/objects/fields/operations without raw values.  
**Where**: `src/Csharp2Md.Core/Analysis/Extraction/ConfigurationPersistenceExtractor.cs`  
**Depends on**: T13  
**Reuses**: configuration and persistence detectors/builders that can emit the new factual contract  
**Requirement**: PKG-07, PKG-09, DEP-02, CRT-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Published graph inputs contain keys, sections, bindings, categories and safe locators only.
- [ ] Environment values, connection strings, credentials and absolute paths never enter the graph.
- [ ] Persistence relations remain observable and evidence-backed.
- [ ] At least 12 configuration, EF/SQL, secret and negative cases pass; quick gate passes.

**Tests**: integration — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(analysis): extract safe configuration and persistence facts`

### T15: Assemble an immutable FactualGraph per solution

**What**: Orchestrate inventory, variants and extractors into a deterministic in-memory graph with extraction measurements.  
**Where**: `src/Csharp2Md.Core/Analysis/SolutionAnalyzer.cs`  
**Depends on**: T14  
**Reuses**: useful failure diagnostics from the legacy pipeline; no pipeline-stage or persistence abstractions  
**Requirement**: PKG-03, PKG-09, VAR-06, CRT-03, PUB-08

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Analysis returns a graph and never writes or commits package files.
- [ ] Entity/relation/evidence order and extraction/filter measurements are deterministic.
- [ ] Diagnostics identify solution, project, variant and cause when applicable.
- [ ] At least 10 orchestration, cancellation, determinism and no-write cases pass; build gate passes.

**Tests**: integration — ≥10 focused cases  
**Gate**: build  
**Commit**: `feat(analysis): assemble factual solution graphs`

## Phase 3: Retention, dependencies and measures

### T16: Retain the confirmed journey closure

**What**: Select proven roots and retain only the confirmed causal/ownership closure required by supported journeys.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Retention/RetainedGraphBuilder.cs`  
**Depends on**: T15  
**Reuses**: factual graph only; no legacy projection allowlists or wire families  
**Requirement**: PKG-02, PKG-03, PKG-09, EDG-01

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Closure starts at all four proven root kinds and follows only confirmed causal relations to approved terminals.
- [ ] Required membership, occurrences and evidence are retained; disconnected inventory is absent.
- [ ] A confirmed relation without resolvable endpoints/evidence is demoted with known disposition or rejects the build.
- [ ] At least 12 closure and invalid-evidence cases pass; quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): retain confirmed journey closure`

### T17: Retain relevant gaps and cited sources

**What**: Add incoming support, journey-affecting gaps, cited sources and include-tests policy to the retained graph.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Retention/RetentionPolicy.cs`  
**Depends on**: T16  
**Reuses**: source redaction policy concepts, not legacy source payload layout  
**Requirement**: PKG-04, PKG-05, PKG-06, PKG-08, MET-07, EDG-01

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Candidate, Unknown and Open Frontier survive only when they can alter/interrupt a retained journey.
- [ ] Gap ordering uses affected journeys, affected roots and canonical identity.
- [ ] Only cited documents survive; test sources require explicit policy recorded in measurements.
- [ ] At least 12 gap, incoming, source and test-policy cases pass; quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): retain relevant gaps and sources`

### T18: Aggregate dependencies at four scopes

**What**: Build auditable direct dependency edges at Document, Project, Component and Deployment Unit scopes.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Measures/DependencyAggregator.cs`  
**Depends on**: T17  
**Reuses**: proven membership maps from retention and relation evidence handles  
**Requirement**: DEP-01, DEP-02, DEP-03, DEP-04, DEP-05, DEP-06, DEP-07, DEP-08, EDG-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Edges aggregate by solution/scope/source/target/category and include count, variants, relations and deduplicated evidence.
- [ ] Low-level relation payload is referenced across scopes, not copied.
- [ ] Candidate/Unknown/Open Frontier never contribute to confirmed counts; unproven upper scopes are omitted.
- [ ] At least 16 independently calculated aggregation/category/scope cases pass; quick gate passes.

**Tests**: unit — ≥16 focused cases  
**Gate**: quick  
**Commit**: `feat(package): aggregate scoped dependencies`

### T19: Calculate direct dependency measures

**What**: Calculate fan-in, fan-out, occurrence totals and cross-component edge counts from retained direct edges.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Measures/DirectMeasureCalculator.cs`  
**Depends on**: T18  
**Reuses**: aggregated edges only; test expectations use hand-authored graphs  
**Requirement**: MET-01, MET-02, MET-03, MET-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Fan-in/out count distinct origins/destinations in the requested scope.
- [ ] Occurrences are counted before edge deduplication and cross-component counts require proven distinct ownership.
- [ ] At least 12 hand-calculated empty, duplicate, multi-category and multi-scope cases pass.
- [ ] Quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): calculate direct dependency measures`

### T20: Calculate directed cycles

**What**: Calculate deterministic strongly connected components and cycle membership independently per scope.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Measures/CycleCalculator.cs`  
**Depends on**: T19  
**Reuses**: retained directed edges; no external graph library  
**Requirement**: MET-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] SCCs identify self-cycles and multi-node cycles without mixing scopes or solutions.
- [ ] Cycle IDs and member order are deterministic under input permutation.
- [ ] At least 10 acyclic, cyclic, self-loop, disconnected and permutation cases pass.
- [ ] Quick gate passes.

**Tests**: unit — ≥10 focused cases  
**Gate**: quick  
**Commit**: `feat(package): calculate directed cycles`

### T21: Calculate reverse impact and gap counts

**What**: Traverse incoming indexes for minimum-depth reverse impact and attach separate relevant-gap counts.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Measures/ImpactCalculator.cs`  
**Depends on**: T20  
**Reuses**: incoming adjacency from direct dependencies  
**Requirement**: MET-06, MET-07, MET-08, NAV-09

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Each reachable source appears once with minimum traversal depth.
- [ ] Gap kinds remain separate from confirmed measures and no composite score/quality label exists.
- [ ] At least 12 depth, diamond, cycle, scope, gap and omission cases pass.
- [ ] Quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): calculate reverse impact`

### T22: Assemble the single RetrievalModel

**What**: Combine retained graphs, dependencies, measures and navigation-ready records into the sole renderer input.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/RetrievalModelBuilder.cs`  
**Depends on**: T21  
**Reuses**: outputs of T16-T21 without introducing new factual semantics  
**Requirement**: DEP-07, MET-08, NAV-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Machine and Markdown writers can consume the same immutable model without querying the source graph again.
- [ ] Model ordering is canonical across solution/input permutations.
- [ ] At least 8 assembly, omission and permutation cases pass.
- [ ] Build gate passes.

**Tests**: unit — ≥8 focused cases  
**Gate**: build  
**Commit**: `feat(package): assemble retrieval model`

## Phase 4: Identity, layout and retrieval artifacts

### T23: Generate collision-safe public IDs

**What**: Generate type-prefixed 80-bit SHA-256 base32hex IDs and detect digest collisions before serialization.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Identity/PublicIdRegistry.cs`  
**Depends on**: T22  
**Reuses**: .NET cryptographic primitives; no legacy fact ID grammar  
**Requirement**: STO-01, STO-02

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] IDs match `^[a-z]{3}_[0-9a-v]{16}$`, use unique kind prefixes and exactly the first 80 SHA-256 bits.
- [ ] A forced collision names the ID and both canonical categories without leaking absolute paths.
- [ ] At least 12 vector, grammar, prefix and collision cases pass.
- [ ] Quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): generate compact public ids`

### T24: Build solution-local handle tables

**What**: Deduplicate identities, documents, strings, variants and evidence and assign ordered base36 handles.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Identity/LocalTableBuilder.cs`  
**Depends on**: T23  
**Reuses**: deterministic interning concepts from `InternTable`, with the new per-solution contract  
**Requirement**: STO-03, STO-04, STO-05, VAR-06

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Tables sort by canonical key and assign lowercase base36 ordinals from `0`.
- [ ] Handles resolve directly; overflow above six characters rejects the build.
- [ ] Deduplication never crosses solution boundaries and projection records use handles instead of repeated payload.
- [ ] At least 14 ordering, deduplication, overflow and isolation cases pass; quick gate passes.

**Tests**: unit — ≥14 focused cases  
**Gate**: quick  
**Commit**: `feat(package): build solution local tables`

### T25: Pack deterministic byte-bounded shards

**What**: Pack canonical bulk records into stable ordinal shards with a 64 KiB target and 96 KiB hard ceiling.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Layout/ShardPacker.cs`  
**Depends on**: T24  
**Reuses**: real-byte sizing concept from `LayoutPlanner`, not hash-prefix bucket layout  
**Requirement**: STO-06, STO-07, EDG-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Records sort canonically and every bulk shard respects the hard ceiling.
- [ ] A single oversized record fails as `oversized-record`; normal records are never emitted one-file-per-record.
- [ ] Identical logical inputs produce identical shard boundaries, paths and bytes.
- [ ] At least 12 boundary, oversized, permutation and reproducibility cases pass; quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): pack deterministic shards`

### T26: Build directly resolvable navigation indexes

**What**: Build identity, roots, outgoing, incoming, contract, persistence and evidence/disposition indexes with direct shard locators.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Navigation/NavigationIndexBuilder.cs`  
**Depends on**: T25  
**Reuses**: handles and shard locations from T24-T25  
**Requirement**: NAV-01, NAV-04, NAV-06, NAV-07, NAV-08, NAV-09, NAV-10

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Every supported start key resolves to artifact plus ordinal without directory enumeration, manual shard choice or ID decoding.
- [ ] Index links remain within the same solution and resolve to retained records/evidence.
- [ ] At least 14 direct-resolution, missing-key, scope and solution-isolation cases pass.
- [ ] Quick gate passes.

**Tests**: unit — ≥14 focused cases  
**Gate**: quick  
**Commit**: `feat(package): build navigation indexes`

### T27: Render machine artifacts and root manifest

**What**: Render tables, graph, indexes, dependencies, measures and a single root manifest from the RetrievalModel.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Rendering/MachineArtifactWriter.cs`  
**Depends on**: T26  
**Reuses**: canonical JSON and planned-artifact contracts  
**Requirement**: PKG-01, STO-05, NAV-01, NAV-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] The root manifest lists solutions and all proven roots with readable name, handle, machine citation and Markdown link.
- [ ] All required families and journey entry paths are declared; every manifest path is normalized and relative.
- [ ] At least 12 artifact-layout, manifest-link and canonical-byte cases pass.
- [ ] Quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): render machine artifacts`

### T28: Render equivalent Markdown navigation

**What**: Render summary, Component, Deployment Unit and retained-document pages from the same RetrievalModel.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/Rendering/MarkdownRenderer.cs`  
**Depends on**: T27  
**Reuses**: safe Markdown escaping/linking concepts only; no legacy catalogs/guides/pages  
**Requirement**: NAV-02, NAV-03, NAV-04, NAV-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Summary shows components, Deployment Units, cycles, top fan-in/out and all four journeys.
- [ ] Entity pages show outgoing, incoming, measures, effects and relevant gaps with existing relative links.
- [ ] No page invents Service/Deployment Unit identity or omits an equivalent machine dependency/measure.
- [ ] At least 12 content, escaping, link and equivalence cases pass; quick gate passes.

**Tests**: unit — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(package): render markdown navigation`

### T29: Rehydrate retrieval artifacts and prove equivalence

**What**: Rehydrate the RetrievalModel from machine artifacts, rerender Markdown and compare exact bytes.  
**Where**: `src/Csharp2Md.Core/Publication/RetrievalModelReader.cs`  
**Depends on**: T28  
**Reuses**: manifest-led reading principle and the same Markdown renderer  
**Requirement**: NAV-05, PUB-03, EDG-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Rehydration reads only manifest-declared machine paths and resolves all handles/references.
- [ ] Any machine/Markdown divergence reports package corruption with the offending artifact.
- [ ] At least 10 round-trip, missing-reference and Markdown-mutation cases pass.
- [ ] Quick gate passes.

**Tests**: integration — ≥10 focused cases  
**Gate**: quick  
**Commit**: `feat(publication): verify markdown machine equivalence`

### T30: Build the complete deterministic PackagePlan

**What**: Orchestrate retention, retrieval, tables, renderers, packing, manifest and measurements into all final pre-certification bytes.  
**Where**: `src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs`  
**Depends on**: T29  
**Reuses**: T16-T29 outputs; no deferred fragments, plugin projectors or batch composer  
**Requirement**: PKG-01, PKG-03, PKG-04, PKG-05, PKG-06, PKG-08, STO-04, STO-06, STO-07, CRT-03, EDG-03

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Plan contains all artifact bytes and reserves deterministic certification/measurement artifacts before staging.
- [ ] Measurements separate extraction/publication and filtered reasons by family, journey and corpus.
- [ ] Same graphs/policy under input permutation produce byte-identical plans and digest; package ceilings fail before publication.
- [ ] At least 14 orchestration, determinism, multi-solution and budget cases pass; build gate passes.

**Tests**: integration — ≥14 focused cases  
**Gate**: build  
**Commit**: `feat(package): build complete package plans`

## Phase 5: Validation, certification and atomic publication

### T31: Enforce typed and lexical publication safety

**What**: Validate typed JSON fields and lexically inspect/redact cited C# source without treating comments as paths.  
**Where**: `src/Csharp2Md.Core/Publication/Safety/PublicationSafetyScanner.cs`  
**Depends on**: T30  
**Reuses**: Roslyn C# syntax APIs and deterministic redaction behavior, not arbitrary text token scanning  
**Requirement**: PKG-07, PUB-06, PUB-07, CRT-08

**Tools**: MCP: Context7; Skills: `tlc-spec-driven`, `context7-mcp`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] C# `//` comments/trivia are never classified as UNC paths.
- [ ] Actual absolute paths, escapes and secrets in structured fields/source are deterministically redacted or rejected before commit.
- [ ] Removed values never appear in diagnostics or retained bytes; per-document disposition remains auditable.
- [ ] At least 14 lexical, JSON, path, secret and no-leak cases pass; quick gate passes.

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

- [ ] Reader starts only at root `manifest.json` and opens reachable files in its immutable generation.
- [ ] Rooted, `..`, drive, malformed and symlink-escape paths are rejected before open.
- [ ] Reader holds the shared lock for the complete validation view.
- [ ] At least 14 valid, traversal, symlink, missing and concurrent-reader cases pass; quick gate passes.

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

- [ ] The same reader and validator entry point serves staging and public `validate`.
- [ ] Every corruptible field/family has a mutation test that reports code, stage, family/artifact and cause.
- [ ] Validation never touches source solutions or mutates the package.
- [ ] At least 18 integrity, corruption and diagnostic cases pass; quick gate passes.

**Tests**: integration — ≥18 focused cases  
**Gate**: quick  
**Commit**: `feat(publication): validate package integrity`

### T34: Certify locate and evidence journeys

**What**: Add a measured reader plus Locate and Evidence/Disposition certification with applicability semantics.  
**Where**: `src/Csharp2Md.Core/Publication/Certification/JourneyCertifier.cs`  
**Depends on**: T33  
**Reuses**: direct navigation indexes and package reader only; no directory enumeration  
**Requirement**: NAV-06, NAV-07, NAV-10, CRT-01, CRT-02, EDG-02

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Every open records path, UTF-8 bytes and one read; tokens use `ceil(bytes / 4.0)`.
- [ ] Component locate fits 5 reads; other roots fit 8 reads/12,000 tokens; evidence fits 12 reads/25,000 tokens.
- [ ] Inapplicable journeys record `not_applicable` plus reason and never count as pass.
- [ ] At least 12 applicable, N/A, missing-terminal and over-budget cases pass; quick gate passes.

**Tests**: integration — ≥12 focused cases  
**Gate**: quick  
**Commit**: `feat(certification): certify locate and evidence journeys`

### T35: Certify flow and reverse-impact journeys

**What**: Add causal-flow and reverse-impact traversal certification over declared indexes and terminals.  
**Where**: `src/Csharp2Md.Core/Publication/Certification/GraphJourneyCertifier.cs`  
**Depends on**: T34  
**Reuses**: measured reader and direct indexes from T34/T26  
**Requirement**: NAV-08, NAV-09, CRT-01, CRT-02, EDG-02

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Applicable flow reaches contracts, external effects and persistence in ≤32 reads/125,000 tokens.
- [ ] Reverse impact supports file, project, Component, Deployment Unit, contract and data roots with minimum depths in the same budget.
- [ ] Missing answers or exceeded reads/tokens fail and name journey plus exceeded measure.
- [ ] At least 14 hand-oracle flow, impact, N/A and budget cases pass; quick gate passes.

**Tests**: integration — ≥14 focused cases  
**Gate**: quick  
**Commit**: `feat(certification): certify graph journeys`

### T36: Publish immutable generations atomically

**What**: Materialize, rehydrate, validate, certify and atomically commit one immutable generation under an exclusive lock.  
**Where**: `src/Csharp2Md.Core/Publication/PackagePublication.cs`  
**Depends on**: T35  
**Reuses**: local filesystem retry primitives and existing real-directory failure-test style  
**Requirement**: PUB-01, PUB-02, PUB-04, PUB-05, PUB-08, EDG-02, EDG-03, EDG-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-skills:csharp-concurrency-patterns`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] All planned bytes are staged on the same volume, rehydrated and validated before manifest swap.
- [ ] Certification/measurements are rewritten only in their reserved artifacts and final validation repeats before commit.
- [ ] Move plus root-manifest replacement is the only commit point; failure before it preserves the old package byte-for-byte.
- [ ] Concurrent writer/read behavior and cleanup diagnostics match the approved lock model.
- [ ] At least 18 success, injected-failure, lock, preservation and cleanup cases pass; quick gate passes.

**Tests**: integration — ≥18 focused cases  
**Gate**: quick  
**Commit**: `feat(publication): commit immutable generations`

### T37: Wire KnowledgeEngine analysis and validation

**What**: Connect the facade to Analysis, PackageBuilding and Publication and return committed status only after certification.  
**Where**: `src/Csharp2Md.Core/KnowledgeEngine.cs`  
**Depends on**: T36  
**Reuses**: public contracts from T2 and internal result seams completed in T15/T30/T36  
**Requirement**: PKG-08, PUB-03, PUB-04, PUB-08, VAR-06

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Multiple input solutions produce isolated graphs and one package plan/atomic commit.
- [ ] `Validate` uses the same reader, validator and certifier and never opens a source solution.
- [ ] Success is returned only for a committed/certified package; expected failures are structured and non-successful.
- [ ] At least 12 facade integration, multi-solution, cancellation and failure cases pass; build gate passes.

**Tests**: integration — ≥12 focused cases  
**Gate**: build  
**Commit**: `feat(core): wire knowledge engine workflow`

## Phase 6: CLI acceptance and clean cut

### T38: Wire the analyze CLI command

**What**: Replace analyze wiring with `KnowledgeEngine`, supporting one-or-more solutions, output and explicit test inclusion.  
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`  
**Depends on**: T37  
**Reuses**: current System.CommandLine parsing conventions, not legacy engine/store/projector wiring  
**Requirement**: PKG-01, PKG-08, PUB-04, PUB-08, VAR-06

**Tools**: MCP: Context7; Skills: `tlc-spec-driven`, `context7-mcp`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Analyze rejects invalid inputs before staging and invokes the Core facade exactly once per request.
- [ ] Include-tests policy reaches the manifest/run identity; diagnostics print concise code/stage/cause and applicable coordinates.
- [ ] CLI reports success only after committed certification and non-zero for all rejection classes.
- [ ] At least 12 command-tree, option, multi-solution and exit/diagnostic cases pass; full gate passes.

**Tests**: e2e — ≥12 focused cases  
**Gate**: full  
**Commit**: `feat(cli): wire knowledge package analysis`

### T39: Wire validate and remove compose

**What**: Route validate through `KnowledgeEngine.Validate` and remove the compose command and batch-manifest path.  
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`  
**Depends on**: T38  
**Reuses**: existing in-process CLI test harness  
**Requirement**: PKG-10, PUB-03, PUB-04

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] CLI exposes only current analyze/validate behavior; compose and version/compatibility dispatch are absent.
- [ ] Validate reads only the package, returns the same certification interpretation as pre-commit validation and never mutates it.
- [ ] At least 10 command-surface, offline-source, corruption and exit-code cases pass.
- [ ] Full gate passes.

**Tests**: e2e — ≥10 focused cases  
**Gate**: full  
**Commit**: `feat(cli): validate current package contract`

### T40: Expand the versioned synthetic fixture

**What**: Add one coherent fixture scenario covering per-project multi-targeting, production/test distinction, repeated calls, component dependency, runtime integration, cycle, safety inputs and one controlled gap.  
**Where**: `fixtures/SyntheticSolution`  
**Depends on**: T39  
**Reuses**: existing Acme projects and manual fixture-manifest/oracle style; no new versioned corpus  
**Requirement**: CRT-08, VAR-01, VAR-02, VAR-03, PUB-06, PUB-07

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Fixture contains every CRT-08 characteristic without generated bin/obj or external clone content.
- [ ] A hand-authored manifest/oracle names expected roots, edges, occurrences, cycle, gap and excluded test/safety values.
- [ ] At least 12 fixture-integrity and expected-feature cases pass.
- [ ] Full gate passes.

**Tests**: e2e — ≥12 focused cases  
**Gate**: full  
**Commit**: `test(fixture): cover knowledge package journeys`

### T41: Prove the four journeys end to end

**What**: Analyze the fixture, open from root manifest, complete all four journeys, validate immediately and compare to manual expectations.  
**Where**: `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs`  
**Depends on**: T40  
**Reuses**: `CliInvoke`, temporary output helpers and T40's independent oracle  
**Requirement**: PKG-01, PKG-02, PKG-03, PKG-04, PKG-05, PKG-06, PKG-07, PKG-08, PKG-09, DEP-01, DEP-02, DEP-03, DEP-04, DEP-05, DEP-06, DEP-07, MET-01, MET-02, MET-03, MET-04, MET-05, MET-06, MET-07, MET-08, NAV-01, NAV-02, NAV-03, NAV-04, NAV-05, NAV-06, NAV-07, NAV-08, NAV-09, NAV-10, CRT-01, CRT-02, CRT-03, CRT-09

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Tests start only at `manifest.json`, record files/bytes/reads/tokens and never enumerate package directories to find answers.
- [ ] Expected dependencies and measures are calculated in test code from hand-authored fixture edges, never by production helpers.
- [ ] Standard package excludes every prohibited family/value; immediate CLI validate passes with equivalent interpretation.
- [ ] At least 16 end-to-end answer, equivalence, budget and exclusion cases pass; full gate passes.

**Tests**: e2e — ≥16 focused cases  
**Gate**: full  
**Commit**: `test(cli): certify knowledge package journeys`

### T42: Prove rejection preserves the committed package

**What**: Exercise every specified rejection class from a valid baseline and prove byte-for-byte atomic preservation.  
**Where**: `tests/Csharp2Md.Cli.Tests/KnowledgePackageFailureTests.cs`  
**Depends on**: T41  
**Reuses**: real temporary-directory failure injection patterns from Storage/CLI tests  
**Requirement**: STO-02, PUB-05, PUB-06, PUB-07, PUB-08, EDG-01, EDG-02, EDG-03, EDG-04, EDG-05

**Tools**: MCP: NONE; Skills: `tlc-spec-driven`, `dotnet-test:code-testing-agent`, `dotnet-test:run-tests`.

**Done when**:

- [ ] Variant, retention, safety, size, materialization, rehydration, validation, equivalence and journey-budget failures are injected.
- [ ] Each failure returns the required structured coordinates and leaves root manifest plus prior generation byte-identical.
- [ ] No staging debris is reachable after failure; cancellation also preserves the prior package.
- [ ] At least 16 rejection/preservation cases pass; full gate passes.

**Tests**: e2e — ≥16 focused cases  
**Gate**: full  
**Commit**: `test(cli): prove atomic rejection behavior`

### T43: Certify optional local corpora

**What**: Update LocalCorpus acceptance to assert eShop variant isolation and the eShopOnContainers/Pitstop committed-package ceilings.  
**Where**: `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs`  
**Depends on**: T42  
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

### T44: Complete the clean-cut repository topology

**What**: Remove legacy product/test projects and obsolete contracts, point CLI solely at Core, update solution/package/docs, and prove only the current contract remains.  
**Where**: `csharp2md.slnx`  
**Depends on**: T43  
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
| PKG-01 | T2, T5, T27, T30, T38, T41 | CLI journey package |
| PKG-02 | T3, T12, T16, T41 | CLI roots/closure |
| PKG-03 | T3, T15, T16, T30, T41 | CLI retained set |
| PKG-04 | T3, T17, T30, T41 | CLI relevant gaps |
| PKG-05 | T8, T17, T30, T41 | CLI exclusions |
| PKG-06 | T8, T17, T30, T41 | CLI cited sources |
| PKG-07 | T3, T7, T8, T14, T31, T41 | CLI safety/config |
| PKG-08 | T5, T8, T17, T30, T37, T38, T41 | CLI policy identity |
| PKG-09 | T3, T12-T16, T41 | factual graph + CLI |
| PKG-10 | T1, T39, T44 | topology surface |
| DEP-01..DEP-08 | T4, T12-T13, T18, T22, T41 | hand-recalculated dependencies |
| MET-01..MET-04 | T4, T19, T41 | hand-recalculated direct measures |
| MET-05 | T4, T20, T41 | hand-recalculated SCCs |
| MET-06..MET-08 | T4, T17, T21-T22, T41 | hand-recalculated impact/gaps |
| NAV-01 | T26-T27, T32, T41 | manifest/index traversal |
| NAV-02..NAV-05 | T22, T27-T29, T41 | machine/Markdown equivalence |
| NAV-06..NAV-07 | T26, T34, T41 | locate budgets |
| NAV-08..NAV-09 | T26, T35, T41 | graph journey budgets |
| NAV-10 | T26, T34, T41 | evidence budget |
| VAR-01..VAR-02 | T9-T10, T40 | real workspace fixture |
| VAR-03..VAR-05 | T3, T7, T11, T40 | occurrence/collision tests |
| VAR-06 | T7, T11, T15, T24, T37-T38 | multi-solution CLI |
| STO-01..STO-02 | T23, T33, T42 | vectors/collision/rejection |
| STO-03..STO-05 | T5, T24, T27, T33 | tables and resolver |
| STO-06..STO-07 | T6, T25, T30, T33 | byte/shard determinism |
| PUB-01 | T5-T6, T36 | single materialization path |
| PUB-02 | T5, T32-T33, T36 | staged rehydration |
| PUB-03..PUB-04 | T2, T29, T32-T33, T37-T39 | shared public validation |
| PUB-05 | T32-T33, T36, T42 | byte preservation |
| PUB-06..PUB-07 | T31, T40, T42 | safety fixture/rejection |
| PUB-08 | T2, T15, T36-T38, T42 | structured diagnostics |
| CRT-01..CRT-03 | T15, T30, T34-T35, T41 | journey certification |
| CRT-04..CRT-07 | T9, T43 | optional corpora |
| CRT-08 | T8, T12-T14, T31, T40 | fixture integrity |
| CRT-09 | T41 | CLI E2E |
| EDG-01 | T13, T16-T17, T33, T42 | evidence rejection |
| EDG-02 | T34-T36, T42 | journey budget rejection |
| EDG-03 | T25, T30, T36, T42 | package budget rejection |
| EDG-04 | T11, T13, T18, T42 | variant-qualified dependency |
| EDG-05 | T29, T33, T36, T42 | representation divergence |

All 71 requirements have at least one focused owning task and a final acceptance seam.

## Task Granularity Check

| Tasks | Deliverable boundary | Status |
| ----- | -------------------- | ------ |
| T1 | Repository scaffold | ✅ Cohesive build topology |
| T2-T7 | One facade/model/serializer/primitive component each | ✅ Granular |
| T8-T15 | One inventory/planner/workspace/accumulator/extractor/orchestrator component each | ✅ Granular |
| T16-T22 | One retention/measure/model-builder component each | ✅ Granular |
| T23-T30 | One identity/layout/index/renderer/reader/orchestrator component each | ✅ Granular |
| T31-T37 | One safety/reader/validator/certifier/publisher/facade component each | ✅ Granular |
| T38-T39 | One CLI command surface per task | ✅ Granular |
| T40 | One coherent fixture scenario | ✅ Cohesive fixture deliverable |
| T41-T43 | One acceptance concern per task | ✅ Granular |
| T44 | One final repository topology cutover | ✅ Cohesive clean-cut deliverable |

T1, T40 and T44 necessarily touch multiple physical files, but each is one indivisible deliverable. Splitting any of them would create an invalid scaffold, a fixture with no stable oracle, or a repository with mixed contracts.

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
| T38 | T37 | phase 6 after phase 5 | ✅ Match |
| T39 | T38 | T38 -> T39 | ✅ Match |
| T40 | T39 | T39 -> T40 | ✅ Match |
| T41 | T40 | T40 -> T41 | ✅ Match |
| T42 | T41 | T41 -> T42 | ✅ Match |
| T43 | T42 | T42 -> T43 | ✅ Match |
| T44 | T43 | T43 -> T44 | ✅ Match |

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
| T37 | Facade workflow | integration | integration | ✅ OK |
| T38 | Analyze CLI | e2e | e2e | ✅ OK |
| T39 | Validate CLI | e2e | e2e | ✅ OK |
| T40 | Versioned fixture | e2e | e2e | ✅ OK |
| T41 | Journey acceptance | e2e | e2e | ✅ OK |
| T42 | Failure acceptance | e2e | e2e | ✅ OK |
| T43 | Optional corpora | e2e | e2e | ✅ OK |
| T44 | Topology + CLI current contract | unit + e2e + build | unit + e2e + build | ✅ OK |

No task defers its required tests to a later task. Later E2E tests add acceptance coverage; they do not substitute for the focused tests committed with the component that they exercise.

## Tool Confirmation Before Execute

Proposed task tooling:

- MCP: Context7 only for Roslyn (`T9`, `T10`, `T31`) and System.CommandLine (`T38`) API verification; none for the remaining tasks.
- Skills: `tlc-spec-driven` throughout; `dotnet-test:code-testing-agent` for test authoring; `dotnet-test:run-tests` for every gate; `dotnet-skills:serialization` for canonical JSON/package readers; `dotnet-skills:csharp-concurrency-patterns` for publication locking; `dotnet-skills:api-design` for the public facade.

The task list was approved by the user on 2026-09-15. Confirm the proposed tool choices and the sequential batch/sub-agent strategy when Execute begins.
