# RelationResolver Specification

## Problem Statement

Every relation the pipeline emits today carries `target_id: null`. `RelationCollector` hardcodes it
(`RelationCollector.cs:255`) with a fixed `UnresolvedReason` that literally defers to "a future
RelationResolver", and `SymbolIndex` was built for that consumer (`ISymbolIndex`'s own doc comment names
"a future relation resolver") and has none. The consequence is not cosmetic: `RelationProjector`'s Mermaid
projection filters on `TargetId is not null`, so a run over a real solution produces a dependency graph with
zero edges outside the `data` partition. The pipeline knows `OrderService` calls something named
`Authorize`, and separately knows `PaymentClient.Authorize` exists, and never joins the two.

## Goals

- [ ] A relation whose target the run actually contains gets a proven `target_id`, so `raw/dependencies.mmd`
      and the relation partitions carry real edges instead of source-only stubs.
- [ ] No relation is ever dropped or silently downgraded: an unresolvable relation stays in the output with
      its observed text, a named resolution method, and a reason.
- [ ] Resolution is deterministic, auditable, and never picks a winner out of an ambiguous candidate set.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| `SemanticResolver` / `CandidateSymbolResolver` as pass-2 strategies (spec sections 12-13) | `SemanticModel` and `SyntaxNode` are discarded with the compilation at the end of pass 1 and do not exist when the resolver runs. Semantic binding results must be *captured* during collection, not re-derived. Replacing pass-1 semantic refinement is not this feature's job. |
| `GrpcRelationResolver` (spec section 31) | No producer: the whole `Detection/` tree - `GrpcRelationDetector`, `DependencyInjectionDetector`, `AspNetCoreDetector`, `CompileTimeReferenceDetector`, `DetectorHost` - has no production caller and is referenced only from tests. gRPC was also explicitly deprioritized during `relation-collector`. |
| `DependencyInjectionResolver` (spec section 32) | Same reason: `DependencyInjectionDetector` is unwired, so no `registers` / `resolves-to` relation ever reaches the resolver. Wiring it is its own feature. |
| Minting `external-service`, `database-connection`, `schema`, `view`, `stored-procedure` node families (spec section 36) | No stage in the pipeline produces them; the families would be born empty. Deferred until a producer exists. |
| Minting `DatabaseObjectFact` / `DatabaseColumnFact` identities | `DatabaseMappingResolver` already owns that under AD-016. The resolver consumes those nodes; it never creates one. |
| Entity-to-table and property-to-column mapping (spec sections 19-21) | Already shipped by `data-access-discovery`: `DatabaseMappingResolver` resolves both with configured-over-convention precedence. This feature only exposes *which* of the two produced a given target. |
| LLM-assisted resolution, executing SQL, connecting to a database, impact analysis, business-rule discovery, persisting a knowledge graph (spec section 51) | The user's own out-of-scope list; belongs to later stages. |
| Discrimination-sensor / mutation validation (Stryker) at the Verifier step | Standing user request across `symbol-index`, `relation-collector` and `data-access-discovery`: the user runs Stryker manually. |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here - nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Where resolved relations are persisted | `RelationCollector` stops persisting into the document fragment and buffers its facts; the resolver persists every relation in one pass-2 fragment of its own | The `SymbolIndex` only exists after the document loop, so resolving before persistence is impossible today. `DatabaseFragmentBuilder` already sets the pass-2-fragment precedent. Keeps exactly one truth per relation instead of a resolved aggregate disagreeing with an unresolved fragment. | y |
| Resolution vocabulary | `FactResolution` is untouched; the relation gains its own `resolution_method` carrying the eight states `exact`, `candidate`, `syntactic`, `configured`, `convention`, `dynamic`, `heuristic`, `unresolved` | Additive: no other fact family changes, no snapshot churn across the whole suite, and AD-016's convention-to-`Heuristic` header mapping is not superseded. | y |
| `resolution_method` and `candidates` are first-class fields, not `RelationDetail` entries | Both become fields on `RelationFact` and `RelationFactJson` | `RelationFactId` is minted from a fingerprint of the details. Putting resolution state in a detail would change a relation's identity every time its resolution improved, breaking the stability RELR-22 requires. | n - mechanically forced; confirm in Design |
| Non-symbolic target nodes | Events resolve to the existing `SymbolFactId` of the message type, so no new family is needed. New families only for `http-client`, `http-endpoint` and `configuration`, and only when the name comes from a source string literal | Generalizes AD-016's proven-by-literal rule rather than puncturing it; AD-011's ban on promoting logical names without evidence stays intact. P2 scope. | y |
| MVP cut | P1 is the pipeline plus `ExistingTarget`, `SymbolIndex`, `ReceiverType`, `Candidate`, `Database`, `Convention` and `Unresolved` strategies, determinism, metrics and diagnostics. P2 is HTTP, events and configuration. P3 is confidence, attempts and cross-relation enrichment | Keeps the feature validatable in one pass; the protocol resolvers all depend on the P1 pipeline existing first. | y |
| Schema versioning | The factual fragment schema moves from 4 to 5, because relations leave the document fragment and `RelationFactJson` gains two fields. The aggregate envelope stays at 2 because both new fields are optional and additive | Mirrors AD-017's precedent, where the fragment schema bumped for a new fact family and the aggregate envelopes did not. | n - confirm in Design |
| Ownership of `data` relations | `DatabaseMappingResolver` keeps producing its resolved database relations, which enter the resolver as already-targeted raw relations and pass through the existing-target strategy. `DatabaseObjectFact` and `DatabaseColumnFact` stay in the database fragment untouched | Makes the resolver the single writer of `RelationFact` without rewriting a stage that already passed validation. | n - confirm in Design |
| Metrics artifact | A new `raw/facts/relations/resolution.json`, envelope version 1 | A new file avoids versioning the existing partition envelopes; `coverage.json` measures analysis attempt, which is a different question from resolution outcome. | n - confirm in Design |
| Confidence for `exact` | Omitted rather than written as `1.0` | Spec section 8 permits either; omitting keeps the field meaningfully sparse. P3 scope regardless. | n - P3 |

**Open questions:** none - all resolved or logged above.

---

## User Stories

### P1: Relations resolve to proven targets (MVP)

**User Story**: As the LLMWiki ingest, I want each relation to name the fact its target actually is, so that
the knowledge graph has edges instead of dangling source-and-a-string stubs.

**Why P1**: This is the entire point of the feature. Without it `dependencies.mmd` stays empty and
`SymbolIndex` stays unused.

**Acceptance Criteria** (each line is one EARS pattern):

1. The system SHALL run relation resolution as a single pass-two stage that reads the run's complete `SymbolIndex` and every buffered raw relation, and SHALL be the only writer of `RelationFact` instances.
2. WHEN a raw relation already carries a non-null `target_id` that is present in the run's fact set THEN the resolver SHALL keep that `target_id` unchanged and SHALL record the resolution method the producing stage reported.
3. IF a raw relation carries a non-null `target_id` that is absent from the run's fact set THEN the resolver SHALL set `target_id` to null, SHALL set `resolution_method` to `unresolved`, and SHALL emit diagnostic `C2M-RELR-007`.
4. WHEN a raw relation's `target_text` produces exactly one best-ranked candidate from `ISymbolIndex.FindCandidates` THEN the resolver SHALL set `target_id` to that candidate's `SymbolFactId` and SHALL set `resolution_method` to `syntactic`.
5. WHEN a `calls` relation carries both a receiver type text and a member name THEN the resolver SHALL look its target up through `ISymbolIndex.FindMethods` using receiver type, member name, argument count and argument types.
6. WHEN `ISymbolIndex.FindMethods` returns exactly one method at the top rank THEN the resolver SHALL set `target_id` to that method's `SymbolFactId` and SHALL set `resolution_method` to `syntactic`.
7. WHEN a `calls` relation's receiver is an identifier rather than a type name THEN the resolver SHALL resolve that identifier's declared type from the enclosing member's captured declaration context - primary-constructor parameter, constructor parameter, field, property, local variable, method parameter or pattern variable - before attempting the member lookup.
8. IF the receiver identifier's declared type cannot be determined THEN the resolver SHALL leave `target_id` null, SHALL set `resolution_method` to `unresolved`, and SHALL emit diagnostic `C2M-RELR-003`.
9. The resolver SHALL apply its strategies in one fixed declared order and SHALL stop at the first strategy that produces a target.
10. WHERE a strategy does not handle a relation's kind the resolver SHALL leave that relation unchanged and SHALL continue to the next strategy.
11. IF a strategy throws THEN the resolver SHALL discard that strategy's partial result for that relation only, SHALL emit diagnostic `C2M-RELR-006`, SHALL continue with the next strategy, and SHALL leave the run's exit code unchanged.

**Independent Test**: Run the CLI over `fixtures/SyntheticSolution` in the default syntax-only mode and read
`raw/facts/relations/structural.json`: the `calls` relation whose source is `OrderService`'s method carries a
`target_id` equal to the `SymbolFactId` of the `PaymentClient.Authorize` method declared in another project,
with `resolution_method` set to `syntactic`.

---

### P1: Nothing is lost and ambiguity is explicit (MVP)

**User Story**: As a maintainer reading the output, I want an unresolved relation to still be there and to
say why, so that a failed resolution is visible information rather than a hole.

**Why P1**: The pipeline's whole contract (AD-011, AD-016) is that an unproven target degrades instead of
disappearing. A resolver that dropped relations would be a regression, not a feature.

**Acceptance Criteria**:

1. The system SHALL emit exactly one relation in the output for every raw relation it received, regardless of resolution outcome.
2. WHEN a candidate lookup returns more than one candidate at the best rank THEN the resolver SHALL leave `target_id` null, SHALL set `resolution_method` to `candidate`, SHALL list every candidate's fact id in the relation's `candidates` field, and SHALL emit diagnostic `C2M-RELR-002`.
3. The resolver SHALL never set `target_id` from a candidate set that holds more than one entry at the best rank.
4. WHEN a lookup returns no candidate at all THEN the resolver SHALL leave `target_id` null, SHALL set `resolution_method` to `unresolved`, SHALL keep the observed `target_text` detail unchanged, and SHALL emit diagnostic `C2M-RELR-001`.
5. WHEN a relation is left unresolved THEN the resolver SHALL write an `unresolved_reason` that names the observed text it could not resolve, replacing `RelationCollector`'s fixed placeholder text.
6. The resolver SHALL preserve every `Evidence` entry its raw relation carried, and SHALL only append entries rather than removing or replacing one.
7. WHEN the resolver proves a target through a declaration the raw relation's own span does not cover THEN it SHALL append that declaration's span as an additional `Evidence` entry on the relation.
8. The resolver SHALL preserve every `RelationDetail` its raw relation carried.
9. The resolver SHALL preserve the `FactProvenance` entries its raw relation carried and SHALL append its own.

**Independent Test**: Add a type name to the fixture that exists in two namespaces, reference it from a third
project, and confirm the emitted relation has `target_id` null, `resolution_method` set to `candidate`, both
candidate ids listed in `candidates`, and a `C2M-RELR-002` entry in `raw/facts/diagnostics.json`.

---

### P1: Resolution is deterministic and identity is stable (MVP)

**User Story**: As a consumer diffing two runs, I want the same inputs to produce the same bytes and a
relation to keep its id as its resolution improves, so that incremental output is comparable.

**Why P1**: Spec sections 42-43. Without it, every re-run churns the whole relation set and no downstream
consumer can tell an improvement from noise.

**Acceptance Criteria**:

1. The system SHALL produce byte-identical relation output for two runs over identical inputs.
2. The resolver SHALL NOT derive any part of a relation's `RelationFactId` from that relation's resolution outcome.
3. WHILE a relation's observed shape is unchanged the system SHALL keep its `RelationFactId` identical across runs whose resolution outcome differs.
4. The resolver SHALL order the `candidates` field by ordinal comparison of the candidate fact id.
5. IF two candidates tie on every ranking signal THEN the resolver SHALL report both and SHALL NOT break the tie by enumeration order, hash order, or first-seen order.
6. The resolver SHALL order the relations it persists by ordinal comparison of `RelationFactId`.

**Independent Test**: Run the CLI twice over the same fixture into two output roots and confirm every
`raw/facts/relations/*.json` file is byte-identical; then narrow the `SymbolIndex` so one relation degrades
from `syntactic` to `unresolved`, and confirm that relation's `relation_id` is unchanged between the two runs.

---

### P1: Database relations distinguish configured from convention (MVP)

**User Story**: As the LLMWiki, I want to know whether a table name was written in the code or guessed by an
ORM convention, so that I do not present an inference as a fact.

**Why P1**: `DatabaseMappingResolver` already makes the distinction internally and then collapses it into the
header's `Exact` or `Heuristic` value. The finer state is exactly what the new `resolution_method` field
exists for.

**Acceptance Criteria**:

1. WHEN a database relation's target was named by an explicit configuration call THEN the resolver SHALL set `resolution_method` to `configured`.
2. WHEN a database relation's target was reached by an ORM naming convention THEN the resolver SHALL set `resolution_method` to `convention` and SHALL emit diagnostic `C2M-RELR-005`.
3. The resolver SHALL NOT set `resolution_method` to `exact` for any target reached by convention.
4. WHEN a database relation's target name came from interpolated or concatenated SQL THEN the resolver SHALL leave `target_id` null, SHALL set `resolution_method` to `dynamic`, SHALL keep the observed `target_text`, and SHALL emit diagnostic `C2M-RELR-004`.
5. The resolver SHALL preserve the `DatabaseOperation` a database relation was collected with and SHALL NOT alter its relation kind.
6. The resolver SHALL NOT create, alter, or delete any `DatabaseObjectFact` or `DatabaseColumnFact`.

**Independent Test**: Run the CLI over `fixtures/SyntheticSolution` and read `raw/facts/relations/data.json`:
the relation targeting the `ToTable`-configured object carries `resolution_method` set to `configured`, the
relation targeting the convention-named object carries `resolution_method` set to `convention` with the
header resolution still `Heuristic`, and the interpolated-SQL relation carries `resolution_method` set to
`dynamic` with `target_id` null.

---

### P1: Resolution outcomes are measurable and explained (MVP)

**User Story**: As a maintainer, I want counts per resolution method and a diagnostic per failure, so that I
can tell whether a change improved resolution or just moved relations around.

**Why P1**: Spec sections 44 to 47. Without counts there is no way to state the feature worked.

**Acceptance Criteria**:

1. WHEN a run completes THEN the system SHALL write `raw/facts/relations/resolution.json` containing the total relation count and a count for each of the eight `resolution_method` values.
2. WHEN a run completes THEN `raw/facts/relations/resolution.json` SHALL additionally carry a per-`RelationPartition` breakdown of the same counts.
3. The system SHALL write `raw/facts/relations/resolution.json` even when the run produced zero relations, with every count at zero.
4. The system SHALL make the counts in `raw/facts/relations/resolution.json` equal the relations actually written across the partition files.
5. Every diagnostic the resolver emits SHALL carry the affected relation's fact id as its scope.
6. Every diagnostic the resolver emits SHALL use a code from the declared set `C2M-RELR-001` through `C2M-RELR-007`.
7. IF the resolver emits a diagnostic THEN it SHALL NOT change the run's exit code on that account alone.

**Independent Test**: Run the CLI over `fixtures/SyntheticSolution`, sum the `resolution_method` values across
every `raw/facts/relations/*.json` partition file, and confirm the totals match
`raw/facts/relations/resolution.json` exactly.

---

### P2: Protocol targets resolve to literal-proven nodes

**User Story**: As the LLMWiki, I want an HTTP call, a named client, an event and a configuration key to
reach a real node, so that cross-service edges appear in the graph.

**Why P2**: Each of these depends on the P1 pipeline existing first, and none of them blocks the core
resolution loop from shipping.

**Acceptance Criteria**:

1. WHEN a `publishes`, `subscribes` or `handles` relation names a message type the `SymbolIndex` contains THEN the resolver SHALL set `target_id` to that type's existing `SymbolFactId` and SHALL NOT mint a new node family for it.
2. WHEN a `http-client` relation's client name came from a source string literal THEN the resolver SHALL mint an `http-client` node identity from that literal and SHALL set `target_id` to it.
3. WHEN a `http-call` relation carries both an HTTP method and a route read from source string literals THEN the resolver SHALL mint an `http-endpoint` node identity from that method and route and SHALL set `target_id` to it.
4. IF a client name, route, or configuration key was not read from a source string literal THEN the resolver SHALL leave `target_id` null and SHALL set `resolution_method` to `dynamic`.
5. The resolver SHALL NOT write any configuration value into a node identity, a detail, or a diagnostic, and SHALL write only the configuration key.
6. The resolver SHALL NOT mint a node identity for a password, token, secret, API key, credential, or private certificate value.

**Independent Test**: Run the CLI over the fixture's named-HTTP-client scenario and confirm the `http-client`
relation carries a `target_id` derived from the literal client name while the interpolated-route call carries
`target_id` null with `resolution_method` set to `dynamic`.

---

### P3: Resolution is auditable and improves across passes

**User Story**: As a maintainer debugging a wrong edge, I want to see which strategies were tried and how
confident the winner was, and I want a second pass to use what the first pass learned.

**Why P3**: Diagnostics already explain failures at a coarse level; per-strategy attempt records and
confidence are refinement, and cross-relation enrichment is a whole additional pass.

**Acceptance Criteria**:

1. WHEN the resolver resolves a relation THEN it SHALL record one attempt entry per strategy that ran, naming the strategy and its outcome.
2. WHEN the resolver sets a `resolution_method` other than `exact` THEN it SHALL record a confidence value in the inclusive range 0.0 to 1.0.
3. WHEN a first resolution pass produced relations that resolve an identifier's type THEN a second pass SHALL use those results to resolve relations the first pass left unresolved.
4. The system SHALL run at most one enrichment pass beyond the first, so that resolution terminates.

---

## Edge Cases

- IF the `SymbolIndex` is empty THEN the resolver SHALL emit every relation unresolved rather than throwing.
- IF the run produced zero raw relations THEN the resolver SHALL persist no relation fragment and SHALL still write `raw/facts/relations/resolution.json` with zero counts.
- IF a relation's source fact id is absent from the run's fact set THEN the resolver SHALL emit diagnostic `C2M-RELR-007` and SHALL still emit the relation.
- IF a raw relation's `target_text` is empty or whitespace THEN the resolver SHALL leave it unresolved and SHALL NOT query the index.
- IF two raw relations mint the same `RelationFactId` THEN the run SHALL fail structurally, matching `RelationProjector`'s existing duplicate-identity behaviour.
- WHEN the same type name is declared in two projects that do not reference each other THEN the resolver SHALL report both as candidates rather than preferring the source relation's own project.
- IF cancellation is requested mid-resolution THEN the resolver SHALL propagate the cancellation and SHALL NOT convert it into a diagnostic.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| RELR-01 | P1: Proven targets | T23 | Verified |
| RELR-02 | P1: Proven targets | T19 | Verified |
| RELR-03 | P1: Proven targets | T19 | Verified |
| RELR-04 | P1: Proven targets | T17 | Verified |
| RELR-05 | P1: Proven targets | T18 | Verified |
| RELR-06 | P1: Proven targets | T18 | Verified |
| RELR-07 | P1: Proven targets | T30 | Verified |
| RELR-08 | P1: Proven targets | T18 | Verified |
| RELR-09 | P1: Proven targets | T16 | Verified |
| RELR-10 | P1: Proven targets | T18 | Verified |
| RELR-11 | P1: Proven targets | T16 | Verified |
| RELR-12 | P1: Nothing is lost | T16 | Verified |
| RELR-13 | P1: Nothing is lost | T17 | Verified |
| RELR-14 | P1: Nothing is lost | T17 | Verified |
| RELR-15 | P1: Nothing is lost | T16 | Verified |
| RELR-16 | P1: Nothing is lost | T16 | Verified |
| RELR-17 | P1: Nothing is lost | T21 | Verified |
| RELR-18 | P1: Nothing is lost | T21 | Verified |
| RELR-19 | P1: Nothing is lost | T22 | Verified |
| RELR-20 | P1: Nothing is lost | T22 | Verified |
| RELR-21 | P1: Deterministic identity | T30 | Verified |
| RELR-22 | P1: Deterministic identity | T21 | Verified |
| RELR-23 | P1: Deterministic identity | T21 | Verified |
| RELR-24 | P1: Deterministic identity | T17 | Verified |
| RELR-25 | P1: Deterministic identity | T21 | Verified |
| RELR-26 | P1: Deterministic identity | T21 | Verified |
| RELR-27 | P1: Database configured vs convention | T20 | Verified |
| RELR-28 | P1: Database configured vs convention | T20 | Verified |
| RELR-29 | P1: Database configured vs convention | T20 | Verified |
| RELR-30 | P1: Database configured vs convention | T20 | Verified |
| RELR-31 | P1: Database configured vs convention | T20 | Verified |
| RELR-32 | P1: Database configured vs convention | T20 | Verified |
| RELR-33 | P1: Measurable outcomes | T26 | Verified |
| RELR-34 | P1: Measurable outcomes | T26 | Verified |
| RELR-35 | P1: Measurable outcomes | T26 | Verified |
| RELR-36 | P1: Measurable outcomes | T30 | Verified |
| RELR-37 | P1: Measurable outcomes | T28 | Verified |
| RELR-38 | P1: Measurable outcomes | T28 | Verified |
| RELR-39 | P1: Measurable outcomes | T28 | Verified |
| RELR-40 | P2: Protocol targets | - | Pending |
| RELR-41 | P2: Protocol targets | - | Pending |
| RELR-42 | P2: Protocol targets | - | Pending |
| RELR-43 | P2: Protocol targets | - | Pending |
| RELR-44 | P2: Protocol targets | - | Pending |
| RELR-45 | P2: Protocol targets | - | Pending |
| RELR-46 | P3: Auditable resolution | - | Pending |
| RELR-47 | P3: Auditable resolution | - | Pending |
| RELR-48 | P3: Auditable resolution | - | Pending |
| RELR-49 | P3: Auditable resolution | - | Pending |

**ID format:** `RELR-[NUMBER]`, assigned in story order. RELR-01 to RELR-11 map to P1 "Proven targets"
criteria 1 to 11; RELR-12 to RELR-20 to P1 "Nothing is lost" criteria 1 to 9; RELR-21 to RELR-26 to P1
"Deterministic identity" criteria 1 to 6; RELR-27 to RELR-32 to P1 "Database configured vs convention"
criteria 1 to 6; RELR-33 to RELR-39 to P1 "Measurable outcomes" criteria 1 to 7; RELR-40 to RELR-45 to P2
criteria 1 to 6; RELR-46 to RELR-49 to P3 criteria 1 to 4.

**Status values:** Pending -> In Design -> In Tasks -> Implementing -> Verified

**Coverage:** 49 total. All 39 P1 rows (`RELR-01`..`RELR-39`) are mapped to a task and `Verified` with
located `file:line` evidence (cited in the closing commit body). The remaining 10 are `Pending` by design:
P2 (`RELR-40`..`RELR-45`) and P3 (`RELR-46`..`RELR-49`) are explicitly out of this task list's scope per
the Out of Scope table above (no `Detection/`-tree producer exists for the protocol resolvers, and P3's
per-strategy attempt logging and cross-pass enrichment were never scheduled).

---

## Diagnostic Codes

| Code | Meaning | Severity |
| --- | --- | --- |
| `C2M-RELR-001` | No candidate found for the observed target text | Information |
| `C2M-RELR-002` | Ambiguous candidates; every candidate listed, none chosen | Information |
| `C2M-RELR-003` | Receiver identifier's declared type could not be determined | Information |
| `C2M-RELR-004` | Target depends on a value computed at runtime | Information |
| `C2M-RELR-005` | Target reached by naming convention, not by configuration | Information |
| `C2M-RELR-006` | A resolution strategy threw; its partial result was discarded | Warning |
| `C2M-RELR-007` | A relation names a source or target fact absent from the run | Warning |

---

## Success Criteria

- [ ] A run over `fixtures/SyntheticSolution` in the default syntax-only mode produces a non-empty
      `raw/dependencies.mmd` with at least one edge outside the `data` partition. Not met: per AD-020, this
      is unreachable regardless of resolution quality (no production path mints a `ComponentFact`, and
      `RelationProjector.Mermaid`'s project-keyed lookup would miss a symbol/document-shaped
      `SourceId`/`TargetId` even if one existed) - both pre-existing gaps outside this feature's scope. The
      file is confirmed written and non-empty (`RelationResolverEndToEndTests.cs:120`).
- [x] Every relation in every `raw/facts/relations/*.json` partition carries a `resolution_method` from the
      declared eight-value set.
- [x] Two runs over identical inputs produce byte-identical relation partition files.
- [x] No relation present before the resolver is absent after it: the raw relation count equals the persisted
      relation count for every run.
- [x] `raw/facts/relations/resolution.json` counts match the partition files exactly.
