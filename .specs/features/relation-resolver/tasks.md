# RelationResolver Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and
Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the
full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**User-confirmed deviation from the standard flow:** the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by standing user request, as it was for
`symbol-index`, `relation-collector` and `data-access-discovery`. No mutants are injected at any point. The
Verifier's spec-anchored outcome check, per-AC `file:line` evidence, and `validation.md` report still run as
normal.

---

**Spec**: `.specs/features/relation-resolver/spec.md`
**Design**: `.specs/features/relation-resolver/design.md`
**Status**: Approved

**Scope of this task list**: P1 only — `RELR-01` through `RELR-39`. The spec's P2 (`RELR-40`..`RELR-45`) and
P3 (`RELR-46`..`RELR-49`) stories are deliberately not broken down here; they attach behind the same
`IRelationResolutionStrategy` seam in a later pass.

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationCollectorTests.cs`,
> `Analysis/RelationCollectorEndToEndTests.cs`, `Analysis/RelationCollectorWiringTests.cs`,
> `Analysis/Indexes/SymbolIndexTests.cs`, `Analysis/DataAccess/DatabaseMappingResolverTests.cs` and
> `DatabaseFragmentBuilderTests.cs`, `Facts/Validation/FactValidatorTests.cs`,
> `Facts/Serialization/FactualJsonTests.cs` / `FactualSchemaSyncTests.cs`,
> `Projection/Aggregates/RelationProjectorTests.cs`, `Analysis/V3DeterminismTests.cs`) and project guidelines.
> Guidelines found: `AGENTS.md` / `CLAUDE.md` — they route test quality to the `dotnet-test:*` skills as
> post-hoc gates rather than declaring a coverage threshold. No coverage-threshold tool config and no CI
> workflow exist in this repo, so strong defaults apply to the Coverage Expectation column.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Fact model + wire contract (`ResolutionMethod`, `RelationFact`, `RelationFactJson`, `FactualJsonMapper`) | unit | All branches; round-trip serialize/deserialize of both new fields; every one of the eight method values maps to its wire string and back | `tests/Csharp2Md.Core.Tests/Facts/Model/FactualModelTests.cs`, `Facts/Serialization/FactualJsonTests.cs` | `dotnet test csharp2md.slnx` |
| Fact validation (`FactValidator`, `C2M-FV-008`) | unit | All branches; every legal and every illegal `resolution_method` / `target_id` / `candidates` combination pinned | `tests/Csharp2Md.Core.Tests/Facts/Validation/FactValidatorTests.cs` | `dotnet test csharp2md.slnx` |
| JSON schema (`facts.schema.json`, `SchemaVersion`) | unit | Schema sync assertion moved to 5; every approved snapshot containing a fragment re-approved line by line | `tests/Csharp2Md.Core.Tests/Facts/Serialization/FactualSchemaSyncTests.cs`, `tests/**/snapshots/*.verified.*` | `dotnet test csharp2md.slnx` |
| Claim model + accumulator (`RawRelation`, `RelationClaimAccumulator`) | unit | All branches; 1:1 to RELR-21; construction-time evidence guard; canonical ordering stable across two collections; a document with no claim contributes no extent | `tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationClaimAccumulatorTests.cs` (new) | `dotnet test csharp2md.slnx` |
| Syntax capture (`SyntacticRelationCandidate`, `ClassifyCallsInvocation`, `DeclaredReceiverTypeName`) | unit | All branches; 1:1 to RELR-07 — one test per receiver shape the criterion names, plus the `var`-local shape that stays unresolvable | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs` | `dotnet test csharp2md.slnx` |
| Collector (`RelationCollector.CreateClaims` / `RefineClaims`) | unit | All branches; every one of the ten relation kinds still produces a claim; refined claims merge to the stronger `ShapeConfidence` | `tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationCollectorTests.cs` | `dotnet test csharp2md.slnx` |
| Resolution strategies (`ExistingTarget`, `DatabaseRelation`, `ReceiverType`, `SymbolIndex`, `Unresolved`) | unit | All branches; 1:1 to spec ACs RELR-02..RELR-08, RELR-13..RELR-16, RELR-27..RELR-32; each strategy's decline path proven, not assumed | `tests/Csharp2Md.Core.Tests/Analysis/Relations/Resolution/*Tests.cs` (new) | `dotnet test csharp2md.slnx` |
| Resolver orchestration (`RelationResolver`) | unit | All branches; 1:1 to RELR-09..RELR-12, RELR-22..RELR-26; a throwing strategy is isolated; ordinal keying reproduces today's ids | `tests/Csharp2Md.Core.Tests/Analysis/Relations/Resolution/RelationResolverTests.cs` (new) | `dotnet test csharp2md.slnx` |
| Fragment build (`RelationFragmentBuilder`) | unit | All branches; 1:1 to RELR-17..RELR-20; evidence, details and provenance preserved and only appended to | `tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationFragmentBuilderTests.cs` (new) | `dotnet test csharp2md.slnx` |
| Metrics projection (`ResolutionMetricsProjector`) | unit | All branches; 1:1 to RELR-33..RELR-36; a zero-relation run still yields every key at zero | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ResolutionMetricsProjectorTests.cs` (new) | `dotnet test csharp2md.slnx` |
| Aggregate writing (`CanonicalAggregateWriter`) | unit | `resolution.json` written on every run, including the empty one | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/CanonicalAggregateWriterTests.cs` | `dotnet test csharp2md.slnx` |
| Pipeline wiring (`AnalysisEngine`) | integration | Relations absent from document fragments and present in the resolver's fragment after a real `AnalyzeAsync` run; exit code unchanged by any `C2M-RELR-*` diagnostic | `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs` | `dotnet test csharp2md.slnx` |
| End-to-end pipeline | integration | All five P1 Independent Tests verbatim, plus every Edge Case listed in spec.md and the two-run byte-identity check | `tests/Csharp2Md.Core.Tests/Analysis/RelationResolverEndToEndTests.cs` (new, mirrors `DataAccessDiscoveryEndToEndTests.cs`) | `dotnet test csharp2md.slnx` |
| Contracts with no behaviour (`IRelationResolutionStrategy`, `RelationResolutionContext`, `RelationResolutionOutcome`) | none | Build gate only — an interface and two records with no logic; every branch that consumes them is covered by the strategy layer above | `src/Csharp2Md.Core/Analysis/Relations/Resolution/IRelationResolutionStrategy.cs` | build gate only |
| Fixture source documents | none | Build gate only — fixture code is analysed input, not production code | `fixtures/SyntheticSolution/**` | build gate only |

## Gate Check Commands

> Generated from the project's established commands, consistent with every prior feature's handoff in
> `.specs/STATE.md`. No CI workflow file exists in this repo to source them from instead.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks with unit tests only | `dotnet test csharp2md.slnx` |
| Full | After tasks with integration tests (pipeline wiring, fixture-level) | `dotnet test csharp2md.slnx` (integration tests carry `[Trait("Category","Integration")]` in the same suite; no separate command exists in this repo) |
| Build | After phase completion, the schema-version bump, and contract- or fixture-only changes | `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx` |

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a
phase execute in order.

### Phase 1: Wire contract and schema

The relation's new fields, their validation invariant, and the breaking schema bump. Everything downstream
serializes through this.

```
T1 → T2 → T3 → T4
```

### Phase 2: Claim model and syntax capture

What pass one must observe so pass two can resolve. No resolution happens here.

```
T5 → T6 → T7 → T8 → T9 → T10 → T11
```

### Phase 3: Collector emits claims

The three producers stop minting facts.

```
T12 → T13 → T14
```

### Phase 4: Resolution

The chain, its terminal strategy, the four resolving strategies, and identity minting.

```
T15 → T16 → T17 → T18 → T19 → T20 → T21
```

### Phase 5: Fragment and pipeline wiring

Where the resolver's output becomes a persisted fragment and the engine stops persisting relations per
document.

```
T22 → T23 → T24 → T25
```

### Phase 6: Metrics, fixtures, end-to-end verification

```
T26 → T27 → T28 → T29 → T30 → T31 → T32
```

---

## Task Breakdown

### T1: Add the ResolutionMethod vocabulary

**What**: Introduce the `ResolutionMethod` enum with its eight members and the ordinal wire-string mapping in
both directions.
**Where**: `src/Csharp2Md.Core/Facts/Model/RelationFact.cs`
**Depends on**: None
**Reuses**: `FactResolutionAlgebra`'s switch-with-explicit-throw shape for exhaustive enum mapping
**Requirement**: RELR-33

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] All eight members present: `Exact`, `Candidate`, `Syntactic`, `Configured`, `Convention`, `Dynamic`, `Heuristic`, `Unresolved`
- [x] Wire mapping is total: an undefined value throws `ArgumentOutOfRangeException`, never falls through to a default string
- [x] A test asserts every member round-trips through its wire string
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): add the relation resolution-method vocabulary`

---

### T2: Add Method and Candidates to the relation fact and its JSON contract

**What**: Add `ResolutionMethod Method` and `ImmutableArray<FactId> Candidates` to `RelationFact`, and
`resolution_method` / `candidates` to `RelationFactJson` at `JsonPropertyOrder` 8 and 9 so `details` keeps
order 7.
**Where**: `src/Csharp2Md.Core/Facts/Model/RelationFact.cs`, `src/Csharp2Md.Core/Facts/Serialization/FactualJsonContracts.cs`
**Depends on**: T1
**Reuses**: `RelationDetailJson`'s optional-array-with-default shape; `FactualJsonMapper.MapRelation`
**Requirement**: RELR-13

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:serialization`

**Done when**:

- [x] Existing serialized field order is unchanged for the seven fields that already existed
- [x] A round-trip test covers a relation with a target and no candidates, and one with candidates and no target
- [x] `candidates` serializes as an ordinal-ordered array of fact id strings
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): carry resolution method and candidates on a relation`

---

### T3: Validate the resolution-method invariant

**What**: Add `C2M-FV-008` to `FactValidator`: a non-null `target_id` implies a resolving method, `Candidate`
/ `Dynamic` / `Unresolved` imply a null target, and `candidates` is non-empty only for `Candidate`.
**Where**: `src/Csharp2Md.Core/Facts/Validation/FactValidator.cs`
**Depends on**: T2
**Reuses**: `C2M-FV-002`'s per-fact diagnostic construction and severity convention
**Requirement**: RELR-14

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Every legal combination validates and every illegal one produces `C2M-FV-008`, each pinned by its own test case
- [x] The diagnostic scope is the offending relation's fact id
- [x] No existing validator rule changes behaviour
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): reject a relation whose target contradicts its resolution method`

---

### T4: Bump the factual fragment schema to version 5

**What**: Move `FactualJsonSerializer.SchemaVersion` and `schemas/facts.schema.json`'s `const` from 4 to 5
together, add `resolution_method` to `relation_fact`'s required list and `candidates` to its properties, and
update every assertion and snapshot that names version 4.
**Where**: `src/Csharp2Md.Core/Facts/Serialization/FactualJsonSerializer.cs`, `schemas/facts.schema.json`
**Depends on**: T3
**Reuses**: AD-017's 3→4 bump as the exact precedent, including the aggregate envelopes staying at 2
**Requirement**: RELR-33

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:snapshot-testing`

**Done when**:

- [x] `FactualSchemaSyncTests` asserts 5 and still cross-checks the serializer constant against the schema file
- [x] Every version assertion found in `FactualJsonTests`, `SymbolFactEnricherTests` and `FactStoreTests` names 5, including test-method names that spell the number
- [x] Aggregate envelope assertions still name 2 and are untouched
- [x] Each re-approved snapshot diff is reviewed line by line and reported in the commit body
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release`, then `dotnet format csharp2md.slnx --verify-no-changes`, then `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `feat(facts)!: move the fragment schema to version 5`

---

### T5: Add the RawRelation claim contract

**What**: Introduce `RawRelation`, an internal never-serialized record carrying the observation plus every
field pass two needs, with the evidence guard enforced at construction.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/RelationContracts.cs`
**Depends on**: None (first task of its phase; phases run sequentially)
**Reuses**: `RawDatabaseClaim` (`Analysis/DataAccess/DataAccessContracts.cs`) shape for shape
**Requirement**: RELR-01

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] Constructing a claim with default `Evidence` throws, proven by a test
- [x] `Details` holds exactly what `RelationCollector.DetailsFor` produces today — no resolution field is a detail
- [x] The type carries no `SyntaxNode`, no `SemanticModel` and no source text
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): add the raw relation claim contract`

---

### T6: Add the relation claim accumulator

**What**: Introduce `RelationClaimAccumulator` and `RelationClaimSnapshot` with canonical evidence-first
ordering and extents retained only for documents that produced a claim.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/RelationClaimAccumulator.cs`
**Depends on**: T5
**Reuses**: `DatabaseClaimAccumulator` line for line, including its memory rule and ordering rationale
**Requirement**: RELR-21

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Two collections that add the same claims in different document orders produce an identical snapshot
- [x] A document that produced no claim contributes no `DocumentExtent`, proven by a test
- [x] `AddResolved` accepts already-targeted claims without an extent, for the database resolver's output
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): accumulate relation claims across the run`

---

### T7: Capture receiver, member and argument shape on the syntactic candidate

**What**: Extend `SyntacticRelationCandidate` with `ReceiverText`, `ReceiverTypeText`, `MemberName`,
`ArgumentCount` and `ArgumentTypes`, and populate them in `ClassifyCallsInvocation`.
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T6
**Reuses**: `ClassifyCallsInvocation`'s existing `DeclaredReceiverTypeName` call and `NormalizeNode`
**Requirement**: RELR-05

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] `target_text` keeps its exact current value, so no relation identity changes — asserted by a test
- [x] `ArgumentTypes` holds simple type names, with `null` for an argument whose type syntax cannot be read
- [x] A test covers a call with zero arguments, one with typed arguments, and one with an unreadable argument type
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(syntax): capture receiver and argument shape on a call candidate`

---

### T8: Resolve field and property receivers

**What**: Extend `DeclaredReceiverTypeName` to resolve an identifier bound to a field or a property declared
on the enclosing type.
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T7
**Reuses**: The method's existing enclosing-member walk and `SimpleTypeName`
**Requirement**: RELR-07

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A call through a field-backed receiver resolves to the field's declared type
- [x] A call through a property-backed receiver resolves to the property's declared type
- [x] A field whose type is `var`-like or unreadable still returns `null` rather than guessing
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(syntax): resolve field and property receivers`

---

### T9: Resolve constructor and primary-constructor parameter receivers

**What**: Extend `DeclaredReceiverTypeName` to resolve an identifier bound to a constructor parameter or a
primary-constructor parameter of the enclosing type.
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T8
**Reuses**: The `BaseMethodDeclarationSyntax` parameter lookup already present for method parameters
**Requirement**: RELR-07

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] The design's worked example resolves: `public OrderService(PaymentClient paymentClient)` makes `paymentClient` resolve to `PaymentClient`
- [x] A primary-constructor parameter on a class and on a record both resolve
- [x] A parameter shadowed by a local of a different type resolves to the local, matching C# scoping
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(syntax): resolve constructor and primary-constructor receivers`

---

### T10: Resolve pattern-variable receivers and pin the var-local limit

**What**: Extend `DeclaredReceiverTypeName` to resolve a declaration-pattern variable, and add the test that
pins a `var`-declared local as deliberately unresolvable by syntax.
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T9
**Reuses**: The existing `VariableDeclaratorSyntax` walk
**Requirement**: RELR-07

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A call through `is PaymentClient client` resolves `client` to `PaymentClient`
- [x] A `var`-declared local returns `null`, with the limit stated in the method's doc comment rather than left implicit
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(syntax): resolve pattern-variable receivers`

---

### T11: Capture the document's namespace and imports

**What**: Capture the enclosing namespace and the document's using directives once per document and carry
them onto every candidate, so `SymbolLookup` can use them as contextual hints.
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T10
**Reuses**: `SymbolLookup.Namespace` / `.Imports`, which already exist unused on the index's query shape
**Requirement**: RELR-04

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] File-scoped and block-scoped namespace declarations both captured
- [x] Global usings present in the document are captured; a document with no usings yields an empty array, never null
- [x] Capture happens once per document, not once per candidate
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(syntax): capture namespace and imports for relation lookups`

---

### T12: Turn the syntax-only collector into a claim producer

**What**: Replace `RelationCollector.CreateFacts` with `CreateClaims`, returning `RawRelation` instead of
`RelationFact` and deleting the fixed `UnresolvedReasonText` placeholder.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/RelationCollector.cs`
**Depends on**: None (first task of its phase; phases run sequentially)
**Reuses**: `DetailsFor` and `PartitionFor` unchanged; the ordinal logic moves to the resolver in T21
**Requirement**: RELR-16

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] All ten relation kinds still produce a claim, each pinned by an existing test rewritten to the new return type
- [x] `UnresolvedReasonText` no longer exists anywhere in the codebase (grep-verified: only stale `bin/`/`obj/` binaries match)
- [x] No test is rewritten away without a claim-shape replacement, except one disclosed below
- [~] Gate check passes: `dotnet test csharp2md.slnx` - see disclosed deviation
- [x] Test count recorded (no silent deletions) - see disclosed deviation

**Disclosed deviation**: `CreateClaims` can no longer be called from `AnalysisEngine.cs`'s document loop the
way `CreateFacts` was - `RawRelation` carries no `RelationFactId` (ordinal/id minting moves to the resolver
at T21, per this task's own Reuses note), so it cannot be wrapped into a `RelationFact` for the document
fragment. The forced, minimal, disclosed fix (`AnalysisEngine.cs`, outside this task's literal `Where`) drops
relations from the per-document `baseline` entirely, which is what T23 was always going to do
("Remove relations from the per-document baseline and enrichment") - done here only because the type change
leaves no other option that compiles. This turns 12 pre-existing, out-of-scope integration/snapshot tests
red (`RelationCollectorWiringTests`, `RelationCollectorEndToEndTests`, `AggregateRelationPartitionTests`,
`EndToEndTests.Run_AgainstFixture_WritesSyntaxOnlyRelationPartitionsWithoutV2Graph`,
`V3DeterminismTests.AnalyzeAsync_RepresentativeDocument_MatchesApprovedMarkdownSnapshot`) plus, once T13 lands,
the trusted-mode counterparts (`RelationCollectorTrustedWiringTests`) - exactly the "tests that assert
relations inside document fragments" AD-018's Trade-off section names and T25 is scoped to fix, in Phase 5
after T23 wires the real resolver. `dotnet test csharp2md.slnx` at T12: 1765/1779 passed, 12 relation-pipeline
failures (all pre-existing, none newly written by T12) + 2 unrelated pre-existing flakes
(`DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml`, documented flaky in
STATE.md's relation-collector history; `V3SecurityBoundaryTests.EvaluationProcessCancellation_...`, a timing
test unrelated to relations). One test removed with no claim-level equivalent:
`CreateFacts_RealisticDocumentInput_PassesFactValidatorCleanly` validated that `CreateFacts`'s output (an
`IFact`-implementing `RelationFact` array) passes `FactValidator` - `RawRelation` is not an `IFact` and is
never validated or serialized (design.md: "never serialized"), so this exact check has no home at the claim
layer; fact-shape validation against `FactValidator` becomes `RelationFragmentBuilder`'s concern at T22.

**Tests**: unit
**Gate**: quick

**Commit**: `refactor(relations): collect relation claims instead of facts`

---

### T13: Turn semantic refinement into claim refinement

**What**: Replace `RelationCollector.Refine` with `RefineClaims`, merging a refined claim into its baseline by
`FactResolutionAlgebra.Stronger` instead of relying on `FactMerger`'s fact-level rank merge.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/RelationCollector.cs`
**Depends on**: T12
**Reuses**: `FactResolutionAlgebra.Stronger`; the existing base-list refinement and inferred-publish discovery
**Requirement**: RELR-01

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A refined `inherits`/`implements` claim replaces its baseline counterpart and keeps the stronger `ShapeConfidence`
- [x] The inferred-publish path still discovers a `PublishAsync(message)` call the syntax pass could read no target from
- [x] A candidate that cannot be refined leaves its baseline claim untouched rather than dropping it
- [~] Gate check passes: `dotnet test csharp2md.slnx` - same disclosed deviation as T12, now +1
- [x] Test count recorded (no silent deletions) - 18 tests in RelationCollectorTests.cs before and after (3 `Refine_*` tests rewritten 1:1 to 3 `RefineClaims_*` tests)

**Disclosed deviation (continues T12's)**: `TrustedSemanticProjectProcessor.cs`'s call to `RelationCollector.Refine`
(not in this task's literal `Where`, forced by the signature change to `RefineClaims`) is updated to call
`RefineClaims` and its `EnrichedRelations` field is retyped to `ImmutableArray<RawRelation>`, but
`AnalysisEngine.cs`'s `enrichment` no longer concatenates it (`RawRelation` is not an `IFact`) - the same
minimal, disclosed, T23-anticipating fix as T12. This surfaces one additional pre-existing, out-of-scope
integration test red: `RelationCollectorTrustedRefinementWiringTests.AnalyzeAsync_TrustedMode_PublishAsyncThroughAVariable_ResolvesTargetTextThroughTheRealPipeline`
(the trusted-mode counterpart of the inferred-publish path, asserted end-to-end through the real pipeline).
`dotnet test csharp2md.slnx` after T13: 1763/1779 passed, 15 relation-pipeline failures (14 from T12's set +
this 1 new one) + 1 unrelated pre-existing flake (`DotnetMsBuildEvaluatorTests...`; the other flake from T12's
run, `V3SecurityBoundaryTests...`, did not reproduce this run - confirming it is non-deterministic and
unrelated). All still exactly the class of test AD-018 and T25 name.

**Tests**: unit
**Gate**: quick

**Commit**: `refactor(relations): merge semantic refinement at the claim level`

---

### T14: Turn database relations into claims

**What**: Change `DatabaseMappingResolver` to emit `RawRelation` claims carrying their already-proven
`TargetId`, the `mapping` detail and the SQL details, instead of feeding `DatabaseFragmentBuilder`'s
`RelationFacts`.
**Where**: `src/Csharp2Md.Core/Analysis/DataAccess/DatabaseMappingResolver.cs`
**Depends on**: T13
**Reuses**: `ResolvedDatabaseRelation`, which already holds every field the claim needs
**Requirement**: RELR-32

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] `DatabaseFragmentBuilder` stops emitting relation facts and keeps emitting object and column facts unchanged
- [x] Every existing `DatabaseMappingResolverTests` assertion about relation kind, operation and target survives, rewritten against the claim (51/51 pass in this file; net +5 tests, see below)
- [x] The `mapping` detail's `configured` / `convention` value reaches the claim intact (unchanged `Detail(..., "mapping")` assertions all still pass)
- [~] Gate check passes: `dotnet test csharp2md.slnx` - **major disclosed deviation, larger than T12/T13's, see below**
- [x] Test count recorded (no silent deletions) - see below

**MAJOR DISCLOSED DEVIATION - wider blast radius than T12/T13, flagging for orchestrator review before
Phase 4**: Removing `RelationFacts` from `DatabaseFragmentBuilder.Build` (this task's own explicit
requirement) means the real `AnalyzeAsync` pipeline no longer writes *any* database relation anywhere -
`resolution.Relations` is computed by `DatabaseMappingResolver.Resolve` but nothing yet consumes it
(threading it into `RelationClaimAccumulator` is T23's explicit job: "thread the claim accumulator", and
nothing can persist it before the resolver chain exists in Phase 4-5 regardless). Unlike T12/T13's
disconnect - which broke only *this feature's own* prior-phase tests (`RelationCollector*`) - this one
breaks `raw/facts/relations/data.json` and the `database` fragment for the entire, already-shipped,
previously-merged **`data-access-discovery` feature**, whose own `DataAccessDiscoveryEndToEndTests.cs`
end-to-end suite is not a "relations inside document fragments" case (AD-018's Trade-off section, written
for `relation-collector`'s document-embedded relations, does not name this file or this feature).
`dotnet test csharp2md.slnx` after T14: 1734/1781 passed, 47 failures:
- 15 continuing unchanged from T13's set (`RelationCollectorWiringTests`, `RelationCollectorTrustedWiringTests`,
  `RelationCollectorTrustedRefinementWiringTests`, `RelationCollectorEndToEndTests`,
  `AggregateRelationPartitionTests.HttpPartitionFile_.../StructuralPartitionFile_...`, one CLI end-to-end
  test, one `V3DeterminismTests` snapshot) - not worsened by T14.
- **30 newly red, caused by T14, NOT anticipated by AD-018's text**: all 27 test cases in
  `DataAccessDiscoveryEndToEndTests.cs` (`DAD01`-`DAD28`), `AnalysisEngineTests.AnalyzeAsync_EfCoreProject_WritesTheResolvedDataRelationPartition`,
  and `AggregateRelationPartitionTests.DataPartitionFile_CarriesThePersistenceRelationsPassTwoResolved` /
  `DataPartitionFile_RecordsTheFixturesUnconfiguredEntityAsAConventionMapping`.
- 2 unrelated, pre-existing, order-dependent flakes, both pass in isolation and reproduce on files this
  task never touched: `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml`
  (documented flaky in STATE.md's relation-collector history) and
  `ProcessTreeProbeTests.ServiceTimeout_KillsParentAndDescendantBeforeReturningAndDoesNotCancelNextService`
  (2/2 pass standalone, confirmed here).

No way was found to implement this task's own explicit Done-when (`DatabaseFragmentBuilder` stops emitting
relation facts) without this consequence - `RelationClaimAccumulator` is not threaded into `AnalyzeAsync`
until T23, and nothing else in Phase 3/4 can persist a `RawRelation` claim. **Recommendation**: before Phase 4
starts, either fold `DataAccessDiscoveryEndToEndTests.cs` / `AnalysisEngineTests.AnalyzeAsync_EfCoreProject_WritesTheResolvedDataRelationPartition` /
`AggregateRelationPartitionTests.DataPartitionFile_*` explicitly into T25's scope (currently titled only
"tests that assert relations inside document fragments," which does not literally cover them), or add a
dedicated task for them - so Phase 5 does not close this feature while leaving an already-shipped feature's
own end-to-end proof broken.

**Test count**: `DatabaseMappingResolverTests.cs` +5 (new: `Resolve_EveryRelation_IsADataPartitionClaimCarryingEvidence`,
`Resolve_RelationCarryingAnAlreadyProvenTarget_ReportsConfiguredAsTheProducerMethod`,
`Resolve_UntargetedRelation_ReportsNoProducerMethod`,
`Resolve_TwoIdenticalAccessesInOneMember_BothSurviveAsDistinctClaims`,
`Resolve_RelationCarryingMultilineSql_PreservesTheRawTextWithoutThrowing`). `DatabaseFragmentBuilderTests.cs`
net -3 (removed 4 relation-fact tests whose subject no longer exists at this layer - one,
`Build_ConventionMapping_KeepsItsUnresolvedReasonOnTheFact`, had its exact concern already covered verbatim
by the pre-existing `Resolve_EntitySetWithoutConfiguration_YieldsHeuristicMapsToNamingTheSetWithNoTarget`;
the other three relocated to `DatabaseMappingResolverTests.cs` above, since their subject -
`DatabaseMappingResolver.Resolve`'s own claim output - now lives there, not in the built fragment; added 1
new test, `Build_ResolutionWithOnlyRelationsAndNoObjectsOrColumns_YieldsNoFragment`, covering this task's
`DatabaseResolution.IsEmpty` redefinition).

`dotnet build csharp2md.slnx -c Release`: 0 warnings, 0 errors. `dotnet format csharp2md.slnx
--verify-no-changes`: clean.

**Tests**: unit
**Gate**: quick

**Commit**: `refactor(dataaccess): emit database relations as claims`

---

### T15: Define the resolution strategy contract

**What**: Introduce `IRelationResolutionStrategy`, `RelationResolutionContext` and
`RelationResolutionOutcome`, with the doc comment recording why the context carries no `SemanticModel`.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/Resolution/IRelationResolutionStrategy.cs`
**Depends on**: None (first task of its phase; phases run sequentially)
**Reuses**: `IDataAccessAnalyzer`'s single-method, no-I/O strategy shape
**Requirement**: RELR-09

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] `RelationResolutionOutcome.None` expresses "declined" distinctly from "handled with no target"
- [x] The context exposes the index and the known-fact-id set, and nothing that would let a strategy do I/O
- [x] The `SemanticModel` omission is documented on the type, not left to be rediscovered
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release`, then `dotnet format csharp2md.slnx --verify-no-changes`, then `dotnet test csharp2md.slnx`

**Tests**: none
**Gate**: build

**Commit**: `feat(relations): define the relation resolution strategy contract`

---

### T16: Add the resolver chain and its terminal strategy

**What**: Implement `RelationResolver` walking the strategy list in fixed order with per-strategy failure
isolation, plus `UnresolvedStrategy`, which always handles.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/Resolution/RelationResolver.cs`
**Depends on**: T15
**Reuses**: `DataAccessCollector`'s per-analyzer failure isolation (`C2M-DA-001`) as the model for `C2M-RELR-006`
**Requirement**: RELR-09

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] The chain stops at the first strategy that handles a claim, proven with a recording double
- [x] A throwing strategy contributes `C2M-RELR-006`, has its outcome discarded, and the walk continues
- [x] Every claim yields exactly one relation, because the terminal strategy cannot be fallen through
- [x] A relation left unresolved carries an `unresolved_reason` naming the observed text, and `C2M-RELR-001`
- [x] Cancellation propagates and is never converted into a diagnostic
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): resolve relations through an ordered strategy chain`

---

### T17: Resolve targets through the symbol index

**What**: Implement `SymbolIndexStrategy`: a unique best-ranked candidate becomes a `syntactic` target, a tie
becomes `candidate` with every id listed, nothing found declines to the terminal strategy.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/Resolution/SymbolIndexStrategy.cs`
**Depends on**: T16
**Reuses**: `ISymbolIndex.FindCandidates`, which already returns `Unique` / `Ambiguous` / `NotFound` without choosing
**Requirement**: RELR-04

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A unique match sets `target_id` and `resolution_method: syntactic`
- [x] An ambiguous match leaves `target_id` null, sets `candidate`, lists every tied id ordinal-ordered, and emits `C2M-RELR-002`
- [x] No code path selects an element from a multi-entry best tier
- [x] An empty or whitespace `target_text` declines without querying the index
- [x] Namespace, project and imports are passed as hints, not as filters
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): resolve relation targets through the symbol index`

---

### T18: Resolve call targets through the receiver's type

**What**: Implement `ReceiverTypeStrategy`: look a `calls` target up through `FindMethods` using the captured
receiver type, member name, argument count and argument types.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/Resolution/ReceiverTypeStrategy.cs`
**Depends on**: T17
**Reuses**: `ISymbolIndex.FindMethods` and its `MethodLookup` shape
**Requirement**: RELR-05

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Exactly one method at the top rank sets `target_id` and `syntactic`
- [x] More than one at the top rank yields `candidate` with every tied id, never a pick
- [x] An undeterminable receiver type yields `unresolved` and `C2M-RELR-003`
- [x] A relation kind other than `calls` is declined without a lookup
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): resolve call targets through the receiver type`

---

### T19: Pass through targets the producer already proved

**What**: Implement `ExistingTargetStrategy`: keep a claim's existing `target_id` when the run contains it,
and demote it to `unresolved` with `C2M-RELR-007` when it does not.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/Resolution/ExistingTargetStrategy.cs`
**Depends on**: T18
**Reuses**: The context's known-fact-id set
**Requirement**: RELR-02

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A present target survives unchanged with the producer's reported method
- [x] An absent target is nulled, marked `unresolved` and diagnosed `C2M-RELR-007` at `Warning`
- [x] A claim with a missing source fact id is diagnosed and still emitted
- [x] A claim with no target at all is declined, not handled
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): keep targets the producing stage already proved`

---

### T20: Distinguish configured from convention database targets

**What**: Implement `DatabaseRelationStrategy`: map the `mapping` detail onto `configured` / `convention`,
and an interpolated-SQL target onto `dynamic`.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/Resolution/DatabaseRelationStrategy.cs`
**Depends on**: T19
**Reuses**: `DatabaseMappingResolver.MappingKey`, `ConfiguredMapping`, `ConventionMapping`, `SqlKey`
**Requirement**: RELR-27

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] A `ToTable`-configured target yields `configured`; a convention-named one yields `convention` and `C2M-RELR-005`
- [x] No database target is ever marked `exact`
- [x] An interpolated-SQL target yields `dynamic` with a null target, the observed text kept, and `C2M-RELR-004`
- [x] The claim's `DatabaseOperation` and relation kind are unchanged by the strategy
- [x] No `DatabaseObjectFact` or `DatabaseColumnFact` is created, altered or deleted anywhere in the strategy
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): distinguish configured from convention database targets`

---

### T21: Mint relation identity independently of resolution

**What**: Move identity minting into the resolver, keyed `owner \0 kind \0 fingerprint` with a one-based
occurrence ordinal, and order the output by `RelationFactId`.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/Resolution/RelationResolver.cs`
**Depends on**: T20
**Reuses**: `RelationFactId.Create` and `DatabaseFragmentBuilder`'s ordinal-keyed minting
**Requirement**: RELR-22

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A test asserts every id the resolver mints for the fixture equals the id the pre-change collector minted for the same claim — this is the design's named ordinal risk, so it is proven, not argued
- [ ] No resolution field reaches the fingerprint, proven by resolving one claim two ways and comparing ids
- [ ] Two claims tying on every ranking signal both appear, ordered ordinally, with no first-seen tiebreak
- [ ] Output is ordered by `RelationFactId` ordinal comparison
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): mint relation identity independently of resolution`

---

### T22: Build the resolved relation fragment

**What**: Implement `RelationFragmentBuilder`, turning the resolution into facts and validating them through
the existing `FragmentValidationFunc` path with source and target ids declared known.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/RelationFragmentBuilder.cs`
**Depends on**: None (first task of its phase; phases run sequentially)
**Reuses**: `DatabaseFragmentBuilder` structurally, including its `knownFactIds` argument and its empty-resolution short circuit
**Requirement**: RELR-17

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Every `Evidence`, `RelationDetail` and `FactProvenance` the claim carried is present on the fact, with the resolver's own provenance appended
- [ ] A zero-relation resolution yields no fragment and no diagnostics
- [ ] A validation failure is a structural failure with the validator's own diagnostics, matching the database fragment's behaviour
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): build the resolved relation fragment`

---

### T23: Wire the resolver into the analysis engine

**What**: Remove relations from the per-document baseline and enrichment, thread the claim accumulator, and
run resolve → build → persist once after the symbol index and the database resolver.
**Where**: `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs`
**Depends on**: T22
**Reuses**: The `DatabaseClaimAccumulator` threading and the pass-two block already sitting in `AnalyzeAsync`
**Requirement**: RELR-01

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A real `AnalyzeAsync` run produces document fragments containing no relations and one solution-level fragment containing all of them
- [ ] The resolver is the only call site constructing a `RelationFact`, verified by search and stated in the commit body
- [ ] A `C2M-RELR-*` diagnostic alone leaves `ExitCode` at 0, asserted at the `AnalyzeAsync` level rather than by inspection
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `feat(relations): resolve relations in pass two of the analysis engine`

---

### T24: Append evidence for the declaration that proved a target

**What**: When a strategy proves a target through a declaration the claim's own span does not cover, append
that declaration's span as an additional `Evidence` entry.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/Resolution/RelationResolver.cs`
**Depends on**: T23
**Reuses**: `SymbolFact`'s existing evidence, which already carries the declaration span
**Requirement**: RELR-18

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] A resolved `calls` relation carries both the invocation span and the resolved method's declaration span
- [ ] The claim's original evidence entry is present and unmodified, proven by comparing the entry, not the count
- [ ] An unresolved relation gains no evidence
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): record the declaration that proved a target`

---

### T25: Migrate tests that assert relations inside document fragments

**What**: Rewrite every existing assertion that looks for relations in a document fragment to look in the
resolver's fragment, and re-approve the affected snapshots.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorEndToEndTests.cs`,
`tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorWiringTests.cs`,
`tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorTrustedWiringTests.cs`,
`tests/Csharp2Md.Core.Tests/Analysis/V3DeterminismTests.cs`,
`tests/Csharp2Md.Core.Tests/Projection/Aggregates/AggregateRelationPartitionTests.cs`,
`tests/Csharp2Md.Core.Tests/Cli/EndToEndTests.cs`,
`tests/Csharp2Md.Core.Tests/Analysis/DataAccessDiscoveryEndToEndTests.cs`,
`tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs`
**Depends on**: T24
**Reuses**: The `data-access-discovery` precedent for re-approving a snapshot line by line
**Requirement**: RELR-12

**Scope note (added after Phase 3 execution, 2026-08-21):** The T12-T14 batch confirmed by direct
`dotnet test` run that this breakage is wider than `spec.md`/`design.md` anticipated. Two distinct
groups of pre-existing tests now fail because nothing between T14 and T23 persists a relation yet
(T14's own worker report + this run's own tail confirm 46 failures, matching): (1) tests scoped to
this feature's own `RelationCollector` output — `RelationCollectorWiringTests`,
`RelationCollectorTrustedWiringTests` (incl. its `+Refinement` fixture),
`RelationCollectorEndToEndTests`, `AggregateRelationPartitionTests.HttpPartitionFile_*` /
`StructuralPartitionFile_*`, one `V3DeterminismTests` snapshot, and one
`Cli/EndToEndTests.Run_AgainstFixture_WritesSyntaxOnlyRelationPartitionsWithoutV2Graph` case; (2) a
second, previously-undisclosed group belonging to the **already-shipped** `data-access-discovery`
feature, broken purely because T14 changed `DatabaseMappingResolver`'s output type — all 27 cases in
`DataAccessDiscoveryEndToEndTests.cs`,
`AnalysisEngineTests.AnalyzeAsync_EfCoreProject_WritesTheResolvedDataRelationPartition`, and
`AggregateRelationPartitionTests.DataPartitionFile_CarriesThePersistenceRelationsPassTwoResolved` /
`DataPartitionFile_RecordsTheFixturesUnconfiguredEntityAsAConventionMapping`. Both groups are
explicitly this task's scope now — T25 is not done until every one of them passes again, on top of
the `RelationCollectorEndToEndTests.cs` migration `spec.md` already called for. Two pre-existing,
unrelated flakes (`DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml`,
`ProcessTreeProbeTests`) are out of scope — confirmed reproducing before T12 and passing in isolation.

**Done when**:

- [ ] Every migrated assertion is at least as strict as the one it replaces; any that becomes stricter is called out in the commit body
- [ ] No test is deleted, skipped, or weakened to accommodate the move
- [ ] `RelationCollectorWiringTests` and `RelationCollectorTrustedWiringTests` assert the claim path and still cover what they covered before
- [ ] `DataAccessDiscoveryEndToEndTests.cs` (all 27 cases) and `AnalysisEngineTests.AnalyzeAsync_EfCoreProject_WritesTheResolvedDataRelationPartition` pass again against the resolver's fragment, unweakened
- [ ] Both `AggregateRelationPartitionTests.DataPartitionFile_*` cases pass again
- [ ] Each re-approved snapshot diff is reviewed line by line
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release`, then `dotnet format csharp2md.slnx --verify-no-changes`, then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: integration
**Gate**: build

**Commit**: `test(relations): assert relations in the resolver fragment`

---

### T26: Project resolution metrics

**What**: Implement `ResolutionMetricsProjector` and `ResolutionMetricsAggregate`, counting from the projected
partitions rather than from the resolver's own tally.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/ResolutionMetricsProjector.cs`
**Depends on**: None (first task of its phase; phases run sequentially)
**Reuses**: `RelationProjector`'s partition projection as the single source the counts derive from
**Requirement**: RELR-33

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] All eight method keys always present, at zero when unused
- [ ] A per-partition breakdown covers every `RelationPartition` member, projected over the enum rather than a hand-kept list
- [ ] The totals equal the relations in the projected partitions, asserted rather than assumed
- [ ] A zero-relation projection yields every count at zero, not an empty object
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): count relation resolution outcomes`

---

### T27: Write resolution.json

**What**: Write `raw/facts/relations/resolution.json` at envelope version 1 on every run, including runs that
produced no relations.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/CanonicalAggregateWriter.cs`
**Depends on**: T26
**Reuses**: The writer's existing `files.Write($"raw/facts/relations/{wireName}.json", ...)` block and `AggregateJsonContext`
**Requirement**: RELR-35

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] The file exists after a run with relations and after a run with none
- [ ] The aggregate envelopes for the partition files still name version 2, untouched
- [ ] The file is listed in the manifest alongside the other aggregates
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): write the relation resolution metrics aggregate`

---

### T28: Audit the resolver's diagnostics

**What**: Verify every `C2M-RELR-*` diagnostic is scoped to its relation's fact id, uses a code from the
declared set, and never changes the exit code on its own.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/Relations/Resolution/RelationResolverDiagnosticsTests.cs`
**Depends on**: T27
**Reuses**: `AnalysisDiagnostic`'s scope field and the `C2M-DA-001` scoping convention
**Requirement**: RELR-37

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Each of `C2M-RELR-001` through `C2M-RELR-007` is provoked by a test and asserted for code, severity and scope
- [ ] A test asserts no code outside the declared set can be emitted by the resolver
- [ ] A run producing only `C2M-RELR-*` diagnostics exits 0
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(relations): pin the resolver diagnostic contract`

---

### T29: Extend the synthetic fixture for resolution

**What**: Add the cross-project call, the two-namespace ambiguous type, and one source per receiver shape to
`fixtures/SyntheticSolution`.
**Where**: `fixtures/SyntheticSolution`
**Depends on**: T28
**Reuses**: The existing fixture projects and the `data-access-discovery` fixture-extension pattern
**Requirement**: RELR-04

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] `OrderService` calls `PaymentClient.Authorize` across a project boundary through a constructor-injected receiver
- [ ] One type name exists in two namespaces and is referenced from a third project, producing a genuine tie
- [ ] Field, property, constructor-parameter, primary-constructor-parameter, pattern-variable and `var`-local receivers each appear once
- [ ] The fixture still builds and every pre-existing fixture assertion still passes
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release`, then `dotnet format csharp2md.slnx --verify-no-changes`, then `dotnet test csharp2md.slnx`

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): add resolution scenarios to the synthetic solution`

---

### T30: Prove the five P1 Independent Tests end to end

**What**: Implement each of the spec's five P1 Independent Tests verbatim against a real CLI run over the
fixture.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/RelationResolverEndToEndTests.cs`
**Depends on**: T29
**Reuses**: `DataAccessDiscoveryEndToEndTests` as the structural model for a real-run integration test
**Requirement**: RELR-01

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] The cross-project `calls` relation carries the `PaymentClient.Authorize` symbol id and `syntactic`
- [ ] The ambiguous relation carries a null target, `candidate`, both ids, and a `C2M-RELR-002` entry in `diagnostics.json`
- [ ] Two runs into two output roots produce byte-identical relation partition files
- [ ] The configured, convention and dynamic database relations each carry their expected method, with the convention one's header resolution still `Heuristic`
- [ ] The `resolution.json` totals equal the partition files, summed by the test rather than restated
- [ ] `raw/dependencies.mmd` contains at least one edge outside the `data` partition
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `test(relations): prove the P1 independent tests end to end`

---

### T31: Cover the spec's edge cases

**What**: Add one test per Edge Case listed in `spec.md`.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/Relations/Resolution/RelationResolverEdgeCaseTests.cs`
**Depends on**: T30
**Reuses**: The strategy test doubles built in T16 through T20
**Requirement**: RELR-12

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:test-gap-analysis`

**Done when**:

- [ ] An empty symbol index yields every relation unresolved and no throw
- [ ] A zero-relation run persists no fragment and still writes `resolution.json` at zero
- [ ] A missing source fact id yields `C2M-RELR-007` and the relation is still emitted
- [ ] A whitespace `target_text` is left unresolved with no index query, asserted with a recording index double
- [ ] A duplicate `RelationFactId` fails structurally
- [ ] The same type name in two non-referencing projects yields candidates rather than a same-project preference
- [ ] Cancellation mid-resolution propagates and produces no diagnostic
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(relations): cover the resolver edge cases`

---

### T32: Close the P1 traceability

**What**: Flip all 39 P1 `RELR-NN` rows in `spec.md`'s traceability table to `Verified`, each backed by a
`file:line` citation gathered while closing it.
**Where**: `.specs/features/relation-resolver/spec.md`
**Depends on**: T31
**Reuses**: The `data-access-discovery` closing task as the precedent
**Requirement**: RELR-39

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] All 39 P1 rows read `Verified`; the ten P2/P3 rows are untouched
- [ ] Every flipped row has a test that asserts its criterion, cited by `file:line` in the commit body
- [ ] Any criterion that cannot be substantiated is left `Pending` and reported, never flipped optimistically
- [ ] The Coverage line is updated to the real mapped count
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release`, then `dotnet format csharp2md.slnx --verify-no-changes`, then `dotnet test csharp2md.slnx`

**Tests**: none
**Gate**: build

**Commit**: `docs(spec): close the relation-resolver P1 traceability`

---

## Phase Execution Map

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6

Phase 1:  T1 → T2 → T3 → T4
Phase 2:  T5 → T6 → T7 → T8 → T9 → T10 → T11
Phase 3:  T12 → T13 → T14
Phase 4:  T15 → T16 → T17 → T18 → T19 → T20 → T21
Phase 5:  T22 → T23 → T24 → T25
Phase 6:  T26 → T27 → T28 → T29 → T30 → T31 → T32
```

Execution is strictly sequential - there is no intra-phase parallelism.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1 | 1 enum + its mapping | Granular |
| T2 | 2 fields across a model and its mirror contract | OK - cohesive, one change |
| T3 | 1 validation rule | Granular |
| T4 | 1 version constant + its schema mirror | OK - cohesive, must move together |
| T5 | 1 record | Granular |
| T6 | 1 class | Granular |
| T7 | 1 record extension + its one populating method | Granular |
| T8 | 1 method, 2 receiver shapes | Granular |
| T9 | 1 method, 2 receiver shapes | Granular |
| T10 | 1 method, 1 receiver shape | Granular |
| T11 | 1 capture, once per document | Granular |
| T12 | 1 method signature change | Granular |
| T13 | 1 method signature change | Granular |
| T14 | 1 producer's output type | Granular |
| T15 | 1 interface + 2 records | OK - one contract |
| T16 | 1 orchestrator + its terminal strategy | OK - the terminal strategy is what makes the chain testable |
| T17 | 1 strategy | Granular |
| T18 | 1 strategy | Granular |
| T19 | 1 strategy | Granular |
| T20 | 1 strategy | Granular |
| T21 | 1 concern in 1 class | Granular |
| T22 | 1 class | Granular |
| T23 | 1 wiring change | Granular |
| T24 | 1 concern | Granular |
| T25 | 1 test migration | Granular |
| T26 | 1 projector | Granular |
| T27 | 1 file written | Granular |
| T28 | 1 test file | Granular |
| T29 | 1 fixture | Granular |
| T30 | 1 test file | Granular |
| T31 | 1 test file | Granular |
| T32 | 1 document | Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | phase start | Match |
| T2 | T1 | T1 → T2 | Match |
| T3 | T2 | T2 → T3 | Match |
| T4 | T3 | T3 → T4 | Match |
| T5 | None | prior phase | Match |
| T6 | T5 | T5 → T6 | Match |
| T7 | T6 | T6 → T7 | Match |
| T8 | T7 | T7 → T8 | Match |
| T9 | T8 | T8 → T9 | Match |
| T10 | T9 | T9 → T10 | Match |
| T11 | T10 | T10 → T11 | Match |
| T12 | None | prior phase | Match |
| T13 | T12 | T12 → T13 | Match |
| T14 | T13 | T13 → T14 | Match |
| T15 | None | prior phase | Match |
| T16 | T15 | T15 → T16 | Match |
| T17 | T16 | T16 → T17 | Match |
| T18 | T17 | T17 → T18 | Match |
| T19 | T18 | T18 → T19 | Match |
| T20 | T19 | T19 → T20 | Match |
| T21 | T20 | T20 → T21 | Match |
| T22 | None | prior phase | Match |
| T23 | T22 | T22 → T23 | Match |
| T24 | T23 | T23 → T24 | Match |
| T25 | T24 | T24 → T25 | Match |
| T26 | None | prior phase | Match |
| T27 | T26 | T26 → T27 | Match |
| T28 | T27 | T27 → T28 | Match |
| T29 | T28 | T28 → T29 | Match |
| T30 | T29 | T29 → T30 | Match |
| T31 | T30 | T30 → T31 | Match |
| T32 | T31 | T31 → T32 | Match |

No task depends on a task in a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Fact model + wire contract | unit | unit | OK |
| T2 | Fact model + wire contract | unit | unit | OK |
| T3 | Fact validation | unit | unit | OK |
| T4 | JSON schema | unit | unit | OK |
| T5 | Claim model | unit | unit | OK |
| T6 | Claim accumulator | unit | unit | OK |
| T7 | Syntax capture | unit | unit | OK |
| T8 | Syntax capture | unit | unit | OK |
| T9 | Syntax capture | unit | unit | OK |
| T10 | Syntax capture | unit | unit | OK |
| T11 | Syntax capture | unit | unit | OK |
| T12 | Collector | unit | unit | OK |
| T13 | Collector | unit | unit | OK |
| T14 | Collector (database producer) | unit | unit | OK |
| T15 | Contracts with no behaviour | none | none | OK - matrix says none for this layer |
| T16 | Resolver orchestration + strategy | unit | unit | OK |
| T17 | Resolution strategy | unit | unit | OK |
| T18 | Resolution strategy | unit | unit | OK |
| T19 | Resolution strategy | unit | unit | OK |
| T20 | Resolution strategy | unit | unit | OK |
| T21 | Resolver orchestration | unit | unit | OK |
| T22 | Fragment build | unit | unit | OK |
| T23 | Pipeline wiring | integration | integration | OK |
| T24 | Resolver orchestration | unit | unit | OK |
| T25 | End-to-end pipeline | integration | integration | OK |
| T26 | Metrics projection | unit | unit | OK |
| T27 | Aggregate writing | unit | unit | OK |
| T28 | Resolver orchestration | unit | unit | OK |
| T29 | Fixture source documents | none | none | OK - matrix says none for this layer |
| T30 | End-to-end pipeline | integration | integration | OK |
| T31 | Resolution strategies + orchestration | unit | unit | OK |
| T32 | Specification document | none | none | OK - no code layer; build gate only |
