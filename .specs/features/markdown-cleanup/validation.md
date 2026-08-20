# Markdown Cleanup Validation

**Date**: 2026-08-19
**Spec**: `.specs/features/markdown-cleanup/spec.md`
**Diff range**: `feat/csharp2md-v3..feat/markdown-cleanup` (commits `9c59cf1` core behavior change, `e238572` version bump, `4a73b60` docs-only handoff — excluded from AC coverage, `d7cd2fd` fix commit re-verified in this round)
**Verifier**: independent sub-agent (author ≠ verifier) — **round 2 (re-verification)**

---

## Task Completion

This report **supersedes** the round-1 report (`d7cd2fd`'s predecessor state). Round 1 found MDCLN-11's diagnostics-ordinal-ordering sub-clause (11c) had zero evidence, and two spec-named edge cases (`NotApplicable` symbol counting, YAML-fence independence from source backtick length) were untested. An author (not this Verifier) added fix commit `d7cd2fd`, which — independently confirmed via `git diff d7cd2fd~1..d7cd2fd --stat` — touches **only** `tests/Csharp2Md.Core.Tests/Projection/Markdown/MarkdownProjectorTests.cs` (+3 new `[Fact]` methods, +50/-4 lines) plus `.specs/` bookkeeping files. No `src/` file changed in the fix commit — production behavior is byte-identical to round 1's.

| Commit | Status | Notes |
| --- | --- | --- |
| `9c59cf1` feat(markdown): compact the per-document analysis summary | ✅ Done | `MarkdownProjector.AppendAnnotations` → `AppendAnalysis`; 2 snapshots re-approved |
| `e238572` build(release): bump package version to 3.0.1 | ✅ Done | `Directory.Build.props`, `AnalysisEngine.cs:154` literal, `PackagingSmokeTests.cs` all consistent |
| `4a73b60` docs(state): hand off mid-feature | — excluded | docs-only, no AC surface |
| `d7cd2fd` test(markdown): close verifier-flagged coverage gaps for MDCLN-11 | ✅ Done | 3 new `[Fact]` tests added to `MarkdownProjectorTests.cs`; zero `src/` diff |

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| MDCLN-01: source preserved verbatim, span-coverage partition/fencing unchanged | Byte-identical source reconstruction; `ValidatePartition` untouched | `tests/Csharp2Md.Core.Tests/Projection/Markdown/MarkdownProjectorTests.cs:72` — `Assert.Equal(source, string.Concat(...Sections...))`; `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs:109-121` `ValidatePartition` has zero diff across all 4 commits in this feature | ✅ PASS |
| MDCLN-02: distinct `##` sections per structural kind, unchanged | Same section-kind separation as today | `tests/.../MarkdownProjectorTests.cs:221-232` `Project_RepresentativeStructuralKindsHaveDeterministicHeadings` (untouched by the feature; `Title()` switch at `src/.../MarkdownProjector.cs:136-158` has zero diff) | ✅ PASS |
| MDCLN-03: `facts_ref` continues in frontmatter | `facts_ref` key present | `tests/Csharp2Md.Core.Tests/Projection/Markdown/FrontmatterV2Tests.cs:25` (file untouched by this feature — confirmed via `git diff --stat`); snapshot `V3DeterminismTests....verified.md:13` shows `facts_ref: "facts/document/45/..."` | ✅ PASS |
| MDCLN-04: WHEN ≥1 symbol/relation/diagnostic THEN single `## Analysis` section w/ fenced yaml block, before first structural section | Heading + fence ordering | `src/.../MarkdownProjector.cs:22` (`AppendAnalysis` called before the section loop); `src/.../MarkdownProjector.cs:62` (`"## Analysis\n\n\`\`\`yaml\n"`); `tests/.../MarkdownProjectorTests.cs:71` — `Assert.True(markdown.IndexOf("## Analysis"...) < markdown.IndexOf("\`\`\`csharp"...))` | ✅ PASS |
| MDCLN-05: no per-symbol / per-diagnostic-occurrence line | No `` - `kind` — resolution `` bullets, no per-occurrence diagnostic lines | `tests/.../MarkdownProjectorTests.cs:69-70` — `Assert.DoesNotContain("- \`class\`", markdown)`, `Assert.DoesNotContain("attributes:", markdown)`; implementation only emits grouped counts (`src/.../MarkdownProjector.cs:70-104`) | ✅ PASS |
| MDCLN-06: `resolution:` = `document.Header.Resolution` verbatim | Value tracks header, not hardcoded | `src/.../MarkdownProjector.cs:63` — `.Append("resolution: ").Append(Wire(document.Header.Resolution))`; `tests/.../MarkdownProjectorTests.cs:91-104` `Project_AnalysisBlock_ResolutionLineReflectsDocumentHeaderResolutionNotAHardcodedValue` — header forced to `Exact`, asserts `"resolution: exact"` present **and** `"resolution: syntactic"` absent | ✅ PASS |
| MDCLN-07: `symbols:` map, keys = present `FactResolution` kinds (count>0), values sum to `symbol_count` | Exact aggregated counts | `tests/.../MarkdownProjectorTests.cs:118-152` `Project_AnalysisBlock_AggregatesResolutionKindsAndDiagnosticsByCode` — exact substring `"symbols:\n  exact: 1\n  syntactic: 2\n  unresolved: 1\n"` (4 symbols total, matches fixture) | ✅ PASS |
| MDCLN-08: `relations:` same shape, over `fragment.Facts.OfType<RelationFact>()` with no `DocumentId` filter | Same relation set as frontmatter's `relation_count` | `src/.../MarkdownProjector.cs:48-50` — no `.Where(DocumentId ==...)` filter (contrast with symbols at line 44-47); `tests/.../MarkdownProjectorTests.cs:147-151` asserts `"relations:\n  exact: 1\n  unresolved: 1\n"` | ✅ PASS |
| MDCLN-09: `diagnostics:` keyed by `Code`, aggregated across severities; `{}` when zero | Exact aggregated count, empty-map form | `src/.../MarkdownProjector.cs:56-60` groups by `diagnostic.Code` only; `tests/.../MarkdownProjectorTests.cs:133-136,149` — two diagnostics with different `DiagnosticSeverity` (Warning/Error), same code, asserted as `"diagnostics:\n  C2M-BIND-002: 2\n"`; empty case at `tests/.../MarkdownProjectorTests.cs:68` — `Assert.Contains("diagnostics: {}", markdown)` | ✅ PASS |
| MDCLN-10: IF zero symbols+relations+diagnostics THEN omit `## Analysis` entirely | Section absent | `src/.../MarkdownProjector.cs:51-54` guard; `tests/.../MarkdownProjectorTests.cs:200-205` `Project_NoSymbolsRelationsOrDiagnostics_OmitsAnalysisSection` — `Assert.DoesNotContain("## Analysis", markdown)` | ✅ PASS |
| MDCLN-11a: same fragment rendered twice → byte-identical output | `first == second` | `tests/.../MarkdownProjectorTests.cs:106-115` `Project_AnalysisBlock_IsDeterministicAcrossRepeatedProjection` — `Assert.Equal(first, second)` | ✅ PASS |
| MDCLN-11b: fixed resolution-kind order (`exact, partial, syntactic, unresolved, notapplicable`) | Enum-declaration order, kinds present only | `src/Csharp2Md.Core/Facts/Model/FactResolution.cs:5-9` declares `Exact, Partial, Syntactic, Unresolved, NotApplicable` in that order; `src/.../MarkdownProjector.cs:76` `Enum.GetValues<FactResolution>().Where(counts.ContainsKey)` relies on declaration order; `tests/.../MarkdownProjectorTests.cs:147-151` shows `exact, syntactic, unresolved` sub-sequence; `tests/.../MarkdownProjectorTests.cs:173-186` (new) shows `syntactic, notapplicable` sub-sequence — both consistent with declared order | ✅ PASS |
| MDCLN-11c: diagnostics ordered by `Code`, ordinal | Deterministic ordering across ≥2 distinct codes | **NOW COVERED.** `tests/.../MarkdownProjectorTests.cs:154-171` `Project_AnalysisBlock_DiagnosticsAreOrderedByCodeOrdinalNotEncounterOrder` — constructs `C2M-BIND-900` (encountered first) then `C2M-BIND-001` (encountered second), asserts rendered output is `"diagnostics:\n  C2M-BIND-001: 1\n  C2M-BIND-900: 1\n\`\`\`"` — ordinal order (`001` before `900`) overriding insertion/encounter order. Independently confirmed by this round's discrimination sensor mutation 1 (`OrderBy`→`OrderByDescending`), killed by exactly this test. | ✅ PASS (gap closed) |

**Status**: ✅ All 11 ACs covered with precise, spec-matching, `file:line`-anchored evidence. No spec-precision gaps remain.

---

## Discrimination Sensor

Isolated scratch worktree: `git worktree add ../csharp2md-verify-scratch2 HEAD` (HEAD = `d7cd2fd`, the fix commit — the current tip of `feat/markdown-cleanup`; the branch itself couldn't be checked out a second time since the real tree already has it checked out, so `HEAD` was used directly, same detached-commit content). Baseline `git status --porcelain` on the real tree captured before sensor work; re-diffed byte-identical after cleanup. Mutations applied one at a time to the scratch copy of `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs`, each reverted before the next was applied. `git worktree remove --force` ran at the end; the real tree was never touched.

These are 3 **new** mutations, chosen specifically to probe the 3 previously-uncovered behaviors now closed by `d7cd2fd` (not a repeat of round 1's 3 mutations, which targeted the whole-section-omission guard, the diagnostics grouping key, and reversed resolution-kind order):

| Mutation | File:line | Description | Killed? |
| --- | --- | --- | --- |
| 1 | `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs:58` | Diagnostics ordering: `.OrderBy(group => group.Key, StringComparer.Ordinal)` → `.OrderByDescending(...)` — probes MDCLN-11c | ✅ Killed — 1 failed / 16 total (`Project_AnalysisBlock_DiagnosticsAreOrderedByCodeOrdinalNotEncounterOrder`) |
| 2 | `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs:76` | `AppendResolutionCounts`: filtered `FactResolution.NotApplicable` out of the presented-kinds set (`Enum.GetValues<FactResolution>().Where(kind => counts.ContainsKey(kind) && kind != FactResolution.NotApplicable)`), silently dropping it from the sum — probes the `NotApplicable` edge case | ✅ Killed — 1 failed / 16 total (`Project_AnalysisBlock_NotApplicableSymbolIsCountedNotDropped`) |
| 3 | `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs:22,39,62,67` | Coupled the `## Analysis` YAML fence to the source-derived `fence` variable instead of the hardcoded 3-backtick literal (threaded `fence` into `AppendAnalysis` and used it for the yaml block open/close) — probes the YAML-fence-independence edge case | ✅ Killed — 1 failed / 16 total (`Project_AnalysisBlock_FenceStaysPlainRegardlessOfSourceBacktickRun`) |

**Sensor depth**: lightweight (default tier, 3 mutations)
**Result**: 3/3 killed — ✅ PASS

Each mutation was killed by exactly one test — the specific new test written to close that gap — and no other test in the 16-test `MarkdownProjectorTests` filter broke collaterally. This is strong evidence the 3 new tests are not just present but discriminating for the exact behavior they claim to cover.

---

## Code Quality

| Principle | Status |
| --- | --- |
| Minimum code | ✅ — fix commit is test-only; zero production code changed |
| Surgical changes | ✅ — only `tests/.../MarkdownProjectorTests.cs` and `.specs/` bookkeeping touched in `d7cd2fd`; confirmed via `git diff d7cd2fd~1..d7cd2fd --stat` |
| No scope creep | ✅ — the 3 new tests map 1:1 to the 3 gaps round 1 flagged; nothing extra added |
| Matches patterns | ✅ — new tests reuse the existing `MakeSymbol`/`MakeRelation`/`MakeDiagnostic` helpers and `Fragment` builder already established by the feature's first commit; same `Assert.Contains`-on-exact-substring style |
| Spec-anchored outcome check | ✅ — 11/11 ACs now match spec outcome exactly (up from 10/11 in round 1) |
| Per-layer coverage: domain 1:1 ACs | ✅ — all 11 ACs have direct, precise assertions |
| Every test maps to a spec AC/edge case | ✅ — all 3 new tests map to a specific previously-flagged gap (MDCLN-11c, `NotApplicable` edge case, fence-independence edge case); no unclaimed tests |
| Documented guidelines followed | none - strong defaults applied (no project-specific style doc beyond CLAUDE.md's Roslyn-caution guidance, not applicable to this test-only change) |

No new findings. The round-1 spot-check note about a stray UTF-8 BOM on `MarkdownProjectorTests.Project_RepresentativeDocument_MatchesSpecApprovedSnapshot.verified.md` is unchanged in this round (that file is untouched by `d7cd2fd`) — still non-blocking, still doesn't affect any AC or test outcome.

---

## Edge Cases

- [x] Zero diagnostics but symbols present → `diagnostics: {}` not omitted: `tests/.../MarkdownProjectorTests.cs:68`
- [x] Zero symbols but relations/diagnostics present → `symbols: {}` not omitted: `tests/.../MarkdownProjectorTests.cs:76-88`
- [x] `FactResolution.NotApplicable` symbol counted under `notapplicable:` key — **NOW COVERED**. `tests/.../MarkdownProjectorTests.cs:173-186` `Project_AnalysisBlock_NotApplicableSymbolIsCountedNotDropped` constructs a symbol via `MakeSymbol(documentId, "class", FactResolution.NotApplicable, "not-applicable")` and asserts `"symbols:\n  syntactic: 1\n  notapplicable: 1\n"` is present — proving the count is included, not dropped. Confirmed discriminating by sensor mutation 2.
- [x] YAML fence stays plain ` ```yaml ` independent of source-code longer-fence logic — **NOW COVERED**. `tests/.../MarkdownProjectorTests.cs:188-197` `Project_AnalysisBlock_FenceStaysPlainRegardlessOfSourceBacktickRun` uses a source containing a literal backtick-triplet (`"class C { string S = \"\`\`\`\"; }"`) — which independently triggers the code-fence-lengthening path (asserted via `` "````csharp" `` at line 196) — while simultaneously asserting the Analysis block's fence is still exactly `` "## Analysis\n\n```yaml\n" `` (3 backticks) at line 195. Confirmed discriminating by sensor mutation 3.

---

## Gate Check

- **Gate commands**: `dotnet build -c Release`; `dotnet test -c Release --no-build`; `dotnet format --verify-no-changes`
- **Build result**: 0 errors, 0 warnings
- **Test result**: 1167 total. 3 full-suite runs were executed: run 1 → 1166 passed / 1 failed; run 2 → 1166 passed / 1 failed; run 3 → 1167 passed / 0 failed. The single failure in runs 1–2 was `Csharp2Md.Core.Tests.Analysis.Semantics.MSBuild.DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml`, which asserts no stray `csharp2md-*.preprocessed.xml` temp files are left in `%TEMP%` across a before/after snapshot. This test:
  - is **not** in this feature's diff surface (file untouched by any of the 4 commits in `feat/csharp2md-v3..feat/markdown-cleanup`, confirmed via `git diff --stat`);
  - **passed in isolation** every time it was run alone (`--filter "FullyQualifiedName~...ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml"`);
  - is consistent with a pre-existing race between concurrently-running MSBuild-evaluator test collections writing/cleaning up temp files, not a regression this feature introduced.

  This is disclosed transparently rather than silently retried past: it is a real gate anomaly, but not evidence of a defect in `markdown-cleanup`. All `MarkdownProjectorTests`, `V3DeterminismTests`, `FrontmatterV2Tests`, and `PackagingSmokeTests` — every test in this feature's actual coverage surface — passed clean in all 3 runs.
- **Format result**: exit 0, no diff
- **Test count before this feature** (per round-1 report, `feat/csharp2md-v3`): 1159 (1164 − 5 net-new facts from `9c59cf1`)
- **Test count after `9c59cf1`** (round 1): 1164
- **Test count after `d7cd2fd`** (this round): 1167
- **Delta**: +3 new tests in the fix commit (0 deleted) — no regression in test count
- **Skipped tests**: none
- **Failures**: 1 flaky, pre-existing, out-of-scope failure (see above); 0 failures within this feature's diff surface
- **Sensor isolation check**: `git status --porcelain` on the real tree matched the pre-sensor baseline exactly, both before sensor work and after `git worktree remove --force ../csharp2md-verify-scratch2`

---

## Requirement Traceability Update

| Requirement | Previous Status (round 1) | New Status (round 2, independent verification) |
| --- | --- | --- |
| MDCLN-01 | ✅ Verified | ✅ Verified |
| MDCLN-02 | ✅ Verified | ✅ Verified |
| MDCLN-03 | ✅ Verified | ✅ Verified |
| MDCLN-04 | ✅ Verified | ✅ Verified |
| MDCLN-05 | ✅ Verified | ✅ Verified |
| MDCLN-06 | ✅ Verified | ✅ Verified |
| MDCLN-07 | ✅ Verified | ✅ Verified |
| MDCLN-08 | ✅ Verified | ✅ Verified |
| MDCLN-09 | ✅ Verified | ✅ Verified |
| MDCLN-10 | ✅ Verified | ✅ Verified |
| MDCLN-11 | ❌ Needs Fix (sub-clause 11c only) | ✅ Verified — all 3 sub-clauses (11a/11b/11c) now have direct evidence |

---

## Summary

**Overall**: ✅ Ready

**Spec-anchored check**: 11/11 ACs matched spec outcome exactly. Zero spec-precision gaps.
**Sensor**: 3/3 mutations killed — ✅ PASS (each killed by exactly the new test targeting that behavior)
**Gate**: 1167 total tests; 1167/1167 passed on the clean run; 1 unrelated, out-of-scope, pre-existing flaky test failed intermittently in 2 of 3 full-suite runs (passes in isolation, file untouched by this feature — see Gate Check for full evidence); build clean (0 warnings); format clean.

**What works**: All 3 gaps flagged by the round-1 Verifier are now closed with direct, evidence-or-zero-compliant `file:line` citations, each independently strengthened by a discrimination-sensor mutation this round designed and killed. MDCLN-11c (diagnostics ordinal ordering) is proven with 2 distinct codes inserted out of order and asserted back in ordinal order. The `NotApplicable` edge case is proven counted, not dropped. The YAML-fence-independence edge case is proven by combining a source backtick run (which lengthens the code fence) with an Analysis-bearing fixture (whose YAML fence stays exactly 3 backticks) in the same assertion. All other 8 ACs and the other 2 MDCLN-11 sub-clauses, re-derived independently rather than trusted from round 1, still hold.

**Issues found**: None within this feature's scope. One unrelated, pre-existing, intermittently-flaky test (`DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml`) was observed during the gate run; it is outside this feature's diff surface and not a regression introduced by `markdown-cleanup`. Recommend a separate ticket to fix its temp-file race, but it does not block this feature.

**Next steps**: None required for `markdown-cleanup`. Feature is verified PASS at the 11/11 AC level with sensor-confirmed discriminating tests.
