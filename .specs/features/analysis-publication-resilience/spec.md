# Analysis Publication Resilience Specification

## Problem Statement

Three publication defects abort or mis-describe an otherwise valid analysis. A constructor whose own observations are only invocations throws when `contains` is built, because structural scoping empties the evidence chain. Canonical signatures that carry named tuples, nested generics or multidimensional arrays fail Storage re-hydration, because top-level splitting only balances `<>`. `retrieval.md` still backticks `postings/unknowns.json` and `postings/frontiers.json` after those families shard, so projection validation rejects the guide.

A fourth contract is already on HEAD and must not regress: an unexpected pipeline failure already publishes a single-line sanitized detail, identifies the failing stage, and leaves any previously committed package byte-identical. This workstream treats that path as a regression contract and proves the three open defects together through the default-budget CLI.

## Goals

- [ ] Keep an unexpected non-cancellation pipeline failure unpublished, identifiable by failing stage and root exception type, and safe on stderr, without weakening atomic publication.
- [ ] Publish a document-to-symbol `contains` relation whenever qualifying structural evidence exists at the symbol or, as fallback, the containing document.
- [ ] Re-hydrate nested canonical symbol signatures with exact Domain equality after a wire round-trip.
- [ ] Keep `retrieval.md` executable when unknown and frontier posting families shard, without quoting an absent base key.
- [ ] Commit a versioned regression fixture through `analyze` with the declared default reading budget, and recover a valid, navigable, deterministic package.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Accidental-complexity items 1–13 in `docs/specs/reducao-complexidade-acidental.md` (docs drift, HTTP receiver association, coverage-rule ownership, effective publication parameters, ceiling calibration, classifier-capability registry, agent-instruction dedup, test-suite slimming) | This workstream is the five `analysis-publication-resilience` issues. Those items stay in that local spec |
| AD-028 deferred gaps (GCPC-012/016/018/034/037/045/088/117) | Owned by the workstream-8 follow-up; this feature does not reopen certification accounting or `InvokesPass.ConcreteImplementors` |
| New fact families, observation kinds, relation kinds, facet axes, identity namespaces, taxonomy versions or schema versions | Workstream 1 is closed; these defects are selection, parse and guide bugs |
| New CLI options, logging frameworks, telemetry sinks, remote reporting or package envelope fields | Issue 01 forbids new surface; failure detail stays on the existing result and stderr path |
| Query engines, `kb`, QMD, embeddings, wiki compilation, business-rule interpretation, incremental analysis | AD-005, AD-011 and the roadmap deferral list |
| General data-flow analysis or interprocedural alias resolution | Not required to select structural `contains` evidence |
| Weakening `EvidenceChain`'s non-empty invariant, `ValidateNoAbsentKeys`, or the derived byte ceiling | The fixes use existing fallbacks and existing shard-aware guide wording |
| Committing `fixtures/eShop`, `fixtures/eShopOnContainers` or Pitstop | Standing constraint; local clones stay optional |
| Changing `fixtures/SyntheticSolution` bytes or the labeled `fixtures/CertificationCorpus` denominators | AD-026; the new regression tree is a sibling, not a mutation |
| Re-implementing pipeline-failure sanitization unless a regression test fails | Already on HEAD (`PipelineFailureDetail` and associated tests) |

---

## Contract dependencies

This feature reads only the contracts below. It does not supersede them.

| Source | Contract consumed | Change made here |
| --- | --- | --- |
| `knowledge-taxonomy-contract` | Relation matrix, `contains`, observation kinds, proof-state axes, identity grammar | Consumed unchanged. No registry edit |
| `roslyn-observation-extraction` | Structural facts, observation ledger, `ContainsRelationEmitter` | Evidence *selection* after `EvidenceScope` changes; emission and identity rules do not |
| `call-linking-flow-frontiers` | Disposition vocabulary (confirmed, candidate, unresolved, open frontier) | Consumed only so the retrieval guide still names those families correctly after posting shards |
| `factual-storage` | Wire mapping, `EvidenceChain` non-empty invariant, structural-corruption abort, atomic commit (STOR abort / preserve-prior-package) | `SplitTopLevel` balances tuples and array ranks; abort semantics stay |
| `retrieval-projections` | `retrieval.md`, posting families, `ValidateNoAbsentKeys`, shard naming | Disposition posting instructions become shard-aware the same way confirmed-relation instructions already are |
| `generator-cli-projections-certification` | Default reading budget, derived ceiling, `analyze` / `validate` CLI, labeled corpus denominators, SyntheticSolution immutability | Consumed. A third versioned fixture is added beside the two existing ones; labeled denominators stay untouched |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here. Nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --------------------- | -------------- | --------- | ---------- |
| Feature boundary | The five `.scratch/analysis-publication-resilience/issues/` items only. Accidental-complexity items 1–13 and AD-028 gaps are out of scope | The user started this workstream by that exact name. The local Portuguese spec remains the home of the broader intervention | y |
| Issue 01 status | Regression contract. No new implementation unless an existing discriminating test fails | HEAD already has `PipelineFailureDetail`, failing-stage on `SolutionOutcome`, single-line stderr and abort-preserves-package. The Portuguese review of 2026-09-14 says the same | y |
| Missing qualifying `contains` evidence | Omit that document-to-symbol relation. Record one operational `DiagnosticRecord`. Commit the package. Do not throw. Do not invent observations | Matches issue 02 and Domain's non-empty `EvidenceChain`. A missing structural edge is an unknown, not structural corruption | y |
| Diagnostic code and payload | Code `contains-evidence-unqualified`. Message is one line naming `contains` and that no qualifying structural evidence was found. `IdentityOrKey` is the symbol fact id | Matches existing kebab-case codes (`missing-project`, `unsupported-document`). Fact id is the legal `IdentityOrKey` form | y |
| Run-certification effect | Omitting one `contains` edge does not by itself set run-certification to `failed` or `degraded` | `contains` is not a numerator of the four coverage metrics. The diagnostic is the explicit degraded outcome | y |
| Signature splitter | One shared top-level split that tracks `<>`, `()` and `[]` depth. Existing simple and generic identities stay byte-identical | Issue 03. The current `<>`-only splitter is the defect. A third parser is forbidden by the Portuguese spec's item 3 | y |
| Malformed wire signatures | Unbalanced or malformed signatures remain structural corruption and abort publication | Issue 03. Silent acceptance would hide identity corruption | y |
| Retrieval guide wording | Exact key in backticks when that artifact exists. When only shards exist, name the family without backticks and say "the matching … shard". Absent family stays "none is recognized" | Copy the confirmed-relation pattern already in `RetrievalGuideProjector.AppendRelationsSection` | y |
| Regression fixture location | New sibling tree `fixtures/PublicationResilience`. Amends the standing "two versioned fixtures" constraint the same way AD-026 admitted `CertificationCorpus` | Issue 05 forbids mutating SyntheticSolution or the labeled corpus denominators | y |
| Default-budget CLI proof | `analyze` with no budget override, real `PackageProjector` and `BatchComposer`, against that fixture | Issue 05. Engine-only tests cannot prove the CLI adapter path | y |
| Local corpora | Pitstop, when present, is an optional acceptance check (15 isolated projects plus the full solution). Absence does not fail CI. Observed diagnostic counts are not golden | Standing `LocalCorpus` rule. Issue 05 states it explicitly | y |
| Discrimination sensor | Skipped for this feature, same standing project override as every prior workstream | User runs Stryker manually. Note the skip in `tasks.md` when Tasks runs | y |

**Open questions:** none — all resolved or logged above.

---

## Implicit-requirement dimensions sweep

| Dimension | Resolution |
| --- | --- |
| Input validation & bounds | APR-22 — unbalanced or malformed wire signatures remain structural corruption. APR-28 — a backtick-quoted key that is not in the publication still fails `ValidateNoAbsentKeys` |
| Failure / partial-failure states | APR-01..APR-07 — unexpected exceptions unpublish the solution, sanitize the detail and preserve the prior package. APR-12 — a `contains` evidence miss is a diagnostic, not an abort |
| Idempotency / retry / duplicate handling | APR-07, APR-40 — a failed retry leaves the prior package byte-identical; two successful default-budget runs of the regression fixture produce identical bytes |
| Auth boundaries & rate limits | N/A because analysis is local and publishes to the local filesystem |
| Concurrency / ordering | N/A because this workstream adds no second writer and does not change the existing per-solution commit lock |
| Data lifecycle / expiry | N/A because each commit still rewrites the package; there is no retained partial publication |
| Observability | APR-01..APR-05 — failing stage, root exception type and a single sanitized line on the existing result and stderr path. APR-12 — `contains-evidence-unqualified` in `diagnostics.json` |
| External-dependency failure | N/A because the defects are in-process selection, parse and projection |
| State-transition integrity | APR-07 — unpublished stays unpublished; a committed package is never half-replaced. APR-12 — omitting one `contains` does not change `PublicationStatus.Committed` |

---

## User Stories

### P1: Safe pipeline failure details ⭐ MVP

**User Story**: As an operator, I want an unexpected analysis, projection or composition failure to name the work that failed in a single safe line, so that I can diagnose the run without losing the last valid package or leaking source or secrets.

**Why P1**: This is the existing abort contract. The three publication fixes must not reopen it.

**Acceptance Criteria** (each line is one EARS pattern):

1. APR-01 — WHEN a non-cancellation exception leaves a solution unpublished THEN the system SHALL set `SolutionOutcome.FailingStage` to the failing pipeline stage and SHALL put the root exception type in `SolutionOutcome.Detail`.
2. APR-02 — WHEN a failure detail is produced THEN the system SHALL emit exactly one line, and SHALL include a root message only after sanitization.
3. APR-03 — IF the root message contains a suspected secret, an absolute path, a source excerpt or a line break THEN the system SHALL remove that content from the detail, and IF no safe message remains THEN the system SHALL still report the exception type.
4. APR-04 — WHEN the CLI reports an unpublished solution THEN standard error SHALL contain the safe detail exactly once, and the existing stdout summary and unpublished exit code SHALL remain unchanged.
5. APR-05 — The system SHALL NOT emit stack traces, inner-exception chains, exception data, environment values or raw syntax text on the failure path.
6. APR-06 — WHEN the run is cancelled THEN the system SHALL keep the existing cancellation behavior and SHALL NOT report cancellation as an unexpected pipeline failure.
7. APR-07 — IF a retry still fails THEN the system SHALL abort staging and SHALL leave any previously committed package byte-for-byte identical.
8. APR-08 — The system SHALL NOT introduce a CLI option, logging framework, telemetry sink, remote reporter, schema field or package artifact for this detail.

**Independent Test**: Drive the existing pipeline, CLI, sanitizer, cancellation and abort-preserves-package tests. They stay green with no new surface.

---

### P1: Preserve valid evidence for contains relations ⭐ MVP

**User Story**: As a consumer, I want a symbol that owns only behavioral observations to still publish `contains` with truthful structural evidence, so that I do not lose the relation or read fabricated justification.

**Why P1**: `EvidenceChain.Create` currently throws when `EvidenceScope` empties a symbol-owned-only-behavioral candidate set. That aborts observation extraction on ordinary constructors.

**Acceptance Criteria**:

1. APR-09 — WHEN a document-to-symbol `contains` relation is emitted and the symbol owns at least one qualifying structural observation THEN the system SHALL use that symbol-owned evidence and no other scope.
2. APR-10 — WHEN the symbol owns only `Invocation` or `DataAccess` observations and the containing document has qualifying structural observations THEN the system SHALL use those document-scoped observations as the evidence chain.
3. APR-11 — WHEN the system emits a confirmed `contains` relation THEN the evidence chain SHALL be non-empty and SHALL contain no `Invocation` or `DataAccess` observation.
4. APR-12 — IF neither the symbol scope nor the document scope has qualifying structural evidence THEN the system SHALL NOT construct a confirmed relation with an empty or invented chain, SHALL record one `contains-evidence-unqualified` diagnostic whose `IdentityOrKey` is the symbol fact id, and SHALL commit the rest of the package.
5. APR-13 — The system SHALL keep the Domain rule that a confirmed relation derives from at least one observation.
6. APR-14 — WHEN a symbol already owns qualifying structural evidence THEN the system SHALL keep the previously published evidence ownership, kinds and cardinality for that `contains` edge.
7. APR-15 — The system SHALL NOT add a fact family, observation kind, relation kind, facet, identity namespace or schema version.

**Independent Test**: Analyze a minimized builder-constructor fixture through the real observation-extraction and publication path. Read the committed `contains` edge and assert ownership, kinds and non-empty cardinality. A second fixture with no qualifying structural evidence at either scope publishes the diagnostic and no invented edge.

---

### P1: Round-trip nested canonical symbol signatures ⭐ MVP

**User Story**: As a consumer, I want signatures that contain tuples, nested generics and multidimensional arrays to re-hydrate with the same canonical identity, so that references stay stable after publication.

**Why P1**: Wire re-hydration currently splits on commas inside tuple parentheses and array ranks. That is structural corruption on valid C# shapes.

**Acceptance Criteria**:

1. APR-16 — WHEN a named-tuple parameter is mapped Domain → wire → Domain THEN the system SHALL restore a symbol whose canonical signature equals the original exactly.
2. APR-17 — WHEN a signature has more than one tuple parameter THEN the system SHALL keep each parameter's internal commas and SHALL keep the parameters distinct.
3. APR-18 — WHEN a tuple is nested inside a generic type, or a generic type is nested inside a tuple, THEN the system SHALL restore exact canonical equality.
4. APR-19 — WHEN a parameter or type argument is a multidimensional array THEN the system SHALL retain the rank commas and SHALL NOT treat them as parameter separators.
5. APR-20 — WHEN generic, tuple and array shapes are mixed THEN the system SHALL split only on top-level commas and SHALL produce a deterministic identity.
6. APR-21 — WHEN an existing simple or generic signature is mapped Domain → wire → Domain THEN the system SHALL keep the current canonical identity and serialized value.
7. APR-22 — IF a wire signature is malformed or delimiter-unbalanced THEN the system SHALL reject it as structural corruption and SHALL NOT accept it silently.
8. APR-23 — The system SHALL leave the identity namespace, signature fields, escaping rules, taxonomy version and schema version unchanged.

**Independent Test**: Domain-unit cases for each shape plus a minimized constructor that reaches publication. Assert full Domain equality and exact canonical identity equality, not merely "parsed".

---

### P1: Shard-aware disposition posting guidance ⭐ MVP

**User Story**: As a consumer, I want `retrieval.md` to name a posting artifact only when that file exists, so that I can follow unproven dispositions after the posting family fragments.

**Why P1**: Confirmed-relation instructions are already shard-aware. Unknown and frontier posting instructions still quote the absent base key, and `ValidateNoAbsentKeys` then rejects the guide.

**Acceptance Criteria**:

1. APR-24 — WHEN `postings/unknowns.json` exists as an exact key THEN the disposition section SHALL keep the current exact-key navigation, including that key in backticks.
2. APR-25 — WHEN the unknown posting family is sharded and the base key is absent THEN the guide SHALL describe selection of a matching unknown shard and SHALL NOT quote `postings/unknowns.json` as an artifact.
3. APR-26 — WHEN the frontier posting family is exact or sharded THEN the system SHALL apply the same exact-versus-sharded rule as APR-24 and APR-25 to `postings/frontiers.json`.
4. APR-27 — WHEN candidate, unresolved or open-frontier *relation* families are exact or sharded THEN the system SHALL keep their existing shard-aware instructions.
5. APR-28 — WHEN `retrieval.md` is published THEN every artifact key enclosed in backticks SHALL exist in that publication, and `ValidateNoAbsentKeys` SHALL keep its current strictness.
6. APR-29 — The system SHALL keep `retrieval.md` and every generated artifact inside the declared ceiling rules, and SHALL NOT add a ceiling exclusion.
7. APR-30 — IF a posting family is missing entirely THEN the guide SHALL report it as absent and SHALL NOT describe it as sharded.
8. APR-31 — WHEN the same input and ceiling are projected twice THEN the system SHALL produce byte-identical `retrieval.md`.
9. APR-32 — The system SHALL leave validator strictness, taxonomy, schemas and retrieval semantics unchanged.

**Independent Test**: Project a real layout with the same small ceiling on planner and projector so the posting family itself shards. Assert no backtick-quoted absent key and an unchanged pass of `ValidateNoAbsentKeys`.

---

### P1: Default-budget CLI publication across the regressions ⭐ MVP

**User Story**: As an operator, I want `analyze` with the default reading budget to publish a fixture that combines the three defects, so that the CLI path is certified without changing labeled-corpus denominators.

**Why P1**: Engine-only tests can pass while `CommandFactory` still wires the live adapters incorrectly. This is the vertical slice.

**Acceptance Criteria**:

1. APR-33 — WHEN the regression solution is added THEN the system SHALL place it in `fixtures/PublicationResilience`, separate from `fixtures/CertificationCorpus`, so labeled precision and recall denominators do not change.
2. APR-34 — The system SHALL keep `fixtures/SyntheticSolution` byte-identical and SHALL keep its integrity gate passing.
3. APR-35 — WHEN `analyze` runs on the regression solution with the declared default reading budget THEN the system SHALL commit without observation-extraction failure, structural corruption or projection-key rejection.
4. APR-36 — WHEN that package is read back THEN the published builder symbol SHALL have a document-to-symbol `contains` relation whose evidence is non-empty, structural and free of `Invocation` and `DataAccess`.
5. APR-37 — WHEN symbols that carry tuple, nested-generic or multidimensional-array signatures are read back THEN the system SHALL restore valid canonical identities equal to the published ones.
6. APR-38 — WHEN the generated retrieval guide is read THEN every artifact it references SHALL resolve in the same publication, including sharded unknown and frontier postings.
7. APR-39 — WHEN the existing package-validation, projection-validation, run-certification, manifest-reachability, determinism and artifact-ceiling gates run on this fixture THEN they SHALL pass without exclusions or weakened assertions.
8. APR-40 — WHEN the regression solution is analyzed twice with the same default budget THEN the system SHALL produce identical package bytes.
9. APR-41 — WHERE the local Pitstop solution is present the system SHALL analyze all 15 isolated project solutions and the full solution as an optional acceptance check, and WHERE it is absent the suite SHALL NOT fail CI.
10. APR-42 — The system SHALL NOT add Pitstop, eShop or eShopOnContainers to source control, and SHALL NOT treat Pitstop diagnostic counts as exact golden baselines.

**Independent Test**: `dotnet run --project src/Csharp2Md.Cli -- analyze` against `fixtures/PublicationResilience` with no budget flags. Read the package through `FactualPackageReader` and `validate`. Two runs compare equal.

---

## Edge Cases

- IF a symbol owns a mix of structural and behavioral observations THEN the system SHALL keep only the qualifying structural ones (APR-09, APR-11).
- IF document-scoped fallback is used THEN the system SHALL still run `EvidenceScope` so unrelated behavioral occurrences never enter the chain (APR-10, APR-11).
- IF sanitization reduces a failure message to empty or to a redaction marker THEN the system SHALL publish the exception type alone (APR-03).
- IF only the unknown posting family shards and the frontier family stays exact THEN the guide SHALL mix shard wording for unknowns with exact-key wording for frontiers (APR-25, APR-26).
- IF a confirmed-relation family is already sharded THEN its existing non-backtick shard sentence SHALL remain (APR-27).
- IF a wire signature contains a comma inside `()`, `<>` or `[]` at depth > 0 THEN the system SHALL treat that comma as part of the current argument (APR-20).

---

## Requirement Traceability

Each requirement gets a unique ID for tracking across design, tasks, and validation.

| Requirement ID | Story | Phase | Status |
| -------------- | ----- | ----- | ------ |
| APR-01 | P1: Safe pipeline failure details | Tasks | Verified |
| APR-02 | P1: Safe pipeline failure details | Tasks | Verified |
| APR-03 | P1: Safe pipeline failure details | Tasks | Verified |
| APR-04 | P1: Safe pipeline failure details | Tasks | Verified |
| APR-05 | P1: Safe pipeline failure details | Tasks | Verified |
| APR-06 | P1: Safe pipeline failure details | Tasks | Verified |
| APR-07 | P1: Safe pipeline failure details | Tasks | Verified |
| APR-08 | P1: Safe pipeline failure details | Tasks | Verified |
| APR-09 | P1: Preserve valid evidence for contains relations | Tasks | Verified |
| APR-10 | P1: Preserve valid evidence for contains relations | Tasks | Verified |
| APR-11 | P1: Preserve valid evidence for contains relations | Tasks | Verified |
| APR-12 | P1: Preserve valid evidence for contains relations | Tasks | Verified |
| APR-13 | P1: Preserve valid evidence for contains relations | Tasks | Verified |
| APR-14 | P1: Preserve valid evidence for contains relations | Tasks | Verified |
| APR-15 | P1: Preserve valid evidence for contains relations | Tasks | Verified |
| APR-16 | P1: Round-trip nested canonical symbol signatures | Tasks | Verified |
| APR-17 | P1: Round-trip nested canonical symbol signatures | Tasks | Verified |
| APR-18 | P1: Round-trip nested canonical symbol signatures | Tasks | Verified |
| APR-19 | P1: Round-trip nested canonical symbol signatures | Tasks | Verified |
| APR-20 | P1: Round-trip nested canonical symbol signatures | Tasks | Verified |
| APR-21 | P1: Round-trip nested canonical symbol signatures | Tasks | Verified |
| APR-22 | P1: Round-trip nested canonical symbol signatures | Tasks | Verified |
| APR-23 | P1: Round-trip nested canonical symbol signatures | Tasks | Verified |
| APR-24 | P1: Shard-aware disposition posting guidance | Tasks | Verified |
| APR-25 | P1: Shard-aware disposition posting guidance | Tasks | Verified |
| APR-26 | P1: Shard-aware disposition posting guidance | Tasks | Verified |
| APR-27 | P1: Shard-aware disposition posting guidance | Tasks | Verified |
| APR-28 | P1: Shard-aware disposition posting guidance | Tasks | Verified |
| APR-29 | P1: Shard-aware disposition posting guidance | Tasks | Verified |
| APR-30 | P1: Shard-aware disposition posting guidance | Tasks | Verified |
| APR-31 | P1: Shard-aware disposition posting guidance | Tasks | Verified |
| APR-32 | P1: Shard-aware disposition posting guidance | Tasks | Verified |
| APR-33 | P1: Default-budget CLI publication across the regressions | Tasks | Verified |
| APR-34 | P1: Default-budget CLI publication across the regressions | Tasks | Implementing (T12 remaining) |
| APR-35 | P1: Default-budget CLI publication across the regressions | Tasks | In Tasks (T12) |
| APR-36 | P1: Default-budget CLI publication across the regressions | Tasks | Implementing (T12 remaining) |
| APR-37 | P1: Default-budget CLI publication across the regressions | Tasks | Implementing (T12 remaining) |
| APR-38 | P1: Default-budget CLI publication across the regressions | Tasks | In Tasks (T11, T12) |
| APR-39 | P1: Default-budget CLI publication across the regressions | Tasks | In Tasks (T12) |
| APR-40 | P1: Default-budget CLI publication across the regressions | Tasks | In Tasks (T12) |
| APR-41 | P1: Default-budget CLI publication across the regressions | Tasks | In Tasks (T13) |
| APR-42 | P1: Default-budget CLI publication across the regressions | Tasks | In Tasks (T13) |

**ID format:** `APR-NN`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 42 total, 42 mapped to tasks, 0 unmapped

---

## Success Criteria

- [ ] An unexpected pipeline failure still unpublished, single-line, secret-safe, and prior-package-preserving.
- [ ] A builder constructor that owns only invocations publishes `contains` with structural evidence, or a diagnostic when no structural evidence exists, and never throws.
- [ ] Named tuples, nested generics and multidimensional arrays survive Domain → wire → Domain with exact identity equality.
- [ ] `retrieval.md` never backticks an absent posting key after unknown or frontier families shard.
- [ ] `analyze` with the default budget commits `fixtures/PublicationResilience` into a valid, navigable, byte-deterministic package.
- [ ] Labeled `CertificationCorpus` denominators and `SyntheticSolution` bytes are unchanged.
