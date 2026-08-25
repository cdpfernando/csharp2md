# Engine Bootstrap Specification

## Problem Statement

`Csharp2Md.Domain` now defines the taxonomy, but nothing consumes it. The only executable code in the repository is the legacy pipeline: `Csharp2Md.Core` mixes orchestration, MSBuild evaluation, Roslyn compilation, fact storage, validation and Markdown rendering across 122 files, and the CLI exposes a markdown/topic-shaped option surface bound to that pipeline. Six downstream workstreams are blocked because the assemblies they are supposed to fill — `Csharp2Md.Analysis`, `Csharp2Md.Storage`, `Csharp2Md.Projection` — do not exist, and because every one of them would otherwise have to negotiate its seams against legacy code that is scheduled for deletion.

## Goals

- [ ] Create `Csharp2Md.Analysis`, `Csharp2Md.Storage` and `Csharp2Md.Projection`, and rewrite `Csharp2Md.Cli`, with the AD-006 boundaries enforced by tests rather than by convention.
- [ ] Ship an executable eight-stage pipeline skeleton behind one analysis facade and one transactional storage port, so workstreams 3 through 6 fill in stages instead of rewiring the orchestrator.
- [ ] Prove the seam invariants that a stub can genuinely prove: stage order and substitutability, cancellation, staging/commit/abort, manifest-published-last, `1..N` solution isolation and order-independent determinism.
- [ ] Remove the legacy pipeline, its tests, its schemas and its CLI contract from the working tree, leaving a committed port ledger so later workstreams can find what they need to port.
- [ ] Close the `ConfirmedRelation.Create` limitation carried forward from workstream 1, while the domain still has no production consumer to migrate.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Any Roslyn or MSBuild use, including solution loading, compilation and the out-of-process BuildHost | Owned by workstream 4 `roslyn-observation-extraction`; AD-003 governs it when it arrives |
| Real Inventory, authorized-root enforcement and symlink-escape rejection | Owned by workstream 4; this feature's Inventory stage is a stub |
| Wire contracts, JSON schemas, sharding, on-disk staging and physical commit | Owned by workstream 3 `factual-storage`; this feature defines only the port and an in-memory adapter |
| The physical package layout and the manifest's content | Owned by workstream 3; this feature fixes only that the manifest is published last |
| Any concrete extractor, classifier, promotion rule or framework support declaration | Owned by workstreams 4 and 5A–5D; this feature defines only the stage seam they plug into |
| Catalogs, postings, source locators, Markdown and retrieval scenarios | Owned by workstream 6 `retrieval-projections`; this feature creates the assembly, not its output |
| Cross-solution correlation and the batch manifest's content | Owned by workstream 7 `multi-solution-composition`; this feature fixes only per-solution isolation |
| The `validate` and `compose` verbs and the full analyze option surface | Owned by workstream 8 `generator-cli-projections-certification` |
| Coverage metrics, run certification and engine certification | Owned by workstreams 3 and 8; there is nothing to measure yet |
| Re-establishing the Roslyn viability probes and the CLI security-boundary tests | Recorded in the port ledger for workstream 4 and workstream 8; both need code this feature does not create |
| Performance baselines and scale budgets | Owned by workstream 8; a stub pipeline would produce a meaningless baseline |
| Changes to `Csharp2Md.Domain` beyond the relation-construction signature | Workstream 1 is closed; only the explicitly carried-forward limitation is reopened |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here — nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| How alive the skeleton is | A walking skeleton: all eight stages execute per solution against an in-memory transactional adapter, writing nothing to disk | Confirmed with the user; a compile-only skeleton validates none of the seam invariants the roadmap demands of an integrated checkpoint, while an on-disk one would define a package layout workstream 3 owns | y |
| Legacy removal strategy | Delete `Csharp2Md.Core`, `Csharp2Md.Core.Tests`, the benchmark project, `schemas/` and the old CLI body outright, and commit a port ledger | Confirmed with the user; quarantined dead code stays visible to agents and greps, while the ledger preserves retrievability without keeping the code | y |
| Provisional CLI surface | One `analyze` verb with a repeatable `--solution`, and nothing the skeleton cannot honor | Confirmed with the user; an option that parses and does nothing is a contract that lies | y |
| Legacy test disposition | Delete all 1,093 legacy test attributes; the port ledger names the Roslyn sanitation probes and the CLI security-boundary tests as items to re-establish | Confirmed with the user; they assert contracts AD-002 breaks, and keeping any would keep a Roslyn dependency in this workstream | y |
| Carried-forward relation-guard limitation | Widen `ConfirmedRelation.Create` now so the three shape guards gain production call sites | Confirmed with the user; the domain has zero production consumers today, so the change costs no migration, and deferring it makes workstream 4 or 5B pay | y |
| CLI packaging | The CLI project stays packaged as a dotnet tool with command name `csharp2md` | Stated as an assumption and not objected to; nothing about the replacement changes how the tool is installed | y |
| Fixture retention | `fixtures/SyntheticSolution` survives the excision | Stated as an assumption and not objected to; it is input data, not legacy contract, and workstream 4 onward needs it | y |
| Central package pruning | `Directory.Packages.props` drops versions that lose their last consumer, keeping the fixture-only packages | Stated as an assumption and not objected to; workstream 4 re-adds Roslyn when it can use it | y |
| Exit-code contract | `0` for a completed run including unknowns and candidates, `1` for an invalid invocation, `2` for a structural failure that aborted publication | Stated as an assumption and not objected to; it follows the quality document's separation of legitimate degradation from abort | y |
| Where the storage port interface is declared | Left to `design.md`, constrained only by the requirement that `Csharp2Md.Analysis` holds no reference to `Csharp2Md.Storage` | The spec fixes the observable boundary without pre-empting whether the port lives in Analysis or in a shared abstraction | n |
| Test project layout for the new assemblies | Left to `design.md` | Whether each assembly gets its own test project or the skeleton shares one is a structural choice with no user-visible outcome | n |
| Duplicate solution paths in one request | Rejected with the duplicate named, rather than silently de-duplicated | A silent de-duplication would make the batch's result count disagree with the invocation, which is harder to diagnose than a rejection | n |
| What a stub stage reports | Zero produced facts, zero observations and zero relations, reported explicitly rather than omitted | An omitted count is indistinguishable from an unimplemented stage; an explicit zero is a claim later workstreams will falsify as they fill stages in | n |
| Port ledger location and format | A single committed Markdown file recording, per removed area, the former path, a one-line description and the last commit SHA | One file is greppable and diffable; per-area files would fragment the very index the ledger exists to be | n |
| Meaning of "no filesystem write" | Asserted by snapshotting the working tree and the process temp directory around a run and comparing, not by static inspection of `System.IO` usage | A static check would pass while a transitive dependency wrote, and would fail on harmless path arithmetic | n |
| Canonical batch ordering | Results are ordered by the solution's logical relative path, not by input order | Input-order dependence is forbidden by the standing determinism constraint, and logical relative path is the identity component AD-013 already uses for solutions | n |

**Open questions:** none — all resolved or logged above.

---

## Implicit-requirement dimensions sweep

Large scope, so every dimension resolves to a requirement or an explicit exclusion.

| Dimension | Coverage |
| --- | --- |
| Input validation and bounds | ENG-33, ENG-34, ENG-40, ENG-41, ENG-42 — an empty solution set, a duplicate path and a non-existent path are all rejected by name |
| Failure and partial-failure states | ENG-21, ENG-27, ENG-28, ENG-36 — a stage failure aborts that solution only, discards its staged fragments, and leaves the rest of the batch running |
| Idempotency, retry, duplicate handling | ENG-25, ENG-30, ENG-34 — commit happens exactly once per solution, staging order does not change committed content, and duplicate inputs are rejected |
| Auth boundaries and rate limits | N/A because the engine is a local in-process tool with no network surface; the security boundary that does exist, authorized-root enforcement, belongs to workstream 4's real Inventory |
| Concurrency and ordering | ENG-18, ENG-22, ENG-23, ENG-30, ENG-37 — stage order is fixed, cancellation is observed between stages, and both staging order and input order are proven not to affect the result |
| Data lifecycle and expiry | ENG-24, ENG-26 — publication is atomic and the manifest is published last, so a previously published output survives a failed run. Retention and expiry of generated packages is N/A because no package is written here |
| Observability | ENG-21, ENG-31, ENG-45 — the failing stage is named, every stage reports its produced counts, and diagnostics go to standard error separately from the summary on standard output |
| External-dependency failure | N/A because ENG-06 gives this feature's production assemblies no external dependency: no Roslyn, no MSBuild, no filesystem, no network |
| State-transition integrity | ENG-24, ENG-27, ENG-28, ENG-29 — staging, commit and abort are distinct operations, a run reaches exactly one terminal state, and unknowns and candidates are a committed outcome rather than a failure |

---

## User Stories

### P1: Target assembly topology with enforced boundaries ⭐ MVP

**User Story**: As an engine contributor, I want the four target assemblies to exist with their AD-006 boundaries enforced by tests so that workstreams 3 through 6 fill in modules that cannot quietly reach across a seam.

**Why P1**: Nothing else in this feature or the next four workstreams can be authored until the assemblies exist, and a boundary that is only documented is a boundary that erodes.

**Acceptance Criteria**:
1. The solution SHALL contain projects named `Csharp2Md.Analysis`, `Csharp2Md.Storage` and `Csharp2Md.Projection`, each targeting `net10.0` and each listed in `csharp2md.slnx` under the `src` folder. (ENG-01)
2. The `Csharp2Md.Cli` project SHALL target `net10.0`, remain packaged as a dotnet tool with command name `csharp2md`, and be listed in `csharp2md.slnx` under the `src` folder. (ENG-02)
3. The `Csharp2Md.Analysis` project SHALL declare no project reference to `Csharp2Md.Storage`, `Csharp2Md.Projection` or `Csharp2Md.Cli`. (ENG-03)
4. The `Csharp2Md.Projection` project SHALL declare no project reference to `Csharp2Md.Analysis`, `Csharp2Md.Storage` or `Csharp2Md.Cli`. (ENG-04)
5. The `Csharp2Md.Domain` project SHALL continue to declare no package reference and no project reference. (ENG-05)
6. IF any production project in the solution declares a package reference whose identifier begins with `Microsoft.CodeAnalysis` or `Microsoft.Build` THEN the boundary test SHALL fail naming the project and the package. (ENG-06)
7. IF any public type on the `Csharp2Md.Analysis` surface exposes a type from `Microsoft.CodeAnalysis`, `Microsoft.Build` or `System.Text.Json` THEN the boundary test SHALL fail naming the offending type. (ENG-07)
8. The `Csharp2Md.Cli` project SHALL declare project references only to `Csharp2Md.Analysis`, `Csharp2Md.Storage` and `Csharp2Md.Projection`, and SHALL NOT declare one to `Csharp2Md.Domain`. (ENG-08)
9. WHEN `csharp2md.slnx` is built with `TreatWarningsAsErrors` enabled THEN the build SHALL succeed. (ENG-09)

**Independent Test**: Build the solution and run a boundary test that reads each project file's declared references and each new assembly's public surface, asserting the forbidden combinations fail with the offending name reported.

---

### P1: One analysis facade over an eight-stage pipeline ⭐ MVP

**User Story**: As a workstream 3–6 author, I want a single analysis entry point running a declared, substitutable eight-stage pipeline so that I replace one stage's implementation without touching the orchestrator or any other stage.

**Why P1**: AD-006 makes the single high-level interface the defining property of the Analysis module, and the stage seam is the contract every later workstream plugs into.

**Acceptance Criteria**:
1. The `Csharp2Md.Analysis` assembly SHALL expose exactly one public interface for performing an analysis. (ENG-10)
2. The `Csharp2Md.Analysis` assembly SHALL expose a public surface limited to its entry interface, its request type, its result type and the transactional storage port. (ENG-11)
3. The `Csharp2Md.Analysis` assembly SHALL NOT expose any pass, classifier, adapter, extractor or stage type as a public type. (ENG-12)
4. WHEN an analysis runs for one solution THEN the pipeline SHALL execute its stages in the order Inventory, Semantic Analysis, Observation Extraction, Classification and Promotion, Validation and Coverage, Persistence, Retrieval Projection, Batch Composition. (ENG-13)
5. WHEN a stage implementation is substituted for another THEN the orchestrator SHALL execute the substitute in that stage's position with no change to the orchestrator. (ENG-14)
6. WHEN the bootstrapped pipeline completes a run THEN every stage SHALL report zero produced facts, zero produced observations and zero produced relations. (ENG-15)
7. WHEN the bootstrapped pipeline completes a run THEN no file and no directory SHALL have been created, modified or deleted in the working tree or the process temporary directory. (ENG-16)
8. The analysis entry interface SHALL accept a cancellation token. (ENG-17)
9. WHEN cancellation is requested during a run THEN the pipeline SHALL stop before beginning the next stage and SHALL NOT commit. (ENG-18)
10. IF a stage fails THEN the pipeline SHALL skip the remaining stages for that solution and SHALL report the failing stage by name. (ENG-19)

**Independent Test**: Run the facade with a recording probe substituted for each stage in turn, asserting the recorded execution order, the substitution taking effect, the zero counts, and a cancellation and a stage failure each producing the specified outcome.

---

### P1: Transactional storage port with atomic publication ⭐ MVP

**User Story**: As a workstream 3 author, I want staging, commit and abort to be distinct port operations with publication ordering already fixed so that I implement a physical adapter against a contract the engine already exercises.

**Why P1**: AD-006 requires Analysis to write through a transactional port, and the abort-on-corruption rule in the quality document is only meaningful if the port distinguishes the three operations from the start.

**Acceptance Criteria**:
1. The transactional storage port SHALL expose staging, commit and abort as three distinct operations. (ENG-20)
2. The `Csharp2Md.Storage` assembly SHALL provide an in-memory adapter implementing the transactional storage port. (ENG-21)
3. WHEN a solution's analysis completes with no structural failure THEN the pipeline SHALL commit its staged fragments through the port exactly once. (ENG-22)
4. WHEN a commit succeeds THEN the manifest SHALL be published after every other staged artifact. (ENG-23)
5. IF structural corruption is reported during the validation stage THEN the pipeline SHALL abort the commit and SHALL leave any previously published output unchanged. (ENG-24)
6. IF a stage fails after staging has begun THEN the port SHALL discard every fragment staged for that solution. (ENG-25)
7. WHEN a run produces unknowns, candidates or open frontiers but no structural corruption THEN the pipeline SHALL commit normally. (ENG-26)
8. WHEN the same fragments are staged in two different orders THEN the committed content SHALL be identical. (ENG-27)
9. The `Csharp2Md.Storage` assembly SHALL NOT classify, promote or interpret any fragment it receives. (ENG-28)

**Independent Test**: Drive the port directly and through the pipeline, asserting exactly-one commit, manifest-last ordering, discard-on-failure, commit-despite-unknowns, and byte-identical committed content across two staging orders.

---

### P1: Multi-solution isolation and determinism ⭐ MVP

**User Story**: As an operator analyzing several solutions in one invocation, I want each solution analyzed and committed independently so that one broken solution neither corrupts nor blocks the others.

**Why P1**: AD-008 makes per-solution isolation a structural property, and building it in later would mean unwinding shared state from every stage added in between.

**Acceptance Criteria**:
1. The analysis request SHALL accept one or more solution paths. (ENG-29)
2. IF the request contains no solution path THEN the facade SHALL reject it naming the missing input. (ENG-30)
3. IF the request contains the same solution path more than once THEN the facade SHALL reject it naming the duplicate. (ENG-31)
4. WHEN more than one solution is requested THEN each SHALL be analyzed and committed independently of the others. (ENG-32)
5. IF one solution's analysis fails THEN every remaining solution SHALL still be analyzed and committed. (ENG-33)
6. WHEN the same set of solutions is supplied in two different orders THEN each solution's result SHALL be identical and the results SHALL be ordered by logical relative path rather than by input order. (ENG-34)
7. WHEN more than one solution is analyzed THEN each SHALL receive its own pipeline context instance carrying no state from another solution. (ENG-35)
8. The bootstrapped pipeline SHALL NOT materialize any compilation, symbol index or graph shared across solutions. (ENG-36)

**Independent Test**: Analyze three synthetic solutions where the second is forced to fail, asserting the other two commit, the failure is isolated, contexts are distinct, and a shuffled input order yields identical per-solution results in the same canonical order.

---

### P1: Provisional analyze CLI ⭐ MVP

**User Story**: As an operator, I want a single honest `analyze` verb so that every option I am offered is one the engine can currently act on.

**Why P1**: The legacy option surface is bound to a pipeline being deleted in this same feature; leaving it in place would leave the tool advertising behavior that no longer exists.

**Acceptance Criteria**:
1. The CLI SHALL expose exactly one verb, named `analyze`. (ENG-37)
2. The `analyze` verb SHALL accept a repeatable `--solution` option and SHALL require at least one occurrence. (ENG-38)
3. IF `analyze` is invoked with no `--solution` option THEN the CLI SHALL exit with code 1 and write a message naming the missing option to standard error. (ENG-39)
4. IF a supplied `--solution` path does not exist THEN the CLI SHALL exit with code 1 and write a message naming that path to standard error. (ENG-40)
5. WHEN a run completes with no structural failure THEN the CLI SHALL exit with code 0. (ENG-41)
6. IF a structural failure aborts publication for any requested solution THEN the CLI SHALL exit with code 2. (ENG-42)
7. WHEN a run reports unknowns, candidates or open frontiers but no structural failure THEN the CLI SHALL exit with code 0. (ENG-43)
8. The CLI SHALL write diagnostics to standard error and the run summary to standard output. (ENG-44)
9. The CLI SHALL NOT expose an option named `--topic`, `--domain`, `--manifest`, `--output`, `--trust`, `--include-source-generators` or `--analysis-timeout`. (ENG-45)

**Independent Test**: Invoke the built tool with a valid multi-solution request, an empty request, a non-existent path and a forced structural failure, asserting the exit code, the stream each message lands on, and the absence of every removed option.

---

### P1: Legacy excision with a port ledger ⭐ MVP

**User Story**: As an agent or contributor working in this repository, I want the legacy pipeline gone and a ledger of where it went so that I neither reason about doomed code nor lose the infrastructure later workstreams intend to port.

**Why P1**: The roadmap makes removal part of this workstream's outcome, and every workstream that starts while Core is still present inherits its seams.

**Acceptance Criteria**:
1. The repository SHALL contain no `src/Csharp2Md.Core` directory. (ENG-46)
2. The repository SHALL contain no `tests/Csharp2Md.Core.Tests` directory. (ENG-47)
3. The repository SHALL contain no `benchmarks/Csharp2Md.RetrievalIndex.Benchmarks` directory and no `schemas` directory. (ENG-48)
4. The `csharp2md.slnx` file SHALL list only `Csharp2Md.Domain`, `Csharp2Md.Analysis`, `Csharp2Md.Storage`, `Csharp2Md.Projection`, `Csharp2Md.Cli` and their test projects. (ENG-49)
5. The repository SHALL retain the `fixtures/SyntheticSolution` directory. (ENG-50)
6. IF `Directory.Packages.props` declares a package version with no consumer in the solution or in `fixtures/` THEN the package-hygiene test SHALL fail naming the package. (ENG-51)
7. The repository SHALL contain a committed port ledger recording, for every removed legacy area, its former path, a one-line description of its responsibility and the last commit SHA in which it existed. (ENG-52)
8. The port ledger SHALL name the Roslyn sanitation probes and the CLI security-boundary tests as items a later workstream must re-establish. (ENG-53)
9. WHEN the full test suite is run over `csharp2md.slnx` THEN it SHALL report zero failing tests. (ENG-54)

**Independent Test**: Assert the absence of each removed path, parse `csharp2md.slnx` for its project list, run the package-hygiene check, read the ledger for the two named items, and run the whole suite green.

---

### P1: Reachable relation shape guards ⭐ MVP

**User Story**: As a workstream 4 or 5B author, I want the relation shape guards enforced by the domain's own construction API so that I inherit enforcement rather than a convention I have to remember.

**Why P1**: Workstream 1's validation recorded these four requirements as verified with a spec-precision gap; this is the last moment the fix costs no consumer migration.

**Acceptance Criteria**:
1. WHEN a confirmed relation whose registered shape requires a callable is constructed through the domain's public relation-construction API with a source that is not a `Symbol` carrying the callable facet THEN the domain SHALL reject the construction. (ENG-55)
2. WHEN a `targets` relation is constructed through the domain's public relation-construction API with a target that is not an inbound boundary operation, a deployment unit or an external system THEN the domain SHALL reject the construction. (ENG-56)
3. WHEN an `operates-on` relation is constructed through the domain's public relation-construction API with a target that is not a data object or a data field THEN the domain SHALL reject the construction. (ENG-57)
4. IF syntactic evidence is supplied through the domain's public relation-construction API where the registry requires semantic evidence THEN the domain SHALL reject the construction. (ENG-58)
5. The domain SHALL invoke each of the three relation shape guards from at least one call site reachable through its public relation-construction API. (ENG-59)
6. WHEN the registry emitter runs after this change THEN `contracts/taxonomy-registry.json` SHALL remain byte-identical to its committed content. (ENG-60)
7. The workstream 1 traceability rows for TAX-46, TAX-50, TAX-51 and TAX-53 SHALL read `Verified` with no spec-precision gap recorded. (ENG-61)

**Independent Test**: Construct each illegal relation through the public API only — never through the guard helpers — and assert a named rejection; then run the drift gate and read the four updated traceability rows.

---

## Edge Cases

- IF a request contains no solution path THEN the facade SHALL reject it naming the missing input (ENG-30).
- IF a request repeats one solution path THEN the facade SHALL reject it naming the duplicate (ENG-31).
- IF one solution of many fails THEN the remaining solutions SHALL still commit (ENG-33).
- IF a stage fails after staging began THEN every fragment staged for that solution SHALL be discarded (ENG-25).
- IF structural corruption is reported THEN the commit SHALL abort and previously published output SHALL survive (ENG-24).
- WHEN a run yields only unknowns and candidates THEN it SHALL still commit and exit 0 (ENG-26, ENG-43).
- WHEN cancellation arrives mid-run THEN the pipeline SHALL stop before the next stage without committing (ENG-18).
- WHEN solutions are supplied in a shuffled order THEN results SHALL be identical and canonically ordered (ENG-34).
- IF a supplied `--solution` path does not exist THEN the CLI SHALL exit 1 naming the path (ENG-40).
- IF a pruned package regains no consumer THEN the package-hygiene test SHALL fail naming it (ENG-51).

---

## Requirement Traceability

Each requirement gets a unique ID for tracking across design, tasks, and validation.

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| ENG-01 | P1: Target assembly topology with enforced boundaries | Tasks | Verified |
| ENG-02 | P1: Target assembly topology with enforced boundaries | Tasks | In Tasks |
| ENG-03 | P1: Target assembly topology with enforced boundaries | Tasks | Verified |
| ENG-04 | P1: Target assembly topology with enforced boundaries | Tasks | Verified |
| ENG-05 | P1: Target assembly topology with enforced boundaries | Tasks | Verified |
| ENG-06 | P1: Target assembly topology with enforced boundaries | Tasks | Implementing |
| ENG-07 | P1: Target assembly topology with enforced boundaries | Tasks | Verified |
| ENG-08 | P1: Target assembly topology with enforced boundaries | Tasks | In Tasks |
| ENG-09 | P1: Target assembly topology with enforced boundaries | Tasks | Verified |
| ENG-10 | P1: One analysis facade over an eight-stage pipeline | Tasks | Verified |
| ENG-11 | P1: One analysis facade over an eight-stage pipeline | Tasks | Verified |
| ENG-12 | P1: One analysis facade over an eight-stage pipeline | Tasks | Verified |
| ENG-13 | P1: One analysis facade over an eight-stage pipeline | Tasks | In Tasks |
| ENG-14 | P1: One analysis facade over an eight-stage pipeline | Tasks | In Tasks |
| ENG-15 | P1: One analysis facade over an eight-stage pipeline | Tasks | In Tasks |
| ENG-16 | P1: One analysis facade over an eight-stage pipeline | Tasks | In Tasks |
| ENG-17 | P1: One analysis facade over an eight-stage pipeline | Tasks | Verified |
| ENG-18 | P1: One analysis facade over an eight-stage pipeline | Tasks | In Tasks |
| ENG-19 | P1: One analysis facade over an eight-stage pipeline | Tasks | In Tasks |
| ENG-20 | P1: Transactional storage port with atomic publication | Tasks | Verified |
| ENG-21 | P1: Transactional storage port with atomic publication | Tasks | Verified |
| ENG-22 | P1: Transactional storage port with atomic publication | Tasks | Implementing |
| ENG-23 | P1: Transactional storage port with atomic publication | Tasks | Verified |
| ENG-24 | P1: Transactional storage port with atomic publication | Tasks | Verified |
| ENG-25 | P1: Transactional storage port with atomic publication | Tasks | Verified |
| ENG-26 | P1: Transactional storage port with atomic publication | Tasks | In Tasks |
| ENG-27 | P1: Transactional storage port with atomic publication | Tasks | Verified |
| ENG-28 | P1: Transactional storage port with atomic publication | Tasks | Verified |
| ENG-29 | P1: Multi-solution isolation and determinism | Tasks | Verified |
| ENG-30 | P1: Multi-solution isolation and determinism | Tasks | Verified |
| ENG-31 | P1: Multi-solution isolation and determinism | Tasks | Verified |
| ENG-32 | P1: Multi-solution isolation and determinism | Tasks | In Tasks |
| ENG-33 | P1: Multi-solution isolation and determinism | Tasks | In Tasks |
| ENG-34 | P1: Multi-solution isolation and determinism | Tasks | In Tasks |
| ENG-35 | P1: Multi-solution isolation and determinism | Tasks | In Tasks |
| ENG-36 | P1: Multi-solution isolation and determinism | Tasks | In Tasks |
| ENG-37 | P1: Provisional analyze CLI | Tasks | In Tasks |
| ENG-38 | P1: Provisional analyze CLI | Tasks | In Tasks |
| ENG-39 | P1: Provisional analyze CLI | Tasks | In Tasks |
| ENG-40 | P1: Provisional analyze CLI | Tasks | In Tasks |
| ENG-41 | P1: Provisional analyze CLI | Tasks | In Tasks |
| ENG-42 | P1: Provisional analyze CLI | Tasks | In Tasks |
| ENG-43 | P1: Provisional analyze CLI | Tasks | In Tasks |
| ENG-44 | P1: Provisional analyze CLI | Tasks | In Tasks |
| ENG-45 | P1: Provisional analyze CLI | Tasks | In Tasks |
| ENG-46 | P1: Legacy excision with a port ledger | Tasks | In Tasks |
| ENG-47 | P1: Legacy excision with a port ledger | Tasks | In Tasks |
| ENG-48 | P1: Legacy excision with a port ledger | Tasks | In Tasks |
| ENG-49 | P1: Legacy excision with a port ledger | Tasks | In Tasks |
| ENG-50 | P1: Legacy excision with a port ledger | Tasks | In Tasks |
| ENG-51 | P1: Legacy excision with a port ledger | Tasks | In Tasks |
| ENG-52 | P1: Legacy excision with a port ledger | Tasks | In Tasks |
| ENG-53 | P1: Legacy excision with a port ledger | Tasks | In Tasks |
| ENG-54 | P1: Legacy excision with a port ledger | Tasks | In Tasks |
| ENG-55 | P1: Reachable relation shape guards | Tasks | Verified |
| ENG-56 | P1: Reachable relation shape guards | Tasks | Verified |
| ENG-57 | P1: Reachable relation shape guards | Tasks | Verified |
| ENG-58 | P1: Reachable relation shape guards | Tasks | Verified |
| ENG-59 | P1: Reachable relation shape guards | Tasks | Verified |
| ENG-60 | P1: Reachable relation shape guards | Tasks | Verified |
| ENG-61 | P1: Reachable relation shape guards | Tasks | Verified |

**ID format:** `ENG-[NUMBER]`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 61 total, 61 mapped to tasks, 0 unmapped

---

## Success Criteria

- [ ] `dotnet build csharp2md.slnx` succeeds with `TreatWarningsAsErrors` and the solution contains no legacy project.
- [ ] `dotnet test` over the solution reports zero failing tests, closing the known `MigrationLedgerTests` failure by deletion.
- [ ] `csharp2md analyze --solution A --solution B` executes all eight stages for both solutions, exits 0, and writes nothing to disk.
- [ ] A recording probe substituted for any single stage is executed in that stage's position with no orchestrator change.
- [ ] A forced structural failure in one solution aborts only that solution's commit while the other commits, and the process exits 2.
- [ ] Shuffling the input order of the same solution set produces identical per-solution results in the same canonical order.
- [ ] A boundary test proves no production assembly references Roslyn or MSBuild, the Analysis public surface is limited to its four declared types, and the CLI holds no direct reference to the domain.
- [ ] The port ledger names every removed legacy area with its last commit SHA, including the Roslyn sanitation probes and the CLI security-boundary tests.
- [ ] TAX-46, TAX-50, TAX-51 and TAX-53 are rejected through the domain's public relation-construction API, and `contracts/taxonomy-registry.json` is unchanged.
- [ ] Every requirement ID ENG-01 through ENG-61 has at least one test asserting a spec-defined outcome.
