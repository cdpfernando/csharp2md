# Markdown Cleanup Validation

**Date**: 2026-08-19
**Spec**: `.specs/features/markdown-cleanup/spec.md`
**Diff range**: `feat/csharp2md-v3..feat/markdown-cleanup` (commits `9c59cf1`, `e238572`; `4a73b60` is docs-only handoff, excluded from AC coverage per task instructions)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Task Completion

No `tasks.md` (spec is "Medium" scope, formal Tasks phase skipped). Treating the two real commits as the unit of completion:

| Commit | Status | Notes |
| --- | --- | --- |
| `9c59cf1` feat(markdown): compact the per-document analysis summary | ✅ Done | `MarkdownProjector.AppendAnnotations` → `AppendAnalysis`; 2 snapshots re-approved; 8 tests added/changed in `MarkdownProjectorTests.cs` |
| `e238572` build(release): bump package version to 3.0.1 | ✅ Done | `Directory.Build.props`, `AnalysisEngine.cs:154` literal, and matching `PackagingSmokeTests.cs` assertions all bumped together and consistent |

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| MDCLN-01: source preserved verbatim, span-coverage partition/fencing unchanged | Byte-identical source reconstruction; `ValidatePartition` untouched | `tests/.../MarkdownProjectorTests.cs:72` — `Assert.Equal(source, string.Concat(...Sections...))`; `src/.../MarkdownProjector.cs:109-121` `ValidatePartition` has zero diff in this feature | ✅ PASS |
| MDCLN-02: distinct `##` sections per structural kind, unchanged | Same section-kind separation as today | `tests/.../MarkdownProjectorTests.cs:176-187` `Project_RepresentativeStructuralKindsHaveDeterministicHeadings` (untouched by this diff, `Title()` switch unchanged) | ✅ PASS |
| MDCLN-03: `facts_ref` continues in frontmatter | `facts_ref` key present | `tests/.../FrontmatterV2Tests.cs:25` (untouched); snapshot `V3DeterminismTests....verified.md:13` shows `facts_ref: "facts/document/45/..."` | ✅ PASS |
| MDCLN-04: WHEN ≥1 symbol/relation/diagnostic THEN single `## Analysis` section w/ fenced yaml block, before first structural section | Heading + fence ordering | `src/.../MarkdownProjector.cs:22` (`AppendAnalysis` called before the section loop at line 23); `src/.../MarkdownProjector.cs:62` (`"## Analysis\n\n\`\`\`yaml\n"`); `tests/.../MarkdownProjectorTests.cs:71` — `Assert.True(markdown.IndexOf("## Analysis"...) < markdown.IndexOf("\`\`\`csharp"...))` | ✅ PASS |
| MDCLN-05: no per-symbol / per-diagnostic-occurrence line | No `` - `kind` — resolution `` bullets, no per-occurrence diagnostic lines | `tests/.../MarkdownProjectorTests.cs:69-70` — `Assert.DoesNotContain("- \`class\`", markdown)`, `Assert.DoesNotContain("attributes:", markdown)`; implementation only ever emits grouped counts (`src/.../MarkdownProjector.cs:70-104`) | ✅ PASS |
| MDCLN-06: `resolution:` = `document.Header.Resolution` verbatim | Value tracks header, not hardcoded | `src/.../MarkdownProjector.cs:63` — `.Append("resolution: ").Append(Wire(document.Header.Resolution))`; `tests/.../MarkdownProjectorTests.cs:91-104` `Project_AnalysisBlock_ResolutionLineReflectsDocumentHeaderResolutionNotAHardcodedValue` — header forced to `Exact`, asserts `"resolution: exact"` present (line 102) **and** `"resolution: syntactic"` absent (line 103) | ✅ PASS |
| MDCLN-07: `symbols:` map, keys = present `FactResolution` kinds (count>0), values sum to `symbol_count` | Exact aggregated counts | `tests/.../MarkdownProjectorTests.cs:118-151` `Project_AnalysisBlock_AggregatesResolutionKindsAndDiagnosticsByCode` — asserts exact substring `"symbols:\n  exact: 1\n  syntactic: 2\n  unresolved: 1\n"` (4 symbols total, matches constructed fixture) | ✅ PASS |
| MDCLN-08: `relations:` same shape, over `fragment.Facts.OfType<RelationFact>()` with no `DocumentId` filter | Same relation set as frontmatter's `relation_count` | `src/.../MarkdownProjector.cs:48-50` — no `.Where(DocumentId ==...)` filter (contrast with symbols at line 45); `tests/.../MarkdownProjectorTests.cs:147-151` asserts `"relations:\n  exact: 1\n  unresolved: 1\n"` | ✅ PASS |
| MDCLN-09: `diagnostics:` keyed by `Code`, aggregated across severities; `{}` when zero | Exact aggregated count, empty-map form | `src/.../MarkdownProjector.cs:56-60` groups by `diagnostic.Code` only (severity dropped); `tests/.../MarkdownProjectorTests.cs:133-136,149` — two diagnostics with different `DiagnosticSeverity` (Warning/Error), same code, asserted as `"diagnostics:\n  C2M-BIND-002: 2\n"`; empty case at `tests/.../MarkdownProjectorTests.cs:68` — `Assert.Contains("diagnostics: {}", markdown)` | ✅ PASS |
| MDCLN-10: IF zero symbols+relations+diagnostics THEN omit `## Analysis` entirely | Section absent | `src/.../MarkdownProjector.cs:51-54` guard; `tests/.../MarkdownProjectorTests.cs:154-160` `Project_NoSymbolsRelationsOrDiagnostics_OmitsAnalysisSection` — `Assert.DoesNotContain("## Analysis", markdown)`. Also empirically confirmed by discrimination sensor mutation 1 (flipping `&&`→`\|\|` in this guard killed 5 tests). | ✅ PASS |
| MDCLN-11a: same fragment rendered twice → byte-identical output | `first == second` | `tests/.../MarkdownProjectorTests.cs:107-115` `Project_AnalysisBlock_IsDeterministicAcrossRepeatedProjection` — `Assert.Equal(first, second)` | ✅ PASS |
| MDCLN-11b: fixed resolution-kind order (`exact, partial, syntactic, unresolved, notapplicable`) | Enum-declaration order, kinds present only | `src/.../FactResolution.cs:5-9` declares `Exact, Partial, Syntactic, Unresolved, NotApplicable` in that order; `src/.../MarkdownProjector.cs:76` `Enum.GetValues<FactResolution>().Where(counts.ContainsKey)` relies on declaration order; `tests/.../MarkdownProjectorTests.cs:147-151` shows `exact, syntactic, unresolved` sub-sequence consistent with that order; killed by sensor mutation 3 (reversed enum order) | ✅ PASS |
| MDCLN-11c: diagnostics ordered by `Code`, ordinal | Deterministic ordering across ≥2 distinct codes | No test in scope constructs two **distinct** diagnostic codes and asserts their relative order — the only diagnostics fixture (`MarkdownProjectorTests.cs:133-136`) uses the same code (`C2M-BIND-002`) twice, so ordering-by-code is unobservable from any existing assertion. Sensor mutation 2 (group-by `Message` instead of `Code`) was killed, but that proves grouping-key correctness, not ordering-among-multiple-codes. | ❌ GAP (no `file:line` proves ordinal ordering across distinct codes) |

**Status**: ❌ Gaps present — MDCLN-11's diagnostics-ordinal-ordering sub-clause has no evidence (evidence-or-zero). All other 10 ACs (and the other two sub-clauses of MDCLN-11) PASS with precise, spec-matching evidence.

---

## Discrimination Sensor

Isolated scratch worktree: `git worktree add ../csharp2md-verify-scratch HEAD` (HEAD = `4a73b60`). Baseline `git status --porcelain` on the real tree captured before sensor work and re-diffed identical after cleanup (confirmed match, see Gate Check note below). Mutations applied one at a time to the scratch copy of `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs`, each reverted before the next was applied. `git worktree remove --force` ran at the end; the real tree was never touched.

| Mutation | File:line | Description | Killed? |
| --- | --- | --- | --- |
| 1 | `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs:51` | Flipped the whole-section-omission guard `&&` → `\|\|` (`if (symbolResolutions.Length == 0 \|\| relationResolutions.Length == 0 \|\| fragment.Diagnostics.IsEmpty)`) | ✅ Killed — 5 failed / 19 total (`MarkdownProjectorTests` + `V3DeterminismTests` filter) |
| 2 | `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs:57` | Changed diagnostics grouping key from `diagnostic.Code` to `diagnostic.Message` | ✅ Killed — 1 failed / 19 total (`Project_AnalysisBlock_AggregatesResolutionKindsAndDiagnosticsByCode`) |
| 3 | `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs:76` | Reversed the fixed resolution-kind enumeration order (`Enum.GetValues<FactResolution>().Reverse().Where(...)`) | ✅ Killed — 1 failed / 19 total (same test as mutation 2) |

**Sensor depth**: lightweight (default tier, 3 mutations)
**Result**: 3/3 killed — ✅ PASS

Note: mutation 2's and 3's single-test kill count is itself evidence of a thin margin — only one test (`Project_AnalysisBlock_AggregatesResolutionKindsAndDiagnosticsByCode`) currently exercises the exact-substring shape of a populated `symbols:`/`diagnostics:` block. It is sufficient to kill both mutations tried here, but see the MDCLN-11c gap above: a mutation that reordered *diagnostics* specifically (e.g., sorted descending, or by insertion order) would likely **survive**, since no fixture has two distinct diagnostic codes.

---

## Code Quality

| Principle | Status |
| --- | --- |
| Minimum code | ✅ — `AppendAnalysis`/`AppendResolutionCounts`/`AppendDiagnosticCounts`/`Wire<T>` are the minimal set needed for the map-emission shape; no speculative generality |
| Surgical changes | ✅ — only `MarkdownProjector.cs`, its tests, two snapshots, plus the pre-agreed version-bump files (`Directory.Build.props`, `AnalysisEngine.cs:154`, `PackagingSmokeTests.cs`) touched; `FrontmatterV2.cs` and `schemas/` untouched (confirmed via `git diff --stat`) |
| No scope creep | ✅ — version bump was an explicit, spec-recorded decision (spec.md "Assumptions & Open Questions" row), not silent scope creep |
| Matches patterns | ✅ — hand-rolled YAML emission via `StringBuilder`, 2-space indent, matches `FrontmatterV2.ToYaml()`'s existing style as the spec's "Block format" decision requires |
| Spec-anchored outcome check | ⚠️ — 10/11 ACs match spec outcome exactly; MDCLN-11c (diagnostics ordinal order) has no asserting test (see gap above) |
| Per-layer coverage: domain 1:1 ACs | ⚠️ — 10 of 11 ACs have direct assertions; MDCLN-11c does not |
| Every test maps to a spec AC/edge case | ✅ — all 6 new/renamed tests in `MarkdownProjectorTests.cs` map to specific ACs or edge cases (no unclaimed tests found) |
| Documented guidelines followed | none - strong defaults applied (no project-specific style doc beyond CLAUDE.md's Roslyn-caution guidance, not applicable to this change) |

**One additional spot-check finding (not an AC, not blocking):** the re-approved snapshot `tests/Csharp2Md.Core.Tests/Projection/Markdown/snapshots/MarkdownProjectorTests.Project_RepresentativeDocument_MatchesSpecApprovedSnapshot.verified.md` gained a UTF-8 BOM (`EF BB BF`) at byte 0 that the pre-feature file did not have (confirmed via `git show ...:<path> | head -c 10 | xxd` on both sides of the diff). The sibling snapshot (`V3DeterminismTests....verified.md`) did **not** gain a BOM. `MarkdownProjector`'s own `StringBuilder`-based output has no code path that emits a BOM, so this is an artifact of however the snapshot file was re-approved (e.g. a text editor or `Set-Content` default encoding), not a behavior change in the projector. It does not affect any test (all 1164 pass) or any AC. Flagged only because the spec's Success Criteria explicitly calls for these snapshots to be "human-reviewed... not blindly accepted" — a stray encoding diff unrelated to content is the kind of thing a byte-level accept-without-diff-review would miss.

---

## Edge Cases

- [x] Zero diagnostics but symbols present → `diagnostics: {}` not omitted: `tests/.../MarkdownProjectorTests.cs:68`
- [x] Zero symbols but relations/diagnostics present → `symbols: {}` not omitted: `tests/.../MarkdownProjectorTests.cs:76-88`
- [ ] `FactResolution.NotApplicable` symbol counted under `notapplicable:` key — **NOT covered**. No test in `MarkdownProjectorTests.cs` (or elsewhere in the diff) constructs a symbol with `FactResolution.NotApplicable`. `MakeSymbol` helper (`tests/.../MarkdownProjectorTests.cs:213-225`) is only ever called with `Exact` and `Unresolved` in this diff. The implementation path (`AppendResolutionCounts`, generic over `Enum.GetValues<FactResolution>()`) has no special-casing that would obviously break this case, but the spec explicitly calls it out as an edge case ("never silently dropped from the sum") and evidence-or-zero means an untested code path claiming to handle it does not count as verified.
- [ ] YAML fence stays plain ` ```yaml ` independent of source-code longer-fence logic — **NOT covered**. `Project_SourceBackticks_UsesLongerFence` (`tests/.../MarkdownProjectorTests.cs:49-55`) only asserts the *code* fence lengthens; it never combines that fixture with an Analysis-section assertion to prove the yaml fence stays at exactly 3 backticks. The implementation hardcodes `` "```yaml\n" `` literally (`src/.../MarkdownProjector.cs:62`), independent of the computed `fence` variable, so this is very likely correct by inspection — but no test proves it.

---

## Gate Check

- **Gate commands**: `dotnet build -c Release`; `dotnet test -c Release --no-build`; `dotnet format --verify-no-changes`
- **Build result**: 0 errors, 0 warnings
- **Test result**: 1164 passed, 0 failed, 0 skipped
- **Format result**: exit 0, no diff
- **Test count before feature**: not independently re-run (would require a second full build/test pass on `feat/csharp2md-v3`); inferred from diff inspection — 5 net-new `[Fact]` methods added in `MarkdownProjectorTests.cs` (`Project_AnalysisBlock_ZeroSymbolsRenderAsEmptyMapWhenRelationsPresent`, `Project_AnalysisBlock_ResolutionLineReflectsDocumentHeaderResolutionNotAHardcodedValue`, `Project_AnalysisBlock_IsDeterministicAcrossRepeatedProjection`, `Project_AnalysisBlock_AggregatesResolutionKindsAndDiagnosticsByCode`, `Project_NoSymbolsRelationsOrDiagnostics_OmitsAnalysisSection`), 1 renamed (`Project_SymbolAnnotationsRemainOutsideEveryCodePayload` → `Project_AnalysisSectionRemainsOutsideEveryCodePayload`), 0 deleted
- **Delta**: +5 net new tests, 0 deleted — no regression in test count
- **Skipped tests**: none
- **Failures**: none
- **Sensor isolation check**: `git status --porcelain` on the real tree matched the pre-sensor baseline exactly after `git worktree remove --force ../csharp2md-verify-scratch`

---

## Fix Plans

### Fix 1: MDCLN-11c (diagnostics ordinal ordering) has no asserting test

- **Root cause**: `Project_AnalysisBlock_AggregatesResolutionKindsAndDiagnosticsByCode` (the only test exercising a populated `diagnostics:` map) uses a single diagnostic code (`C2M-BIND-002`) repeated twice. With one distinct code, any ordering is indistinguishable from ordinal-by-`Code` ordering.
- **Fix task**: Add a test (or extend the existing one) using ≥2 distinct diagnostic codes chosen so ordinal order differs from insertion order (e.g. `C2M-BIND-003` inserted before `C2M-BIND-002` in fixture construction), and assert the rendered `diagnostics:` map lists them in ordinal-`Code` order.
- **Priority**: Minor (implementation appears correct by inspection — `OrderBy(group => group.Key, StringComparer.Ordinal)` at `src/.../MarkdownProjector.cs:58` — this is a test-coverage gap, not a known behavior defect).

### Fix 2: `NotApplicable` symbol-counting edge case untested

- **Root cause**: No fixture in the diff constructs a `FactResolution.NotApplicable` symbol.
- **Fix task**: Add a test using `MakeSymbol(..., FactResolution.NotApplicable, ...)` and assert `symbols:` includes a `notapplicable: N` line whose value is included in the total.
- **Priority**: Minor (same reasoning as Fix 1 — likely correct by inspection, but the spec calls this out by name as an edge case).

### Fix 3: Combined backtick-run + Analysis-section fence-independence untested

- **Root cause**: `Project_SourceBackticks_UsesLongerFence` doesn't combine with an Analysis-bearing fixture.
- **Fix task**: Extend that test (or add a new one) with a fixture that has both a source backtick run *and* at least one symbol, and assert the emitted text contains literal `` "```yaml\n" `` (3 backticks, not lengthened) alongside the longer code fence.
- **Priority**: Minor.

---

## Requirement Traceability Update

| Requirement | Previous Status (spec.md, author's self-claim) | New Status (independent verification) |
| --- | --- | --- |
| MDCLN-01 | Verified | ✅ Verified |
| MDCLN-02 | Verified | ✅ Verified |
| MDCLN-03 | Verified | ✅ Verified |
| MDCLN-04 | Verified | ✅ Verified |
| MDCLN-05 | Verified | ✅ Verified |
| MDCLN-06 | Verified | ✅ Verified |
| MDCLN-07 | Verified | ✅ Verified |
| MDCLN-08 | Verified | ✅ Verified |
| MDCLN-09 | Verified | ✅ Verified |
| MDCLN-10 | Verified | ✅ Verified |
| MDCLN-11 | Verified | ❌ Needs Fix (sub-clause 11c only; 11a/11b independently confirmed) |

---

## Summary

**Overall**: ⚠️ Issues

**Spec-anchored check**: 10/11 ACs matched spec outcome exactly; MDCLN-11 partially covered (2 of 3 sub-clauses proven, 1 sub-clause — diagnostics ordinal ordering — has zero evidence)
**Sensor**: 3/3 mutations killed
**Gate**: 1164 passed, 0 failed, 0 skipped; build clean; format clean

**What works**: The compact `## Analysis` YAML block correctly replaces the old per-symbol/per-diagnostic bullet listing. `resolution:` tracks the document header (not hardcoded — proven by a test that deliberately sets a non-default value). `symbols:`/`relations:` aggregate by `FactResolution` kind in fixed enum-declaration order, `{}` when empty. `diagnostics:` aggregates by `Code` across severities, `{}` when empty. The whole-section omission guard correctly extends to check relations too. Both snapshots re-approved and content-consistent with the new contract. `facts.json`, frontmatter, and the schema are untouched, as scoped. The version bump (`3.0.0` → `3.0.1`) is applied consistently across `Directory.Build.props`, the previously-hidden `AnalysisEngine.cs:154` literal, and `PackagingSmokeTests.cs`, verified by a real pack/install/run smoke test.

**Issues found**:
1. MDCLN-11's diagnostics-ordinal-ordering sub-clause has no test with ≥2 distinct diagnostic codes — see Fix 1.
2. Edge case "`NotApplicable` symbols counted, not dropped" is untested — see Fix 2.
3. Edge case "yaml fence stays plain regardless of source backtick-run length" is untested in combination — see Fix 3.

None of these are evidence of an actual defect (the implementation reads correct by inspection for all three), but under evidence-or-zero they count as unverified, not verified. All three are cheap, additive test-only fixes with no production-code risk.

**Next steps**: Add the three tests above (each is a small addition to `MarkdownProjectorTests.cs`, no new test infrastructure needed), re-run the gate, and re-verify. This is well within the 3 fix→re-verify iteration budget.
