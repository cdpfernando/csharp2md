# csharp2md v3: Factual Model and Semantic Analysis Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** The skill owns the per-task test gate, atomic commit, requirement traceability, adequacy review, independent Verifier, discrimination sensor, and lessons distillation.

**If the skill cannot be activated, STOP and tell the user.** Do not implement this plan without it.

---

**Design**: `.specs/features/csharp2md-v3/design.md`
**Status**: Approved (user, 2026-08-17)
**Branch**: `feat/csharp2md-v3` at the verified Phase 1 head; never push, open/update a PR, merge, tag, or publish without separate user authorization.

---

## Test Coverage Matrix

> Generated from codebase, project guidelines, and spec - confirm before Execute. Guidelines found: `AGENTS.md`, `Directory.Build.props`, `Directory.Packages.props`, and the v3 specification/testing strategy. No CI workflow, contributor guide, or numerical coverage threshold is present, so the strong default applies for depth.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Factual domain, identities, validation, and composition | unit | All branches; 1:1 to FACT-08..17 and FACT-50; every listed invalid state and identity edge case | `tests/Csharp2Md.Core.Tests/Facts/**/*Tests.cs` | `dotnet test csharp2md.slnx --filter "Category!=Integration"` |
| Syntax extraction and Markdown projection | unit | Every language shape in FACT-57; exact source-span partition and byte reconstruction; annotations outside source spans | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/**/*Tests.cs`, `tests/Csharp2Md.Core.Tests/Projection/Markdown/**/*Tests.cs` | `dotnet test csharp2md.slnx --filter "Category!=Integration"` |
| MSBuild, Roslyn, process, and generator adapters | integration | Healthy + every specified failure mode; no-target, no-extension, timeout, process-tree, per-TFM, and analyzer-sanitation boundaries | `tests/Csharp2Md.Core.Tests/Analysis/Semantics/**/*Tests.cs` with `[Trait("Category", "Integration")]` | `dotnet test csharp2md.slnx` |
| Detectors, indexes, and component classification | unit | 1:1 to FACT-37..49 and FACT-66..68; positive, negative, and lookalike cases per detector rule | `tests/Csharp2Md.Core.Tests/Detection/**/*Tests.cs` | `dotnet test csharp2md.slnx --filter "Category!=Integration"` |
| Fact storage and canonical aggregate output | integration | Artifact set, atomic persistence, hashes, schema sync, referential integrity, canonical ordering, failure paths, and bounded aggregation | `tests/Csharp2Md.Core.Tests/Facts/Storage/**/*Tests.cs`, `tests/Csharp2Md.Core.Tests/Projection/Aggregates/**/*Tests.cs` | `dotnet test csharp2md.slnx` |
| Analysis engine and migration cuts | integration | Full runs through `AnalysisEngine`; sequential scope lifecycle, syntax fallback, scoped degradation, coverage, diagnostics, and no weakened Phase 1 behavior | `tests/Csharp2Md.Core.Tests/Analysis/**/*Tests.cs` with `[Trait("Category", "Integration")]` | `dotnet test csharp2md.slnx` |
| CLI and package | integration | Happy path plus every invalid option/trust/output/failure path, real exit codes, packaged execution, and version contract | `tests/Csharp2Md.Core.Tests/Cli/**/*Tests.cs` | `dotnet test csharp2md.slnx` |
| Published JSON/frontmatter contracts | unit + snapshot | Record/schema synchronization, exact representative shapes, schema version, and canonical serialized bytes | `tests/Csharp2Md.Core.Tests/Facts/Schemas/**/*Tests.cs`, `tests/Csharp2Md.Core.Tests/Topic/**/*Tests.cs` | `dotnet test csharp2md.slnx --filter "Category!=Integration"` |
| Fixtures and specification artifacts | none | Build gate for inert files; behavior is asserted by the consuming adapter, detector, engine, or CLI task | `fixtures/`, `.specs/features/csharp2md-v3/` | build gate only |

Provenance: xUnit 2.9.3 on VSTest was detected from `tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj`, `Directory.Packages.props`, and `global.json` (SDK 10 with no native-MTP runner selection). Tests mirror source folders, integration tests use `[Trait("Category", "Integration")]`, Verify snapshots live under adjacent `snapshots/` directories, nullable analysis and warnings-as-errors are enabled, and the current verified baseline is **428 passing tests, 0 failing**. Existing samples included span coverage, pipeline invariants, cross-run determinism, CLI end-to-end behavior, semantic loading, detector behavior, and schema synchronization.

## Gate Check Commands

> Generated from the repository and the mandatory `dotnet-test:run-tests` skill - confirm before Execute.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks whose production surface and tests are unit-only | `dotnet test csharp2md.slnx --filter "Category!=Integration"` |
| Full | After adapter, storage, engine, output, CLI, fixture, or migration tasks | `dotnet test csharp2md.slnx` |
| Build | After each phase and for release/configuration tasks | `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx` |

Every task records the discovered test count in `tasks.md` before its commit. The count must be at least the preceding committed count plus the task's minimum named new cases; existing tests may be replaced only when their behavioral assertion is mapped in the migration ledger and preserved at equal or stronger depth.

## Knowledge Verification (binding)

Roslyn and MSBuild calls follow the repository's retrieval-led chain: existing code -> project docs and AD-003 -> installed XML/API documentation -> official Microsoft/Roslyn documentation -> explicitly flagged uncertainty. No Roslyn member signature may be invented. Increment 0 resolves the production semantic adapter before later semantic tasks begin.

Authoring uses `dotnet-skills:modern-csharp-coding-standards`; public-contract work also uses `dotnet-skills:api-design` and `dotnet-skills:csharp-nullable-reference-types`. Test execution uses `dotnet-test:run-tests`; snapshot tasks use `dotnet-skills:snapshot-testing`. The pre-Verifier release task uses `dotnet-test:test-gap-analysis`, `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns`, and `dotnet-skills:dotnet-slopwatch`.

---

## Execution Plan

Phases and tasks run strictly in order. A later phase cannot start until the preceding phase's build gate and atomic commits are complete.

### Phase 0: Semantic viability gate

```
T1 -> T2 -> T3 -> T4 -> T5
```

### Phase 1: Factual kernel

```
T5 -> T6 -> T7 -> T8 -> T9 -> T10 -> T11 -> T12
```

### Phase 2: Safe syntax-only vertical cut

```
T12 -> T13 -> T14 -> T15 -> T16 -> T17 -> T18 -> T19 -> T20 -> T21
```

### Phase 3: Trusted semantic enrichment

```
T21 -> T22 -> T23 -> T24 -> T25 -> T26 -> T27 -> T28 -> T29
```

### Phase 4: Reusable analysis infrastructure

```
T29 -> T30 -> T31 -> T32 -> T33
```

### Phase 5: Priority factual detectors

```
T33 -> T34 -> T35 -> T36 -> T37 -> T38 -> T39
```

### Phase 6: Aggregation, migration, and release closure

```
T39 -> T40 -> T41 -> T42 -> T43 -> T44 -> T45 -> T46 -> T47
```

---

## Task Breakdown

### T1: Create the Phase 1 behavior-migration ledger

**What**: Inventory all 428 baseline tests and map each asserted behavior to a v3 requirement, its replacement test surface, or an explicit retained invariant.
**Where**: `.specs/features/csharp2md-v3/test-migration.md`
**Depends on**: None
**Reuses**: Existing test names, the v3 requirement mapping, and the design's migration policy.
**Requirement**: FACT-55

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-test:assertion-quality`

**Done when**:

- [x] Every baseline test is listed exactly once with a v3 disposition and requirement or retained-invariant reference.
- [x] The ledger rejects deletion without an equal-or-stronger replacement assertion.
- [x] Baseline gate records 428 passing tests and no unexpected working-tree changes.

**Completed evidence (2026-08-17)**: `test-migration.md` contains 428 distinct ledger rows; `dotnet test csharp2md.slnx` passed 428 tests with 0 failed and 0 skipped before and after the documentation-only change.

**Tests**: none - specification artifact; existing baseline is executed
**Gate**: full
**Commit**: `docs(v3): map baseline behavior to factual requirements`

### T2: Prove target-free MSBuild property and item evaluation

**What**: Add an isolated evaluation probe that uses property/item queries for healthy, multi-target, imported, missing-SDK, incomplete-restore, and invalid-reference projects and proves no custom target executes.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/Viability/`
**Depends on**: T1
**Reuses**: `ProcessRunner`, synthetic fixtures, AD-003, and official MSBuild query contracts.
**Requirement**: FACT-26, FACT-27, FACT-28, FACT-29

**Tools**:

- MCP: official web documentation
- Skills: `tlc-spec-driven`, `dotnet-test:run-tests`

**Done when**:

- [x] Arguments are passed through `ProcessStartInfo.ArgumentList` with no target, restore, build, publish, or target-result switch.
- [x] Multi-target values stay separate and preprocessing retains import paths only.
- [x] At least six new integration cases prove successful and degraded query outcomes plus absence of the target marker.
- [x] Full gate passes and the discovered count is at least the prior baseline plus six.

**Completed evidence (2026-08-17)**: seven integration probes cover healthy, multi-target, imported, missing-SDK, incomplete-restore, invalid-reference, and inert extension inventory cases; `dotnet test csharp2md.slnx` passed 435 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: full
**Commit**: `test(v3): prove target-free msbuild evaluation`

### T3: Prove timeout and complete process-tree termination

**What**: Add a child-spawning evaluator probe that enforces a per-service timeout and verifies timeout or cancellation terminates the complete descendant tree.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/Viability/ProcessTreeProbeTests.cs`
**Depends on**: T2
**Reuses**: `ProcessRunner` and `Process.Kill(entireProcessTree: true)` official contract.
**Requirement**: FACT-28, FACT-62, FACT-63

**Tools**:

- MCP: official web documentation
- Skills: `tlc-spec-driven`, `dotnet-test:run-tests`

**Done when**:

- [x] Timeout is scoped to one service and cancellation remains caller cancellation.
- [x] Both timeout and cancellation cases prove the child and descendant processes are gone before control returns.
- [x] At least two new integration cases pass without leaving marker processes or files.
- [x] Full gate passes and the discovered count is at least the prior count plus two.

**Completed evidence (2026-08-17)**: timeout and caller-cancellation probes terminate recorded parent/descendant PIDs before return and leave no completion marker; `dotnet test csharp2md.slnx` passed 437 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: full
**Commit**: `test(v3): prove evaluator process-tree termination`

### T4: Prove Roslyn compilation sanitation before binding

**What**: Probe the Roslyn 5.6 workspace and compilation paths with marker analyzers and generators, replacing analyzer references before the first compilation request and recording whether the workspace path meets the security contract.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/Viability/RoslynSanitationProbeTests.cs`
**Depends on**: T3
**Reuses**: Installed Roslyn XML documentation, AD-003, and existing `SolutionLoader` fixtures without reusing its unsafe call order.
**Requirement**: FACT-30, FACT-33, FACT-34, FACT-64

**Tools**:

- MCP: official Microsoft and Roslyn documentation
- Skills: `tlc-spec-driven`, `dotnet-test:run-tests`

**Done when**:

- [x] Probe evidence states whether opening/evaluating a workspace runs forbidden targets or extensions.
- [x] Analyzer references are absent before compilation and no marker analyzer or generator executes.
- [x] Separate target compilations retain distinct target identities.
- [x] At least three new integration cases pass; full gate passes with no count decrease.

**Completed evidence (2026-08-17)**: opening a Roslyn 5.6 `MSBuildWorkspace` executed the custom `BeforeTargets=\"Compile\"` marker target but did not load marker extensions. Sanitizing with `Project.WithAnalyzerReferences([])` before `GetCompilationAsync` prevented analyzer/generator loading, retained scoped failure syntax, and preserved distinct net9/net10 identities. `dotnet test csharp2md.slnx` passed 441 tests with 0 failed and 0 skipped. This evidence rejects the workspace adapter under the approved no-target contract.

**Tests**: integration
**Gate**: full
**Commit**: `test(v3): prove sanitized roslyn compilation`

### T5: Select and record the semantic compilation strategy

**What**: Prove generator-only opt-in behavior, measure the viability cases, and amend the design with the evidence-backed workspace, evaluated-compilation, or stop-and-revise decision.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/Viability/GeneratorIsolationProbeTests.cs`
**Depends on**: T4
**Reuses**: T2-T4 probes and the design's Increment-0 decision gate.
**Requirement**: FACT-26, FACT-30, FACT-31, FACT-64

**Tools**:

- MCP: official Microsoft and Roslyn documentation
- Skills: `tlc-spec-driven`, `dotnet-test:run-tests`

**Done when**:

- [x] Explicit opt-in executes generators while diagnostic analyzers remain absent; disabled mode loads neither.
- [x] Elapsed time, peak working set, process count, and output volume are recorded without introducing an SLA.
- [x] `design.md` and the decision log are amended only if the evidence selects or changes a project-level constraint.
- [x] At least two new integration cases and the phase Build gate pass; if no safe backend exists, execution stops with evidence.

**Completed evidence (2026-08-17)**: four integration probes cover disabled, untrusted opt-in rejection, trusted generator-only execution, and repository-source measurement. The design selects evaluated per-TFM compilation because T4 proved workspace target execution. `.specs/STATE.md` remains unchanged under the explicit batch-worker instruction; the orchestrator owns the corresponding decision-log update. Release build and formatting verification passed, then `dotnet test csharp2md.slnx` passed 445 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: build
**Commit**: `docs(v3): select the proven semantic backend`

### T6: Define analysis request, result, mode, and trust contracts

**What**: Create the sole external `AnalysisRequest` and `AnalysisResult` surface with defaults and validation for trust, generator opt-in, and positive service timeout.
**Where**: `src/Csharp2Md.Core/Analysis/Contracts/`
**Depends on**: T5
**Reuses**: `TopicOptions`, manifest/directory input semantics, and existing typed CLI failure patterns.
**Requirement**: FACT-01, FACT-04, FACT-05, FACT-06, FACT-07, FACT-08

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:api-design`, `dotnet-skills:csharp-nullable-reference-types`

**Done when**:

- [x] Omitted options produce syntax-only, untrusted, and ten-minute-per-service defaults.
- [x] Every illegal combination returns a typed validation failure before any side-effecting collaborator can run.
- [x] At least eight new unit cases cover defaults, valid combinations, and every invalid option edge.
- [x] Quick gate passes and the discovered count is at least the prior count plus eight.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(v3): define analysis request and result contracts`

### T7: Create stable factual identity value types

**What**: Implement dedicated canonical IDs for projects, targets, documents, symbols, components, relations, diagnostics, detectors, and persisted artifact references.
**Where**: `src/Csharp2Md.Core/Facts/Identity/`
**Depends on**: T6
**Reuses**: `ProjectIdentityReader` normalization behavior and repository value-type conventions.
**Requirement**: FACT-09, FACT-10

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:type-design-performance`

**Done when**:

- [x] IDs reject absolute and non-normalized paths and never include the analysis root or span start.
- [x] Resolved and syntactic symbol ID grammars follow the normative rules and remain stable after unrelated preceding edits.
- [x] At least twelve new unit cases cover relocation, separators, case policy, long IDs, collisions, fallback signatures, and invalid inputs.
- [x] Quick gate passes with no discovered-test decrease.

**Completed evidence (2026-08-17)**: dedicated ID value types implement the ordered `id1` grammar, uppercase UTF-8 percent escapes, ordinal case/Unicode behavior, location-free semantic and syntactic symbol signatures, one-based relation occurrences, reverse-DNS detector IDs, and SHA-256 artifact references with explicit collision detection. The quick gate passed 396 tests with 0 failed and 0 skipped, up from 366 at T6.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(facts): add stable identity value types`

### T8: Define evidence, provenance, and diagnostic values

**What**: Add immutable versioned provenance, relative line-range evidence, structured diagnostic, stage, severity, and scope values.
**Where**: `src/Csharp2Md.Core/Facts/Metadata/`
**Depends on**: T7
**Reuses**: `SourceLocation` intent while replacing its unvalidated shape.
**Requirement**: FACT-09, FACT-14, FACT-15, FACT-69

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:csharp-nullable-reference-types`

**Done when**:

- [x] Evidence is relative, ordered, in-range-capable, and tied to a document ID.
- [x] Provenance distinguishes engine and optional detector ID/version; diagnostics are deterministic and scope-addressable.
- [x] At least eight new unit cases cover valid and invalid ranges, absolute paths, empty versions, and canonical ordering.
- [x] Quick gate passes with no discovered-test decrease.

**Completed evidence (2026-08-17)**: immutable evidence validates normalized relative paths, one-based ordered ranges, document identity, and canonical comparison. Provenance enforces engine identity/version and paired optional detector identity/version. Structured diagnostics derive stable IDs from scope, stage, code, message, and sorted data while canonically ordering data/evidence. The quick gate passed 417 tests with 0 failed and 0 skipped.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(facts): add evidence provenance and diagnostics`

### T9: Define factual families and resolution algebra

**What**: Create immutable solution, project, target, document, section, symbol, component, relation, and coverage facts plus the `FactResolution` aggregation rules.
**Where**: `src/Csharp2Md.Core/Facts/Model/`
**Depends on**: T8
**Reuses**: Existing manifest, graph, render-section, and load-report concepts without retaining Roslyn or presentation types.
**Requirement**: FACT-09, FACT-13, FACT-17, FACT-50, FACT-69

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:type-design-performance`

**Done when**:

- [x] Factual records contain no Roslyn, filesystem, YAML, Markdown, or CLI types.
- [x] Resolution algebra implements exact, partial, syntactic, unresolved, and not-applicable document aggregation exactly as specified.
- [x] Runtime relations represent a nullable target and unresolved reason; configuration resolution is a distinct type.
- [x] At least twelve new unit cases cover every resolution combination and fact-family invariant; quick gate passes.

**Completed evidence (2026-08-17)**: immutable headers and specialized solution, project, target, document, section, symbol, component, relation, and coverage records depend only on factual-domain values. The document-resolution algebra covers every specified homogeneous, not-applicable, and mixed-quality outcome. Runtime relations preserve nullable targets/reasons, error-symbol participation remains explicit, and configuration resolution is a separate enum. The quick gate passed 443 tests with 0 failed and 0 skipped.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(facts): define factual model and resolution algebra`

### T10: Define versioned factual detector contracts

**What**: Add project- and document-granularity detector contracts, descriptors, contexts, and results that return facts and diagnostics without side effects.
**Where**: `src/Csharp2Md.Core/Detection/Contracts/`
**Depends on**: T9
**Reuses**: AD-004's compiled-interface boundary and existing detector granularity split.
**Requirement**: FACT-15, FACT-49

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:api-design`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Every descriptor requires stable ID, version, supported levels, and supported fact kinds.
- [x] Detector results cannot smuggle presentation or persistence side effects.
- [x] At least six new unit cases cover descriptor validation, granularity, supported levels, and canonical result ordering.
- [x] Quick gate passes with no discovered-test decrease.

**Completed evidence (2026-08-17)**: internal project/document detector interfaces retain AD-004 granularity without enlarging the external API. Descriptors require stable detector identity, independent version, non-empty supported levels, and fact kinds. Contexts/results expose factual-domain values only, and result facts/diagnostics are immutable and canonical. The quick gate passed 452 tests with 0 failed and 0 skipped.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(detection): define factual detector contracts`

### T11: Validate fragment and relation invariants

**What**: Implement pure fragment and bounded aggregate validation for identity, evidence, reference, resolution, provenance, relation-kind, and unresolved-target rules.
**Where**: `src/Csharp2Md.Core/Facts/Validation/`
**Depends on**: T10
**Reuses**: Existing frontmatter validation result style and factual constructors.
**Requirement**: FACT-11, FACT-12, FACT-13, FACT-14, FACT-15, FACT-16, FACT-17

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Every FACT-12..17 rejection rule produces a deterministic diagnostic naming the fact and rule.
- [x] Invalid facts/fragments cannot be represented as validated values or passed to storage/projection.
- [x] At least fourteen new unit cases cover each invalid rule and corresponding valid lookalike.
- [x] Quick gate passes and the discovered count is at least the prior count plus fourteen.

**Completed evidence (2026-08-17)**: pure fragment validation checks duplicate identities, bounded missing references, exact error-symbol facts, document/path/range evidence, runtime provenance/evidence, compile-time-only reference kinds, and unresolved targets. Each rule yields a deterministic `C2M-FV-*` diagnostic naming the fact and rule; invalid results expose no `ValidatedFactFragment`. Twenty-two new discovered cases include every invalid rule and valid lookalike. The quick gate passed 474 tests with 0 failed and 0 skipped.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(facts): validate factual invariants`

### T12: Add canonical factual JSON contracts

**What**: Create source-generated JSON contexts and schema-sync tests for every factual family using explicit ordering, schema version 2, UTF-8/LF, and no timestamps or absolute paths.
**Where**: `src/Csharp2Md.Core/Facts/Serialization/`
**Depends on**: T11
**Reuses**: `ManifestJsonContext` and frontmatter schema synchronization patterns.
**Requirement**: FACT-09, FACT-19, FACT-56, FACT-70

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-skills:snapshot-testing`

**Done when**:

- [x] Every supported fact category round-trips through source-generated serialization.
- [x] Schemas and record fields/enums fail tests when they drift; representative bytes are approved from spec-defined expectations.
- [x] At least eight new unit/snapshot cases cover families, ordering, line endings, absent timestamps, and schema version.
- [x] Phase Build gate passes with no discovered-test decrease.

**Completed evidence (2026-08-17)**: schema-version-2 wire records cover every factual family, diagnostics, coverage, provenance, and evidence through the source-generated `FactualJsonContext`. Serialization canonicalizes family ordering, emits UTF-8/LF without BOM/timestamps/null fields, round-trips every category, and rejects other schema versions. `facts.schema.json` record/enum synchronization and a spec-authored representative Verify snapshot guard drift. Release build and repository formatting passed, then the full suite passed 589 tests with 0 failed and 0 skipped.

**Tests**: unit + snapshot
**Gate**: build
**Commit**: `feat(facts): serialize schema-version-two fragments`

### T13: Build inert source and project inventory

**What**: Discover services, solutions, projects, eligible source/configuration files, declared imports, and analyzer/generator paths without constructing or invoking any executable analysis adapter.
**Where**: `src/Csharp2Md.Core/Analysis/Inventory/`
**Depends on**: T12
**Reuses**: `ManifestLoader`, `ServiceDiscoverer`, `ProjectIdentityReader`, XML/configuration parsing, and exclusion rules.
**Requirement**: FACT-02, FACT-03, FACT-29, FACT-53

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] A recording adapter proves syntax inventory creates no process, workspace, compilation, analyzer, generator, or plugin.
- [x] Broken/unrestored projects still inventory every eligible source file and declared extension path.
- [x] Catalog ordering and all paths are canonical and root-relative.
- [x] At least eight new integration cases pass; full gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: `InertInventory` resolves directory or manifest inputs through inert filesystem/XML reads, inventories broken and unrestored projects, source/configuration files, literal and unevaluated imports, and declared analyzer/generator paths. The recording execution observer received zero executable-adapter invocations. Eight new integration cases also prove exclusions plus ordinal root-relative catalog/path ordering. The full gate passed 597 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: full
**Commit**: `feat(analysis): add inert project inventory`

### T14: Extract source-faithful section facts

**What**: Move the contiguous Roslyn syntax partition into a syntax extractor that produces document and source-section facts retaining every source byte exactly once.
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SourceSectionExtractor.cs`
**Depends on**: T13
**Reuses**: `MarkdownRenderer` span partition and `XmlDocProse` behavior.
**Requirement**: FACT-03, FACT-21, FACT-57

**Tools**:

- MCP: official Roslyn documentation for any unproven syntax API
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Section spans are contiguous from zero to source length and concatenate to the original source exactly.
- [x] Usings, namespaces, directives, regions, comments, top-level statements, nested types, and trailing trivia are retained.
- [x] At least twelve new unit/theory cases cover the language and byte-fidelity matrix.
- [x] Quick gate passes with no discovered-test decrease.

**Completed evidence (2026-08-17)**: `SourceSectionExtractor` parses source without semantic binding and emits syntactic document/section facts whose positive spans form the renderer-proven contiguous partition. Twelve theory rows cover empty/source, usings, block/file namespaces, directives, regions, comments, top-level statements, nested types, trailing trivia, Unicode, and error-bearing source; focused extraction adds structural-kind and stable-local-ordinal checks. The quick gate passed 513 tests with 0 failed and 0 skipped.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(syntax): extract source-faithful section facts`

### T15: Extract syntactic declarations and stable symbols

**What**: Emit declaration and syntactic symbol facts for namespaces, types, members, signatures, XML prose, and syntactic relation candidates without semantic binding.
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T14
**Reuses**: Existing renderer/detector syntax walks and the T7 canonical signature grammar.
**Requirement**: FACT-03, FACT-10, FACT-21, FACT-57

**Tools**:

- MCP: official Roslyn documentation for every syntax member without repository precedent
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Overloads, generics, records, interfaces, overrides, conditional compilation, and error-bearing source have spec-defined syntactic facts.
- [x] IDs survive root relocation and unrelated insertion before declarations.
- [x] At least fourteen new unit/theory cases cover positive shapes and lookalikes.
- [x] Quick gate passes with no discovered-test decrease.

**Completed evidence (2026-08-17)**: `SyntaxFactExtractor` emits canonically ordered syntactic symbol facts, document symbol references, XML prose, referenced types, attributes, and unresolved base/interface candidates without semantic binding. Location-free identities use trivia-free declaration headers plus lexical containment and ignore bodies, roots, and preceding declaration offsets. Fourteen shape rows plus four focused cases cover overloads, generics, records, interfaces, overrides, members, conditional compilation, errors, stability, relations, and valid lookalikes. The quick gate passed 531 tests with 0 failed and 0 skipped. Previously unproven Roslyn `DescendantTokens`, `Ancestors`, `WithoutTrivia`, and `NormalizeWhitespace` contracts were verified against official Microsoft API documentation before use.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(syntax): extract declaration and symbol facts`

### T16: Persist validated fragments atomically

**What**: Implement path-safe artifact mapping, canonical serialization, content hashing, output-local temporary writes, atomic rename, and stable artifact references for validated fragments.
**Where**: `src/Csharp2Md.Core/Facts/Storage/`
**Depends on**: T15
**Reuses**: `OutputWriter` safety concepts and T12 canonical serializers.
**Requirement**: FACT-11, FACT-18, FACT-19, FACT-23

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:serialization`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Only validated fragments can be written and returned references resolve inside `raw/facts/`.
- [x] Long IDs and hash collisions map deterministically without portable-path violations.
- [x] Hashes cover exact written bytes and failed writes leave no claimed or partial artifact.
- [x] At least eight new integration cases pass; full gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: `FactStore` accepts only `ValidatedFactFragment`, maps it to the source-generated schema-version-2 contract, serializes canonical UTF-8/LF bytes, hashes the exact payload, writes through an output-local temporary file, and renames into the SHA-256 `raw/facts/` artifact path. Fixed-length long-ID paths, deterministic rewrites/order, injected move failure cleanup, and an injected distinct-ID reference collision are covered. Eight new integration cases passed; the full gate passed 637 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: full
**Commit**: `feat(storage): persist validated fact fragments atomically`

### T17: Project Markdown exclusively from document facts

**What**: Add a pure Markdown projector that accepts only a validated document fragment and reconstructs structural code sections plus factual annotations outside source spans.
**Where**: `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs`
**Depends on**: T16
**Reuses**: Existing Markdown titles/fences and XML prose rendering, not its syntax-tree input shape.
**Requirement**: FACT-20, FACT-21, FACT-56

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:snapshot-testing`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] The projector has no Roslyn, detector, compilation, semantic-model, or filesystem input.
- [x] Every section payload remains verbatim and annotations cannot consume or alter source spans.
- [x] At least eight unit/snapshot cases cover representative documents, fences, prose, empty files, and annotations.
- [x] Quick gate passes with no discovered-test decrease.

**Completed evidence (2026-08-17)**: the pure `MarkdownProjector` accepts only a validated factual fragment, rejects broken partitions, reconstructs ordered source-section payloads verbatim, selects safe dynamic fences, and renders symbol/diagnostic annotations before all code spans. Eight unit/snapshot cases cover representative structure, empty files, missing trailing newline, embedded fences, annotations, partition failure, deterministic headings, and the reviewed Markdown snapshot. The quick gate passed 539 tests with 0 failed and 0 skipped.

**Tests**: unit + snapshot
**Gate**: quick
**Commit**: `feat(projection): render markdown from validated facts`

### T18: Emit schema-version-two frontmatter

**What**: Replace the v2 frontmatter model and YAML projection with the compact document-local v2 schema and a resolvable `facts_ref`.
**Where**: `src/Csharp2Md.Core/Projection/Markdown/FrontmatterV2.cs`
**Depends on**: T17
**Reuses**: `FrontmatterYaml` escaping and existing schema-sync tests.
**Requirement**: FACT-22, FACT-56, FACT-70

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:snapshot-testing`, `dotnet-skills:serialization`

**Done when**:

- [x] Only the specified schema-version-2 fields are emitted; topic, domain, generator version, timestamps, and absolute paths are absent.
- [x] `facts_ref` resolves to the exact stored document fragment and classifications/diagnostics are summarized deterministically.
- [x] At least six new unit/snapshot cases and the schema-sync test pass.
- [x] Quick gate passes with no discovered-test decrease.

**Completed evidence (2026-08-17)**: `FrontmatterV2` emits only schema/document/project/component/classification/analysis/diagnostic/reference fields, requires the stored root to match the document, and canonicalizes component IDs, classifications, and diagnostic summaries. The dedicated published schema forbids extra properties and synchronizes required record fields; a reviewed YAML snapshot fixes field order and representative values. Six new unit/snapshot cases passed; the quick gate passed 545 tests with 0 failed and 0 skipped.

**Tests**: unit + snapshot
**Gate**: quick
**Commit**: `feat(projection): emit frontmatter schema version two`

### T19: Write canonical aggregate and manifest skeletons

**What**: Create canonical output preparation plus deterministic empty/summary outputs for the factual manifest, solutions, projects, symbols, relation partitions, diagnostics, coverage, indexes, Mermaid, topic scaffold, and audit log.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/`
**Depends on**: T18
**Reuses**: `OutputWriter`, `TopicLayout`, `IndexWriter`, `MermaidWriter`, `TopicScaffoldWriter`, and `RunLogWriter` safety/time seams.
**Requirement**: FACT-18, FACT-23, FACT-25, FACT-51, FACT-52, FACT-54

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:snapshot-testing`, `dotnet-skills:serialization`

**Done when**:

- [x] The exact v3 tree is written, the manifest is last, and `raw/dependencies.json` is absent.
- [x] Machine files are UTF-8/LF, canonical, timestamp-free, and reference only existing fragments/hashes.
- [x] Only `raw/log.md` receives a timestamp and it is not a machine-fact source.
- [x] At least ten integration/snapshot cases pass; full gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: `CanonicalAggregateWriter` prepares the owned output once, creates every v3 fact/index/partition/scaffold path, emits source-generated canonical UTF-8/LF JSON and deterministic YAML/Markdown, validates fragment existence/hash/length, omits v2 `dependencies.json`, and writes the manifest last. Only `raw/log.md` receives `TimeProvider` time. Ten integration/snapshot cases cover the exact tree, ordering, safety, determinism, security/coverage manifest fields, invalid references, stale replacement, and reviewed manifest bytes. The full gate passed 661 tests with 0 failed and 0 skipped.

**Tests**: integration + snapshot
**Gate**: full
**Commit**: `feat(output): add canonical factual aggregates`

### T20: Orchestrate the syntax-only analysis transaction

**What**: Implement `AnalysisEngine.AnalyzeAsync` for request validation, output safety, inert inventory, sequential extraction, validation, persistence, Markdown projection, bounded summaries, aggregation, and result reporting.
**Where**: `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs`
**Depends on**: T19
**Reuses**: T6 contracts and T13-T19 modules; output preparation occurs once.
**Requirement**: FACT-01, FACT-02, FACT-03, FACT-08, FACT-11, FACT-18, FACT-20, FACT-24, FACT-36, FACT-53

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:csharp-concurrency-patterns`

**Done when**:

- [x] Default and explicit syntax-only runs invoke zero executable adapters and emit facts/Markdown for broken projects.
- [x] Scopes process in canonical sequential order and prior fragment/Roslyn objects are released before the next service.
- [x] Structural invalidity returns exit 1; valid syntax fallback returns exit 0 with complete coverage.
- [x] At least ten new integration cases pass; full gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: `AnalysisEngine.AnalyzeAsync` owns one validated transaction: inert inventory, one canonical sequential service/project/document scope at a time, syntax extraction, structural validation, atomic fragment persistence, fact-only frontmatter/Markdown projection, compact counts, and prepared aggregate commit. The recording inventory observer remains unused, invalid fragments are omitted with exit 1, and trusted semantic requests degrade explicitly to syntax with exit 0 in this safe cut. Ten integration cases cover broken projects, manifests, cancellation, scope non-overlap, preparation, validation failure, coverage, and fallback. The full gate passed 671 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: full
**Commit**: `feat(analysis): orchestrate syntax-only factual pipeline`

### T21: Route the CLI through the v3 engine

**What**: Add v3 analysis/trust/generator/timeout options, validate them before output preparation, invoke only `AnalysisEngine`, and remove the v2 pipeline from the production CLI path.
**Where**: `src/Csharp2Md.Cli/Program.cs`
**Depends on**: T20
**Reuses**: Existing System.CommandLine patterns, manifest/directory inputs, error formatting, and exit-code tests.
**Requirement**: FACT-01, FACT-02, FACT-04, FACT-05, FACT-06, FACT-07, FACT-08, FACT-25

**Tools**:

- MCP: official System.CommandLine documentation only if an unproven API is needed
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Zero-option CLI uses syntax-only/untrusted and produces v3 output without starting semantic infrastructure.
- [x] Invalid trust, generator, and timeout combinations exit 1 and preserve a sentinel output byte-for-byte.
- [x] CLI returns the engine exit code and no production call reaches `AnalysisPipeline`.
- [x] At least eight new CLI integration cases and the phase Build gate pass.

**Completed evidence (2026-08-17)**: The production CLI now validates analysis, trust, generator, and positive timeout options before output preparation, creates one `AnalysisRequest`, invokes only `AnalysisEngine`, reports its diagnostics/summary, and returns its exit code. Ten new integration cases cover zero-option inert defaults, explicit syntax, sentinel preservation for invalid combinations and values, semantic syntax fallback, and structural exit-code propagation. Existing CLI/end-to-end/package assertions now exercise the v3 artifact contract, including external manifest roots with canonical fact paths. The phase Build gate passed: Release build with 0 warnings/errors, format verification clean, and 681 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: build
**Commit**: `feat(cli): route analysis through the v3 engine`

### T22: Implement controlled MSBuild evaluation

**What**: Convert the proven property/item probe into the production evaluation adapter with one outer query, one query per target framework, inert import/extension inventory, timeout, and process-tree cleanup.
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/MSBuild/`
**Depends on**: T21
**Reuses**: T2-T3 proven command construction and process ownership.
**Requirement**: FACT-26, FACT-27, FACT-28, FACT-29, FACT-32, FACT-62, FACT-63

**Tools**:

- MCP: official Microsoft documentation
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:csharp-concurrency-patterns`

**Done when**:

- [x] Production arguments are identical in security properties to the proven probe and never invoke restore or targets.
- [x] Results include every FACT-32 evaluated property/item and separate TFM scopes; expanded XML is discarded.
- [x] Failures/timeouts return scoped degraded data after descendant termination.
- [x] At least ten integration cases pass; full gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: `DotnetMsBuildEvaluator` requires a trusted-semantic request before process creation, issues one target-free outer query plus one query per canonical TFM through `ProcessStartInfo.ArgumentList`, inventories evaluated analyzer/generator paths without loading them, parses import paths from a deleted preprocess file, and owns timeout/cancellation process-tree cleanup. Twelve new integration cases cover healthy fields, independent TFMs, forbidden target/restore absence, import cleanup, inert extensions, missing SDK, incomplete restore, invalid reference, trust-before-process, argument construction, timeout, and caller cancellation. The full gate passed 693 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: full
**Commit**: `feat(semantics): evaluate trusted projects safely`

### T23: Implement the proven sanitized compilation adapter

**What**: Build target-scoped compilations and semantic models using the Increment-0-selected strategy, excluding analyzer references before any compilation request.
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/Roslyn/SemanticCompilationAdapter.cs`
**Depends on**: T22
**Reuses**: T4 evidence and AD-003's Roslyn 5.6 constraints.
**Requirement**: FACT-30, FACT-33, FACT-34, FACT-64

**Tools**:

- MCP: installed Roslyn XML docs and official Microsoft/Roslyn docs
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:csharp-nullable-reference-types`

**Done when**:

- [x] No compilation/model is requested before analyzer sanitation and target scopes never merge.
- [x] Null/unsupported compilation/model and error symbols return ordinary degraded results.
- [x] Marker analyzer/generator assemblies remain unexecuted.
- [x] At least eight integration cases pass; full gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: `SemanticCompilationAdapter` constructs direct target-scoped `CSharpCompilation` instances from evaluated sources and references, applies evaluated language/constant/output inputs, and excludes analyzer items before compilation construction. It never creates `MSBuildWorkspace`. Null compilation/model, compiler errors, error symbols, and one-document model failures return scoped degraded data while preserving syntax trees and unaffected bindings. Ten new integration cases passed; the full gate passed 703 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: full
**Commit**: `feat(semantics): add sanitized compilation adapter`

### T24: Execute source generators only after explicit consent

**What**: Implement the generator-only adapter selected by Increment 0, recording loaded extensions, generated documents, and generator diagnostics while never running diagnostic analyzers.
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/Roslyn/SourceGeneratorAdapter.cs`
**Depends on**: T23
**Reuses**: T5 generator isolation probe and T23 sanitized compilations.
**Requirement**: FACT-29, FACT-30, FACT-31, FACT-34

**Tools**:

- MCP: installed Roslyn XML docs and official Roslyn documentation
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Disabled mode never constructs/loads the adapter; enabled trusted mode runs only generators.
- [x] Generated documents receive stable scope identities and failures retain pre-generator facts.
- [x] Loaded extensions and diagnostics are deterministic and evidence-backed.
- [x] At least six integration cases pass; full gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: generator execution now requires an explicitly constructed trusted-semantic opt-in request. `SourceGeneratorAdapter` loads only source/incremental generator implementations in a collectible context, never instantiates diagnostic analyzers, applies the compilation's parse options, emits stable generated-document identities, and records deterministic loaded extensions, source-backed diagnostics, load failures, and generator failures while retaining the pre-generator compilation. Seven new marker-backed integration cases passed; the full gate passed 710 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: full
**Commit**: `feat(semantics): add explicit generator execution adapter`

### T25: Enrich project and target facts

**What**: Convert evaluated results into project/target facts containing declared SDK, imports, output type, TFMs, assembly/root namespace, compile items, references, constants, language version, nullable mode, and compiled extensions.
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/ProjectFactEnricher.cs`
**Depends on**: T24
**Reuses**: T22 results, factual identities, evidence, provenance, and diagnostics.
**Requirement**: FACT-32, FACT-34, FACT-40, FACT-65

**Tools**:

- MCP: official Microsoft documentation for evaluated properties/items
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Every FACT-32 field is populated only from evidence available at its target scope.
- [x] Missing/failed evaluation retains syntactic project facts and records requested/effective mode, restore false, and isolation none.
- [x] Multi-target facts cannot be collapsed into one exact result.
- [x] At least eight unit/integration cases pass; full gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: Added target-scoped project/target enrichment and schema-v2 wire contracts for every FACT-32 field, with canonical collections, diagnostic references, and explicit requested/effective resolution plus restore/isolation metadata on fallback. Eight integration cases cover healthy, failed, mixed, and distinct multi-target evaluation, canonicalization, diagnostic deduplication, serialization, and identity rejection. Full gate: 718/718 passed, 0 failed, 0 skipped (no discovered-test decrease).

**Tests**: integration
**Gate**: full
**Commit**: `feat(semantics): enrich project and target facts`

### T26: Enrich symbol facts semantically

**What**: Add resolved symbol identities, bases, interfaces, implementations, overrides, attributes, and relevant type references without promoting error symbols to exact facts.
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/SymbolFactEnricher.cs`
**Depends on**: T25
**Reuses**: T15 syntactic facts, T23 semantic models, and `GetDocumentationCommentId()` with canonical fallback.
**Requirement**: FACT-10, FACT-13, FACT-33, FACT-34, FACT-57

**Tools**:

- MCP: installed Roslyn XML docs and official Microsoft/Roslyn docs
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:csharp-nullable-reference-types`

**Done when**:

- [x] Overloads, generics, records, interfaces, implementations, overrides, attributes, and type references match spec-defined expected symbols.
- [x] Null documentation IDs use canonical signatures; error symbols remain syntactic/unresolved.
- [x] Binding failure affects only its document/fact scope and retains syntax evidence.
- [x] At least twelve unit/integration cases pass; full gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: Added target-scoped semantic symbol enrichment using Roslyn-declared symbols, documentation-comment IDs with canonical-signature fallback, resolved local bases/interfaces/implementations/overrides, attributes, and fully qualified relevant types. Error symbols retain syntactic IDs at unresolved resolution, while missing/throwing bindings preserve syntax with fact/document-scoped diagnostics. Fourteen integration cases cover the FACT-57 language matrix, fallback/error behavior, scoped degradation, and schema-v2 serialization. Full gate: 732/732 passed, 0 failed, 0 skipped (14-test increase).

**Tests**: integration
**Gate**: full
**Commit**: `feat(semantics): enrich symbol facts`

### T27: Merge enrichment without erasing lower-resolution evidence

**What**: Implement immutable fact merging, exact-claim conflict rejection, higher-resolution claim replacement, diagnostic/evidence deduplication, and document resolution recomputation.
**Where**: `src/Csharp2Md.Core/Facts/Composition/FactMerger.cs`
**Depends on**: T26
**Reuses**: T9 resolution algebra and T11 validation diagnostics.
**Requirement**: FACT-13, FACT-34, FACT-36, FACT-50, FACT-65, FACT-69

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Semantic absence/failure can only retain/add/downgrade with diagnostics; it never removes syntax facts.
- [x] Conflicting exact claims fail structurally rather than using last-write-wins.
- [x] Mixed resolutions and diagnostic references recompute exactly per the normative table.
- [x] At least ten new unit cases pass; quick gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: Added pure immutable fact composition that retains additive syntax facts, replaces only lower-quality claims, unions canonical provenance/evidence/diagnostic references, and emits scoped structural diagnostics for incompatible or conflicting equal-quality claims. Document resolution is recomputed from referenced symbols through the normative algebra, with affected diagnostic references propagated. Fourteen unit cases cover absence, addition, higher/lower resolution, exact conflicts, every document-resolution outcome, diagnostic deduplication, and canonical ordering. Quick gate: 559/559 passed, 0 failed, 0 skipped.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(facts): merge semantic enrichment safely`

### T28: Project honest diagnostics and coverage

**What**: Aggregate deterministic diagnostics and coverage for every inventoried project, target, document, detector, fact level, attempt state, resolution, and diagnostic reference.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/CoverageProjector.cs`
**Depends on**: T27
**Reuses**: T19 aggregate outputs, analysis diagnostics, and coverage facts.
**Requirement**: FACT-35, FACT-36, FACT-40, FACT-50, FACT-51, FACT-52, FACT-53, FACT-54, FACT-65, FACT-69

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:snapshot-testing`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Not-applicable, unattempted, syntactic, unresolved, partial, exact, and failed scopes remain distinguishable.
- [x] Every degradation diagnostic is scoped and referenced by affected fragments/coverage without absolute paths or stack traces.
- [x] The audit log summarizes but does not define machine facts.
- [x] At least ten integration/snapshot cases pass; full gate records no discovered-test decrease.

**Completed evidence (2026-08-17)**: Added canonical coverage projection for inventoried hierarchy and detector scopes, preserving applicability, attempt, resolution, and diagnostic references while projecting sanitized structured diagnostics for every stage. Aggregate output now writes authoritative diagnostics/coverage documents and limits the audit log to deterministic summary counts plus its allowed timestamp. Twenty-six integration/theory/snapshot cases cover all resolution and attempt states, failures, stages, redaction, ordering, conflicts, structured output, and a spec-derived approved snapshot. Full gate: 772/772 passed, 0 failed, 0 skipped (26-test increase from T27's total suite).

**Tests**: integration + snapshot
**Gate**: full
**Commit**: `feat(output): project diagnostics and coverage`

### T29: Integrate trusted semantic execution into the engine

**What**: Route trusted semantic requests through evaluation, compilation, optional generators, enrichment, merging, fallback, and coverage while preserving deterministic sequential service/target/document processing.
**Where**: `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs`
**Depends on**: T28
**Reuses**: T20 syntax transaction and T22-T28 semantic modules.
**Requirement**: FACT-26, FACT-30, FACT-31, FACT-32, FACT-33, FACT-34, FACT-35, FACT-36, FACT-62, FACT-63, FACT-64, FACT-65

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:csharp-concurrency-patterns`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Healthy semantic runs enrich facts; every specified evaluation/workspace/compilation/model/generator/detector failure degrades only its scope.
- [x] Timeout kills one service's tree and falls back with exit 0; caller cancellation propagates.
- [x] At most one semantic scope is alive at once and syntax facts exist for every inventoried source.
- [x] At least twelve integration cases and the phase Build gate pass with no discovered-test decrease.

**Completed evidence (2026-08-17)**: `AnalysisEngine` now routes only trusted semantic requests through the inert evaluator, per-target sanitized compilation, explicit generator opt-in, symbol enrichment, safe fact merging, scoped diagnostics, and honest coverage overrides while retaining syntax facts for every inventoried source. Evaluation/timeout, compilation/workspace, missing-model, generator, and downstream structured-diagnostic failures degrade their affected scope without changing exit code; caller cancellation propagates and semantic target scopes remain sequential. Fourteen new integration cases cover healthy exact output, each available adapter boundary, multi-target partial success, generator opt-in, cancellation, complete source persistence, and not-attempted coverage. The phase Build gate passed 786 tests with 0 failed and 0 skipped.

**Tests**: integration
**Gate**: build
**Commit**: `feat(analysis): integrate trusted semantic enrichment`

### T30: Build reusable solution symbol and relation indexes

**What**: Construct target-aware symbol, project-reference, type-reference, and relation indexes once per solution for detector and classification queries.
**Where**: `src/Csharp2Md.Core/Analysis/Indexes/`
**Depends on**: T29
**Reuses**: Stable fact IDs, semantic symbol facts, and project/target facts.
**Requirement**: FACT-37

**Tools**:

- MCP: installed Roslyn XML docs and official documentation for symbol keys used
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:type-design-performance`

**Done when**:

- [x] Indexes build once per solution/target set and detectors cannot initiate repeated solution-wide searches.
- [x] Canonical ordering and target identity prevent cross-TFM symbol conflation.
- [x] At least six unit/integration cases prove reuse, lookup correctness, ordering, and bounded retained summaries.
- [x] Full gate passes with no discovered-test decrease.

**Completed evidence (2026-08-17)**: `SolutionAnalysisIndex` materializes one immutable target-keyed snapshot per solution input and exposes reusable symbol, type-reference, project-reference, and relation lookups without retaining Roslyn objects. Seven integration cases prove single enumeration/reuse, cross-TFM isolation, canonical lookup ordering, resolved and unresolved project references, relation scoping, and project-wide relation queries. The full gate passed 793 tests with 0 failed and 0 skipped, a 7-test increase.

**Tests**: integration
**Gate**: full
**Commit**: `feat(analysis): add reusable semantic indexes`

### T31: Classify executable and test project roots

**What**: Classify confirmed executable roots as web API, worker, or CLI in priority order and classify test and non-test library projects without business inference.
**Where**: `src/Csharp2Md.Core/Analysis/Classification/ProjectClassifier.cs`
**Depends on**: T30
**Reuses**: Evaluated project facts and reusable indexes.
**Requirement**: FACT-38, FACT-39, FACT-40, FACT-41, FACT-66

**Tools**:

- MCP: official framework documentation for classification evidence when needed
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Web API precedes worker and worker precedes generic executable/CLI classification.
- [x] Test support and non-test library outcomes require confirmed technical evidence only.
- [x] At least ten unit cases cover every class, priority collision, negative, and lookalike.
- [x] Quick gate passes with no discovered-test decrease.

**Completed evidence (2026-08-17)**: `ProjectClassifier` classifies only exact evaluated targets and confirmed indexed evidence, applying `service/web-api` before `service/worker` before `tool/cli`, with `Microsoft.NET.Test.Sdk` identifying `test-support` and confirmed non-executables becoming `library`. Thirteen discovered unit cases cover every class, priority collisions, WinExe, degraded targets, name/type lookalikes, and outgoing HTTP that is not endpoint evidence. The quick gate passed 572 tests with 0 failed and 0 skipped.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(classification): classify technical project roots`

### T32: Assign library component ownership

**What**: Classify libraries as private to one executable root, shared across several roots, or standalone when unconsumed using compile-time reachability only.
**Where**: `src/Csharp2Md.Core/Analysis/Classification/LibraryOwnershipClassifier.cs`
**Depends on**: T31
**Reuses**: T30 project-reference index and T31 technical root facts.
**Requirement**: FACT-42, FACT-67, FACT-68

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Exactly-one, multiple, zero, transitive, cyclic, and test-only consumers have deterministic spec-defined outcomes.
- [x] Runtime logical destinations never influence compile-time ownership.
- [x] At least eight unit cases cover the reachability matrix and ordering.
- [x] Quick gate passes with no discovered-test decrease.

**Completed evidence (2026-08-18)**: `LibraryOwnershipClassifier` computes deterministic executable-root reachability from resolved target-aware project references only, assigning libraries as private, shared dependency, or standalone with canonical owner identities. Nine unit cases cover direct, transitive, multiple, zero, cyclic, test-only, unresolved, runtime-lookalike, and ordering outcomes. The quick gate passed 581 tests with 0 failed and 0 skipped, a 9-test increase.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(classification): assign library component ownership`

### T33: Isolate factual detector execution

**What**: Implement `DetectorHost` level routing, descriptor enforcement, canonical result collection, and per-invocation exception isolation that discards incomplete facts.
**Where**: `src/Csharp2Md.Core/Detection/DetectorHost.cs`
**Depends on**: T32
**Reuses**: T10 contracts, T30 indexes, and structured diagnostics.
**Requirement**: FACT-35, FACT-49

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Only supported levels/fact kinds run and every result receives descriptor provenance.
- [x] A throwing detector loses only that invocation's incomplete facts and adds one scoped diagnostic.
- [x] Other detectors/scopes continue in canonical order.
- [x] At least eight unit cases and the phase Build gate pass with no discovered-test decrease.

**Completed evidence (2026-08-18)**: `DetectorHost` routes project/document detectors through index-bearing factual contexts in canonical descriptor order, enforces declared levels and fact kinds, stamps successful facts and diagnostics with descriptor identity/version, and converts each exception into one scoped diagnostic while discarding only that invocation. Ten unit cases cover both levels, provenance, contract violation, exception isolation, detector/scope continuation, duplicate same-level registration, and valid shared identity across granularities. The phase Build gate passed: Release build with 0 warnings/errors, format verification clean, and 825 tests with 0 failed and 0 skipped.

**Tests**: unit
**Gate**: build
**Commit**: `feat(detection): isolate factual detector execution`

### T34: Detect ASP.NET Core facts

**What**: Emit evidence-backed facts for controllers, actions, Minimal APIs, routes, authorization, policies, filters, health checks, and entrypoints, preserving irreducible route expressions as partial.
**Where**: `src/Csharp2Md.Core/Detection/AspNetCore/`
**Depends on**: T33
**Reuses**: T30 indexes, semantic symbol/operation contexts, and existing fixture controller/filter/program shapes.
**Requirement**: FACT-43, FACT-58

**Tools**:

- MCP: official ASP.NET Core and Roslyn documentation
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Every specified ASP.NET fact carries detector version, navigable evidence, source identity, and correct resolution.
- [x] Constant and irreducible route expressions remain distinguishable; no route or framework role is invented from names.
- [x] At least eighteen positive, negative, and lookalike unit/integration cases cover every listed rule.
- [x] Full gate passes with no discovered-test decrease.

**Completed evidence (2026-08-18)**: `AspNetCoreDetector` walks the document's semantic tree once and emits `RelationFact`s (partition `Http`, always `TargetId: null` with an explicit `UnresolvedReason`, since ASP.NET source never identifies a remote target) for confirmed `ControllerBase`/`[ApiController]` controllers and their public actions (route, HTTP verb, `[NonAction]` exclusion), Minimal API `MapGet/MapPost/MapPut/MapDelete/MapPatch/MapMethods/Map`, `MapHealthChecks`, `[Authorize]`/`[AllowAnonymous]`/`RequireAuthorization`/`AddPolicy`, filter attributes and `AddEndpointFilter(Factory)`, and `WebApplication.CreateBuilder`/`.Run()` entrypoints. Every fact carries the detector's descriptor provenance (`io.csharp2md.aspnet-core`, `1.0.0`) via `DetectorHost`, line/column evidence, and `FactResolution.Exact` for literal routes/policies or `Partial` with a `*_expression` detail key for non-constant (irreducible) expressions, including single-element `params` arrays. `RelationFact` gained an ordered `Details: ImmutableArray<RelationDetail>` payload threaded through `FactMerger` equality, `FactualJsonContracts`/`FactStore` schema-v2 serialization, and `facts.schema.json`; `DetectionContexts` gained the optional `SemanticDocument` a document-level detector needs. Twenty discovered unit/theory cases (`AspNetCoreDetectorTests.Cases`) cover every positive rule, three lookalikes (mismatched namespace client, mismatched `Authorize` attribute type, non-ASP.NET `MapHealthChecks` extension), and two negatives (`[NonAction]`, a plain class not inheriting `ControllerBase`). Two real bugs surfaced only by running the gate (not by inspection) were fixed: (1) `IInvocationOperation.Arguments` for these reduced extension-method calls includes the receiver as a positional argument, so `MapHealthChecks`/`RequireAuthorization` extraction now looks up the argument by parameter name (`pattern`, `policyNames`) exactly as the already-passing Minimal API/`AddPolicy` paths did, and `ExpressionValue` now unwraps a single-element `params` array's constant; (2) the test fixture originally parsed the ASP.NET framework stubs and the case body as one syntax tree, so the stub's own `Controller : ControllerBase` declaration was itself detected as a confirmed controller in every case — the fixture now compiles the stubs and the case body as two separate trees in one compilation, and only the body tree is the detection document. The full gate passed: Release build 0 warnings/errors, `dotnet format --verify-no-changes` clean, and 845 tests with 0 failed and 0 skipped (up from 825 at T33, a 20-test increase).

**Tests**: integration
**Gate**: full
**Commit**: `feat(detection): extract aspnet core facts`

### T35: Detect dependency-injection facts

**What**: Emit all confirmed DI registrations with lifetime, implementation/factory, open generic, multiple implementation, key, and navigable local expansion evidence.
**Where**: `src/Csharp2Md.Core/Detection/DependencyInjection/`
**Depends on**: T34
**Reuses**: T30 indexes and existing service-collection extension fixture patterns.
**Requirement**: FACT-44, FACT-58

**Tools**:

- MCP: official Microsoft DI and Roslyn documentation
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Distinct registrations are never collapsed and local expansion methods link to their definition evidence.
- [x] Factories, keys, open generics, and multiple implementations retain exact/partial evidence appropriately.
- [x] Name-only lookalikes and unrelated extension calls emit nothing.
- [x] At least sixteen positive, negative, and lookalike cases pass; full gate records no count decrease.

**Completed evidence (2026-08-18)**: `DependencyInjectionDetector` walks the document's invocations once and, for every confirmed extension-method call whose *declared* (unreduced) receiver parameter type is exactly `Microsoft.Extensions.DependencyInjection.IServiceCollection`, either emits a `di-registration` fact (partition `DependencyInjection`, always `TargetId: null` with an explicit `UnresolvedReason` per FACT-47 — an in-process registration never proves a remote target) when the method is declared in the real `Microsoft.Extensions.DependencyInjection` namespace, or a `di-registration-expansion` fact (with two evidence entries — call site and local definition site — when the defining method lives in the same document) for any other extension on that same real interface, so a locally-authored `AddXyzServices(this IServiceCollection ...)` helper is captured without conflating it with the framework's own methods. Registrations carry `lifetime` (singleton/scoped/transient, including the keyed variants), `service`/`implementation` (from generic type arguments or, for the non-generic `Type`-parameter overload, from `typeof(...)` operands via `ITypeOfOperation.TypeOperand`), `open_generic: true` for unbound generic types, `key`/`key_expression` for keyed registrations, and `factory` with `FactResolution.Partial` when an `implementationFactory` delegate hides the concrete type. Distinct registrations for the same service, and textually identical repeats, both keep distinct `RelationId`s via the same per-claim ordinal scheme as T34. Receiver-type matching uses `method.ReducedFrom?.Parameters[0] ?? method.Parameters[0]` rather than trusting `Instance`/`Arguments`, so it works regardless of whether Roslyn represents a given extension call in reduced or unreduced form — this sidesteps T34's `Arguments`-includes-receiver quirk entirely instead of re-deriving it per call shape. One real bug found only by running the gate: `services.AddKeyedSingleton<IClock, SystemClock>("primary")`'s `object? serviceKey` parameter boxes the string literal, so `IArgumentOperation.Value` is an `IConversionOperation` wrapping the literal rather than the literal itself — its own `ConstantValue.HasValue` was false, silently downgrading an exact key to a `key_expression` with the quoted source text; `ExpressionValue` now unwraps a boxing `IConversionOperation` to its `Operand` before checking `ConstantValue`, verified against `Microsoft.CodeAnalysis.xml`. Twenty-five discovered unit/theory cases (`DependencyInjectionDetectorTests`) cover all FACT-44 outcomes plus four lookalikes (a same-named method on an unrelated `IServiceCollection` type in another namespace, an unrelated extension on a same-named-but-foreign type, passing the collection to a plain non-extension method, and a user method literally named `AddSingleton` with a non-matching signature — the last one correctly becomes an expansion, not a registration). The full gate passed: Release build 0 warnings/errors, `dotnet format --verify-no-changes` clean, and 870 tests with 0 failed and 0 skipped (up from 845 at T34, a 25-test increase).

**Tests**: integration
**Gate**: full
**Commit**: `feat(detection): extract dependency injection facts`

### T36: Detect HTTP client relations

**What**: Replace heuristic HTTP signals with `IOperation`-confirmed named/typed-client relation facts carrying method, route expression, base URL, headers, timeout, logical destination evidence, and honest target resolution.
**Where**: `src/Csharp2Md.Core/Detection/Http/`
**Depends on**: T35
**Reuses**: Existing `HttpClientDetector` behavioral cases, configuration indexing, and T30 indexes.
**Requirement**: FACT-45, FACT-47, FACT-58

**Tools**:

- MCP: official Roslyn `IOperation` and Microsoft HTTP client documentation
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Confirmed calls preserve named/typed identity, request method/expression, configuration evidence, and navigation.
- [x] Unproved targets remain null with resolution and non-empty reason; logical names/URLs never become service IDs.
- [x] Null/error semantic contexts degrade honestly and syntactic/name lookalikes do not emit confirmed relations.
- [x] At least sixteen positive, negative, and lookalike cases pass; full gate records no count decrease.

**Completed evidence (2026-08-18)**: `HttpRelationDetector` replaces the heuristic v2 `HttpClientDetector` with five `IOperation`-confirmed relation kinds under partition `Http` (always `TargetId: null` with an explicit `UnresolvedReason` per FACT-47): `http-named-client` for a confirmed `Microsoft.Extensions.Http.IHttpClientFactory.CreateClient("name")` call not immediately chained into a request; `http-request` for a confirmed `System.Net.Http.HttpClient` verb call (`GetAsync`/`GetStringAsync`/`GetByteArrayAsync`/`GetStreamAsync`→GET, `PostAsync`, `PutAsync`, `DeleteAsync`, `PatchAsync`), carrying `http_method`, `route`/`route_expression`, and a `client` detail that is `named:<name>` when the receiver is a same-expression `CreateClient(...)` chain (deduplicated against a standalone `http-named-client` fact for that same call, one relation not two) or `typed:<receiver-expression>` otherwise; `http-base-address` and `http-timeout` for confirmed `BaseAddress`/`Timeout` property assignments on an `HttpClient`-typed receiver; and `http-header` for a confirmed `DefaultRequestHeaders.Add(name, value)` call, verified via `IPropertyReferenceOperation.Property.Name` plus the property's own receiver type rather than by name alone. `Dispose`/`CancelPendingRequests` are excluded from request detection, and every check confirms the *declared* receiver/property type via IOperation (not identifier names), so a same-named method or property on an unrelated type (a lookalike `Contoso.Client.GetAsync`, a lookalike `Contoso.IHttpClientFactory`, a lookalike `FakeHeaders.Add`) emits nothing. All real API surface used (`HttpClient`, `HttpRequestHeaders`, `Uri`, `TimeSpan`) is genuine BCL, available directly through the test host's trusted platform assemblies — only `Microsoft.Extensions.Http.IHttpClientFactory` needed a same-namespace stub. This detector needed none of T34/T35's extension-method `Arguments`-position or namespace-shadowing workarounds, because every real `HttpClient`/`IHttpClientFactory` member used here is a genuine instance method or property, not an extension method — confirming those workarounds are specific to extension-method call shapes, not a general Roslyn/IOperation quirk. Twenty-six discovered unit/theory cases (`HttpRelationDetectorTests`) cover every request verb, the named/typed client distinction and its chained-call dedup, base address and timeout (literal and irreducible), headers (literal and irreducible), four lookalikes, non-request members, repeated-identical-request identity distinctness, and the no-semantic-document case. The full gate passed: Release build 0 warnings/errors, `dotnet format --verify-no-changes` clean, and 896 tests with 0 failed and 0 skipped (up from 870 at T35, a 26-test increase).

**Tests**: integration
**Gate**: full
**Commit**: `feat(detection): extract http relation facts`

### T37: Detect gRPC relations

**What**: Replace heuristic gRPC signals with confirmed generated-client/type and invocation evidence, preserving unresolved destinations without fictional service identities.
**Where**: `src/Csharp2Md.Core/Detection/Grpc/`
**Depends on**: T36
**Reuses**: Existing gRPC detector behavior and fixture client, T30 indexes, and relation validation.
**Requirement**: FACT-46, FACT-47, FACT-58

**Tools**:

- MCP: official gRPC .NET and Roslyn documentation
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Confirmed calls emit source, optional target, method/type evidence, provenance, and correct resolution.
- [x] Name-only client lookalikes emit nothing; unproved logical destinations remain null with reasons.
- [x] At least ten positive, negative, lookalike, and degraded cases pass.
- [x] Full gate passes with no discovered-test decrease.

**Completed evidence (2026-08-18)**: `GrpcRelationDetector` replaces the heuristic v2 `GrpcClientDetector` with a single `IOperation`-confirmed `grpc-call` relation kind under partition `Grpc` (always `TargetId: null` with an explicit `UnresolvedReason` per FACT-47). A call is confirmed only when its receiver's type derives (walking `BaseType`) from a type literally named `ClientBase` in the namespace `Grpc.Core` — matching the real generated-client shape (`FooClient : Grpc.Core.ClientBase<FooClient>`) without depending on the `Grpc.Core` package, same non-dependency approach the v2 detector and its fixture (`fixtures/SyntheticSolution/Acme.Orders/PaymentsGrpcClient.cs`) already used. Each confirmed call carries `service` (the client type's name with a trailing `Client` suffix stripped, or kept whole when absent — "type evidence" per FACT-46, never promoted to a resolved target), `method` (the RPC method name), and `call_shape` (`streaming` when the method's declared return type is one of `Grpc.Core`'s `AsyncServerStreamingCall`/`AsyncClientStreamingCall`/`AsyncDuplexStreamingCall`, `unary` otherwise — including a blocking overload that returns the response type directly). `WithHost` (client configuration, not an RPC) is excluded. Because every check confirms the receiver's actual base-type chain and containing namespace via the semantic model, a same-named `FooClient : ClientBase` where `ClientBase` is a different, unrelated type in a different namespace (FACT-46's "not from an unconfirmed name alone") correctly emits nothing. Twelve discovered unit/theory cases (`GrpcRelationDetectorTests`) cover unary (both call-handle and direct-blocking-return shapes), server/client/duplex streaming, the client-suffix-stripping and no-suffix service-name rules, the configuration-call exclusion, a non-client receiver, the namespace-qualified lookalike, and the no-semantic-document case. The full gate passed: Release build 0 warnings/errors, `dotnet format --verify-no-changes` clean, and 908 tests with 0 failed and 0 skipped (up from 896 at T36, a 12-test increase).

**Tests**: integration
**Gate**: full
**Commit**: `feat(detection): extract grpc relation facts`

### T38: Detect messaging and event relations

**What**: Replace heuristic messaging signals with confirmed producer/consumer framework or type evidence, correlating targets only when identity is proved and retaining unpaired observations honestly.
**Where**: `src/Csharp2Md.Core/Detection/Messaging/`
**Depends on**: T37
**Reuses**: Existing messaging detector behavior, shared-contract fixtures, and T30 indexes.
**Requirement**: FACT-46, FACT-47, FACT-58

**Tools**:

- MCP: official framework and Roslyn documentation for implemented messaging patterns
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Producers/consumers require confirmed framework/type evidence and retain contract/topic evidence separately from service identity.
- [x] Correlated, unpaired, unresolved, negative, and name-lookalike cases have spec-defined outcomes.
- [x] At least twelve positive, negative, lookalike, and degradation cases pass.
- [x] Full gate passes with no discovered-test decrease.

**Completed evidence (2026-08-18)**: `MessagingRelationDetector` replaces the heuristic v2 `MessagingDetector` — which fired on any member-access call literally named `Publish`/`PublishAsync`/`Subscribe`/`SubscribeAsync` regardless of receiver — with a structural-shape confirmation (FACT-46 names no specific messaging package, so unlike T34/T36/T37's single-framework-namespace check, the contract is recognized by shape): a qualifying call must resolve to an *interface* method with exactly one generic type parameter, named `Publish`/`PublishAsync` with that type parameter used directly as the message parameter's type, or named `Subscribe`/`SubscribeAsync` with a parameter (the handler) whose own type arguments reference that type parameter. Confirmation checks both an interface-typed call site (the invoked symbol itself) and a concrete-typed call site (every interface the receiver implements, via `AllInterfaces`), so `IEventBus bus` and `EventBus bus` (a class implementing `IEventBus`) both confirm identically. Two `messaging-publish`/`messaging-subscribe` relation kinds land in the new `Events` partition (always `TargetId: null` with an explicit `UnresolvedReason` — a single document can only prove its own side of an exchange, matching AD-011/FACT-47 and the v2 detector's own P2-15 half-edge design that cross-document pairing happens later, now at T40's aggregate projection rather than a document detector). The `topic` detail is read directly from the invocation's resolved `TypeArguments[0].Name` rather than re-parsing syntax (a real improvement over v2's three-branch syntax/semantic fallback), which works identically whether the call site wrote an explicit type argument or let it infer from a constructed message. One real bug found only by running the gate: comparing a *constructed* generic method's `Parameters[0].Type` (call-site-substituted, e.g. `OrderPlaced`) against its own `TypeParameters[0]` (always unsubstituted, e.g. `TEvent`) can never succeed — every direct interface-typed call site silently failed confirmation until the shape check ran against `method.ConstructedFrom` instead, which keeps both sides consistently unsubstituted; the concrete-type fallback path was accidentally unaffected because `INamedTypeSymbol.GetMembers(name)` already returns unconstructed declarations. Fourteen discovered unit/theory cases (`MessagingRelationDetectorTests`) cover explicit and inferred publish, explicit subscribe, a document producing both independently, the concrete-implementing-type fallback, repeated-identical-publish identity distinctness, an unrelated non-generic lookalike, a generic-but-non-interface lookalike, an interface method matching the name but not the parameter-uses-TEvent shape (both publish and subscribe variants), and the no-semantic-document case. The full gate passed: Release build 0 warnings/errors, `dotnet format --verify-no-changes` clean, and 922 tests with 0 failed and 0 skipped (up from 908 at T37, a 14-test increase).

**Tests**: integration
**Gate**: full
**Commit**: `feat(detection): extract messaging relation facts`

### T39: Detect direct and project references as compile-time facts

**What**: Emit confirmed type/direct and project/package references exclusively in the compile-time partition and reject any runtime classification of those sources.
**Where**: `src/Csharp2Md.Core/Detection/CompileTime/`
**Depends on**: T38
**Reuses**: Existing direct-reference behavior, evaluated references, T30 indexes, and FACT-16 validation.
**Requirement**: FACT-46, FACT-48, FACT-58

**Tools**:

- MCP: installed Roslyn XML docs and official documentation for referenced-symbol APIs
- Skills: `tlc-spec-driven`, `dotnet-skills:modern-csharp-coding-standards`

**Done when**:

- [x] Project and package references appear only in compile-time facts; local type references are semantically confirmed.
- [x] Runtime partitions contain none of these references and name-only lookalikes emit nothing.
- [x] At least ten positive, negative, lookalike, and invalid-kind cases pass.
- [x] Phase Build gate passes with no discovered-test decrease.

**Completed evidence (2026-08-18)**: `CompileTimeReferenceDetector`, a project-level (`IProjectFactDetector`) detector, projects the already-evaluated `TargetEvaluationDetails.ProjectReferences`/`PackageReferences` from T25's real MSBuild property/item evaluation — never a re-parse of the project XML, which is the "semantically confirmed" upgrade over v2's `XDocument.Load` + `Descendants("ProjectReference")` approach that couldn't account for MSBuild conditions or `Directory.Build.props`-injected references — into `project-reference`/`package-reference` relation facts under `RelationPartition.CompileTime`. Project references resolve their target through T30's `SolutionAnalysisIndex.GetProjectReferences`, which already matches evaluated reference paths against indexed projects (`TargetId` populated when found, otherwise null with an explicit reason); package references are always external by construction and stay unresolved even when a package id happens to coincide with an indexed project's name — v3 deliberately drops v2's `PackageId`-to-service name-matching guess. Every fact carries empty `Header.Evidence`: `FactValidator.ValidateRelation`/`ValidateEvidence` only require evidence for `IsRuntime` relations, and a project/package reference has no source-document location to point at (it originates from project XML, not a `DocumentFact`) — this was verified by reading the existing validator rather than assumed. The exact relation-kind strings `"project-reference"`/`"package-reference"` were read directly from `FactValidator.CompileTimeOnlyRelationKinds` (T11) rather than invented, so this detector's output is guaranteed compatible with the pre-existing compile-time-only validation rule (FACT-16) instead of merely hoping to match it. Ten discovered unit cases (`CompileTimeReferenceDetectorTests`) cover a resolved project reference, an unresolved (out-of-solution) project reference, a package reference and multiple distinct ones, a package id colliding with an indexed project's name staying external, an empty-reference project, the never-runtime/never-other-partition invariant, detector provenance, one fact per target on a multi-target project (mirroring T25's own multi-target discipline), and — closing the "invalid-kind" requirement directly — a test that hand-constructs a `project-reference`-kind fact in the `Http` (runtime) partition and confirms `FactValidator.Validate` rejects it with `C2M-FV-006`, proving this detector's output and the pre-existing validator agree. The Phase 5 Build gate passed: Release build 0 warnings/errors, `dotnet format --verify-no-changes` clean, and 932 tests with 0 failed and 0 skipped (up from 922 at T38, a 10-test increase).

**Tests**: integration
**Gate**: build
**Commit**: `feat(detection): emit compile-time reference facts`

### T40: Project relation partitions and component graphs

**What**: Stream validated relation summaries into compile-time, inheritance, DI, HTTP, gRPC, and event partitions plus component indexes and Mermaid without rehydrating a whole factual solution.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/RelationProjector.cs`
**Depends on**: T39
**Reuses**: T19 canonical output, T30 indexes, T31-T32 component facts, and T34-T39 relation facts.
**Requirement**: FACT-18, FACT-25, FACT-37, FACT-38, FACT-39, FACT-40, FACT-41, FACT-42, FACT-43, FACT-44, FACT-45, FACT-46, FACT-47, FACT-48, FACT-49, FACT-66, FACT-67, FACT-68

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:snapshot-testing`, `dotnet-skills:type-design-performance`

**Done when**:

- [ ] Every relation is in exactly one legal partition with valid evidence/provenance and honest nullable targets.
- [ ] Mermaid and component/index output derive only from validated summaries and never promote logical names to identities.
- [ ] Aggregation reads one partition/summary stream at a time in canonical order.
- [ ] At least twelve integration/snapshot cases pass; full gate records no discovered-test decrease.

**Tests**: integration + snapshot
**Gate**: full
**Commit**: `feat(output): project factual relations and components`

### T41: Complete the Phase 1 behavior migration

**What**: Resolve every migration-ledger row by retaining, relocating, or replacing its assertion at equal or stronger depth, then remove superseded v2-only tests and production paths.
**Where**: `.specs/features/csharp2md-v3/test-migration.md`
**Depends on**: T40
**Reuses**: T1 ledger and all completed v3 interfaces.
**Requirement**: FACT-25, FACT-55

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns`

**Done when**:

- [ ] Every one of the 428 baseline rows is marked preserved with its executable replacement evidence.
- [ ] No production reference remains to `AnalysisPipeline`, `SolutionLoader` orchestration, direct semantic Markdown enrichment, old detector interfaces/signals/graph builder, or `DependencyJsonWriter`.
- [ ] Removed tests have equal-or-stronger v3 assertions and the discovered count does not fall below the task-entry count.
- [ ] Full gate passes.

**Tests**: integration
**Gate**: full
**Commit**: `refactor(v3)!: remove superseded analysis paths`

### T42: Complete the language-shape factual matrix

**What**: Add or close fixture and assertion gaps for overloads, generics, records, interfaces, overrides, conditional compilation, error symbols, ID stability, and source-span fidelity.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/LanguageMatrixTests.cs`
**Depends on**: T41
**Reuses**: T14-T15 and T26 test helpers plus existing render sources.
**Requirement**: FACT-10, FACT-13, FACT-21, FACT-57

**Tools**:

- MCP: official Roslyn docs for any newly exercised member
- Skills: `tlc-spec-driven`, `dotnet-test:test-gap-analysis`

**Done when**:

- [ ] Every FACT-57 language shape has explicit expected syntax and semantic facts.
- [ ] Root relocation and unrelated preceding edits preserve IDs while evidence locations move correctly.
- [ ] Error symbols never become exact and every source byte remains covered once.
- [ ] At least ten new theory cases pass; full gate records no discovered-test decrease.

**Tests**: integration
**Gate**: full
**Commit**: `test(v3): cover language and identity edge cases`

### T43: Close every detector lookalike gap

**What**: Audit all detector requirements and add missing positive, negative, and lookalike cases with spec-defined expected facts, evidence, provenance, and resolution.
**Where**: `tests/Csharp2Md.Core.Tests/Detection/DetectorMatrixTests.cs`
**Depends on**: T42
**Reuses**: T34-T39 detector fixtures and the requirement-to-test mapping.
**Requirement**: FACT-43, FACT-44, FACT-45, FACT-46, FACT-47, FACT-48, FACT-49, FACT-58

**Tools**:

- MCP: official framework/Roslyn docs for any gap
- Skills: `tlc-spec-driven`, `dotnet-test:test-gap-analysis`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] Every detector rule has at least one positive, negative, and confusing lookalike assertion.
- [ ] Assertions verify outcome values, evidence/provenance, resolution, and absence of fictional targets—not implementation call structure.
- [ ] The audit table contains no uncovered rule and all added cases pass.
- [ ] Full gate passes with no discovered-test decrease.

**Tests**: integration
**Gate**: full
**Commit**: `test(v3): close detector discrimination gaps`

### T44: Prove trust, extension, and fallback boundaries end to end

**What**: Add real-engine and real-CLI adversarial tests for zero executable syntax calls, trust-before-output, generator opt-in, analyzer exclusion, scoped failures, timeout cleanup, and recoverable exit 0 versus structural exit 1.
**Where**: `tests/Csharp2Md.Core.Tests/Cli/V3SecurityBoundaryTests.cs`
**Depends on**: T43
**Reuses**: Recording/failing adapters, sentinel-output pattern, marker extensions/processes, and packaged CLI helpers.
**Requirement**: FACT-02, FACT-04, FACT-05, FACT-06, FACT-30, FACT-31, FACT-34, FACT-35, FACT-36, FACT-59, FACT-62, FACT-63, FACT-64, FACT-65

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-test:test-gap-analysis`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] Every FACT-59 security/fallback boundary is asserted at the deepest observable interface available.
- [ ] Invalid requests preserve sentinel output; degraded valid runs retain syntax facts/coverage and exit 0; structural invalidity exits 1.
- [ ] Process and extension markers prove absence/presence, not merely returned flags.
- [ ] At least twelve new integration/e2e cases pass; full gate records no count decrease.

**Tests**: integration
**Gate**: full
**Commit**: `test(v3): prove analysis security boundaries`

### T45: Prove cross-root canonical output and snapshots

**What**: Run identical fixtures from unrelated absolute roots and compare the complete byte trees except `raw/log.md`, validating all hashes/references and representative fact/frontmatter/Markdown snapshots.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/V3DeterminismTests.cs`
**Depends on**: T44
**Reuses**: Existing determinism comparison, Verify setup, T12 serializers, and T19/T40 outputs.
**Requirement**: FACT-19, FACT-20, FACT-22, FACT-23, FACT-24, FACT-25, FACT-56, FACT-59

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:snapshot-testing`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] File sets and bytes match across roots except the log; no absolute root appears in machine artifacts.
- [ ] Every manifest reference resolves and every hash matches exact artifact bytes.
- [ ] Source reconstruction and representative snapshots assert spec-defined content, not blind captures.
- [ ] At least six integration/snapshot cases pass; full gate records no discovered-test decrease.

**Tests**: integration + snapshot
**Gate**: full
**Commit**: `test(v3): prove canonical cross-root output`

### T46: Mark package and schemas as v3

**What**: Set package version `3.0.0`, ensure factual and frontmatter schema version 2, and update packaging/smoke assertions for the incompatible artifact contract.
**Where**: `Directory.Build.props`
**Depends on**: T45
**Reuses**: Existing package smoke and schema synchronization tests.
**Requirement**: FACT-60, FACT-70

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-skills:package-management`

**Done when**:

- [ ] Packaged metadata reports exactly 3.0.0 and the real packaged CLI emits schema version 2.
- [ ] No v2 compatibility artifact or `dependencies.json` appears.
- [ ] At least three updated/new packaging and schema assertions pass without weakening existing checks.
- [ ] Build gate passes with no discovered-test decrease.

**Tests**: integration
**Gate**: build
**Commit**: `chore(release)!: mark factual output version 3.0.0`

### T47: Close documentation and pre-Verifier quality gates

**What**: Update user-facing v3 usage/output/trust documentation, run the required code/test quality audits, close every blocking gap, and record final task traceability before the independent Verifier.
**Where**: `README.md`
**Depends on**: T46
**Reuses**: Approved spec/design, generated CLI help, migration ledger, and final output fixtures.
**Requirement**: FACT-55, FACT-59, FACT-61

**Tools**:

- MCP: NONE
- Skills: `tlc-spec-driven`, `dotnet-test:test-gap-analysis`, `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns`, `dotnet-skills:dotnet-slopwatch`

**Done when**:

- [ ] English and Portuguese documentation describe syntax-only default, explicit trust/generator consent, v3 layout, diagnostics/coverage, and incompatible migration accurately.
- [ ] Test-gap, assertion-quality, anti-pattern, and Slopwatch audits have no unresolved Critical/High or feature-blocking findings.
- [ ] All 70 spec requirements map to completed tasks and executable evidence; no task or test count regressed silently.
- [ ] Final Build gate passes; then a fresh author-not-verifier agent runs the mandatory validation and discrimination sensor.

**Tests**: integration
**Gate**: build
**Commit**: `docs(v3): document factual analysis contract`

---

## Phase Execution Map

Phases and tasks execute strictly sequentially:

```
Phase 0 -> Phase 1 -> Phase 2 -> Phase 3 -> Phase 4 -> Phase 5 -> Phase 6

T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7 -> T8 -> T9 -> T10 -> T11 -> T12
   -> T13 -> T14 -> T15 -> T16 -> T17 -> T18 -> T19 -> T20 -> T21
   -> T22 -> T23 -> T24 -> T25 -> T26 -> T27 -> T28 -> T29
   -> T30 -> T31 -> T32 -> T33 -> T34 -> T35 -> T36 -> T37 -> T38 -> T39
   -> T40 -> T41 -> T42 -> T43 -> T44 -> T45 -> T46 -> T47
```

**Proposed batch packing** (~7 tasks, whole phases, sequential):

| Batch | Phase | Tasks | Count |
| --- | --- | --- | --- |
| 1 | Phase 0 | T1-T5 | 5 |
| 2 | Phase 1 | T6-T12 | 7 |
| 3 | Phase 2 | T13-T21 | 9 |
| 4 | Phase 3 | T22-T29 | 8 |
| 5 | Phase 4 | T30-T33 | 4 |
| 6 | Phase 5 | T34-T39 | 6 |
| 7 | Phase 6 | T40-T47 | 8 |

Forty-seven tasks produce seven sequential batches. No phase is split across workers. The mandatory Verifier is dispatched separately after T47; it writes `validation.md`, runs the discrimination sensor in isolated scratch state, and can create up to three bounded fix/re-verify iterations.

---

## Task Granularity Check

| Tasks | Scope | Status |
| --- | --- | --- |
| T1 | One migration ledger | PASS - granular |
| T2-T5 | One viability question/probe per task | PASS - granular |
| T6-T12 | One contract, value family, validator, or serializer module per task | PASS - granular |
| T13-T15 | One inventory/extraction responsibility per task | PASS - granular |
| T16-T19 | One storage or projection component per task | PASS - granular |
| T20-T21 | One engine integration and one CLI cut | PASS - granular |
| T22-T29 | One semantic adapter/enricher/composer/projector/integration per task | PASS - granular |
| T30-T33 | One reusable index/classifier/host per task | PASS - granular |
| T34-T39 | One detector family per task | PASS - granular |
| T40 | One relation/component aggregate projector | PASS - granular |
| T41-T47 | One migration, matrix, boundary, determinism, release, or documentation closure per task | PASS - granular |

Every code task names one primary component or cohesive directory; its spec-derived tests are part of the same commit, never a deferred test task. T41-T45 modify tests as their primary deliverable to close independently verifiable coverage matrices after production behavior exists.

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | start at T1 | PASS |
| T2-T47 | Immediately preceding task T(N-1) | T1 -> T2 -> ... -> T47 | PASS |

The detailed phase diagrams and the full execution map contain the same single dependency chain. No dependency points to a later task or phase.

---

## Test Co-location Validation

| Tasks | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Specification artifact | none + baseline run | none + full gate | PASS |
| T2-T5 | Semantic viability adapters/probes | integration | integration | PASS |
| T6-T12 | Contracts/factual domain/validation/schema | unit or unit + snapshot | unit or unit + snapshot | PASS |
| T13 | Inventory | integration | integration | PASS |
| T14-T15 | Syntax extraction | unit | unit | PASS |
| T16 | Fact storage | integration | integration | PASS |
| T17-T18 | Markdown/frontmatter projection | unit + snapshot | unit + snapshot | PASS |
| T19-T21 | Aggregate output/engine/CLI | integration | integration | PASS |
| T22-T26 | Semantic adapters/enrichers | integration | integration | PASS |
| T27 | Fact composition | unit | unit | PASS |
| T28-T30 | Aggregate/engine/index integration | integration | integration | PASS |
| T31-T33 | Classifiers and detector host | unit | unit | PASS |
| T34-T40 | Detectors and relation projection | integration | integration | PASS |
| T41-T47 | Migration/release behavior | integration or integration + snapshot | integration or integration + snapshot | PASS |

No production-code task uses `Tests: none`, and no test obligation is deferred beyond the task that introduces its behavior.

---

## Requirement Coverage

| Requirements | Tasks |
| --- | --- |
| FACT-01..07 | T6, T20, T21 |
| FACT-08..11 | T6-T9, T11, T16, T20 |
| FACT-12..17 | T7-T11, T27 |
| FACT-18..25 | T12, T16-T21, T40, T41, T45 |
| FACT-26..36 | T2-T5, T22-T29, T44 |
| FACT-37..42 | T30-T32, T40 |
| FACT-43..49 | T10, T33-T40, T43 |
| FACT-50..54 | T9, T19, T27-T28 |
| FACT-55..61 | T1, T12, T17-T18, T41-T47, mandatory Verifier |
| FACT-62..65 | T3-T5, T22-T29, T44 |
| FACT-66..69 | T8-T9, T27-T28, T31-T32, T40 |
| FACT-70 | T12, T18, T46 |

All 70 requirements have at least one implementation task and one explicit test/evidence path. FACT-61 closes only after T47's Build gate and the mandatory independent Verifier both pass.
