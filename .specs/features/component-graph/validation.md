# Component Graph Validation

**Date**: 2026-08-22  
**Spec**: `.specs/features/component-graph/spec.md`  
**Diff range**: `79c3aa9..843315d`  
**Verifier**: independent sub-agent (author != verifier)

---

## Task Completion

All T1--T18 tasks in `tasks.md` are checked complete. `validate_tasks.py component-graph` returned **0 errors, 2 warnings**: T16 and T17 declare `Tests: none`, matching the coverage matrix's document-only rows.

## Spec-Anchored Acceptance Criteria

Every P1 criterion has an assertion that targets the specified outcome, not just implementation presence.

| Requirement | Evidence-or-zero assertion evidence | Result |
| --- | --- | --- |
| COMP-01 | `tests/Csharp2Md.Core.Tests/Analysis/Components/ComponentFragmentBuilderTests.cs:14` asserts exactly one `project` component owning exactly its project; real fixture count/ownership at `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:28`. | PASS |
| COMP-02 | `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs:255` follows the manifest's component fragment and asserts its SHA and positive byte length at lines 262-268. | PASS |
| COMP-03 | `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs:293` asserts five distinct component identities for the project reached through several paths at lines 300-311. | PASS |
| COMP-04 | `tests/Csharp2Md.Core.Tests/Analysis/Components/ComponentFragmentBuilderTests.cs:28`, `:40`, and `:52` assert mirrored syntactic/exact resolution, producer provenance, and empty evidence. | PASS |
| COMP-05 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:285` asserts ordered ids, kind and project ids; `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:61` asserts all five and no extra index entries. | PASS |
| COMP-06 | `tests/Csharp2Md.Core.Tests/Analysis/Components/ComponentFragmentBuilderTests.cs:81` asserts no component result; writer fallbacks are asserted exactly at `tests/Csharp2Md.Core.Tests/Projection/Aggregates/CanonicalAggregateWriterTests.cs:56`. | PASS |
| COMP-07 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/CanonicalAggregateWriterTests.cs:84` asserts schema version `5`; graph-writer hand-off is asserted at `:67`. | PASS |
| COMP-08 | `tests/Csharp2Md.Core.Tests/Analysis/Components/ComponentFragmentBuilderTests.cs:68` asserts `InvalidOperationException` and its duplicate-identity message. | PASS |
| COMP-09 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/GraphNodeIndexTests.cs:98` asserts failure when two components claim one project. | PASS |
| COMP-10 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:22` asserts the resolved source, target, partition, kind and count; real named lines at `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:85`. | PASS |
| COMP-11 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:162` asserts one exact `structural:calls ×5` label; exact real-fixture lines at `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:77`. | PASS |
| COMP-12 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:92` asserts empty edges for a self-edge; fixture-wide non-self-edge check at `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:97`. | PASS |
| COMP-13 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:38` asserts empty edges for a null target. | PASS |
| COMP-14 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:80` asserts an unmapped endpoint yields no edge. | PASS |
| COMP-15 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:175` asserts that untouched `Unused` has no node line. | PASS |
| COMP-16 | Unit input-order equality is asserted at `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:204`; two real output files are byte-equal at `tests/Csharp2Md.Core.Tests/Analysis/V3DeterminismTests.cs:103`. | PASS |
| COMP-17 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:149` asserts the project `Name` rectangle label. | PASS |
| COMP-18 | Node escaping is asserted for `#`, `\"`, `|`, CR and LF at `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:247`; complete edge lines for every valid relation-kind special character (`#`, `\"`, `|`) at `:269`. `RelationFactId.Create` calls `RequireCanonicalText` at `src/Csharp2Md.Core/Facts/Identity/FactIds.cs:132-144`; that grammar rejects CR/LF at `src/Csharp2Md.Core/Facts/Identity/FactId.cs:55-65`, so CR/LF cannot be valid edge-label input. | PASS |
| COMP-19 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:104` asserts count 3 for one group; `:119` asserts different kinds remain two edges. | PASS |
| COMP-20 | Unit rectangle/cylinder distinction at `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:149`; each real database object has a reached cylinder at `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:125`. | PASS |
| COMP-21 | Column-to-object identity is asserted at `tests/Csharp2Md.Core.Tests/Projection/Aggregates/GraphNodeIndexTests.cs:72`; exactly three cylinders/no column node at `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:151`. | PASS |
| COMP-22 | Column- and object-targeted relations collapse to one count-2 edge at `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:134`; fixture `writes-column ×4` exact line at `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:140`. | PASS |
| COMP-23 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:149` asserts Mermaid's exact cylinder syntax. | PASS |
| COMP-24 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:320` asserts a database object is absent from the index; live fixture check at `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:162`. | PASS |
| COMP-25 | Exactly one diagnostic with exact count is asserted at `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:342`; information/projection metadata at `:358`. | PASS |
| COMP-26 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:373` asserts self-edge/null-target-only input yields no diagnostic. | PASS |
| COMP-27 | A real run asserts exactly one persisted `C2M-CG-001`, information severity and count `1` at `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs:321-373`. | PASS |
| COMP-28 | `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:112` asserts `Acme.Broken` is a component but absent from the diagram. | PASS |
| COMP-29 | `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs:51` asserts `Acme.DoesNotExist` is a component/index entry and absent from Mermaid. | PASS |
| COMP-30 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:222` asserts two same-named objects remain distinct and output is stable across opposite construction orders. | PASS |
| COMP-31 | `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs:275` asserts exit code `1` and persisted `C2M-FV-002`. | PASS |
| COMP-32 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs:194` asserts exactly `flowchart LR\n` for no edges; output determinism coverage at `tests/Csharp2Md.Core.Tests/Analysis/V3DeterminismTests.cs:103`. | PASS |

**Status**: **32/32 P1 criteria matched their specified outcomes; 0 spec-precision gaps.**

## Edge Cases

- [x] COMP-28: no-cross-component-relation project stays indexed but absent from Mermaid.
- [x] COMP-29: no-document project is still a component and index entry.
- [x] COMP-30: same-labelled database objects remain distinct and canonically ordered.
- [x] COMP-31: unknown project reference fails with `C2M-FV-002`.
- [x] COMP-32: no edges writes exactly the flowchart header.

## Discrimination Sensor

**Skipped by repository instruction.** `AGENTS.md` permanently directs agents not to run the `tlc-spec-driven` fault-injection/discrimination sensor; the user runs Stryker manually. No mutation was injected and no real-tree source or test file was modified.

## Gate Check

| Gate | Result |
| --- | --- |
| `python .agents/skills/tlc-spec-driven/scripts/validate_spec.py component-graph` | PASS — 0 errors, 0 warnings |
| `python .agents/skills/tlc-spec-driven/scripts/validate_tasks.py component-graph` | PASS — 0 errors, 2 expected document-only warnings (T16/T17) |
| `dotnet build csharp2md.slnx -c Release` | PASS — 0 warnings, 0 errors |
| `dotnet format csharp2md.slnx --verify-no-changes` | PASS |
| `dotnet test csharp2md.slnx` | 1917 passed, 1 failed, 0 skipped, 1918 total |

The sole test failure is `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` at `tests/Csharp2Md.Core.Tests/Analysis/Semantics/MSBuild/DotnetMsBuildEvaluatorTests.cs:110` (`Assert.True(before.SetEquals(after))`). `git diff 79c3aa9..843315d --` for that file is empty, so it is unchanged and unrelated to the feature; all feature tests were included in the 1917 passing results. No feature test is skipped or disabled.

## Test Integrity and Quality Review

- Diff review: the feature adds focused component-builder, node-index, projector and real-pipeline tests; the narrowed `RelationProjector` tests preserve partition behaviour while graph-specific coverage moved to the stricter graph projector suite. No feature test was deleted without replacement, disabled, or weakened.
- Assertion review: the scoped component-graph tests use exact equality, collection cardinality/content, negative assertions, and exact exception assertions; no reviewed test is assertion-free, trivial-only, or self-referential. The end-to-end tests assert complete Mermaid lines and exact collapsed counts, rather than non-empty output.
- Anti-pattern review: 0 Critical, 0 High, 0 Medium, 0 Low findings in the feature test surface. Tests use xUnit `[Theory]` cases for the escape and fixture matrices, per-test temporary outputs/fixtures, and cleanup through `IAsyncLifetime`/`IDisposable`.
- Slopwatch: `dotnet tool run slopwatch analyze` reports five SW004 delay warnings, all outside `79c3aa9..843315d`; no introduced disabled-test, warning-suppression, empty-catch, delay, or package-management shortcut was found.
- Code quality: changed production code follows AD-003's bounded aggregate pass and AD-014's opaque-id navigation; graph diagnostics are added before coverage at `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs:282-302`, preventing the documented persistence gap. Changes are feature-scoped and format-clean.

## Summary

**Overall**: PASS — ready, with the independently confirmed pre-existing MSBuild evaluator test failure documented above.

**Spec-anchored check**: 32/32 P1 criteria matched; 0 precision gaps.  
**Sensor**: skipped under the permanent repository instruction.  
**Gate**: structural validators, Release build, and format pass; full suite is 1917/1918 passing with one unchanged unrelated failure.
