# Database Access Discovery Validation

**Date**: 2026-08-20
**Spec**: `.specs/features/data-access-discovery/spec.md`
**Diff range**: `67bbbe0..a9626f6` (41 commits on `feat/data-access-discovery`, branched from `master` at
`67bbbe0` — the original 38 feature commits `0fa984d..20856f2`, plus three fix commits from a first
Verifier's FAIL: `ecff901`, `e49bbd7`, `a9626f6`)
**Verifier**: iteration 2 of a bounded fix-then-reverify loop. Re-derived independently against the current
tree rather than trusting the fix commits' own summaries; every finding below was reproduced first-hand
(a real CLI run over the fixture, direct JSON field inspection, `file:line` reads of the current test
files, a fresh `dotnet build`/`format`/`test` gate).
**Result**: PASS — 28/28 P1 criteria substantiated.

---

## Scope

P1 only (`DAD-01`..`DAD-28`). P2 (`DAD-29`..`DAD-35`) and P3 (`DAD-36`..`DAD-38`) were deliberately not
implemented; their `Pending` rows are correct and are not counted against this feature.

---

## Iteration 1 → Iteration 2: what changed

A first Verifier pass (report superseded by this one) returned FAIL on three items. Each is re-checked
below, independently, against the current tree:

1. **`DAD-15` (FAIL-level)** — a real run put `Password=inline-fixture-secret` into `raw/facts/document/*.json`
   at `symbols[].signature`, `symbols[].symbol_id`, `symbols[].header.id` and `documents[].symbol_ids[]` —
   synthesised fact fields, not rendered source. Fixed by commit `ecff901`: `SyntaxFactExtractor`'s
   `DeclarationSignature` now redacts a string-literal token when it assigns a credential-shaped value,
   using a `CredentialText.Carries` predicate shared with `SqlTextAnalyzer`'s pre-existing guard
   (`src/Csharp2Md.Core/Analysis/CredentialText.cs`, new file).
2. **`DAD-18`'s "exit code unchanged" clause (evidence gap)** — no assertion existed at any layer for the
   final clause of AC18. Fixed by commit `e49bbd7`: `AnalysisEngine` gained an internal
   `dataAccessAnalyzers` constructor seam (mirroring the existing `onSymbolIndexBuilt` pattern), letting a
   test inject a throwing `IDataAccessAnalyzer` through the real `AnalyzeAsync` path and assert the result.
3. **`DAD-21`/`DAD-22` spec-precision gaps (non-blocking, confirmed genuine)** — `MERGE`'s operation and the
   SQL access relation's own resolution were tested but not stated in `spec.md`. Fixed by commit `a9626f6`:
   wording-only edit to AC21 and AC22.

---

## Task Completion

All 36 tasks across 7 phases are marked complete in `tasks.md` (187 done-when boxes checked, 0 unchecked).
Unchanged since iteration 1; the three fixes above are fix-loop commits, not new tasks.

---

## Gate Check

Re-run fresh against `a9626f6`, not carried forward from iteration 1:

- **Build**: `dotnet build csharp2md.slnx -c Release` — exit 0, 0 warnings, 0 errors.
- **Format**: `dotnet format csharp2md.slnx --verify-no-changes` — exit 0.
- **Test**: `dotnet test csharp2md.slnx` — **1717 passed, 1 failed, 1718 total** (25 s). The one failure is
  `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml`, a documented
  pre-existing temp-directory race under xUnit parallel execution, unrelated to this feature (the file is
  untouched by any commit in the diff range). Re-run filtered to just that test: **1/1 passed** in
  isolation, confirming the characterisation rather than assuming it.

---

## Discrimination Sensor

**Skipped by explicit, reaffirmed user request** — recorded in `tasks.md`'s `## Execution Protocol` header.
No mutants were injected, no scratch worktree was created, no mutation tooling was run. The user runs
Stryker manually.

---

## Spec-Anchored Acceptance Criteria

`E2E` = `tests/Csharp2Md.Core.Tests/Analysis/DataAccessDiscoveryEndToEndTests.cs` (default-mode
`AnalyzeAsync` over `fixtures/SyntheticSolution`; mode pinned at `E2E:26-31`). Line numbers below are
re-read from the current file — the `DAD-15` fix inserted ~35 lines into this file, so citations after that
point (`DAD-19`, `DAD-20`) were re-verified at their current locations rather than carried forward from
iteration 1.

| # | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| DAD-01 | one `exposes` per `DbSet<T>`, sourced at the declaring type's symbol id, `target_text` = entity simple name | `E2E:39-44` — `Assert.Equal(2, exposes.Length)`; `Assert.Equal(["Order","OrderLine"], exposes.Select(r => Detail(r,"target_text")).Order(...))`; `Assert.All(exposes, r => Assert.Contains("OrderDbContext", r.SourceId, ...))`. Owner pinned exactly at `EfCoreAnalyzerTests.cs:47` | PASS |
| DAD-02 | unconfigured entity yields `maps-to`, `target_id` null, `target_text` = `DbSet` property name, `Heuristic` | `E2E:55-58` — `Assert.Equal("heuristic", convention.Header.Resolution)`; `Assert.Null(convention.TargetId)`; `Assert.Equal("OrderLines", Detail(convention,"target_text"))` | PASS |
| DAD-03 | `ToTable("literal")` mints a node of kind `table` at `Exact`; `maps-to.target_id` is that node | `E2E:65-75` — `Assert.Equal("table", table.Kind)`; `Assert.Equal("exact", table.Header.Resolution)`; `Assert.Equal(table.ObjectId, configured.TargetId)`; `Assert.Equal("order_headers", Detail(configured,"target_text"))` | PASS |
| DAD-04 | configured mapping wins; only it is emitted | `E2E:87-92` — `var only = Assert.Single(mappingsForOrder); Assert.Equal("configured", Detail(only,"mapping"))` | PASS |
| DAD-05 | `HasColumnName("literal")` mints a column owned by the entity's object; `maps-property-to-column` at `Exact` | `E2E:100-112` — `Assert.Equal(table.ObjectId, column.ObjectId)`; `Assert.Equal(column.ColumnId, mapping.TargetId)`; `Assert.Equal("exact", mapping.Header.Resolution)`; `Assert.Equal("order_status", Detail(mapping,"target_text"))` | PASS |
| DAD-06 | unconfigured property: `target_id` null, `target_text` = own name, `Heuristic` | `E2E:124-128` — `Assert.All(conventions, r => Assert.Equal("heuristic", r.Header.Resolution))`; `Assert.All(conventions, r => Assert.Null(r.TargetId))`; `Assert.Contains(conventions, r => Detail(r,"target_text") == "Amount")` | PASS |
| DAD-07 | `reads` at the enclosing member, `operation` = `read`, targeting the entity's **mapped** object | `E2E:141-144` — `Assert.Equal("read", Detail(read,"operation"))`; `Assert.Equal(table.ObjectId, read.TargetId)` where `table.Name == "order_headers"` | PASS |
| DAD-08 | one `reads-column` per property referenced outside `Where`, `usage` = `read` | `E2E:155-158` — `Assert.Equal(["Amount","Id","Status"], readColumns.Select(r => Detail(r,"target_text")).Order(...))`; `Assert.All(readColumns, r => Assert.Equal("read", Detail(r,"usage")))` | PASS |
| DAD-09 | one `filters-by` per property in the `Where` lambda, `usage` = `filter` | `E2E:165-170` — `Assert.Single(... "filters-by" ...)`; `Assert.Equal("Id", Detail(filter,"target_text"))`; `Assert.Equal("filter", Detail(filter,"usage"))` | PASS |
| DAD-10 | `Add` family yields `writes` with `operation` = `insert` | `E2E:181-183` — `Assert.Equal("insert", Detail(write,"operation"))`; `Assert.Equal("Orders", Detail(write,"target_text"))`; `Assert.NotNull(write.TargetId)`. Update/Delete families pinned at `DatabaseMappingResolverTests.cs:408-412` | PASS |
| DAD-11 | tracked write matching exactly one exposed entity yields `writes-column` to that property's mapped column, `usage` = `write`, `Heuristic` | `E2E:196-199` — `Assert.Equal(column.ColumnId, write.TargetId)`; `Assert.Equal("heuristic", write.Header.Resolution)`; `Assert.Equal("write", Detail(write,"usage"))`; `Assert.Equal("order.Status", Detail(write,"target_text"))` | PASS |
| DAD-12 | ambiguous match: `target_id` null, `target_text` = `receiver.property`, `Candidate` | `E2E:210-214` — `Assert.Null(write.TargetId)`; `Assert.Equal("candidate", write.Header.Resolution)`; `Assert.Equal("ambiguous-entity-attribution", write.UnresolvedReason)`; `Assert.Equal("order.Amount", Detail(write,"target_text"))` | PASS |
| DAD-13 | every persistence node and relation carries doc id, normalized path, one-based start/end line and column | `AggregateRelationPartitionTests.cs:59-60`; `DatabaseFragmentBuilderTests.cs:63-68`; exact spans at `EfCoreAnalyzerTests.cs:64-67`; construction-time rejection at `DataAccessContractsTests.cs:101-110` | PASS |
| DAD-14 | unresolvable access still emitted: `target_id` null, populated `unresolved_reason`, `target_text` preserved | `DatabaseMappingResolverTests.cs:401-404`; invariant over a mixed snapshot at `:668-671`; survives into facts at `DatabaseFragmentBuilderTests.cs:81-82` | PASS |
| **DAD-15** | **never** write a connection-string value, password, token or credential text into **any fact, relation detail, or diagnostic** the system produces | **Independently reproduced against the current tree, not taken on trust.** Ran the CLI over `fixtures/SyntheticSolution` to a scratch output directory and searched the whole tree: `appsettings-fixture-secret` (the config credential) reaches **zero files**. `inline-fixture-secret` (the credential `SqlTextAnalyzer` walks past in C# source) reaches exactly two files: `raw/codebase/Acme.Orders/Data/OrderSqlQueries.cs.md` (rendered source) and `raw/facts/document/93/93cf9bde....json`. Parsed that JSON and walked every string value in it: the credential appears at exactly one field path, `source_sections[2].source` — the verbatim-source array, a first-class sibling of `symbols`/`database_objects`/`relations`/`diagnostics` in the document schema, not nested inside any of them. It is **absent** from every `symbols[].signature`, `symbols[].symbol_id`, `symbols[].header.id` and `documents[].symbol_ids[]` — the exact fields the iteration-1 Verifier found it in. The fix (`ecff901`): `SyntaxFactExtractor.DeclarationSignature` now replaces a string-literal token with `"<redacted>"` when `CredentialText.Carries` recognises it as a credential assignment, so the id derived from the signature changes too. Narrowness proven at `SyntaxFactExtractorTests.cs:648-665` (the credential is absent from both `Signature` and `SymbolId.Value`, the placeholder is present, the declaration itself is not blanked) and `:672-680` (a `[Theory]` over an ordinary literal, a parameterized `SET Password = @password`, and an unrelated numeric literal — none is redacted). `E2E:400-457` was rewritten to assert this directly via `JsonDocument`, and additionally proves the split is genuine by requiring the credential to still surface in `source_sections` (`sawInlineCredentialInSource` assertion at `:455-457`) — a vacuous split (never testing the positive half) would fail this test. | **PASS** |
| DAD-16 | SQL detail truncated to at most 2000 characters | `DataAccessContractsTests.cs:83-84`; through the analyzer at `SqlTextAnalyzerTests.cs:72-73`; single chokepoint (`RawDatabaseClaim.SqlText` init accessor) | PASS |
| DAD-17 | classification only from observed persistence API usage; name, file or namespace is not evidence | `E2E:243-248` — `Assert.Contains(fixture.AnalysedDocuments, ...)` **and** `Assert.DoesNotContain(fixture.DataRelations, ...)`. Default-state check at `DataAccessContractsTests.cs:120-125` | PASS |
| **DAD-18** | analyser throw yields a diagnostic naming the document, the run continues, **exit code unchanged** | `DataAccessCollectorTests.cs:36-45` proves the diagnostic (code, severity, scope, document path, analyzer id); partial work discarded `:58`; other analyzers survive `:72-76`. **The exit-code clause — the previously-unproven half — is now proven at the real `AnalysisEngine.AnalyzeAsync` level**: `AnalysisEngineTests.cs:241-273`, `AnalyzeAsync_WhenADataAccessAnalyzerThrows_LeavesTheExitCodeUnchangedAndKeepsAnalysingRemainingDocuments`. Independently re-read the test body: it constructs a real `AnalysisEngine` via the public-shaped internal constructor with an injected throwing `IDataAccessAnalyzer` (`StubDataAccessAnalyzer.AppendingThenThrowing`) across a **two-document** project, runs the real `AnalyzeAsync`, and asserts `result.ExitCode == 0` (`:254`) — the actual spec-defined value, contrasted directly against the neighbouring `AnalyzeAsync_StructuralValidationFailure_ReturnsExitOneAndOmitsFragment` (`:79-85`) which proves `1` for a genuine structural failure, so the two tests together pin that only a structural failure moves the code. Also asserts exactly 2 distinct `C2M-DA-001` diagnostic entries with distinct `scope_id`s naming `Broken.cs` and `Fine.cs` respectively (`:257-263`), and that both documents' symbols still made it into the output (`:267-272`) — proving the run did not halt. The seam itself (`AnalysisEngine`'s new `dataAccessAnalyzers` constructor parameter, `AnalysisEngine.cs`) mirrors the pre-existing `onSymbolIndexBuilt` pattern rather than inventing a new shape. | **PASS** |
| DAD-19 | one aggregate entry per node id, merged evidence, highest-confidence resolution retained | `DatabaseAggregateProjectorTests.cs:22-26,39,98-101`. End to end at `E2E:255-263` (re-verified at the current line numbers, shifted from iteration 1's `257-263`) — `Assert.Equal(3, repeated.Header.Evidence.Length)`; evidence spans are distinct; object ids are distinct across the whole catalogue | PASS |
| DAD-20 | byte-identical persistence facts across two runs on unchanged input | `E2E:463-478` (re-verified at the current line numbers, shifted from iteration 1's `430-437`) — two independent `AnalyzeAsync` runs; `Assert.Equal(relativeA, relativeB)`; `Assert.True(bytesA.AsSpan().SequenceEqual(bytesB), ...)` over `relations/data.json`, `database.json` and every `database-column/**` fragment. Order-independence at `DatabaseAggregateProjectorTests.cs:79-85` and `DataAccessCollectorTests.cs:121-130` | PASS |
| DAD-21 | recognised verb yields one access relation whose `operation` derives from the verb, **MERGE named explicitly** | `E2E:274-281` `[Theory]` over the fixture's five verb shapes. `MERGE`/`CALL` (absent from the fixture) at `SqlStatementReaderTests.cs:18-23`. `spec.md` AC21 now states `MERGE` derives `update` explicitly, matching `SqlStatementReader.cs:74,84` | PASS |
| DAD-22 | readable plain-identifier target mints a node at `Exact`; relation `target_id` is that node; **relation's own resolution named** | `E2E:293-299` — `Assert.Equal("exact", orders.Header.Resolution)`; `Assert.Equal(orders.ObjectId, access.TargetId)`. `spec.md` AC22 now states the access relation resolves at `Syntactic`, matching `SqlTextAnalyzer.cs:116` and the spec's own message-literal Edge Case | PASS |
| DAD-23 | `EXEC`/`CALL` yield node kind `procedure`; every other verb yields kind `unknown` | `E2E:307-313`; both branches over all verbs at `SqlStatementReaderTests.cs:82-101` | PASS |
| DAD-24 | one `writes-column` per `INSERT` column, `usage` = `write` | `E2E:324-328`; reader at `SqlStatementReaderTests.cs:128` | PASS |
| DAD-25 | one `writes-column` per `UPDATE ... SET` assignment, `usage` = `write` | `E2E:338-340`; single-relation shape enforced at `:335` | PASS |
| DAD-26 | `WHERE col = literal` or `= @param` yields `filters-by`, `usage` = `filter` | `E2E:350-354` `[Theory]` over both right-hand sides | PASS |
| DAD-27 | dynamic SQL: `target_id` null, `target_text` `dynamic-table`, reason naming dynamic SQL, `Unresolved`, **no node** | `E2E:364-372` — full field set asserted, plus `Assert.DoesNotContain(fixture.Database.Objects, n => n.Name is "dynamic-table" or "tableName" or "{tableName}")` | PASS |
| DAD-28 | readable verb with unreadable target: `target_id` null, `Unresolved`, statement preserved as a `sql` detail | `E2E:382-386` | PASS |

**Status**: 28/28 substantiated. 0 open gaps.

### Payload / conjunction rule

Applied to every emitted field, unchanged from iteration 1 since no fix touched this behaviour. Every named
detail in the criteria — `operation`, `usage`, `target_text`, `sql`, `mapping` — is asserted by *value*, not
by call occurrence. Re-confirmed the `DAD-15` and `DAD-18` fixes follow the same discipline: the redaction
fix is asserted on `Signature`/`SymbolId.Value` content, not on whether a method ran; the exit-code fix is
asserted on `result.ExitCode`'s actual value, not on the absence of a thrown exception.

---

## Findings

### Finding 1 — `DAD-15` RESOLVED

Iteration 1's Finding 1 (FAIL-level) is closed. See the DAD-15 row above for the independently-reproduced
evidence. The fix is a genuine production change (`SyntaxFactExtractor` + new `CredentialText` helper), not
a re-scoped assertion — the opposite of what closed the gap the first time. The distinction the fix relies
on (facts vs. verbatim source reproduction) is now demonstrably real: `source_sections` is confirmed to be
a top-level array sibling to `symbols`/`database_objects`/`relations`/`diagnostics` in the document JSON,
not a nested field of any fact.

### Finding 2 — `DAD-22` spec-precision gap RESOLVED

`spec.md` AC22 now states the access relation resolves at `Syntactic`, matching the tested behaviour and
the spec's own Edge Case. `validate_spec.py` confirms the requirement is still well-formed EARS.

### Finding 3 — `DAD-21` (`MERGE`) spec-precision gap RESOLVED

`spec.md` AC21 now states `MERGE` derives `update`, matching `SqlStatementReader.cs`.

### Finding 4 — `DAD-16` honestly closed (unchanged); `DAD-18` evidence gap RESOLVED

`DAD-16`'s closure stands as assessed in iteration 1 (single chokepoint, exact-length and exact-prefix
assertions). `DAD-18`'s previously-unasserted exit-code clause now has a direct assertion at the
`AnalysisEngine.AnalyzeAsync` level — see the DAD-18 row above.

### Finding 5 — No `DataAccessCollector.Refine`, and no dead production code (CONFIRMED, unchanged)

Re-checked against the fix commits too: `ecff901` and `e49bbd7` add a redaction helper and a constructor
seam, neither of which is dead — `CredentialText.Carries` is called from both `SyntaxFactExtractor` and
`SqlTextAnalyzer`, and the new `dataAccessAnalyzers` field is read at the `SyntaxFactExtractor.Extract` call
site in `AnalysisEngine.AnalyzeProjectAsync`. No `Refine` overload exists; still correctly omitted, per the
reasoning in iteration 1's Finding 5 (unchanged, since no fix touched the analyzer seam's shape).

### Finding 6 — Phase 0's three repairs are genuine, and the rewritten assertion is stricter (CONFIRMED, unchanged)

Unchanged from iteration 1 — none of the three fix commits touched `AnalysisEngine`'s structural-failure
path, `CanonicalAggregateWriter`, or `CoverageProjector`.

---

## Edge Cases (from `spec.md`)

- [x] `DbSet` on a non-`DbContext` type emits nothing — `EfCoreAnalyzerTests.cs:101`
- [x] Assigned property matching no exposed entity emits no `writes-column` — `EfCoreAnalyzerTests.cs:420`
- [x] Non-literal `ToTable` / `HasColumnName` falls back to convention at `Heuristic` — `EfCoreAnalyzerTests.cs:133,215`
- [x] Document with no persistence API usage emits nothing and records no diagnostic —
      `AnalysisEngineTests.AnalyzeAsync_ProjectWithoutPersistenceCode_WritesAnEmptyDataPartitionAndNoDiagnostic`
- [x] Verb-leading literal used as a message yields an access at `Syntactic` — `SqlTextAnalyzerTests.cs:125-134`
- [x] Several `DbContext`s exposing the same entity — covered by T15's multi-context test (commit `7165a06`)

---

## Code Quality

| Principle | Status |
| --- | --- |
| Minimum code — nothing beyond the criteria | PASS (no `Refine`; the two fix commits are each the smallest change that closes their gap) |
| Surgical changes, no unrelated "improvements" | PASS |
| No dead production code | PASS (scan in Finding 5, re-checked against the fix commits) |
| Matches existing patterns (`RelationCollector`/`RelationResolver` split, AD-014 identity grammar, AD-015 two-pass shape, `onSymbolIndexBuilt`-style test seams) | PASS |
| Spec-anchored outcome check — asserted values match the spec | 28/28 |
| Per-layer Coverage Expectation met (matrix in `tasks.md`) | PASS |
| Every test maps to an AC, Edge Case or Done-when criterion | PASS |
| Documented guidelines followed (`CLAUDE.md` / `AGENTS.md`) | PASS |

---

## Requirement Traceability Update

| Requirement | Current status in `spec.md` | Substantiated? |
| --- | --- | --- |
| DAD-01..DAD-28 | Verified | Yes — `file:line` evidence above, all 28/28 |
| DAD-29..DAD-38 | Pending | Correct — P2/P3 outside this task list's scope |

No traceability edit is needed in `spec.md`: every P1 row already reads `Verified`, and this report now
substantiates all of them.

---

## Summary

**PASS.** All 28 P1 acceptance criteria are spec-anchored with `file:line` evidence matching the
spec-defined outcome. The two gaps a first Verifier pass found are closed by genuine production fixes, not
by narrowing assertions: `DAD-15`'s credential leak is fixed at its source (`SyntaxFactExtractor`) and
independently re-reproduced absent from the tree; `DAD-18`'s previously-untested exit-code clause now has a
real assertion at the engine level. Both spec-precision gaps are resolved by wording-only `spec.md` edits
matching already-tested behaviour. The gate is clean (build, format, all tests but one pre-existing,
unrelated, isolation-confirmed flake). The discrimination sensor remains skipped per the user's standing
request for this feature — not run, not counted toward this PASS.

Feature: **done**, pending the user's own deferred Stryker pass and the push/PR decision, both explicitly
out of Execute's authority per the blast-radius rule.
