# Relation Retrieval Index Tasks

## Execution Protocol

Implement these tasks with the tlc-spec-driven skill and follow its Execute flow and critical rules.
The discrimination sensor is a standing skip for this repository. Record that skip in validation, but run
every other verifier check.

**Design**: .specs/features/relation-retrieval-index/design.md
**Status**: Draft

---

## Test Coverage Matrix

> Generated from AGENTS.md, Directory.Build.props, the existing xUnit/Verify test suite and the feature
> specification. Tests use xUnit in tests/Csharp2Md.Core.Tests; Nullable and warnings-as-errors are enabled.
>
> **Authoritative current test-method count:** 1,523, discovered on 2026-08-22 with `dotnet test csharp2md.slnx --list-tests --no-restore | Select-String '^\s{4}Csharp2Md\.Core\.Tests\.' | Measure-Object`. The full runner separately reports 1,950 expanded test cases because theories generate multiple cases. Earlier T1--T10 before/after count claims are superseded; they are not a trustworthy baseline.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Factual metadata and JSON contracts | unit | Every schema field, default and generated-origin branch maps to a concrete assertion; schema-sync remains exact. | tests/Csharp2Md.Core.Tests/Facts/**/*.cs | dotnet test csharp2md.slnx |
| Aggregate index projection and shard writer | unit | Every RRI contract branch, boundary and failure path has a direct assertion. | tests/Csharp2Md.Core.Tests/Projection/Aggregates/**/*.cs | dotnet test csharp2md.slnx |
| Aggregate writer integration | unit | Output paths, manifests and no-partial-manifest failure are asserted against written bytes. | tests/Csharp2Md.Core.Tests/Projection/Aggregates/**/*.cs | dotnet test csharp2md.slnx |
| Pipeline retrieval behaviour | integration | Every P1 independent test and applicable edge case reads the produced index without loading the relation aggregate. | tests/Csharp2Md.Core.Tests/Analysis/**/*.cs | dotnet test csharp2md.slnx |
| Specification, state and roadmap documents | none | Build, format and full tests protect documentation-only changes. | .specs/** and .scratch/** | build gate |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Unit-test task | dotnet test csharp2md.slnx |
| Full | Integration-test task | dotnet test csharp2md.slnx |
| Build | Final task in a phase or documentation-only task | dotnet build csharp2md.slnx -c Release; dotnet format csharp2md.slnx --verify-no-changes; dotnet test csharp2md.slnx |

---

## Execution Plan

### Phase 1: Factual provenance contract

```text
T1 → T2 → T3
```

### Phase 2: Derived retrieval index

```text
T3 → T4 → T5 → T6 → T7
```

### Phase 3: Independent behaviour proof

```text
T7 → T8 → T9
```

### Phase 4: Traceability

```text
T9 → T10
```

## Task Breakdown

### T1: Record factual schema version decision

**What**: Record AD-022, superseding only AD-017's fragment-version statement and fixing the schema 5 to 6 reason.
**Where**: .specs/STATE.md
**Depends on**: None
**Reuses**: AD-017 and the project decision record format.
**Requirement**: RRI-10, RRI-18, RRI-21

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] AD-022 states that generated-origin metadata is an additive factual field and that the factual schema changes from 5 to 6.
- [x] AD-017 is marked superseded only for its fragment-version statement; its remaining scope remains active.
- [x] The documentation names the backward-read default of false.
- [x] Build and format passed. Historical test-count figures are superseded by T12's authoritative discovery; the narrowly scoped MSBuild-flake waiver remains recorded.

**Tests**: none
**Gate**: build
**Commit**: docs(state): version generated-origin metadata

---

### T2: Add generated-origin domain metadata

**What**: Add the small metadata value and source-header detection needed to classify generated origin before source text is discarded.
**Where**: src/Csharp2Md.Core/Facts/Metadata/GeneratedOrigin.cs
**Depends on**: T1
**Reuses**: Evidence validation and SyntaxFactExtractor's existing source-text input.
**Requirement**: RRI-21

**Tools**:

- MCP: NONE
- Skill: dotnet-skills:csharp-coding-standards, dotnet-skills:csharp-type-design-performance

**Done when**:

- [x] A source beginning with a case-insensitive auto-generated header yields generated origin; ordinary source yields not detected.
- [x] Facts and evidence can carry an explicit boolean without changing fact identities.
- [x] Unit tests cover generated, ordinary and no-source defaults in tests/Csharp2Md.Core.Tests/Facts/Metadata/GeneratedOriginTests.cs.
- [x] Quick gate passed with the approved known-flake waiver. Historical test-count figures are superseded by T12's authoritative discovery.

**Tests**: unit
**Gate**: quick
**Commit**: feat(facts): capture generated source origin

---

### T3: Version and serialize generated-origin metadata

**What**: Map generated-origin metadata into factual JSON, update the schema to version 6 and document the two resolution fields.
**Where**: src/Csharp2Md.Core/Facts/Serialization/FactualJsonContracts.cs
**Depends on**: T2
**Reuses**: FactualJsonMapper, FactualSchemaSyncTests and the approved factual JSON snapshot.
**Requirement**: RRI-09, RRI-10, RRI-18, RRI-19, RRI-21

**Tools**:

- MCP: NONE
- Skill: dotnet-skills:csharp-coding-standards, dotnet-test:assertion-quality

**Done when**:

- [x] Header and evidence JSON emit generated_origin and readers accept its absence as false.
- [x] FactualJsonSerializer.SchemaVersion and schemas/facts.schema.json both read 6.
- [x] The schema explicitly distinguishes proof-quality resolution from relation resolution_method.
- [x] Schema-sync and JSON snapshot tests cover exact required fields, version and descriptions.
- [x] Build and format passed with the approved known-flake waiver. Historical test-count figures are superseded by T12's authoritative discovery.

**Tests**: unit
**Gate**: build
**Commit**: feat(facts): version generated-origin serialization

---

### T4: Define retrieval-index JSON contracts

**What**: Add source-generated, versioned contracts for manifests, shard envelopes, relation entries, evidence, catalogues, summaries and UNKNOWN groups.
**Where**: src/Csharp2Md.Core/Projection/Aggregates/RetrievalIndexContracts.cs
**Depends on**: T3
**Reuses**: AggregateContracts, AggregateJsonContext and RelationFactJson field names.
**Requirement**: RRI-02, RRI-07, RRI-08, RRI-09, RRI-10, RRI-11, RRI-12, RRI-15, RRI-16, RRI-17

**Tools**:

- MCP: NONE
- Skill: dotnet-skills:csharp-api-design, dotnet-skills:csharp-type-design-performance

**Done when**:

- [x] Index schema version 1 and deterministic analysis_run_id occur in manifest and shard contracts.
- [x] Entry, provenance and extension fields represent every spec-required lookup, evidence and compatibility value.
- [x] Summary contracts hold analysis metadata, counts and exact/dynamic/unresolved percentages by kind and partition.
- [x] Unit tests serialize representative contracts with snake-case fields and no accidental computed members.
- [x] Quick gate passed with the approved known-flake waiver. Historical test-count figures are superseded by T12's authoritative discovery.

**Tests**: unit
**Gate**: quick
**Commit**: feat(index): define retrieval contracts

---

### T5: Write bounded deterministic shards

**What**: Implement the shard splitter that writes canonically ordered entry envelopes within the 262144-byte ceiling.
**Where**: src/Csharp2Md.Core/Projection/Aggregates/BoundedShardWriter.cs
**Depends on**: T4
**Reuses**: IAggregateFileWriter and aggregate UTF-8 serialization rules.
**Requirement**: RRI-03, RRI-04

**Tools**:

- MCP: NONE
- Skill: dotnet-skills:csharp-coding-standards, dotnet-skills:csharp-type-design-performance, dotnet-test:assertion-quality

**Done when**:

- [x] Entries exactly at the size boundary are accepted and entries one byte over begin a new numbered shard.
- [x] A one-entry oversized envelope raises a deterministic exception with family, key and relation id.
- [x] Path key hashing and descriptor ordering are byte-stable for reordered input.
- [x] Unit tests cover empty input, exact boundary, overflow, oversized singleton and deterministic ordering.
- [x] Quick gate passed with the approved known-flake waiver. Historical test-count figures are superseded by T12's authoritative discovery.

**Tests**: unit
**Gate**: quick
**Commit**: feat(index): write bounded relation shards

---

### T6: Project persisted fragments into retrieval entries

**What**: Stream manifest-referenced factual bytes into compact relation entries, project catalogues, summary metrics and ordered UNKNOWN groups.
**Where**: src/Csharp2Md.Core/Projection/Aggregates/RetrievalIndexProjector.cs
**Depends on**: T5
**Reuses**: FactualManifest, ManifestFragment, RelationProjector wire values and SHA-256 fragment hashes.
**Requirement**: RRI-01, RRI-02, RRI-06, RRI-08, RRI-09, RRI-11, RRI-12, RRI-13, RRI-14, RRI-15, RRI-16, RRI-17, RRI-18, RRI-19, RRI-20

**Tools**:

- MCP: NONE
- Skill: dotnet-skills:csharp-coding-standards, dotnet-skills:csharp-type-design-performance, dotnet-test:assertion-quality

**Done when**:

- [x] The projector reads persisted fragment references and never invokes the typed factual deserializer for relation/evidence compatibility input.
- [x] It emits five lookup families, a deterministic analysis_run_id, per-run quality metrics, provenance and zero-valued empty output.
- [x] It preserves relation/evidence unknown JSON properties as opaque extensions and preserves separate same-semantic relations with distinct evidence/context.
- [x] It groups UNKNOWNs by reason, source and observed target text, then orders by proven entry point, impact and ordinal tie-breaker without inventing targets or entry points.
- [x] Unit tests cover missing target, missing evidence project, syntax-only limits, extension round-trip, dedup preservation and generated-origin propagation.
- [x] Quick gate passed with the approved known-flake waiver. Historical test-count figures are superseded by T12's authoritative discovery.

**Tests**: unit
**Gate**: quick
**Commit**: feat(index): project persisted relation fragments

---

### T7: Integrate the index into aggregate output

**What**: Invoke the retrieval projector from the canonical aggregate writer and publish the index only after factual fragment validation succeeds.
**Where**: src/Csharp2Md.Core/Projection/Aggregates/CanonicalAggregateWriter.cs
**Depends on**: T6
**Reuses**: ValidateFragments, the factual manifest commit marker and existing writer test doubles.
**Requirement**: RRI-01, RRI-03, RRI-04, RRI-07, RRI-08

**Tools**:

- MCP: NONE
- Skill: dotnet-skills:csharp-coding-standards, dotnet-test:assertion-quality, dotnet-skills:slopwatch

**Done when**:

- [x] A successful writer run creates raw/index/manifest.json, summary.json and all required zero or populated catalogues.
- [x] A missing or hash-invalid factual fragment prevents both factual and index manifests from being advertised.
- [x] The writer leaves raw/facts bytes unchanged while producing raw/index bytes from their validated references.
- [x] Writer tests assert the output paths and the failure ordering against bytes, not only mock calls.
- [x] Build and format passed; Slopwatch was clean for T7 files, apart from five pre-existing out-of-scope `Task.Delay` warnings. Historical test-count figures are superseded by T12's authoritative discovery.

**Tests**: unit
**Gate**: build
**Commit**: feat(index): publish retrieval index with aggregates

---

### T8: Prove selective retrieval and run metadata end to end

**What**: Add integration coverage proving the independent lookup and analysis-limit stories against a real synthetic run.
**Where**: tests/Csharp2Md.Core.Tests/Analysis/RelationRetrievalIndexEndToEndTests.cs
**Depends on**: T7
**Reuses**: RelationResolverEndToEndFixture and V3DeterminismFixture run-once patterns.
**Requirement**: RRI-05, RRI-06, RRI-07, RRI-08, RRI-09, RRI-10, RRI-11, RRI-12, RRI-13

**Tools**:

- MCP: NONE
- Skill: dotnet-test:code-testing-agent, dotnet-test:assertion-quality

**Done when**:

- [x] The test resolves Orders.Status through entities and one target shard without opening the relation aggregate.
- [x] Two identical-input runs have identical relation ids and analysis_run_id values.
- [x] The summary has exact/dynamic/unresolved percentages by kind and partition, indexed-symbol count, zero mapped entry points and explicit syntax-only limits.
- [x] Every index evidence record identifies generator version, project, document, fragment hash and source coordinates.
- [x] Full gate passed with the approved known-flake waiver. Historical test-count figures are superseded by T12's authoritative discovery.

**Tests**: integration
**Gate**: full
**Commit**: test(index): prove selective relation retrieval

---

### T9: Prove compatibility, UNKNOWN priority and generated origin

**What**: Add focused integration tests for extension preservation, UNKNOWN grouping/order, distinct evidence and generated-source facts.
**Where**: tests/Csharp2Md.Core.Tests/Analysis/RelationRetrievalIndexEndToEndTests.cs
**Depends on**: T8
**Reuses**: T8's real-output fixture plus projection fixtures for synthetic unknown and extension inputs.
**Requirement**: RRI-14, RRI-15, RRI-16, RRI-17, RRI-18, RRI-19, RRI-20, RRI-21

**Tools**:

- MCP: NONE
- Skill: dotnet-test:code-testing-agent, dotnet-test:assertion-quality, dotnet-test:test-anti-patterns

**Done when**:

- [x] An unknown relation/evidence field survives projector ingestion in extensions.
- [x] Two semantically similar relations at different locations remain two index entries.
- [x] UNKNOWNs group occurrences by the three required keys and sort by impact with deterministic ties.
- [x] Generated source emits true and ordinary source emits false in factual and index provenance.
- [x] Build Release, format and the full suite passed. Historical test-count figures are superseded by T12's authoritative discovery.

**Tests**: integration
**Gate**: build
**Commit**: test(index): cover compatibility and unknown priority

---

### T10: Close implementation traceability

**What**: Mark requirement coverage with exact test evidence, update the local issue phase and roadmap status, then record the final suite count.
**Where**: .specs/features/relation-retrieval-index/spec.md
**Depends on**: T9
**Reuses**: relation-resolver and component-graph traceability tables.
**Requirement**: RRI-01 through RRI-21

**Tools**:

- MCP: NONE
- Skill: dotnet-test:test-gap-analysis, dotnet-test:assertion-quality, dotnet-test:test-anti-patterns

**Done when**:

- [x] Every RRI requirement is mapped to an exact covering test assertion or a specified documentation evidence line.
- [x] The coverage line carries the final test count and no requirement remains silently unmapped.
- [x] The issue phase is Implemented and both roadmap rows remain Pending until the independent verifier returns PASS.
- [x] Build Release and format passed; the full suite passed. Historical test-count figures are superseded by T12's authoritative discovery.

**Tests**: none
**Gate**: build
**Commit**: docs(index): close implementation traceability

---

### T11: Prove exact shard and manifest boundary contracts

**What**: Strengthen existing index tests with the specified 262144/262145-byte outcomes and manifest provenance assertions found missing by independent validation.
**Where**: tests/Csharp2Md.Core.Tests/Projection/Aggregates/BoundedShardWriterTests.cs and tests/Csharp2Md.Core.Tests/Analysis/RelationRetrievalIndexEndToEndTests.cs
**Depends on**: T10
**Reuses**: BoundedShardWriter default-boundary and real-output fixture patterns.
**Requirement**: RRI-03, RRI-04, RRI-07; exact-boundary edge case

**Tools**:

- MCP: NONE
- Skill: dotnet-test:code-testing-agent, dotnet-test:assertion-quality

**Done when**:

- [x] Tests assert that shards at the actual 262144-byte default limit are accepted and a 262145-byte entry fails with the deterministic family, key and relation identity.
- [x] The real index manifest asserts `schema_version: 1` and an `analysis_run_id` matching the relation shards.
- [x] Tests assert specified values rather than a dynamically selected substitute boundary.
- [x] Build Release and format passed. The full suite had only the approved MSBuild flake; its isolated retry passed 1/1.

**Tests**: integration
**Gate**: build
**Commit**: test(index): prove shard boundary and manifest contract

---

### T12: Prove UNKNOWN priority and catalogue provenance

**What**: Close independent-validator gaps for entry-point-first UNKNOWN ordering, proven catalogue contents, multi-document evidence ordinal preservation and test-count traceability.
**Where**: src/Csharp2Md.Core/Projection/Aggregates/RetrievalIndexProjector.cs, tests/Csharp2Md.Core.Tests/Analysis/RelationRetrievalIndexEndToEndTests.cs and .specs/features/relation-retrieval-index/spec.md
**Depends on**: T11
**Reuses**: T9 unknown/catalogue fixture patterns and the test runner's authoritative discovered-count output.
**Requirement**: RRI-15, RRI-16; multi-document-evidence edge case

**Tools**:

- MCP: NONE
- Skill: dotnet-test:code-testing-agent, dotnet-test:assertion-quality, dotnet-test:test-anti-patterns

**Done when**:

- [x] A proven entry point sorts before higher-impact non-entry-point UNKNOWN groups; impact and ordinal ties remain deterministic.
- [x] Each populated catalogue entry is asserted to be derived from matching persisted facts, and empty families remain empty without proof.
- [x] Two evidence documents for one relation preserve two provenance entries in ordinal order.
- [x] `tasks.md` and `spec.md` record the authoritative current test-count method and value without a conflicting before/after claim; no trailing whitespace remains.
- [x] Build Release and format passed. The full suite had only the approved MSBuild flake; its isolated retry passed 1/1.

**Tests**: integration
**Gate**: build
**Commit**: test(index): prove unknown priority and catalogue provenance

---

### T13: Prove named shards and absence contracts

**What**: Strengthen projection tests to assert every specified lookup-family name, explicit missing target identity and empty catalogue contents.
**Where**: tests/Csharp2Md.Core.Tests/Projection/Aggregates/RetrievalIndexProjectorTests.cs
**Depends on**: T12
**Reuses**: Existing empty-run and missing-target projection fixtures.
**Requirement**: RRI-02, RRI-16, RRI-17; empty-run and no-entry-point edge cases

**Done when**:

- [x] The five exact shard-family names are asserted from the projected manifest.
- [x] A missing runtime target produces an entry without `target_id`, without a target shard or fabricated key.
- [x] Empty input and runs without a proven entry point both serialize catalogue entries as empty arrays.
- [x] Build Release, format and the full suite passed, subject only to the approved MSBuild-flake waiver with an isolated passing retry.

**Tests**: unit
**Gate**: build
**Commit**: test(index): prove named shards and empty catalogues

---

### T14: Prove additive index-read compatibility

**What**: Add the minimum consumer-contract reader coverage proving an index missing newly additive fields remains readable, then map the currently unnumbered P4 acceptance criterion to that evidence.
**Where**: src/Csharp2Md.Core/Projection/Aggregates/RetrievalIndexReader.cs and tests/Csharp2Md.Core.Tests/Projection/Aggregates/RetrievalIndexContractsTests.cs
**Depends on**: T13
**Reuses**: Existing JSON contract serialization tests and System.Text.Json options.
**Requirement**: P4 additive/missing-field compatibility acceptance criterion

**Done when**:

- [x] A consumer reader deserializes a prior shard and summary that omit the additive generated-origin and analysis-limitations fields.
- [x] The read values prove the documented false/default and optional-field behavior while retaining required index content.
- [x] The unnumbered P4 additive/missing-field criterion is mapped to the exact reader assertions in spec traceability.
- [x] Build Release, format and the full suite passed, subject only to the approved MSBuild-flake waiver with an isolated passing retry.

**Tests**: unit
**Gate**: build
**Commit**: test(index): prove additive index compatibility

---

## Phase Execution Map

```text
Phase 1: T1 → T2 → T3
Phase 2: T3 → T4 → T5 → T6 → T7
Phase 3: T7 → T8 → T9
Phase 4: T9 → T10
Phase 5: T10 → T11 → T12
Phase 6: T12 → T13 → T14
```

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1 | One project decision record | OK |
| T2 | One generated-origin metadata component | OK |
| T3 | One factual JSON wire contract | OK |
| T4 | One index contract family | OK |
| T5 | One bounded shard writer | OK |
| T6 | One fragment-to-index projector | OK |
| T7 | One aggregate writer integration point | OK |
| T8 | One independent retrieval test suite | OK |
| T9 | One independent compatibility test suite | OK |
| T10 | One traceability closure | OK |
| T11 | One boundary and manifest proof suite | OK |
| T12 | One UNKNOWN and catalogue proof suite | OK |
| T13 | One named-shard and absence proof suite | OK |
| T14 | One additive reader compatibility proof | OK |

## Diagram-Definition Cross-Check

| Task | Depends On | Diagram Shows | Status |
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

No task depends on a later phase.

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Decision record | none | none | OK |
| T2 | Factual metadata | unit | unit | OK |
| T3 | Factual JSON/schema | unit | unit | OK |
| T4 | Index contracts | unit | unit | OK |
| T5 | Shard writer | unit | unit | OK |
| T6 | Index projector | unit | unit | OK |
| T7 | Aggregate writer | unit | unit | OK |
| T8 | Pipeline retrieval behaviour | integration | integration | OK |
| T9 | Pipeline compatibility behaviour | integration | integration | OK |
| T10 | Traceability documents | none | none | OK |
| T11 | Shard and manifest contract proof | integration | integration | OK |
| T12 | UNKNOWN and catalogue behaviour proof | integration | integration | OK |
| T13 | Projection behaviour proof | unit | unit | OK |
| T14 | Index consumer contract proof | unit | unit | OK |

The planned execution needs two task-budgeted batches (3 + 4 tasks, then 2 + 1 tasks). Before Execute,
the tlc-spec-driven process requires an explicit user choice about sequential batch workers. No external
MCP is necessary; the listed C# and test skills are the available task tools.
