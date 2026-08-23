# Compact Relation Retrieval Index Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: activate it by name and follow its Execute flow and
Critical Rules. If the skill cannot be activated, stop and tell the user instead of proceeding.

The discrimination sensor is a standing skip for this repository. Record that skip in validation, but run
every other Verifier step: spec-anchored coverage, gate checks and code-quality review.

Each task includes its tests, passes its named gate, updates this file and the spec traceability when
applicable, and lands as one atomic Conventional Commit. Local implementation and local commits are
authorized only after task approval. Push, PR, tag and every other remote action require separate approval.

**Design**: `.specs/features/compact-retrieval-index/design.md`
**Status**: Approved

---

## Test Coverage Matrix

> Generated from `AGENTS.md`, `Directory.Build.props`, `global.json`, `Directory.Packages.props`,
> `tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj`, six relevant test files and the approved spec.
> The repository uses SDK-style `net10.0`, xUnit v2 on VSTest, warnings as errors and Verify snapshots.
> The strong default applies: every CRI acceptance criterion and listed edge case receives a direct assertion.
>
> **Authoritative current test-method count:** 1,523, discovered on 2026-08-23 with
> `dotnet test csharp2md.slnx --list-tests --no-restore`. Every task records its pre-task discovered count and
> must finish at or above that baseline; theory-expanded case counts are reported separately by the runner.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Compact index contracts and JSON encoding | unit | Every physical field, schema marker, default and removed-field contract has a direct assertion; serialized bytes are compact UTF-8 with one final LF. | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --filter "FullyQualifiedName~Csharp2Md.Core.Tests.Projection.Aggregates"` |
| Streaming scanner, metadata/posting builder and shard writer | unit | All branches and boundaries are covered, including malformed input, ordering, collisions, exact 262144 bytes, overflow, split lists and linear instrumentation. | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --filter "FullyQualifiedName~Csharp2Md.Core.Tests.Projection.Aggregates"` |
| Projector, reader and aggregate publication | unit | CRI-01..CRI-31 map 1:1 to assertions across happy, empty, corruption and failure paths; tests prove the reader never opens factual relations. | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --filter "FullyQualifiedName~Csharp2Md.Core.Tests.Projection.Aggregates"` |
| Real-output retrieval behavior | integration | Every functional Independent Test and listed edge case reads a real generated schema-2 index and compares canonical logical results or exact bytes. | `tests/Csharp2Md.Core.Tests/Analysis/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --filter "FullyQualifiedName~RelationRetrievalIndexEndToEndTests"` |
| Benchmark harness and frozen schema-1 baseline | integration | Baseline parity is byte-for-byte; process isolation, raw samples, medians, missing metrics and non-zero failure outcomes are exercised with a small deterministic test corpus. | `tests/Csharp2Md.Core.Tests/Benchmarks/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --filter "FullyQualifiedName~Csharp2Md.Core.Tests.Benchmarks"` |
| Package and version contract | integration | The packed tool carries version 4.0.0 and emits schema 2 from an installed execution outside the repository. | `tests/Csharp2Md.Core.Tests/Cli/PackagingSmokeTests.cs` | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --filter "FullyQualifiedName~PackagingSmokeTests"` |
| Project configuration, benchmark report and spec documents | none | Build/format/full-suite gates validate config and documents; the canonical report additionally passes the strict benchmark gate. | `*.slnx`, `*.props`, `benchmarks/**/*.csproj`, `.specs/**` | build or benchmark gate |

## Gate Check Commands

> Generated from the repository's SDK/test configuration and confirmed as VSTest with xUnit v2.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Unit-test task | `dotnet test tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj --filter "FullyQualifiedName~Csharp2Md.Core.Tests.Projection.Aggregates"` |
| Full | Integration-test task | `dotnet test csharp2md.slnx` |
| Benchmark | Canonical scale proof | `dotnet run --project benchmarks/Csharp2Md.RetrievalIndex.Benchmarks/Csharp2Md.RetrievalIndex.Benchmarks.csproj -c Release -- compare --report .specs/features/compact-retrieval-index/benchmark-report.json` |
| Build | Phase completion or config/documentation task | `dotnet build csharp2md.slnx -c Release; dotnet format csharp2md.slnx --verify-no-changes; dotnet test csharp2md.slnx` |

---

## Execution Plan

Phases and tasks execute strictly in order.

### Phase 1: Freeze the schema-1 baseline

```text
T1 -> T2
```

### Phase 2: Build compact-index foundations

```text
T2 -> T3 -> T4 -> T5 -> T6 -> T7
```

### Phase 3: Publish and consume schema 2

```text
T7 -> T8 -> T9 -> T10 -> T11
```

### Phase 4: Measure and release the breaking format

```text
T11 -> T12 -> T13 -> T14 -> T15 -> T16
```

---

## Task Breakdown

### T1: Scaffold the isolated benchmark harness [x]

**What**: Add the tooling-only console project, its solution registration and the internal-access boundary needed to benchmark retrieval projection without adding a production assembly.
**Where**: `benchmarks/Csharp2Md.RetrievalIndex.Benchmarks/`
**Depends on**: None
**Reuses**: `csharp2md.slnx`, central package management and the existing `Csharp2Md.Core` project reference pattern.
**Requirement**: CRI-32, CRI-33, CRI-35

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:dotnet-project-structure`, `dotnet-skills:package-management`

**Done when**:

- [ ] The benchmark is an executable `net10.0` project under `benchmarks/`, references `Csharp2Md.Core` and uses no external benchmark package.
- [ ] `csharp2md.slnx` builds the benchmark without creating another production project under `src/`.
- [ ] The benchmark assembly can call only the internal projection surface required by the approved design.
- [ ] The discovered test-method count is recorded before the task and does not decrease.
- [ ] Build gate passes.

**Tests**: none
**Gate**: build
**Commit**: `build(benchmark): scaffold retrieval index harness`

**Execution evidence**: Pre-task discovered test methods: 1,523. Discrimination sensor skipped by standing project override.

---

### T2: Freeze and verify the schema-1 baseline [x]

**What**: Copy the verified schema-1 projection into a benchmark-only adapter and prove byte-for-byte parity with production before production changes.
**Where**: `benchmarks/Csharp2Md.RetrievalIndex.Benchmarks/Schema1RetrievalIndexProjector.cs`
**Depends on**: T1
**Reuses**: Current `RetrievalIndexProjector`, `BoundedShardWriter` and recording-file fixtures.
**Requirement**: CRI-32, CRI-33, CRI-34, CRI-35

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:modern-csharp-coding-standards`, `dotnet-test:code-testing-agent`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] The adapter contains the current schema-1 behavior and remains isolated from production code.
- [ ] A deterministic reference corpus produces the same relative paths and exact bytes through the adapter and the current production projector.
- [ ] The parity fixture pins hashes or bytes so later schema-1 baseline drift fails visibly.
- [ ] Integration tests live under `tests/Csharp2Md.Core.Tests/Benchmarks/` and cover populated and empty runs.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Full gate passes.

**Tests**: integration
**Gate**: full
**Commit**: `test(benchmark): freeze schema one retrieval baseline`

**Execution evidence**: Pre-task discovered test methods: 1,523. Discrimination sensor skipped by standing project override.

---

### T3: Replace whole-file reads with a streaming file seam [x]

**What**: Split aggregate file reading from writing and expose read-only streams throughout factual validation and index projection.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/CanonicalAggregateWriter.cs`
**Depends on**: T2
**Reuses**: Existing `IAggregateFileWriter`, `LocalAggregateFileWriter` and recording adapters.
**Requirement**: CRI-14

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:api-design`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] `IAggregateFileReader` exposes `Exists` and `OpenRead`; `IAggregateFileWriter` inherits it and no longer exposes `Read(): byte[]`.
- [ ] Local reads return a read-only sequential `FileStream`; every test adapter returns an independent readable stream.
- [ ] Fragment SHA-256 validation consumes the stream and still rejects missing or mismatched fragments before a manifest is published.
- [ ] Unit tests prove progressive reads and disposal without relying on `File.ReadAllBytes`.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Quick gate passes.

**Tests**: unit
**Gate**: quick
**Commit**: `refactor(index): stream aggregate file reads`

**Execution evidence**: Pre-task discovered test methods: 1,525. Quick aggregate gate passed with 122 tests. Discrimination sensor skipped by standing project override.

---

### T4: Define the compact schema-2 contracts [x]

**What**: Add normalized schema-2 manifest, shard, metadata, posting, UNKNOWN and logical-reader models plus a dedicated compact JSON context alongside the still-running schema-1 contracts.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/RetrievalIndexContracts.cs`
**Depends on**: T3
**Reuses**: `ManifestAnalysis`, existing snake-case JSON conventions and logical `RetrievalRelationEntry` semantics.
**Requirement**: CRI-13, CRI-15, CRI-17, CRI-19, CRI-22, CRI-26, CRI-27, CRI-28, CRI-29

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:type-design-performance`, `dotnet-skills:api-design`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] Manifest descriptors represent relation ordinal ranges, metadata ordinal ranges and posting SHA-256 ranges without raw keys in the manifest.
- [ ] Relation records store known details/candidates once, evidence stores document ordinals and origin metadata is normalized.
- [ ] Manifest owns analysis/trust/restore fields; summary omits them and uses `indexed_endpoint_count`, complete resolution axes and no percentages.
- [ ] Every required physical artifact has `schema_version: 2` and `analysis_run_id` where the approved design requires it.
- [ ] Source-generated compact serialization writes snake_case UTF-8 without indentation; existing aggregate formatting remains unchanged.
- [ ] Schema-1 contracts remain available until T8 replaces the production projector, so the T4 gate is green in isolation.
- [ ] Unit tests assert exact fields, defaults, omissions and logical promotion of schema-1 details/candidates.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Quick gate passes.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(index)!: define compact retrieval schema`

**Execution evidence**: Pre-task discovered test methods: 1,525. Quick aggregate gate passed with 122 tests. Discrimination sensor skipped by standing project override.

---

### T5: Write exact bounded UTF-8 shards

**What**: Implement byte-counted relation, metadata and posting shard packing over once-serialized UTF-8 records.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/BoundedUtf8ShardWriter.cs`
**Depends on**: T4
**Reuses**: The existing 262144-byte contract, ordinal comparers and `IAggregateFileWriter`.
**Requirement**: CRI-07, CRI-08, CRI-09, CRI-10, CRI-11, CRI-12, CRI-13

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:type-design-performance`, `dotnet-test:code-testing-agent`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] Exact accounting includes envelope, commas, closing bytes and one LF; totals of 262144 pass and 262145 fail.
- [ ] Oversized singleton errors name record kind, identity/key and the 262144 ceiling.
- [ ] Accepted records are serialized once; instrumentation has no prefix-reserialization path.
- [ ] Oversized posting lists split into consecutive segments without first serializing a rejected complete list.
- [ ] Paths use flat sequential shard names and no lookup key becomes a directory.
- [ ] Unit tests cover empty, exact-boundary, delimiter overflow, oversized singleton, split postings, N/N+1 counters and hash-range ordering.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Quick gate passes.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(index): write exact bounded utf8 shards`

---

### T6: Scan factual fragments progressively

**What**: Add an incremental UTF-8 scanner that extracts document metadata and one canonical relation value at a time from manifest-referenced streams.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/FactualFragmentScanner.cs`
**Depends on**: T5
**Reuses**: Factual schema-6 property names, AD-018 ordering and `JsonReaderState`.
**Requirement**: CRI-14, CRI-18, CRI-24, CRI-25, CRI-26

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:type-design-performance`, `dotnet-test:code-testing-agent`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] Document fragments stop after the documents array and do not read or retain source-section text.
- [ ] Relation fragments yield one disposable relation value at a time while preserving unconsumed UTF-8 bytes across buffers.
- [ ] The scanner rejects malformed JSON, missing required fields and a second non-empty relation array with fragment context.
- [ ] Large source text and buffer-boundary tests prove progressive consumption without whole-document DOM or `ReadAllBytes`.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Quick gate passes.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(index): stream factual relation fragments`

---

### T7: Normalize metadata and build ordinal postings

**What**: Build dense document/origin tables, five compact posting maps, UNKNOWN groups and corrected summary metrics from streamed relation observations.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/CompactRetrievalIndexBuilder.cs`
**Depends on**: T6
**Reuses**: Existing first-evidence project rule, UNKNOWN priority, `FactResolution`, `ResolutionMethod` and ordinal comparers.
**Requirement**: CRI-02, CRI-03, CRI-06, CRI-11, CRI-20, CRI-21, CRI-24, CRI-25, CRI-28, CRI-29

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:type-design-performance`, `dotnet-test:code-testing-agent`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] Five families retain only lookup keys and relation ordinals; a missing target omits only the target posting.
- [ ] Known documents and factual origins receive deterministic dense ordinals and conflicting document metadata fails.
- [ ] UNKNOWN groups contain ordinals once and sort by proven entry point, descending impact and ordinal identity.
- [ ] Metrics contain every resolution and resolution-method bucket separately and count distinct endpoints under the corrected name.
- [ ] Unit tests cover repeated metadata, evidence-only documents, absent targets, all enum buckets, UNKNOWN ties and 10 versus 10000 unique keys.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Build gate passes.

**Tests**: unit
**Gate**: build
**Commit**: `feat(index): build compact ordinal postings`

---

### T8: Project schema 2 and publish its manifest last

**What**: Replace the materializing schema-1 projector with the streaming schema-2 coordinator and remove the obsolete shard implementation.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/RetrievalIndexProjector.cs`
**Depends on**: T7
**Reuses**: T3-T7 components, current analysis-run hashing and aggregate publication ordering.
**Requirement**: CRI-01, CRI-03, CRI-06, CRI-10, CRI-15, CRI-17, CRI-18, CRI-19, CRI-20, CRI-21, CRI-22, CRI-24, CRI-25, CRI-26, CRI-27, CRI-28, CRI-29

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:type-design-performance`, `dotnet-test:code-testing-agent`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] Each relation payload is serialized once in ordinal relation shards and all postings contain ordinals only.
- [ ] Relation ids must be strictly increasing, unique and equal to `header.id` before an ordinal is assigned.
- [ ] `analysis_run_id` includes the schema-2 marker and is invariant to manifest collection order.
- [ ] Empty input publishes valid manifest, summary, UNKNOWNs and entry points with no relation/posting shards.
- [ ] All artifacts are complete before `raw/index/manifest.json` is written; projection failure publishes no index manifest and never writes facts.
- [ ] The schema-1 `BoundedShardWriter` and obsolete aggregate-context registrations are removed after the frozen adapter protects the baseline.
- [ ] Unit and existing integration tests cover populated, empty, reordered, duplicate, decreasing, header-mismatched, opaque-extension and multi-evidence inputs.
- [ ] Existing schema-1 integration expectations are migrated or frozen in the benchmark during this task; no known test is left broken for T11 to repair.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Build gate passes.

**Tests**: integration
**Gate**: build
**Commit**: `feat(index)!: project compact retrieval artifacts`

---

### T9: Reconstruct selective queries through the reader

**What**: Turn the reader into the schema-2 consumer that locates posting ranges, joins ordinal shards and returns complete logical relations and UNKNOWN groups.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/RetrievalIndexReader.cs`
**Depends on**: T8
**Reuses**: Schema-2 descriptors, `IAggregateFileReader`, SHA-256 and existing logical entry models.
**Requirement**: CRI-04, CRI-05, CRI-16, CRI-23, CRI-24, CRI-25, CRI-26, CRI-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:api-design`, `dotnet-test:code-testing-agent`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] `Open` rejects every schema except 2 and reports received and expected versions.
- [ ] `Query` opens only candidate posting, relation and metadata shards and never discovers or opens `raw/facts/`.
- [ ] Split posting segments and theoretical hash collisions are resolved by exact key comparison; results are unique and ordinal.
- [ ] `ReadUnknownGroups` reconstructs complete relations while preserving cause, source, observed text, count and impact.
- [ ] Wrong run ids, missing/duplicate relation ordinals and missing document/origin ordinals fail with family, key, ordinal and shard context.
- [ ] Unit tests compare every logical field with canonicalized schema-1 results and cover all listed corruption edge cases.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Quick gate passes.

**Tests**: unit
**Gate**: quick
**Commit**: `feat(index)!: resolve compact retrieval queries`

---

### T10: Integrate compact publication with aggregate output

**What**: Wire the schema-2 projector through the canonical aggregate writer while preserving factual validation and commit-marker ordering.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/CanonicalAggregateWriter.cs`
**Depends on**: T9
**Reuses**: Existing `WritePrepared`, `ValidateFragments`, manifest-last tests and `OutputWriter.PrepareRun` replacement behavior.
**Requirement**: CRI-05, CRI-14, CRI-19, CRI-30

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:modern-csharp-coding-standards`, `dotnet-test:code-testing-agent`, `dotnet-test:assertion-quality`, `dotnet-skills:dotnet-slopwatch`

**Done when**:

- [ ] A successful aggregate run publishes a reachable schema-2 index and writes the factual manifest after index projection returns.
- [ ] Missing or hash-invalid factual fragments advertise neither index nor factual manifests.
- [ ] A replacement run leaves none of the four removed catalogues or schema-1 per-key directories.
- [ ] Writer tests assert exact call/write order and unchanged factual bytes through the streaming file seam.
- [ ] Slopwatch reports no new finding in the feature diff.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Build gate passes.

**Tests**: unit
**Gate**: build
**Commit**: `feat(index): publish compact index with aggregates`

---

### T11: Prove compact retrieval against real output

**What**: Replace schema-1 end-to-end expectations with independent schema-2 query, normalization, determinism and absence proofs over real generated output.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/RelationRetrievalIndexEndToEndTests.cs`
**Depends on**: T10
**Reuses**: Existing real-output fixture, frozen schema-1 canonicalizer and two-run determinism pattern.
**Requirement**: CRI-01, CRI-04, CRI-05, CRI-06, CRI-12, CRI-15, CRI-18, CRI-19, CRI-20, CRI-21, CRI-22, CRI-23, CRI-24, CRI-25, CRI-26, CRI-27, CRI-28, CRI-29, CRI-30, CRI-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:code-testing-agent`, `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns`, `dotnet-skills:crap-analysis`, `dotnet-test:crap-score`

**Done when**:

- [ ] All five lookup families return the same ids and complete logical payloads as the frozen schema-1 reference without factual reads.
- [ ] Two input permutations produce identical run ids, ordinals, paths, postings and bytes.
- [ ] Physical inspection proves single relation payloads, normalized documents/origins, known details, one UNKNOWN artifact and removed fields/catalogues.
- [ ] Empty input, missing target, 10/10000 keys, split posting, opaque extensions and multiple evidence paths match the spec outcomes.
- [ ] Assertion-quality and anti-pattern review have no Critical or High finding; targeted CRAP evidence is recorded for changed complex methods.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Full gate passes.

**Tests**: integration
**Gate**: full
**Commit**: `test(index): prove compact retrieval end to end`

---

### T12: Measure both formats in isolated processes

**What**: Implement deterministic corpus generation, child-process samples, metric collection, median comparison and versioned JSON reporting for schema 1 and schema 2.
**Where**: `benchmarks/Csharp2Md.RetrievalIndex.Benchmarks/Program.cs`
**Depends on**: T11
**Reuses**: Frozen schema-1 adapter, production schema-2 projector, `Process`, `Stopwatch` and SHA-256 corpus identities.
**Requirement**: CRI-32, CRI-33, CRI-35

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:modern-csharp-coding-standards`, `dotnet-skills:type-design-performance`, `dotnet-test:code-testing-agent`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] `compare` generates exactly 25000 deterministic relations and launches each variant in a fresh process after one warm-up.
- [ ] Three raw elapsed-time and peak-working-set samples per variant, total bytes, file count and environment metadata reach a versioned JSON report.
- [ ] Medians are calculated deterministically and all four comparisons require schema 2 to be strictly smaller.
- [ ] Missing, equal or regressed metrics are all named and cause a non-zero exit.
- [ ] Integration tests use a small corpus to cover success shape, process isolation, raw samples and every comparison failure without running the full benchmark.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Build gate passes.

**Tests**: integration
**Gate**: build
**Commit**: `feat(benchmark): compare retrieval index formats`

---

### T13: Record the canonical scale result

**What**: Execute the approved 25000-relation methodology and commit the raw samples, medians, environment and strict comparison verdict.
**Where**: `.specs/features/compact-retrieval-index/benchmark-report.json`
**Depends on**: T12
**Reuses**: T12 `compare` command and the approved benchmark report schema.
**Requirement**: CRI-32, CRI-33, CRI-34, CRI-35

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] The report records the 25000-relation corpus, runtime, OS, architecture, methodology and every raw sample.
- [ ] Schema 2 is strictly lower in total bytes, file count, median projection time and median peak working set.
- [ ] The benchmark command exits zero against the committed report and identifies no absent metric.
- [ ] Benchmark gate passes.

**Tests**: none
**Gate**: benchmark
**Commit**: `perf(index): record compact retrieval benchmark`

---

### T14: Publish the local version-4 package contract

**What**: Bump the breaking package version to 4.0.0 and prove the packed tool emits schema 2 from outside the repository.
**Where**: `Directory.Build.props`
**Depends on**: T13
**Reuses**: AD-007 and the existing installed-tool `PackagingSmokeTests` workflow.
**Requirement**: CRI-15, CRI-16, CRI-17

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:package-management`, `dotnet-test:code-testing-agent`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] `<Version>` is 4.0.0 and the AD-007 comment names the schema-2 derived-output break.
- [ ] Packaging smoke assertions select `csharp2md.4.0.0.nupkg` and inspect an emitted schema-2 retrieval manifest.
- [ ] No tag, push, PR or publication occurs.
- [ ] The discovered test-method count is at least the pre-task count.
- [ ] Build gate passes.

**Tests**: integration
**Gate**: build
**Commit**: `chore(release)!: version compact retrieval schema`

---

### T15: Close requirement traceability

**What**: Map every CRI requirement to exact test/report evidence and record final coverage and quality-gate results for the Verifier.
**Where**: `.specs/features/compact-retrieval-index/spec.md`
**Depends on**: T14
**Reuses**: Existing feature traceability tables, AD-023 and the local roadmap mirror.
**Requirement**: CRI-01 through CRI-35

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:test-gap-analysis`, `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns`

**Done when**:

- [ ] Every CRI row cites an exact covering test assertion or benchmark-report field; no requirement remains only design-mapped.
- [ ] Test-gap analysis finds no uncovered acceptance criterion or listed edge case.
- [ ] Assertion-quality and anti-pattern reviews find no Critical or High issue in changed tests.
- [ ] Final discovered test-method count and runner case result are recorded without hiding pre-existing failures.
- [ ] Build gate passes.

**Tests**: none
**Gate**: build
**Commit**: `docs(index): close compact retrieval traceability`

---

### T16: Update the local roadmap state

**What**: Mark the local feature issue implemented, preserve the roadmap's verifier gate and record the execution handoff without changing remote state.
**Where**: `llmwiki-reverse-engineering-roadmap.md`
**Depends on**: T15
**Reuses**: `.scratch/llmwiki-roadmap/issues/` mirror conventions and `.specs/STATE.md` handoff format.
**Requirement**: CRI-01 through CRI-35

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] The local issue mirror records all implementation tasks complete and points to the pending independent validation report.
- [ ] The roadmap row remains gated on Verifier PASS instead of claiming completion early.
- [ ] `.specs/STATE.md` names the exact next step and contains no stale task or benchmark status.
- [ ] No push, PR, tag or publication occurs.
- [ ] Build gate passes.

**Tests**: none
**Gate**: build
**Commit**: `docs(roadmap): stage compact retrieval verification`

---

## Phase Execution Map

```text
Phase 1: T1 -> T2
Phase 2: T2 -> T3 -> T4 -> T5 -> T6 -> T7
Phase 3: T7 -> T8 -> T9 -> T10 -> T11
Phase 4: T11 -> T12 -> T13 -> T14 -> T15 -> T16
```

Execution is sequential. At Execute, the four phases pack into two whole-phase batches: T1-T7 and T8-T16.
Because the feature exceeds one task-budgeted batch, `tlc-spec-driven` requires an explicit user choice before
dispatching batch workers. A fresh Verifier runs after T16; the repository-level discrimination-sensor skip
remains in force.

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1 | One benchmark project boundary | OK |
| T2 | One frozen schema-1 adapter | OK |
| T3 | One streaming file seam | OK |
| T4 | One schema-2 contract family | OK |
| T5 | One bounded UTF-8 shard writer | OK |
| T6 | One factual fragment scanner | OK |
| T7 | One metadata/posting accumulator | OK |
| T8 | One projection coordinator | OK |
| T9 | One selective reader | OK |
| T10 | One aggregate publication integration | OK |
| T11 | One real-output behavior proof | OK |
| T12 | One benchmark runner | OK |
| T13 | One canonical benchmark report | OK |
| T14 | One package-version contract | OK |
| T15 | One traceability closure | OK |
| T16 | One roadmap/handoff update | OK |

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | no inbound arrow | Match |
| T2 | T1 | T1 -> T2 | Match |
| T3 | T2 | T2 -> T3 | Match |
| T4 | T3 | T3 -> T4 | Match |
| T5 | T4 | T4 -> T5 | Match |
| T6 | T5 | T5 -> T6 | Match |
| T7 | T6 | T6 -> T7 | Match |
| T8 | T7 | T7 -> T8 | Match |
| T9 | T8 | T8 -> T9 | Match |
| T10 | T9 | T9 -> T10 | Match |
| T11 | T10 | T10 -> T11 | Match |
| T12 | T11 | T11 -> T12 | Match |
| T13 | T12 | T12 -> T13 | Match |
| T14 | T13 | T13 -> T14 | Match |
| T15 | T14 | T14 -> T15 | Match |
| T16 | T15 | T15 -> T16 | Match |

No task depends on a later phase.

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Project configuration | none | none | OK |
| T2 | Benchmark baseline adapter | integration | integration | OK |
| T3 | Aggregate file seam | unit | unit | OK |
| T4 | Index contracts/JSON | unit | unit | OK |
| T5 | Bounded shard writer | unit | unit | OK |
| T6 | Factual scanner | unit | unit | OK |
| T7 | Compact builder | unit | unit | OK |
| T8 | Retrieval projector and broken-contract migration | integration | integration | OK |
| T9 | Retrieval reader | unit | unit | OK |
| T10 | Aggregate publication | unit | unit | OK |
| T11 | Real-output retrieval | integration | integration | OK |
| T12 | Benchmark runner | integration | integration | OK |
| T13 | Benchmark report | none | none | OK |
| T14 | Package/version contract | integration | integration | OK |
| T15 | Traceability documents | none | none | OK |
| T16 | Roadmap/handoff documents | none | none | OK |

Every code-producing task carries the test type required by the matrix. No test work is deferred to a later
task.
