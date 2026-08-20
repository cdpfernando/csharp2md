# Database Access Discovery Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and
Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the
full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**User-confirmed deviation from the standard flow:** the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by explicit user request, reaffirmed for this feature
on 2026-08-20. No mutants are injected at any point. The Verifier's spec-anchored outcome check, per-AC
`file:line` evidence, and `validation.md` report still run as normal.

---

**Spec**: `.specs/features/data-access-discovery/spec.md`
**Design**: `.specs/features/data-access-discovery/design.md`
**Status**: Approved

**Scope of this task list**: P1 only — `DAD-01` through `DAD-28`. The spec's P2 (`DAD-29`..`DAD-35`) and P3
(`DAD-36`..`DAD-38`) stories are deliberately not broken down here; they attach behind the same
`IDataAccessAnalyzer` seam in a later pass.

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs`,
> `Analysis/RelationCollectorEndToEndTests.cs`, `Analysis/Indexes/SymbolIndexTests.cs`,
> `Facts/Validation/FactValidatorTests.cs`, `Facts/Serialization/FactualJsonTests.cs` /
> `FactualSchemaSyncTests.cs`, `Projection/Aggregates/RelationProjectorTests.cs`,
> `Analysis/AnalysisEngineTests.cs`) and project guidelines.
> Guidelines found: `AGENTS.md` / `CLAUDE.md` — they route test quality to the `dotnet-test:*` skills as
> post-hoc gates rather than declaring a coverage threshold. No coverage-threshold tool config and no CI
> workflow exist in this repo, so strong defaults apply to the Coverage Expectation column.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Aggregate wiring / projection (`AnalysisEngine` snapshot, `CanonicalAggregateWriter`, `CoverageProjector`) | integration | Every Phase 0 repair proven by a test that fails on the current code: a non-empty partition file from a real `AnalyzeAsync` run, a `structural.json` that exists, and a full-fixture-solution run that completes | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/*Tests.cs`, `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs` | `dotnet test csharp2md.slnx` |
| Identity types (`DatabaseObjectFactId`, `DatabaseColumnFactId`) | unit | All branches; AD-014 grammar conformance — percent-encoding, ordinal comparison, canonical-text rejection, and the `connection=unknown` component present from day one | `tests/Csharp2Md.Core.Tests/Facts/Identity/FactIdsTests.cs` | `dotnet test csharp2md.slnx` |
| Fact model + validation (`DatabaseObjectFact`, `DatabaseColumnFact`, `FactValidator`) | unit | All branches; a column referencing a missing object produces `C2M-FV-002`; evidence-range validation applies to both new kinds | `tests/Csharp2Md.Core.Tests/Facts/Validation/FactValidatorTests.cs` | `dotnet test csharp2md.slnx` |
| JSON contract / schema (`FactualJsonContracts`, `FactualJsonMapper`, `facts.schema.json`, `SchemaVersion`) | unit | Round-trip serialize/deserialize of every new field; schema sync assertion updated to 4; every approved snapshot containing a fragment re-approved | `tests/Csharp2Md.Core.Tests/Facts/Serialization/FactualJsonTests.cs`, `FactualSchemaSyncTests.cs`, `tests/**/snapshots/*.verified.*` | `dotnet test csharp2md.slnx` |
| Collector seam (`IDataAccessAnalyzer`, `DataAccessCollector`, `DatabaseClaimAccumulator`) | unit | All branches; 1:1 to DAD-13/DAD-18; a throwing analyzer yields `C2M-DA-001` and never propagates; canonical ordering is stable across two collections | `tests/Csharp2Md.Core.Tests/Analysis/DataAccess/DataAccessCollectorTests.cs` (new) | `dotnet test csharp2md.slnx` |
| Domain analyzers (`EfCoreAnalyzer`, `SqlTextAnalyzer`) | unit | All branches; 1:1 to spec ACs DAD-01/03/05/07..10/16/17/21..28; every listed Edge Case in spec.md has a test | `tests/Csharp2Md.Core.Tests/Analysis/DataAccess/EfCoreAnalyzerTests.cs`, `SqlTextAnalyzerTests.cs` (new) | `dotnet test csharp2md.slnx` |
| SQL tokenizer (`SqlStatementReader`) | unit | All branches; every recognised verb, every unreadable shape returning false rather than guessing; 1:1 to DAD-21..DAD-26 | `tests/Csharp2Md.Core.Tests/Analysis/DataAccess/SqlStatementReaderTests.cs` (new) | `dotnet test csharp2md.slnx` |
| Resolver (`DatabaseMappingResolver`) | unit | All branches; 1:1 to DAD-02/04/06/11/12/14; configured-over-convention precedence and the one-match/many-match/no-match ambiguity fork each pinned | `tests/Csharp2Md.Core.Tests/Analysis/DataAccess/DatabaseMappingResolverTests.cs` (new) | `dotnet test csharp2md.slnx` |
| Fragment build + aggregate (`DatabaseFragmentBuilder`, `DatabaseAggregateProjector`) | unit | All branches; 1:1 to DAD-19; a node emitted twice yields one aggregate entry with merged evidence and the higher-confidence resolution | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/DatabaseAggregateProjectorTests.cs` (new) | `dotnet test csharp2md.slnx` |
| End-to-end pipeline | integration | The spec's two P1 Independent Tests verbatim, plus DAD-15 (no credential text anywhere in the output tree), DAD-17 (a name-only `*Repository` yields nothing) and DAD-20 (two runs byte-identical) | `tests/Csharp2Md.Core.Tests/Analysis/DataAccessDiscoveryEndToEndTests.cs` (new, mirrors `RelationCollectorEndToEndTests.cs`) | `dotnet test csharp2md.slnx --filter "Category=Integration"` |
| Fixture source documents | none | Build gate only — fixture code is analysed input, not production code | `fixtures/SyntheticSolution/**` | build gate only |

## Gate Check Commands

> Generated from the project's established commands, consistent with every prior feature's handoff in
> `.specs/STATE.md`. No CI workflow file exists in this repo to source them from instead.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks with unit tests only | `dotnet test csharp2md.slnx` |
| Full | After tasks with integration tests (pipeline wiring, fixture-level) | `dotnet test csharp2md.slnx` (integration tests carry `[Trait("Category","Integration")]` in the same suite; no separate command exists in this repo) |
| Build | After phase completion, the schema-version bump, and fixture-only changes | `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx` |

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a
phase execute in order.

### Phase 0: Pipeline repairs

Three pre-existing defects, each fixed with a test that fails on the current code first. Everything after
this phase depends on a run that completes and an aggregate that is actually written.

```
T1 → T2 → T3
```

### Phase 1: Fact model, identity, serialization

The persistence fact family and its wire contract.

```
T4 → T5 → T6 → T7 → T8 → T9 → T10
```

### Phase 2: Collector seam

The extension point and the per-document plumbing, with no analyzer behind it yet.

```
T11 → T12 → T13 → T14 → T15
```

### Phase 3: EF Core analyzer

Every P1 EF Core shape, as claims. No resolution happens here.

```
T16 → T17 → T18 → T19 → T20 → T21 → T22
```

### Phase 4: SQL analysis

The bounded tokenizer, then the analyzer that feeds it.

```
T23 → T24 → T25 → T26
```

### Phase 5: Resolver, fragment, aggregate

Pass 2 — where claims become facts.

```
T27 → T28 → T29 → T30 → T31 → T32
```

### Phase 6: Fixtures and end-to-end verification

```
T33 → T34 → T35 → T36
```

---

## Task Breakdown

### T1: Wire RelationProjector into the analysis snapshot

**What**: Pass `RelationProjector.Project(validatedFragments)` into `AggregateOutputSnapshot.Relations` so the
relation partition files stop being written empty.
**Where**: `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs`
**Depends on**: None
**Reuses**: `RelationProjector.Project`, already complete and unit-tested; only its caller is missing
**Requirement**: Design Phase 0.1 — prerequisite for DAD-19 and both P1 Independent Tests

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A test asserting a non-empty `raw/facts/relations/http.json` after a real `AnalyzeAsync` run over the
      fixture is written first and observed to FAIL on the unmodified code
- [x] `AnalyzeAsync` retains the validated fragments it already builds and passes their projection into the
      snapshot; no second validation pass is introduced
- [x] The same test passes after the change
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `fix(aggregate): project relations into the written partition files`

---

### T2: Make the writer's relation partition list exhaustive

**What**: Replace `CanonicalAggregateWriter`'s hand-maintained six-string partition list with an exhaustive
projection over `RelationPartition`, so `structural` is written and no future member can be forgotten.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/CanonicalAggregateWriter.cs`
**Depends on**: T1
**Reuses**: `FactStore`'s existing `RelationPartition` → wire-name mapping as the single source of truth
**Requirement**: Design Phase 0.2

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A test asserting one written file per `RelationPartition` enum member is written first and observed to
      FAIL (7 expected, 6 written)
- [x] `structural.json` is written and contains the fixture's `calls`/`creates`/`references` relations
- [x] The wire-name mapping is not duplicated — the writer and `FactStore` agree by construction
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `fix(aggregate): write every relation partition, including structural`

---

### T3: De-duplicate analysed projects across the run

**What**: Analyse each project at most once per run, keyed by `ProjectFactId`, so a project reachable by more
than one path stops producing duplicate documents, fragments and coverage scopes.
**Where**: `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs`
**Depends on**: T2
**Reuses**: `ProjectFactId` as the identity key; the existing per-project loop structure
**Requirement**: Design Phase 0.3 — prerequisite for DAD-19 and for a correct pass-2 entity catalogue

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A test running the full `fixtures/SyntheticSolution` through `AnalyzeAsync` is written first and
      observed to FAIL with the current `ArgumentException` from `CoverageProjector`
- [x] `Acme.Shared.Contracts` is analysed exactly once even though three paths reach it
- [x] The run completes and reports a project count matching the fixture's distinct project count
- [x] Documents already analysed are skipped without emitting a diagnostic (a shared project is normal, not
      an anomaly)
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `fix(analysis): analyse each project once per run`

---

### T4: Add the persistence enums

**What**: Add `DatabaseObjectKind`, `DatabaseOperation` and `ColumnUsage` with their wire-name mappings.
**Where**: `src/Csharp2Md.Core/Facts/Model/DatabaseFacts.cs`
**Depends on**: None
**Reuses**: `FactResolution`'s wire-name convention (`Wire<T>` lowercasing in `FactualJsonMapper`)
**Requirement**: DAD-23 (object kinds), DAD-21 (operations), DAD-08/DAD-09 (usages)

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] All three enums declared with the full P1 + P2 value sets from design.md's Data Models
- [x] A test asserts every member maps to a distinct, lower-case wire name
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions) — 1383 -> 1411 (+28)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): add persistence operation, usage and object-kind enums`

---

### T5: Add the persistence fact identities

**What**: Add `DatabaseObjectFactId` and `DatabaseColumnFactId` following AD-014's `id1:` grammar, with
`connection` present from the start carrying the literal value `unknown`.
**Where**: `src/Csharp2Md.Core/Facts/Identity/FactIds.cs`
**Depends on**: T4
**Reuses**: `FactIdGrammar.Create`, `RequireCanonicalText`
**Requirement**: AD-016; prerequisite for DAD-03, DAD-05, DAD-22

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] `DatabaseObjectFactId.Create(connection, kind, name)` and
      `DatabaseColumnFactId.Create(objectId, name)` produce ids matching design.md's grammar exactly
- [x] Tests cover percent-encoding of a name needing it, ordinal case-sensitivity (`Orders` ≠ `orders`), and
      rejection of non-canonical text
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions) — 1411 -> 1430 (+19)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): add database object and column fact identities`

---

### T6: Add the persistence fact records

**What**: Add `DatabaseObjectFact` and `DatabaseColumnFact` plus their `FactKind` members.
**Where**: `src/Csharp2Md.Core/Facts/Model/DatabaseFacts.cs`
**Depends on**: T5
**Reuses**: `IFact`, `FactHeader`
**Requirement**: AD-016; prerequisite for DAD-03, DAD-05

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Both records implement `IFact` and carry their typed id plus the fields from design.md
- [x] `FactKind` gains `DatabaseObject` and `DatabaseColumn`
- [x] Every exhaustive `switch` over `FactKind` or `IFact` in the solution compiles — the build is the check,
      and each site is handled deliberately rather than by a catch-all — `FactMerger.ClaimsEqual` and
      `FactMerger.WithHeader` each gained explicit arms
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions) — 1430 -> 1436 (+6)

**Tests**: unit
**Gate**: build

**Commit**: `feat(facts): add database object and column fact records`

---

### T7: Teach FactValidator the persistence facts

**What**: Add both new kinds to `GetReferences` so a column's owning object is a validated reference, and
confirm evidence-range validation applies to them.
**Where**: `src/Csharp2Md.Core/Facts/Validation/FactValidator.cs`
**Depends on**: T6
**Reuses**: the existing `GetReferences` switch and `ValidateEvidence`
**Requirement**: DAD-13; structural integrity for DAD-03/DAD-05

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A `DatabaseColumnFact` whose owning object is absent from the fragment produces `C2M-FV-002`
- [x] A column whose owning object is present validates cleanly
- [x] Out-of-range evidence on either new kind produces `C2M-FV-004`
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions) — 1436 -> 1441 (+5)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): validate database fact references and evidence`

---

### T8: Add the persistence JSON contracts and mapping

**What**: Add `DatabaseObjectFactJson` / `DatabaseColumnFactJson`, the two new arrays on
`FactualJsonDocument`, their `FactualJsonMapper` mappings, and their `FactualJsonContext` entries.
**Where**: `src/Csharp2Md.Core/Facts/Serialization/FactualJsonContracts.cs`,
`src/Csharp2Md.Core/Facts/Serialization/FactualJsonContext.cs`,
`src/Csharp2Md.Core/Facts/Storage/FactStore.cs`
**Depends on**: T7
**Reuses**: the `RelationFactJson` shape and `JsonPropertyOrder` convention
**Requirement**: AD-017; prerequisite for DAD-19

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:serialization`

**Done when**:

- [x] Round-trip serialize/deserialize tests cover every field on both new contracts
- [x] Property order is explicit and deterministic, matching the existing contracts' style
- [x] The three files change together because the source-generated context and the mapper cannot compile
      apart from the contract — this is one wire shape, not three deliverables. `FactualJsonContext` needed
      no edit: it declares only `FactualJsonDocument` and the generator walks nested contracts transitively
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions) — 1441 -> 1448 (+7)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): serialize database object and column facts`

---

### T9: Bump the fragment schema to version 4

**What**: Move `FactualJsonSerializer.SchemaVersion` from 3 to 4, update `facts.schema.json` to match
(including its `const` and the two new required arrays), and re-approve every affected snapshot.
**Where**: `src/Csharp2Md.Core/Facts/Serialization/FactualJsonSerializer.cs`, `schemas/facts.schema.json`
**Depends on**: T8
**Reuses**: `FactualSchemaSyncTests`, which already enforces contract-to-schema parity
**Requirement**: AD-017

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] `FactualSchemaSyncTests` passes against version 4 with both new arrays required
- [x] Every approved snapshot whose content shifted is re-approved deliberately, with the diff reviewed —
      not bulk-accepted — one snapshot embeds a fragment and moved 3 -> 4; the other five embed aggregate
      envelopes, which stay at 2
- [x] The serializer still rejects a document declaring any other schema version — now pinned for 2, 3 and 5
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then
      `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions) — 1448 -> 1450 (+2)

**Tests**: unit
**Gate**: build

**Commit**: `feat(facts)!: bump the factual fragment schema to version 4`

---

### T10: Add the data relation partition

**What**: Add `RelationPartition.Data` with wire name `data`, wired through `FactStore`'s mapping,
`RelationProjector`'s partition list, and `CanonicalAggregateWriter`'s parse.
**Where**: `src/Csharp2Md.Core/Facts/Model/RelationFact.cs`
**Depends on**: T9
**Reuses**: T2's exhaustive partition projection, which makes the writer pick this up automatically
**Requirement**: Prerequisite for every P1 relation AC

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] `raw/facts/relations/data.json` is written by a real run, empty at this point but present
- [x] `RelationProjector` accepts the new partition without throwing `Unsupported relation partition` — its
      hand-kept partition list is now projected over the enum and its duplicate wire-name switch is gone,
      so `FactStore.WireRelationPartition` is the single source of truth for store, writer and projector
- [x] The partition is classified as runtime or compile-time deliberately, with the choice justified in a
      code comment — runtime, like `http` and `grpc`: the target exists only when the program runs
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions) — 1450 -> 1454 (+4)

**Tests**: integration
**Gate**: full

**Commit**: `feat(facts): add the data relation partition`

---

### T11: Define the raw claim and analyzer context

**What**: Add `RawDatabaseClaim`, `DataAccessContext` and `DataAccessAnalyzerId` exactly as design.md's Data
Models section specifies.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/DataAccessContracts.cs`
**Depends on**: T10
**Reuses**: `Evidence`, `FactResolution`, `DetectorId`
**Requirement**: DAD-14, DAD-16 (the `SqlText` cap lives on this shape)

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] `RawDatabaseClaim` carries every field from design.md, with required members enforced by the compiler
- [x] A test pins that `SqlText` longer than 2000 characters is rejected or truncated at construction, not
      at serialization time
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(dataaccess): define the raw database claim contract`

---

### T12: Define the analyzer seam

**What**: Add `IDataAccessAnalyzer` — the extension point brief §4 requires, so P2's Dapper and ADO.NET
analyzers attach without touching the collector.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/IDataAccessAnalyzer.cs`
**Depends on**: T11
**Reuses**: `IDocumentFactDetector`'s shape as a reference, deliberately not its infrastructure
**Requirement**: Design's extension-seam decision

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] Interface declared with the identity and `Analyze` members from design.md
- [x] A test double implementing it compiles and appends a claim, proving the seam is usable
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(dataaccess): add the data access analyzer seam`

---

### T13: Implement the collector with failure isolation

**What**: `DataAccessCollector` runs the registered analyzers in a fixed canonical order and converts an
analyzer failure into a `C2M-DA-001` diagnostic instead of letting it escape.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/DataAccessCollector.cs`
**Depends on**: T12
**Reuses**: `DetectorHost.Run`'s failure-isolation semantics, reimplemented locally rather than depended upon
**Requirement**: DAD-13, DAD-18

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A throwing analyzer produces one `C2M-DA-001` warning naming the document and the analyzer, and its
      partial claims are discarded
- [x] A throwing analyzer does not prevent the other analyzers' claims from being returned
- [x] `OperationCanceledException` propagates rather than being swallowed, matching `DetectorHost`
- [x] Every returned claim carries evidence — a claim without it is rejected at construction (DAD-13)
- [x] Analyzer order is canonical and stable across two collections over the same input
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(dataaccess): collect claims with per-analyzer failure isolation`

---

### T14: Implement the claim accumulator

**What**: `DatabaseClaimAccumulator` holds every document's claims plus the `DocumentExtent` of only those
documents that produced one, and emits a canonically ordered snapshot.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/DatabaseClaimAccumulator.cs`
**Depends on**: T13
**Reuses**: `DocumentExtent` from `FactValidationInput`
**Requirement**: DAD-20; the memory bound AD-008 requires

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A document contributing no claims leaves no extent retained — asserted, not assumed
- [x] `ToSnapshot` returns the same ordering regardless of the order documents were added
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(dataaccess): accumulate claims with bounded extent retention`

---

### T15: Hand claims off from the syntax extractor to the engine

**What**: Add `DatabaseClaims` to `SyntaxFactExtraction`, invoke the collector from inside
`SyntaxFactExtractor.Extract` where the owner map is live, and feed the result into the accumulator from
`AnalysisEngine`.
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T14
**Reuses**: the existing `ownerByDeclaration` map — the whole reason pass 1 lives here
**Requirement**: DAD-13; prerequisite for every Phase 3 and 4 claim

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Only the call site and the owner-map handoff are added to the extractor; no persistence logic lands in
      this file
- [x] A claim's `OwnerId` is the enclosing member's symbol id, falling back to the document id only for code
      with no enclosing member
- [x] With no analyzers registered, the extractor's existing behaviour and every existing test are unchanged
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(dataaccess): collect database claims during syntax extraction`

---

### T16: Discover DbContexts and their entity sets

**What**: `EfCoreAnalyzer` recognises a type whose base list names `DbContext` and emits one
`EntitySetExposed` claim per `DbSet<TEntity>` property it declares.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/EfCore/EfCoreAnalyzer.cs`
**Depends on**: T15
**Reuses**: `RelationNoiseFilter` for framework-type filtering
**Requirement**: DAD-01, DAD-17

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A `DbContext` subclass with two `DbSet<T>` properties yields exactly two claims with the right entity
      names
- [x] A `DbSet<T>` property on a type that does not derive from `DbContext` yields nothing (spec Edge Case)
- [x] A class named `OrderRepository` with no persistence API usage yields nothing (DAD-17)
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(efcore): discover DbContexts and their entity sets`

---

### T17: Read ToTable configuration

**What**: Recognise an `Entity<TEntity>()` chain ending in `ToTable` with a string-literal argument and emit
a `TableConfigured` claim.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/EfCore/EfCoreAnalyzer.cs` (modify)
**Depends on**: T16
**Reuses**: the invocation-chain walking shape `SyntaxFactExtractor.ClassifyHttpInvocation` already uses
**Requirement**: DAD-03

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] `Entity<Order>().ToTable("tb_order")` yields a claim naming `Order` and `tb_order`
- [ ] A non-literal `ToTable` argument yields no configured claim, so the convention path takes over
      (spec Edge Case)
- [ ] A `ToTable` reached through a fluent chain split across statements is handled or explicitly
      documented as unsupported — not silently mis-attributed
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(efcore): read explicit table configuration`

---

### T18: Read HasColumnName configuration

**What**: Recognise a `Property(x => x.P)` chain ending in `HasColumnName` with a string-literal argument and
emit a `ColumnConfigured` claim.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/EfCore/EfCoreAnalyzer.cs` (modify)
**Depends on**: T17
**Reuses**: T17's chain-walking helper
**Requirement**: DAD-05

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] `Property(x => x.Status).HasColumnName("order_status")` yields a claim naming the property and the
      column, attributed to the entity the enclosing `Entity<T>()` names
- [ ] A non-literal argument yields no configured claim (spec Edge Case)
- [ ] A `Property` chain with no enclosing `Entity<T>()` is skipped rather than guessed at
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(efcore): read explicit column configuration`

---

### T19: Detect DbSet reads and projected columns

**What**: Emit an `Access` claim with operation `read` for a `DbSet` property read, plus one `ColumnAccess`
claim per entity property referenced outside a `Where` lambda.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/EfCore/EfCoreAnalyzer.cs` (modify)
**Depends on**: T18
**Reuses**: T16's DbSet property catalogue
**Requirement**: DAD-07, DAD-08

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Brief §7's exact query yields a `read` access plus `Id`, `Status`, `Amount` column claims with usage
      `read`
- [ ] A `DbSet` read with no LINQ chain still yields the access claim with no column claims
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(efcore): detect entity set reads and projected columns`

---

### T20: Detect filter columns

**What**: Emit a `ColumnAccess` claim with usage `filter` for each entity property a `Where` lambda
references.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/EfCore/EfCoreAnalyzer.cs` (modify)
**Depends on**: T19
**Reuses**: T19's chain traversal
**Requirement**: DAD-09

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Brief §7's `Where(x => x.Id == orderId)` yields exactly one `filter` claim for `Id`
- [ ] A property referenced in both a `Where` and a projection yields both a `filter` and a `read` claim,
      not one collapsed claim
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(efcore): detect filter columns from Where lambdas`

---

### T21: Detect the Add, Update and Remove families

**What**: Emit `Access` claims with operation `insert`, `update` or `delete` for the corresponding `DbSet`
method families.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/EfCore/EfCoreAnalyzer.cs` (modify)
**Depends on**: T20
**Reuses**: T16's DbSet property catalogue
**Requirement**: DAD-10

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] All seven method names from DAD-10 map to the correct operation, each with a test
- [ ] A same-named method on a non-`DbSet` receiver yields nothing
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(efcore): detect entity set insert, update and delete calls`

---

### T22: Detect property assignment paired with SaveChanges

**What**: Emit a `ColumnAccess` claim with usage `write` for a property assignment in a member that also
invokes `SaveChanges`/`SaveChangesAsync`, carrying the receiver text so the resolver can disambiguate later.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/EfCore/EfCoreAnalyzer.cs` (modify)
**Depends on**: T21
**Reuses**: the enclosing-member owner id from T15
**Requirement**: DAD-11 (claim side; resolution happens in T29)

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Brief §8's `order.Status = ...; await context.SaveChangesAsync();` yields a write claim carrying both
      `order.Status` and the property name `Status`
- [ ] An assignment in a member with no `SaveChanges` call yields nothing
- [ ] The claim records the observed text only — it makes no entity attribution, which is T29's job
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(efcore): detect tracked property writes paired with SaveChanges`

---

### T23: Read a SQL statement's verb and target

**What**: `SqlStatementReader` recognises the seven P1 verbs and reads a plain-identifier target after
`FROM`, `INTO`, `UPDATE`, `DELETE FROM`, `MERGE INTO`, `EXEC` and `CALL`.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/Sql/SqlStatementReader.cs`
**Depends on**: T22
**Reuses**: nothing — deliberately dependency-free
**Requirement**: DAD-21, DAD-22, DAD-23

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Each of the seven verbs is recognised case-insensitively, with leading whitespace and `--` comments
      skipped
- [ ] `EXEC`/`CALL` targets are classified `procedure`; every other verb's target is `unknown`
- [ ] A target the reader cannot read as a plain identifier returns no target rather than a guess
- [ ] A non-SQL string returns false
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(sql): read statement verb and target object`

---

### T24: Read INSERT and UPDATE column lists

**What**: Extend the reader to extract the `INSERT INTO x (...)` column list and the `UPDATE x SET ...`
assignment list.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/Sql/SqlStatementReader.cs` (modify)
**Depends on**: T23
**Reuses**: T23's tokenizer
**Requirement**: DAD-24, DAD-25

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A three-column `INSERT` yields exactly those three column names
- [ ] A two-assignment `UPDATE ... SET` yields exactly those two column names and does not pick up the
      values
- [ ] A malformed or unclosed list yields no columns rather than a partial guess
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(sql): read insert and update column lists`

---

### T25: Read WHERE filter columns

**What**: Extend the reader to extract column identifiers compared against a literal or a parameter in a
`WHERE` clause.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/Sql/SqlStatementReader.cs` (modify)
**Depends on**: T24
**Reuses**: T23's tokenizer
**Requirement**: DAD-26

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] `WHERE Id = @id AND Status = 'Paid'` yields `Id` and `Status`
- [ ] A qualified identifier (`o.Id`) yields the column name with its alias handled deliberately, documented
      either way
- [ ] A `WHERE` clause the reader cannot parse yields no columns rather than a guess
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(sql): read filter columns from where clauses`

---

### T26: Implement the SQL text analyzer

**What**: `SqlTextAnalyzer` finds SQL-shaped string expressions, routes readable literals through the reader,
and marks interpolated or concatenated expressions `Unresolved` without inventing a target.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/Sql/SqlTextAnalyzer.cs`
**Depends on**: T25
**Reuses**: `SqlStatementReader`, the `IDataAccessAnalyzer` seam
**Requirement**: DAD-16, DAD-21, DAD-27, DAD-28

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] `$"SELECT * FROM {tableName}"` yields an `Unresolved` access with `target_text` `dynamic-table` and no
      object claim (DAD-27)
- [ ] A readable literal whose target cannot be read yields an `Unresolved` access preserving the statement
      (DAD-28)
- [ ] SQL text is truncated at 2000 characters (DAD-16)
- [ ] A connection-string-shaped literal is not treated as SQL, so no credential text can reach a claim
      (DAD-15's guard at the point of capture)
- [ ] A SQL-verb-leading literal used as a log message still yields an access at `Syntactic` resolution, per
      the spec's Edge Case, rather than being dropped
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(sql): analyze literal and dynamic SQL expressions`

---

### T27: Resolve entities to database objects

**What**: `DatabaseMappingResolver` builds the entity→object map, applying configured-over-convention
precedence and minting a node only for a configured name.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/DatabaseMappingResolver.cs`
**Depends on**: T26
**Reuses**: `ISymbolIndex` to attach an entity's text to a real `SymbolFactId`
**Requirement**: DAD-02, DAD-03, DAD-04

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A `DbSet` with no `ToTable` yields a `maps-to` with `target_id` null, `target_text` the set name, and
      resolution `Heuristic` (DAD-02)
- [ ] A `ToTable` in a *different document* from the entity resolves correctly — the case that forced the
      two-pass design
- [ ] An entity with both yields only the configured mapping (DAD-04)
- [ ] Two `DbContext`s exposing the same entity produce one `exposes` each, per the spec's Edge Case
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(dataaccess): resolve entities to database objects`

---

### T28: Resolve properties to columns

**What**: Build the property→column map, minting a column node only for a configured name and falling back
to the convention mapping otherwise.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/DatabaseMappingResolver.cs` (modify)
**Depends on**: T27
**Reuses**: T27's entity→object map, which a column node's identity depends on
**Requirement**: DAD-05, DAD-06

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A `HasColumnName` yields a column node owned by the entity's object with resolution `Exact` (DAD-05)
- [ ] A property with no configuration yields `maps-property-to-column` with `target_id` null and resolution
      `Heuristic` (DAD-06)
- [ ] A configured column on an entity whose table is only convention-mapped is handled deliberately —
      no column node without an owning object node
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(dataaccess): resolve entity properties to columns`

---

### T29: Resolve accesses into relations

**What**: Turn every `Access` and `ColumnAccess` claim into a relation, attaching a resolved target where the
maps allow and the ambiguity rules where they do not.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/DatabaseMappingResolver.cs` (modify)
**Depends on**: T28
**Reuses**: T27 and T28's maps
**Requirement**: DAD-07..DAD-12, DAD-14

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] An assigned property matching exactly one exposed entity yields `writes-column` at `Heuristic`
      (DAD-11)
- [ ] An assigned property matching more than one yields `target_id` null at `Candidate` (DAD-12)
- [ ] An assigned property matching none yields no relation, per the spec's Edge Case
- [ ] Every unresolved access still produces a relation carrying `unresolved_reason` and `target_text`
      (DAD-14) — nothing is dropped
- [ ] Relation kind and the `operation` / `usage` details follow design.md's relation table exactly
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(dataaccess): resolve database accesses into relations`

---

### T30: Build and persist the persistence fragment

**What**: `DatabaseFragmentBuilder` turns the resolver's output into facts, validates them, and persists one
solution-level fragment through the existing store.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/DatabaseFragmentBuilder.cs`
**Depends on**: T29
**Reuses**: `FactValidator`, `FactStore`, unchanged
**Requirement**: DAD-13; structural integrity for all of P1

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] The fragment validates cleanly with the retained extents from T14
- [ ] A validation failure is surfaced as a structural failure exactly as a document fragment's is, with no
      new failure semantics
- [ ] The fragment appears in the manifest with a correct hash and byte length
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(dataaccess): build and persist the persistence fragment`

---

### T31: Run pass two from the analysis engine

**What**: After the document loop, run the resolver, build and persist the fragment, and add it to the
snapshot's fragments so it reaches the manifest and the relation projection.
**Where**: `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs` (modify)
**Depends on**: T30
**Reuses**: the `SymbolIndex` already built at this exact point in `AnalyzeAsync`
**Requirement**: Wires DAD-01..DAD-28 into a real run

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] `raw/facts/relations/data.json` is non-empty after a real run over a fixture containing EF Core code
- [ ] A run over a codebase with no persistence code produces an empty `data.json` and no fragment, with no
      diagnostic (spec Edge Case)
- [ ] `AnalysisResult`'s public shape is unchanged
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `feat(analysis): run database mapping resolution after document analysis`

---

### T32: Project the database aggregate

**What**: `DatabaseAggregateProjector` writes `raw/facts/database.json` with one entry per node id, merged
evidence, the highest-confidence resolution, and a `C2M-DA-002` informational diagnostic when two ids differ
only by case.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/DatabaseAggregateProjector.cs`
**Depends on**: T31
**Reuses**: `RelationProjector`'s ordering and duplicate-handling conventions
**Requirement**: DAD-19; the AD-014 case-sensitivity mitigation from design.md

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A node emitted by three documents yields exactly one aggregate entry with all three evidence spans
      (DAD-19)
- [ ] The higher-confidence resolution wins when two contributions disagree
- [ ] `Orders` and `orders` remain two entries and produce one `C2M-DA-002` informational diagnostic
- [ ] Output ordering is canonical and stable
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(aggregate): project the database node catalogue`

---

### T33: Extend the fixture with EF Core configuration and queries

**What**: Add an entity configuration document and a query/write service to `Acme.Orders` so DAD-01..DAD-12
have real source to run against, including a `ToTable` in a different document from its entity.
**Where**: `fixtures/SyntheticSolution/Acme.Orders/Data/OrderConfiguration.cs`
**Depends on**: T32
**Reuses**: the existing `OrderDbContext.cs` stand-in, extended rather than replaced
**Requirement**: Makes the P1 EF Core Independent Test runnable

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] The configuration document carries a `ToTable` and a `HasColumnName`, both string literals
- [ ] A query service reproduces brief §7's shape and a write service reproduces brief §8's shape
- [ ] A `*Repository`-named class with no persistence API is included, so DAD-17 has a negative case
- [ ] The fixture still builds and every existing fixture-dependent test still passes
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: none — fixture source is analysed input, per the coverage matrix's "Fixture source documents" row
**Gate**: build

**Commit**: `test(fixtures): add EF Core configuration and query documents`

---

### T34: Extend the fixture with literal and dynamic SQL

**What**: Add a document containing the five SQL shapes the spec's P1-B Independent Test names.
**Where**: `fixtures/SyntheticSolution/Acme.Orders/Data/OrderSqlQueries.cs`
**Depends on**: T33
**Reuses**: the fixture's existing framework stand-in convention
**Requirement**: Makes the P1 literal-SQL Independent Test runnable

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] One readable `SELECT ... WHERE`, one `INSERT INTO ... (...)`, one `UPDATE ... SET ... WHERE`, one
      `EXEC usp_X`, and one interpolated `$"SELECT * FROM {tableName}"` are present
- [ ] No credential or connection-string text appears anywhere in the fixture
- [ ] The fixture still builds and every existing fixture-dependent test still passes
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: none — fixture source is analysed input, per the coverage matrix's "Fixture source documents" row
**Gate**: build

**Commit**: `test(fixtures): add literal and dynamic SQL documents`

---

### T35: Verify the EF Core Independent Test end to end

**What**: Restate spec.md's P1 EF Core Independent Test literally, against a real default-mode
`AnalyzeAsync` run over the extended fixture.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/DataAccessDiscoveryEndToEndTests.cs`
**Depends on**: T34
**Reuses**: `RelationCollectorEndToEndTests`'s fixture-and-run structure
**Requirement**: DAD-01..DAD-12, DAD-17, DAD-19

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] `data.json` contains the `maps-to`, `reads`, `reads-column`, `filters-by` and `writes-column` relations
      with the resolutions the spec names
- [ ] `database.json` lists the configured table and column nodes
- [ ] Brief §7's question is answerable: `Id`, `Status`, `Amount` read; `Id` filtered
- [ ] Brief §8's question is answerable: the write member appears as a writer of `Order.Status`'s column
- [ ] The run is in the default syntax-only, untrusted mode — not trusted mode
- [ ] Gate check passes: `dotnet test csharp2md.slnx --filter "Category=Integration"` then
      `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `test(dataaccess): verify EF Core discovery end to end`

---

### T36: Verify SQL, determinism and the secret-absence invariant

**What**: Restate spec.md's P1 literal-SQL Independent Test literally, and pin DAD-15 and DAD-20 against the
whole output tree.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/DataAccessDiscoveryEndToEndTests.cs` (modify)
**Depends on**: T35
**Reuses**: T35's fixture run
**Requirement**: DAD-15, DAD-16, DAD-20, DAD-21..DAD-28

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Each of the five SQL shapes produces the operation, node, columns and resolution the spec names
- [ ] The interpolated case produces an `Unresolved` access with no node and its SQL preserved
- [ ] A search of every file under the output root finds no credential-shaped text (DAD-15)
- [ ] Two consecutive runs over unchanged input produce byte-identical persistence output (DAD-20)
- [ ] All 28 P1 requirement rows in spec.md's traceability table are flipped to `Verified`
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then
      `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded before and after (no silent deletions)

**Tests**: integration
**Gate**: build

**Commit**: `test(dataaccess): verify SQL discovery, determinism and secret absence`

---

## Phase Execution Map

```
Phase 0 → Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6

Phase 0:  T1 → T2 → T3
Phase 1:  T4 → T5 → T6 → T7 → T8 → T9 → T10
Phase 2:  T11 → T12 → T13 → T14 → T15
Phase 3:  T16 → T17 → T18 → T19 → T20 → T21 → T22
Phase 4:  T23 → T24 → T25 → T26
Phase 5:  T27 → T28 → T29 → T30 → T31 → T32
Phase 6:  T33 → T34 → T35 → T36
```

Phase-boundary handoffs — the dependencies that cross a phase line:

```
T10 → T11
T15 → T16
T22 → T23
T26 → T27
T32 → T33
```

Execution is strictly sequential - there is no intra-phase parallelism. A single agent (or batch worker)
works one task at a time, in order.

**36 tasks across 7 phases.** Packing whole phases into ~7-task batches yields roughly five batches, so the
sub-agent offer applies at Execute.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2, T3 | 1 file each, 1 defect each | ✅ Granular |
| T4, T5, T6, T7 | 1 file each, 1 concept each | ✅ Granular |
| T8 | 3 files — one wire shape that cannot compile apart | ⚠️ Cohesive, deliberately not split |
| T9 | 2 files — serializer constant and schema must move together or the sync test fails | ⚠️ Cohesive, deliberately not split |
| T10 | 1 enum member plus its mapping | ✅ Granular |
| T11-T15 | 1 file each | ✅ Granular |
| T16-T22 | 1 file, 1 EF shape each | ✅ Granular |
| T23-T26 | 1 file, 1 parsing concern each | ✅ Granular |
| T27-T32 | 1 file, 1 resolution concern each | ✅ Granular |
| T33, T34 | 1 fixture document each | ✅ Granular |
| T35, T36 | 1 test file, 1 Independent Test each | ✅ Granular |

The two ⚠️ rows are the ones `validate_tasks.py` flags as a granularity smell. Both are deliberate: splitting
them would produce a commit that does not compile (T8) or a commit where the schema and the serializer
disagree and `FactualSchemaSyncTests` fails (T9). The skill's atomic-commit rule outranks the one-file
heuristic here.

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | — | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | None | — | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | T5 → T6 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T7 | T7 → T8 | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T9 | T9 → T10 | ✅ Match |
| T11 | T10 | cross-phase (Phase 1 → 2), backward | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T12 | T12 → T13 | ✅ Match |
| T14 | T13 | T13 → T14 | ✅ Match |
| T15 | T14 | T14 → T15 | ✅ Match |
| T16 | T15 | cross-phase (Phase 2 → 3), backward | ✅ Match |
| T17 | T16 | T16 → T17 | ✅ Match |
| T18 | T17 | T17 → T18 | ✅ Match |
| T19 | T18 | T18 → T19 | ✅ Match |
| T20 | T19 | T19 → T20 | ✅ Match |
| T21 | T20 | T20 → T21 | ✅ Match |
| T22 | T21 | T21 → T22 | ✅ Match |
| T23 | T22 | cross-phase (Phase 3 → 4), backward | ✅ Match |
| T24 | T23 | T23 → T24 | ✅ Match |
| T25 | T24 | T24 → T25 | ✅ Match |
| T26 | T25 | T25 → T26 | ✅ Match |
| T27 | T26 | cross-phase (Phase 4 → 5), backward | ✅ Match |
| T28 | T27 | T27 → T28 | ✅ Match |
| T29 | T28 | T28 → T29 | ✅ Match |
| T30 | T29 | T29 → T30 | ✅ Match |
| T31 | T30 | T30 → T31 | ✅ Match |
| T32 | T31 | T31 → T32 | ✅ Match |
| T33 | T32 | cross-phase (Phase 5 → 6), backward | ✅ Match |
| T34 | T33 | T33 → T34 | ✅ Match |
| T35 | T34 | T34 → T35 | ✅ Match |
| T36 | T35 | T35 → T36 | ✅ Match |

No task depends on a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Aggregate wiring | integration | integration | ✅ OK |
| T2 | Aggregate projection | integration | integration | ✅ OK |
| T3 | Aggregate wiring | integration | integration | ✅ OK |
| T4 | Fact model | unit | unit | ✅ OK |
| T5 | Identity types | unit | unit | ✅ OK |
| T6 | Fact model | unit | unit | ✅ OK |
| T7 | Fact model + validation | unit | unit | ✅ OK |
| T8 | JSON contract / schema | unit | unit | ✅ OK |
| T9 | JSON contract / schema | unit | unit | ✅ OK |
| T10 | Fact model + aggregate projection | integration | integration | ✅ OK |
| T11 | Collector seam | unit | unit | ✅ OK |
| T12 | Collector seam | unit | unit | ✅ OK |
| T13 | Collector seam | unit | unit | ✅ OK |
| T14 | Collector seam | unit | unit | ✅ OK |
| T15 | Collector seam | unit | unit | ✅ OK |
| T16-T22 | Domain analyzers | unit | unit | ✅ OK |
| T23-T25 | SQL tokenizer | unit | unit | ✅ OK |
| T26 | Domain analyzers | unit | unit | ✅ OK |
| T27-T29 | Resolver | unit | unit | ✅ OK |
| T30 | Fragment build | unit | unit | ✅ OK |
| T31 | Aggregate wiring | integration | integration | ✅ OK |
| T32 | Fragment build + aggregate | unit | unit | ✅ OK |
| T33 | Fixture source documents | none | none | ✅ OK |
| T34 | Fixture source documents | none | none | ✅ OK |
| T35 | End-to-end pipeline | integration | integration | ✅ OK |
| T36 | End-to-end pipeline | integration | integration | ✅ OK |

T33 and T34 are the only `Tests: none` entries, and the matrix explicitly says `none` for fixture source
documents — they are analysed input, not production code, and are exercised by T35 and T36.
