# Relation Retrieval Index Validation

**Date**: 2026-08-22  
**Spec**: `.specs/features/relation-retrieval-index/spec.md`  
**Diff range**: `6e2878f^..7279d97` (T1--T14)  
**Verifier**: independent sub-agent (author != verifier)

---

## Validation: PASS

Fresh evidence-or-zero verification found an exact assertion for every numbered RRI
criterion and the formerly unnumbered additive/missing-field reader criterion. T13
and T14 close every gap in the preceding FAIL report.

## Task Completion

| Task | Status | Notes |
| --- | --- | --- |
| T1--T14 | Done | All tasks are checked complete in `tasks.md`; final follow-up commits are `d84fa5e` and `7279d97`. |

## Spec-Anchored Acceptance Criteria

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| RRI-01 | Validated fragments produce `raw/index/` without rewriting factual bytes. | `CanonicalAggregateWriterTests.cs:188-192` -- byte equality plus index manifest, summary, and catalogues written. | PASS |
| RRI-02 | The five named lookup families are available. | `RetrievalIndexProjectorTests.cs:30-33` -- exact `project`, `source`, `target`, `kind`, `resolution` family sequence. | PASS |
| RRI-03 | A shard at the 262144-byte limit is accepted. | `BoundedShardWriterTests.cs:78-83` -- one default-limit descriptor has exactly 262144 bytes. | PASS |
| RRI-04 | A 262145-byte singleton fails with its family, key, and relation identity. | `BoundedShardWriterTests.cs:79-86` -- exact `InvalidOperationException` message. | PASS |
| RRI-05 | `Orders.Status` writers are recovered from its target shard without the relation aggregate. | `RelationRetrievalIndexEndToEndTests.cs:30-45` -- one target shard keyed by `statusId`, nonempty writers, exact target IDs and factual references. | PASS |
| RRI-06 | Equal input preserves relation IDs across runs. | `RelationRetrievalIndexEndToEndTests.cs:57-58` -- exact run-ID and relation-ID equality. | PASS |
| RRI-07 | Manifest and all shards carry schema version and matching run ID. | `RelationRetrievalIndexEndToEndTests.cs:69-75` -- version `1` and equality to manifest `analysis_run_id`. | PASS |
| RRI-08 | Effective analysis, trust, and restore state propagate. | `RelationRetrievalIndexEndToEndTests.cs:77-79` -- exact `syntax-only`, `untrusted`, and `false`. | PASS |
| RRI-09 | Evidence contains generator, project, document, hash, and coordinates. | `RelationRetrievalIndexEndToEndTests.cs:97-104` -- required strings and positive start/end coordinates. | PASS |
| RRI-10 | Proof quality and resolution method remain distinct documented fields. | `FactualSchemaSyncTests.cs:69-70` -- exact distinct descriptions. | PASS |
| RRI-11 | Exact/dynamic/unresolved counts and percentages occur by partition and kind. | `RelationRetrievalIndexEndToEndTests.cs:85-86,274-284` -- exact counts and percentage formula for both metric dimensions. | PASS |
| RRI-12 | Summary reports indexed-symbol and proven-entry-point counts. | `RelationRetrievalIndexEndToEndTests.cs:80-81` -- derived symbol count and exact zero mapped entry points. | PASS |
| RRI-13 | Syntax-only output explicitly names compile-time, DI, and gRPC limits. | `RelationRetrievalIndexEndToEndTests.cs:82-83` -- exact ordered limitations array. | PASS |
| RRI-14 | UNKNOWNs consolidate reason/source/observed-target occurrences. | `RelationRetrievalIndexEndToEndTests.cs:149-154` -- exact reason, target text, count, impact, and relation IDs. | PASS |
| RRI-15 | UNKNOWNs prioritize proven entry points, then impact and deterministic ties. | `RelationRetrievalIndexEndToEndTests.cs:155-158,173-175` -- stable tie order and entry-point-before-higher-impact result. | PASS |
| RRI-16 | Catalogues include only entries proved by persisted facts. | `RelationRetrievalIndexEndToEndTests.cs:188-192` -- five exact catalogue collections. | PASS |
| RRI-17 | Missing runtime target preserves absence rather than fabricating target identity/key. | `RetrievalIndexProjectorTests.cs:44-50` -- missing `target_id`, no target shard keyed empty, exact UNKNOWN relation IDs; `RelationRetrievalIndexEndToEndTests.cs:150-151` asserts reason/text. | PASS |
| RRI-18 | Unknown relation/evidence fields survive as opaque extensions. | `RelationRetrievalIndexEndToEndTests.cs:116-119` -- exact future relation and evidence values. | PASS |
| P4 additive/missing-field compatibility | A prior index missing additive fields is still readable with documented defaults. | `RetrievalIndexContractsTests.cs:11-30` -- reader accepts omitted `generated_origin` and `analysis_limitations`; asserts `false` and `null` while retaining manifest, shard, and summary content. | PASS |
| RRI-19 | Same semantics with distinct locations remain distinct entries. | `RelationRetrievalIndexEndToEndTests.cs:131-132` -- exact two IDs and `[7, 8]` source lines. | PASS |
| RRI-20 | Generated and ordinary origins remain explicit in factual and index evidence. | `RelationRetrievalIndexEndToEndTests.cs:219-222` -- exact factual/index true and false values. | PASS |
| RRI-21 | Only a case-insensitive leading auto-generated header is detected. | `GeneratedOriginTests.cs:13-18` -- generated, upper-case, ordinary trailing marker, and null inputs with exact expected booleans. | PASS |

**Status**: 21/21 RRI requirements and 1/1 additive-reader acceptance criterion match their specified outcomes.

## Edge Cases

| Edge case | Evidence | Result |
| --- | --- | --- |
| No relations emits valid zero output and empty catalogues. | `RetrievalIndexProjectorTests.cs:85-100` -- zero shards/counts, empty metrics/UNKNOWNs, manifest run ID, and every catalogue has empty `entries`. | PASS |
| No target identity is fabricated. | `RetrievalIndexProjectorTests.cs:44-50` -- no `target_id` or target key; grouped UNKNOWN relation IDs remain. | PASS |
| Multiple evidence documents retain ordinal provenance. | `RelationRetrievalIndexEndToEndTests.cs:203-206` -- exact document and path order. | PASS |
| Exact 262144/262145 boundary behavior. | `BoundedShardWriterTests.cs:72-86` -- exact accepted byte length and exact overflow diagnostic. | PASS |
| No proven entry points yields empty catalogue and count zero. | `RetrievalIndexProjectorTests.cs:69-81` -- exact zero count and empty `entry-points.entries`. | PASS |

## Gate Check

- **Gate command**: `dotnet build csharp2md.slnx -c Release; dotnet format csharp2md.slnx --verify-no-changes; dotnet test csharp2md.slnx`
- **Build**: PASS -- Release build: 0 warnings, 0 errors.
- **Format**: PASS -- no formatting changes required.
- **Full tests**: PASS under the sole documented MSBuild-flake waiver. After the authorized temporary isolation of the pre-existing global-tool shim, the suite reported only `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` failing at `DotnetMsBuildEvaluatorTests.cs:110`; PackagingSmoke passed. The isolated retry command below passed 1/1.
- **Flake retry**: `dotnet test csharp2md.slnx --no-build --no-restore --filter "FullyQualifiedName=Csharp2Md.Core.Tests.Analysis.Semantics.MSBuild.DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml"` -- 1 passed, 0 failed, 0 skipped.
- **Shim handling**: `C:\\Users\\cdpfe\\.dotnet\\tools\\csharp2md.exe` was temporarily renamed only for PackagingSmoke and restored in a `finally` block; restoration was verified and no repository file changed.
- **Test count**: 1,523 discovered test methods (`dotnet test csharp2md.slnx --list-tests --no-build --no-restore` with the project filter). The runner expands theories to 1,950 cases per the feature matrix; no tests were skipped.

## Discrimination Sensor

**Skipped by standing project policy.** `AGENTS.md` and `tasks.md` explicitly override `tlc-spec-driven` fault injection because the user runs Stryker manually. The required test-gap review was read-only: the critical shard-boundary, lookup-family, missing-target, empty-catalogue, and additive-reader branches each have direct outcome assertions above. No mutation was applied.

## Code Quality

| Check | Status |
| --- | --- |
| Minimum, feature-scoped changes | PASS |
| Exact, non-shallow assertions | PASS -- bytes, named collections, boundary/message, JSON absence, defaults, and reader outcomes are asserted. |
| No unexplained test decrease | PASS -- current discovery is 1,523 methods; traceability documents the compatible counting method. |
| Diff hygiene | PASS -- `git diff --check d84fa5e^..7279d97` is clean. |
| Spec-anchored coverage | PASS -- all criteria cited above. |

## Summary

**Overall**: Ready.

T13 proves the exact family names and absence/empty-output contracts. T14 supplies the public consumer reader and proves older index documents remain readable when additive fields are absent. The only full-suite failure is the known MSBuild flake, which passed its required isolated retry; packaging smoke passed after an authorized, restored shim isolation.
