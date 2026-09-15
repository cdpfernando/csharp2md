# Analysis Publication Resilience Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**Standing skip — discrimination sensor**: the user runs Stryker manually; do not run the sensor's fault-injection pass. Every other Verifier step (spec-anchored coverage check, gate check, code-quality check) still runs as documented.

---

**Spec**: `.specs/features/analysis-publication-resilience/spec.md`
**Design**: `.specs/features/analysis-publication-resilience/design.md`
**Status**: Approved

---

## Test Coverage Matrix

> Confirmed at task approval. Generated from codebase, project guidelines, and spec. Guidelines found: [`CLAUDE.md`](CLAUDE.md) / [`AGENTS.md`](AGENTS.md) (retrieval-led reasoning, Roslyn API verification, multi-csproj test execution, LocalCorpus skip, quality-gate routing, standing discrimination-sensor skip), [`Directory.Build.props`](Directory.Build.props) (`TreatWarningsAsErrors`). No CI workflow exists. Existing tests are the floor for style and location; thoroughness is 1:1 to spec ACs and listed edge cases.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Pipeline failure sanitizer and orchestrator (regression) | unit + integration | Every APR-01..07 path stays green: failing stage, one sanitized line, cancellation not unexpected, abort preserves prior bytes | `tests/Csharp2Md.Analysis.Tests/Pipeline/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| CLI unpublished reporting and option surface (regression) | unit | APR-04 (stderr exactly once) and APR-08 (no new option, logger, sink, schema or artifact) | `tests/Csharp2Md.Cli.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"` |
| Domain identity (`CanonicalSymbolSignature.SplitTopLevel`) | unit | All branches; 1:1 to APR-16..23 shapes; unbalanced input throws `ArgumentException`; existing simple/generic identities stay byte-identical | `tests/Csharp2Md.Domain.Tests/Identity/**/*.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| EvidenceScope | unit | `Qualifying` returns the structural-vs-causal filter without constructing a chain; empty qualifying set does not throw; `For` still throws on empty; causal kinds keep the caller set | `tests/Csharp2Md.Analysis.Tests/Classification/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| ContainsRelationEmitter | integration | 1:1 to APR-09..15 on the real extraction path: own-scope win, document fallback, mix-of-kinds filter, omit+diagnostic, existing ownership unchanged | `tests/Csharp2Md.Analysis.Tests/Extraction/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Storage wire mapping and construction | unit | Domain → wire → Domain equality for nested signatures; unbalanced wire is `PublicationRejectedException("construction", id)` | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Retrieval guide projection | unit | Exact posting key stays backticked; sharded unknown and frontier posting families name the family without backticks; mixed exact/shard; candidate/unresolved/frontier *relation* wording unchanged; `ValidateNoAbsentKeys` still throws; two projections byte-identical | `tests/Csharp2Md.Projection.Tests/Guides/**/*.cs` | `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |
| Versioned analysis fixture | integration | Each committed file is proven, in the task that adds it, to produce the observations, facts or dispositions later tasks assert | `tests/Csharp2Md.Analysis.Tests/Fixtures/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| End-to-end CLI analyze (default budget) | integration | Full `analyze` with no budget flags against `fixtures/PublicationResilience`; package read-back; `validate`; two-run byte identity; no ceiling/exclusion weakening | `tests/Csharp2Md.Cli.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"` |
| LocalCorpus (Pitstop / eShop) | integration | Optional; skip when the clone path is absent; no golden diagnostic counts | `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs` | excluded from every gate; run after Verifier when the clone exists |
| Documentation, gitignore, decision log | none | - (build gate only; no executable behaviour) | `.specs/**`, `.gitignore` | build gate only |

## Gate Check Commands

> Confirmed at task approval. Generated from codebase. Multi-csproj `dotnet test` hits MSB1008; run each test project separately. `Category=LocalCorpus` is excluded from every gate; those tests run only after the Verifier and only when the local clones exist.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks with unit tests only, scoped to a single assembly | the matching project's command from the matrix above |
| Full | After tasks that cross Storage and Projection or touch the wire contract | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |
| Build | After phase completion, fixture tasks, pipeline/CLI tasks, and every task touching Analysis, Domain or the CLI | `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |

**Measured baseline (STATE.md, 2026-09-11)**: `dotnet build` clean, 0 warnings, 0 errors. **2072 tests passing** — Domain 563, Analysis 827, Storage 389, Cli 64, Projection 229. Confirm the live totals at Execute start and report the new total on every task; a drop is a silent deletion.

---

## Tooling

Confirmed at task approval. The routing in [`CLAUDE.md`](CLAUDE.md) applies as written. No skill is invoked speculatively outside these routes. MCP: Context7 for library docs; MSBuild binlog on build failures. GitHub unused unless a later task needs it.

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

### Phase 1: Pipeline-failure regression

Issue 01 is already on HEAD. Pin APR-01..08 on the existing discriminating tests. No production edit unless a test fails.

```
T1 -> T2
```

### Phase 2: Contains evidence selection

Extract `EvidenceScope.Qualifying`, then select `contains` evidence without ever calling `EvidenceChain.Create` on an empty set.

```
T3 -> T4
```

### Phase 3: Nested signature round-trip

One shared delimiter-aware split in Domain; Storage deletes its `<>`-only copy.

```
T5 -> T6
```

### Phase 4: Shard-aware disposition posting guidance

Copy confirmed-relation exact-vs-sharded wording onto unknown and frontier *posting* keys.

```
T7
```

### Phase 5: Versioned regression fixture

Sibling unlabeled tree (AD-029). `fixtures/SyntheticSolution` stays byte-identical. `fixtures/CertificationCorpus` labels stay untouched.

```
T8 -> T9
T8 -> T10
T8 -> T11
```

### Phase 6: Default-budget CLI and optional Pitstop

Vertical slice through live `analyze` adapters, then the optional local corpus.

```
T12 -> T13
```

---

## Task Breakdown

### Phase 1: Pipeline-failure regression

### T1: Pin the pipeline-failure regression contract

**What**: Existing pipeline sanitizer, failing-stage, cancellation and abort-preserves-package tests carry APR-01, APR-02, APR-03, APR-05, APR-06 and APR-07 traits and keep asserting those outcomes. No production change unless one of those tests fails.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineStageFailureTests.cs`
**Depends on**: None
**Reuses**: `PipelineFailureDetailTests`, `PipelineCancellationTests`, `AbortPreservesPackageTests`, `PipelineFailureDetail`
**Requirement**: APR-01, APR-02, APR-03, APR-05, APR-06, APR-07

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] `PipelineStageFailureTests` asserts `FailingStage` is the throwing stage and `Detail` contains the root exception type (APR-01)
- [x] `PipelineFailureDetailTests` plus the nested-failure case assert one sanitized line, secret/path/source/newline removal, and exception type alone when nothing safe remains (APR-02, APR-03, APR-05)
- [x] `PipelineCancellationTests` still treats cancellation as cancellation, with `FailingStage` and `Detail` null (APR-06)
- [x] `AbortPreservesPackageTests` and the failed-retry cases still leave the prior package byte-identical (APR-07)
- [x] No production file under `src/` changes in this commit unless a discriminating test failed and the fix is the minimum restore of HEAD behaviour
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` (854 passed, 0 failed)
- [x] Test count reported; Analysis 854 pass; total 2102 (Domain 566, Analysis 854, Storage 389, Cli 64, Projection 229). Live floor at Execute start was 2101 (Domain 566, Analysis 853, Storage 389, Cli 64, Projection 229) vs documented 2072.

**Tests**: unit
**Gate**: quick

**Commit**: `test(analysis): pin pipeline-failure detail to APR-01 through APR-07`

---

### T2: Pin unpublished CLI stderr and option-surface freeze

**What**: Unpublished CLI reporting still writes the safe detail exactly once on stderr, and `analyze` still exposes exactly the current product options. No new CLI option, logging framework, telemetry sink, schema field or package artifact.
**Where**: `tests/Csharp2Md.Cli.Tests/AnalyzeExitCodeTests.cs`
**Depends on**: T1
**Reuses**: `Analyze_WhenUnpublishedWithDetail_WritesDetailToStderr`; `AnalyzeOptionSurfaceTests` option freeze
**Requirement**: APR-04, APR-08

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] The unpublished-detail CLI test asserts stderr is exactly one `csharp2md:` line containing the safe detail, stdout summary unchanged, exit code 2 (APR-04)
- [x] `AnalyzeOptionSurfaceTests` still lists exactly `--allowlist`, `--max-file-reads-per-scenario`, `--output`, `--reading-budget-tokens`, `--solution` and carries an APR-08 trait
- [x] No production file under `src/` changes in this commit unless a discriminating test failed
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"` (64 passed, 0 failed)
- [x] Test count reported; Cli 64 pass; total 2102 (Domain 566, Analysis 854, Storage 389, Cli 64, Projection 229)

**Tests**: unit
**Gate**: quick

**Commit**: `test(cli): pin unpublished stderr-once and analyze option freeze`

---

### Phase 2: Contains evidence selection

### T3: Extract EvidenceScope.Qualifying

**What**: A filter that returns the observations allowed to justify a relation kind, without constructing an `EvidenceChain`. `For` becomes `EvidenceChain.Create(Qualifying(...).Select(identity))` and still throws on empty.
**Where**: `src/Csharp2Md.Analysis/Classification/EvidenceScope.cs`
**Depends on**: None
**Reuses**: the `BehavioralNoiseForStructuralRelations` / `CausalRelationKinds` tables already inlined in `For`
**Requirement**: APR-11, APR-13

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] `Qualifying(Contains, [structural, Invocation, DataAccess])` returns only the structural observations
- [x] `Qualifying(Contains, [Invocation, DataAccess])` returns empty and does not throw
- [x] `Qualifying(Invokes, callerSet)` returns the caller set unchanged
- [x] `For` still throws `ArgumentException` ("at least one observation") on an empty qualifying set
- [x] Existing `EvidenceScopeTests` stay green
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` (857 passed, 0 failed)
- [x] Test count reported; Analysis 857 pass; total 2105 (Domain 566, Analysis 857, Storage 389, Cli 64, Projection 229)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): expose EvidenceScope.Qualifying without constructing a chain`

---

### T4: Select contains evidence from qualifying scopes

**What**: Document-to-symbol `contains` uses symbol-owned qualifying observations when any exist, otherwise document-scoped qualifying observations, otherwise omits the relation and records `contains-evidence-unqualified`. Project-to-document emission stays on `EvidenceScope.For`. `DeclaredOn` primary-constructor yield stays.
**Where**: `src/Csharp2Md.Analysis/Extraction/ContainsRelationEmitter.cs`
**Depends on**: T3
**Reuses**: `EvidenceScope.Qualifying`; `SnapshotAccumulator.AddDiagnostic`; `DiagnosticRecord` kebab-case codes (`missing-project`, `unsupported-document`)
**Requirement**: APR-09, APR-10, APR-11, APR-12, APR-13, APR-14, APR-15

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A symbol that owns at least one qualifying structural observation publishes `contains` with that owner, those kinds and that cardinality, and no other scope (APR-09, APR-14). The existing `OrdersController` case stays green
- [x] A primary-constructor / builder symbol that owns only `Invocation` or `DataAccess` observations, in a document that has qualifying structural observations, publishes `contains` whose chain is non-empty, structural, and free of `Invocation` and `DataAccess` (APR-10, APR-11)
- [x] A mix of structural and behavioral observations owned by the same symbol keeps only the structural ones (APR-09, APR-11 edge case)
- [x] When neither scope has qualifying structural evidence, no confirmed `contains` edge is added for that symbol, one `contains-evidence-unqualified` diagnostic is recorded with `IdentityOrKey` equal to the symbol fact id and a one-line message naming `contains` and the missing structural evidence, and the rest of the snapshot still commits (APR-12)
- [x] `EvidenceChain.Create` is never called with an empty sequence from this emitter; the Domain non-empty invariant is unchanged (APR-13)
- [x] No fact family, observation kind, relation kind, facet, identity namespace or schema version is added (APR-15)
- [x] `TopologyEmitter` is not modified
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` (build 0 warnings; Analysis 860 passed, 0 failed; Domain 566, Storage 389, Cli 64, Projection 229)
- [x] Test count reported; Analysis 860 pass; total 2108 (Domain 566, Analysis 860, Storage 389, Cli 64, Projection 229)

**Tests**: integration
**Gate**: build

**Commit**: `feat(analysis): fall back to document-scoped structural contains evidence`

---

### Phase 3: Nested signature round-trip

### T5: Add CanonicalSymbolSignature.SplitTopLevel

**What**: A public top-level comma split that tracks `<>`, `()` and `[]` depth. Final depth not equal to 0 throws `ArgumentException`. `Create` is unchanged and still joins with `,`.
**Where**: `src/Csharp2Md.Domain/Identity/CanonicalSymbolSignature.cs`
**Depends on**: None
**Reuses**: the scan shape of Storage's current private `SplitTopLevel`, plus `()` and `[]`; `FactIdGrammar` already used by `Create`
**Requirement**: APR-16, APR-17, APR-18, APR-19, APR-20, APR-21, APR-22, APR-23

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-api-design`, `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] Named-tuple text splits on the commas between parameters, not the commas inside `(...)` (APR-16, APR-17)
- [x] Nested generic-in-tuple and tuple-in-generic text splits only at depth 0 (APR-18)
- [x] Multidimensional-array rank commas inside `[...]` are not treated as separators (APR-19)
- [x] Mixed generic, tuple and array text is deterministic across two calls (APR-20)
- [x] Existing simple and generic parameter lists still split into the same slices they do today (APR-21)
- [x] Unbalanced `)`, `>` or `]` (final depth != 0) throws `ArgumentException` (APR-22)
- [x] Identity namespace, signature fields, escaping, taxonomy version and schema version are unchanged (APR-23). `contracts/taxonomy-registry.json` stays byte-identical
- [x] `SignatureReader.HasSeparatorAtTopLevel` is not modified
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` (575 passed, 0 failed)
- [x] Test count reported; Domain 575 pass; total 2117 (Domain 575, Analysis 860, Storage 389, Cli 64, Projection 229)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(domain): split canonical signature lists with nested delimiter depth`

---

### T6: Reconstruct signatures through Domain SplitTopLevel

**What**: `WireFactMapping.SignatureFromValue` calls `CanonicalSymbolSignature.SplitTopLevel` for `parameters` and `type-arguments` and deletes the private `<>`-only splitter. `ParseParameter` stays in Storage. Unbalanced wire signatures remain structural corruption.
**Where**: `src/Csharp2Md.Storage/Mapping/WireFactMapping.cs`
**Depends on**: T5
**Reuses**: `CanonicalSymbolSignature.SplitTopLevel`; `PackageValidator.EnsureStructuralConstruction` mapping `ArgumentException` to `PublicationRejectedException("construction", id)`
**Requirement**: APR-16, APR-17, APR-18, APR-19, APR-20, APR-21, APR-22, APR-23

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:serialization`, `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Domain → wire → Domain of a named-tuple parameter, two tuple parameters, nested generic/tuple, multidimensional array, and a mixed shape all restore exact `CanonicalSymbolSignature` equality (APR-16..20)
- [x] The existing simple/generic `Symbol` round-trip in `FactRoundTripTests` stays byte-identical (APR-21)
- [x] A mutated wire signature with an unbalanced delimiter is rejected as `PublicationRejectedException` with gate `construction` and is not stored (APR-22)
- [x] No third parser is added; `ParseParameter` is the only Storage-local remainder
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` (Storage 396 passed, Projection 229 passed, 0 failed)
- [x] Test count reported; Storage 396 pass; Projection 229 pass; total 2124 (Domain 575, Analysis 860, Storage 396, Cli 64, Projection 229)

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): rehydrate nested signatures through Domain SplitTopLevel`

---

### Phase 4: Shard-aware disposition posting guidance

### T7: Make disposition posting keys shard-aware

**What**: Unknown and frontier *posting* instructions in `AppendDispositionsSection` quote the exact key in backticks only when that key exists, and otherwise name the matching shard without backticks, same voice as `AppendRelationsSection`. Candidate, unresolved-relation and frontier-relation lambdas stay.
**Where**: `src/Csharp2Md.Projection/Guides/RetrievalGuideProjector.cs`
**Depends on**: None
**Reuses**: `AppendDisposition`, `HasFamily`, `ValidateNoAbsentKeys`; `ShardedUnresolvedView` / `ShardedInvokesView` ceiling-on-planner-and-projector pattern
**Requirement**: APR-24, APR-25, APR-26, APR-27, APR-28, APR-29, APR-30, APR-31, APR-32

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] When `postings/unknowns.json` exists as an exact key, the unresolved disposition line still backticks that key (APR-24). The current `Project_DispositionsSection_ShardedUnresolvedFamily_IsRecognizedNotReportedAbsent` assertion on `` `postings/unknowns.json` `` stays, because that case shards the *relation* family, not the posting family
- [x] A new test shards the unknown *posting* family itself (same small ceiling on layout and on `Project`) and asserts the guide describes a matching unknown shard and does not contain `` `postings/unknowns.json` `` (APR-25, APR-28)
- [x] The same exact-versus-sharded rule is asserted for `postings/frontiers.json` (APR-26)
- [x] A mixed case (unknown posting family sharded, frontier posting family exact) uses shard wording for unknowns and exact-key wording for frontiers (APR-25, APR-26 edge case)
- [x] Candidate, unresolved-relation and frontier-relation instructions stay shard-aware as they are today (APR-27)
- [x] `ValidateNoAbsentKeys` still throws on a backtick-quoted missing key; no ceiling exclusion is added (APR-28, APR-29)
- [x] A missing posting family still renders "none is recognized in this package" (APR-30)
- [x] Two projections of the same view and ceiling produce byte-identical `retrieval.md` (APR-31)
- [x] Validator strictness, taxonomy, schemas and retrieval semantics are unchanged (APR-32)
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` (Projection 234 passed, Storage 396 passed, 0 failed)
- [x] Test count reported; Projection 234 pass; Storage 396 pass; total 2129 (Domain 575, Analysis 860, Storage 396, Cli 64, Projection 234). Floor after T6 was 2124 (Projection 229); 5 new T7 tests, no drop.

**Tests**: unit
**Gate**: full

**Commit**: `fix(projection): stop quoting absent unknown and frontier posting keys`

---

### Phase 5: Versioned regression fixture

### T8: Create the PublicationResilience solution skeleton

**What**: A new unlabeled `.slnx` and class-library project tree under `fixtures/PublicationResilience`, with a path helper. No `labels/` tree. `fixtures/SyntheticSolution` and `fixtures/CertificationCorpus` labels are untouched.
**Where**: `fixtures/PublicationResilience/PublicationResilience.slnx`
**Depends on**: None
**Reuses**: `fixtures/CertificationCorpus/Directory.Build.props` (SourceLink silenced); `CertificationCorpusPaths`
**Requirement**: APR-33, APR-34

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:

- [x] The solution restores and builds under `net10.0`
- [x] `PublicationResiliencePaths` (or equivalent) points at the `.slnx`
- [x] A test analyzes the empty-enough skeleton end to end and asserts a committed package with a manifest
- [x] `fixtures/SyntheticSolution` is byte-identical (`SyntheticSolutionImmutabilityTests` still passes)
- [x] `fixtures/CertificationCorpus/labels/` is untouched
- [x] `.gitignore` comment for versioned fixtures names SyntheticSolution, CertificationCorpus and PublicationResilience; eShop / eShopOnContainers stay gitignored
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` (build 0 warnings; Analysis 861 passed, 0 failed)
- [x] Test count reported; Analysis 861 pass; total 2130 (Domain 575, Analysis 861, Storage 396, Cli 64, Projection 234). Floor after T7 was 2129; 1 new T8 test, no drop.

**Tests**: integration
**Gate**: build

**Commit**: `test(fixtures): add the unlabeled PublicationResilience solution skeleton`

---

### T9: Add the invocation-only builder constructor

**What**: A primary-constructor / builder type whose own observations are invocations, living in a document that still has qualifying structural observations, so T12 can assert APR-36 on the committed tree.
**Where**: `fixtures/PublicationResilience/Publication.Lib/BuilderConstructor.cs`
**Depends on**: T8
**Reuses**: the T4 minimized builder shape; `DeclaredOn` primary-constructor yield already in `ContainsRelationEmitter`
**Requirement**: APR-36

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A test analyzes this fixture and finds the builder constructor `Symbol`
- [x] That symbol's own observations are only `Invocation` and/or `DataAccess`
- [x] The containing document has at least one qualifying structural observation
- [x] After T4, the published document-to-symbol `contains` edge for that symbol has a non-empty structural chain (asserted here once T4 is already on the branch; this task's own commit lands after T8, and Execute runs phases in order so T4 is already committed)
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` (Analysis 862 passed, 0 failed)
- [x] Test count reported; Analysis 862 pass; total 2131 (Domain 575, Analysis 862, Storage 396, Cli 64, Projection 234). Floor after T8 was 2130; 1 new T9 test, no drop.

**Tests**: integration
**Gate**: quick

**Commit**: `test(fixtures): add the invocation-only builder constructor`

---

### T10: Add nested tuple, generic and array signatures

**What**: Members whose canonical signatures carry a named tuple, a nested generic, a multidimensional array, and a mixed shape, so T12 can assert APR-37 on read-back.
**Where**: `fixtures/PublicationResilience/Publication.Lib/NestedSignatures.cs`
**Depends on**: T8
**Reuses**: the Domain shapes asserted in T5
**Requirement**: APR-37

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A test analyzes this fixture and finds at least one symbol whose signature text contains a named-tuple parameter, one with nested generics, one with a multidimensional array, and one mixed shape
- [ ] Those symbols round-trip through the published package with exact canonical identity equality (engine path; CLI path is T12)
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"`
- [ ] Test count reported; a drop is a silent deletion

**Tests**: integration
**Gate**: quick

**Commit**: `test(fixtures): add nested tuple, generic and array signatures`

---

### T11: Size unproven dispositions past the default ceiling

**What**: Enough unresolved records and open frontiers that unsplit `postings/unknowns.json` and `postings/frontiers.json` exceed `CeilingCalculator.Derive().CeilingBytes` (~32 KiB), not `ShardWriter.DefaultCeilingBytes` (1 MiB). Many small methods, not one giant type body. No labels.
**Where**: `fixtures/PublicationResilience/Publication.Lib/Volume/`
**Depends on**: T8
**Reuses**: `CeilingCalculator.Derive`; live `PackageProjector` constructed with that ceiling (same as `PublicationPipeline`)
**Requirement**: APR-38, APR-29

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Analyzing the fixture through `AnalysisEngine` + `FilesystemTransactionalStore` + `PackageProjector(CeilingCalculator.Derive().CeilingBytes)` publishes unknown and frontier posting *shards*
- [ ] The exact keys `postings/unknowns.json` and `postings/frontiers.json` are absent
- [ ] Every published artifact, including `retrieval.md`, is within the derived ceiling except the existing indivisible-single-record exemption
- [ ] No ceiling exclusion is added
- [ ] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [ ] Test count reported; a drop is a silent deletion

**Tests**: integration
**Gate**: build

**Commit**: `test(fixtures): size unknown and frontier postings past the default ceiling`

---

### Phase 6: Default-budget CLI and optional Pitstop

### T12: Certify default-budget CLI publication

**What**: `analyze` with no budget override, real `PackageProjector` and `BatchComposer`, against `fixtures/PublicationResilience`. The committed package is valid, navigable and byte-deterministic.
**Where**: `tests/Csharp2Md.Cli.Tests/PublicationResilienceAnalyzeTests.cs`
**Depends on**: T4, T6, T7, T9, T10, T11
**Reuses**: `CliInvoke.RunAsync`; `AnalyzeSuccessTests` / `ValidateCommandTests` invoke pattern; `FactualPackageReader`; `SyntheticSolutionImmutabilityTests`
**Requirement**: APR-34, APR-35, APR-36, APR-37, APR-38, APR-39, APR-40

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:run-tests`

**Done when**:

- [ ] `["analyze", "--solution", PublicationResilience.slnx, "--output", ...]` is invoked with no `--reading-budget-tokens` and no `--max-file-reads-per-scenario`
- [ ] The run commits without observation-extraction failure, structural corruption or projection-key rejection (APR-35)
- [ ] The published builder symbol has a document-to-symbol `contains` relation whose evidence is non-empty, structural and free of `Invocation` and `DataAccess` (APR-36)
- [ ] Tuple, nested-generic and multidimensional-array symbols read back with canonical identities equal to the published ones (APR-37)
- [ ] Every backtick-quoted key in `retrieval.md` exists in the same publication, including sharded unknown and frontier postings (APR-38)
- [ ] Package validation, projection validation, run-certification, manifest-reachability, determinism and artifact-ceiling gates pass on this package with no exclusions or weakened assertions (APR-39)
- [ ] Two default-budget runs produce identical package bytes (APR-40)
- [ ] `fixtures/SyntheticSolution` is still byte-identical (APR-34)
- [ ] Gate check passes: the Build command in Gate Check Commands
- [ ] Test count reported; a drop is a silent deletion

**Tests**: integration
**Gate**: build

**Commit**: `test(cli): certify default-budget analyze of PublicationResilience`

---

### T13: Add optional Pitstop LocalCorpus analyze

**What**: When a local Pitstop clone is present, analyze all 15 isolated project solutions and the full solution. Absence does not fail CI. Diagnostic counts are not golden. Pitstop is gitignored.
**Where**: `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs`
**Depends on**: T12
**Reuses**: `$XunitDynamicSkip$` skip when the solution file is absent; existing eShop / eShopOnContainers rows
**Requirement**: APR-41, APR-42

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] `fixtures/Pitstop/` is in `.gitignore` next to eShop and eShopOnContainers
- [ ] TheoryData includes the full Pitstop solution plus 15 isolated project solutions. Discover the 15 relative paths from the clone when it is present; if it is absent during Execute, still commit skippable rows so CI stays green
- [ ] Each row skips with `$XunitDynamicSkip$` when its file is missing (APR-41)
- [ ] When present, each analyze writes a package; observed diagnostic counts are not asserted as exact baselines (APR-42)
- [ ] eShop and eShopOnContainers are not added to source control
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"` (the new rows are `Category=LocalCorpus` and are excluded here by design)
- [ ] Test count reported for the non-LocalCorpus suite; a drop is a silent deletion

**Tests**: integration
**Gate**: quick

**Commit**: `test(cli): analyze Pitstop as an optional LocalCorpus`

---

## Phase Execution Map

Visual representation of task ordering. Phases run in sequence, and tasks within a phase run in order:

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6

Phase 1:  T1 ------→ T2
Phase 2:  T3 ------→ T4
Phase 3:  T5 ------→ T6
Phase 4:  T7
Phase 5:  T8 ------→ T9
          T8 ------→ T10
          T8 ------→ T11
Phase 6:  T12 ------→ T13
```

Execution is strictly sequential - there is no intra-phase parallelism. A single agent (or batch worker) works one task at a time, in order.

At Execute, the 13 tasks pack into two batches (~7-task budget, whole phases):

- Batch 1 (7 tasks): Phase 1 + Phase 2 + Phase 3 + Phase 4 (`T1`–`T7`)
- Batch 2 (6 tasks): Phase 5 + Phase 6 (`T8`–`T13`)

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: Pin pipeline-failure regression | 1 test file (traits + gap fill on existing tests) | Granular |
| T2: Pin CLI stderr and option freeze | 1 test file | Granular |
| T3: EvidenceScope.Qualifying | 1 function | Granular |
| T4: ContainsRelationEmitter selection | 1 emitter | Granular |
| T5: CanonicalSymbolSignature.SplitTopLevel | 1 function | Granular |
| T6: WireFactMapping uses Domain split | 1 mapping method | Granular |
| T7: RetrievalGuideProjector posting lambdas | 1 projector section | Granular |
| T8: PublicationResilience skeleton | 1 solution file (+ required project tree) | Granular |
| T9: Builder constructor fixture | 1 source file | Granular |
| T10: Nested signature fixture | 1 source file | Granular |
| T11: Disposition volume | 1 fixture directory | Granular |
| T12: Default-budget CLI analyze | 1 test file | Granular |
| T13: Pitstop LocalCorpus | 1 test file | Granular |

**Granularity check**: 1 component / 1 function / 1 file = Good. T8's extra `Directory.Build.props` / `.csproj` are the minimum tree a `.slnx` needs to restore, same shape as the certification-corpus skeleton.

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | no inbound arrow | Match |
| T2 | T1 | T1 -> T2 | Match |
| T3 | None | no inbound arrow | Match |
| T4 | T3 | T3 -> T4 | Match |
| T5 | None | no inbound arrow | Match |
| T6 | T5 | T5 -> T6 | Match |
| T7 | None | no inbound arrow | Match |
| T8 | None | no inbound arrow | Match |
| T9 | T8 | T8 -> T9 | Match |
| T10 | T8 | T8 -> T10 | Match |
| T11 | T8 | T8 -> T11 | Match |
| T12 | T4, T6, T7, T9, T10, T11 | T12 inbound only from T13's predecessor within Phase 6; cross-phase deps are out of the phase diagram | Match |
| T13 | T12 | T12 -> T13 | Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1: Pin pipeline-failure regression | Pipeline failure sanitizer and orchestrator | unit + integration | unit | OK (quick gate runs the whole Analysis pipeline suite that holds both) |
| T2: Pin CLI stderr and option freeze | CLI unpublished reporting and option surface | unit | unit | OK |
| T3: EvidenceScope.Qualifying | EvidenceScope | unit | unit | OK |
| T4: ContainsRelationEmitter selection | ContainsRelationEmitter | integration | integration | OK |
| T5: SplitTopLevel | Domain identity | unit | unit | OK |
| T6: WireFactMapping reconstruction | Storage wire mapping and construction | unit | unit | OK |
| T7: Disposition posting keys | Retrieval guide projection | unit | unit | OK |
| T8: Fixture skeleton | Versioned analysis fixture | integration | integration | OK |
| T9: Builder constructor | Versioned analysis fixture | integration | integration | OK |
| T10: Nested signatures | Versioned analysis fixture | integration | integration | OK |
| T11: Disposition volume | Versioned analysis fixture | integration | integration | OK |
| T12: Default-budget CLI | End-to-end CLI analyze | integration | integration | OK |
| T13: Pitstop LocalCorpus | LocalCorpus | integration | integration | OK |
