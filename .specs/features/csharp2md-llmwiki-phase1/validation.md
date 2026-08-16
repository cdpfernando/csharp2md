# csharp2md-llmwiki-phase1 Validation

**Date**: 2026-08-16
**Spec**: `.specs/features/csharp2md-llmwiki-phase1/spec.md`
**Diff range**: `73e0712..HEAD` (21 commits, `5baabda`..`59d4757`)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Task Completion

| Task | Status | Notes |
| --- | --- | --- |
| T1 | ✅ Done | `ManifestLoader.cs:28` fixed; regression tests present |
| T2 | ✅ Done | `TopicOptions.cs` |
| T3 | ✅ Done | `TopicLayout.cs` |
| T4 | ✅ Done | `raw/` layout wired into `AnalysisPipeline.cs` |
| T5 | ✅ Done | `Frontmatter.cs`, `FileType` |
| T6 | ✅ Done | `schemas/frontmatter.schema.json` + `FrontmatterSchemaSyncTests.cs` |
| T7 | ✅ Done | `TitleResolver.cs`, four tiers |
| T8 | ✅ Done | `FileTypeClassifier.cs`, eleven values |
| T9 | ✅ Done | `TagDeriver.cs`, six rules |
| T10 | ✅ Done | `FrontmatterBuilder.cs`; advisory-warning deviation verified below, does not violate WIKI-13 |
| T11 | ✅ Done | `FrontmatterYaml.cs` |
| T12 | ✅ Done | `RenderedDocument.cs` frontmatter property |
| T13 | ✅ Done | Derivation wired into `AnalyzeDocumentAsync` |
| T14 | ✅ Done | `IndexWriter.cs` index frontmatter |
| T15 | ✅ Done | `PipelineRunResult.ExitCode`, `FrontmatterYaml.Field` empty-string fix |
| T16 | ✅ Done | `TopicScaffoldWriter.cs` |
| T17 | ✅ Done | `RunLogWriter.cs`, `TimeProvider` seam |
| T18 | ✅ Done | `--topic`/`--domain` in `Program.cs`; CLI `ExitCode` wiring fix; scaffold/log writer wiring |
| T19 | ✅ Done | `FixtureExpectationsTests.cs`; `TagDeriver` DI-rule fix (identifiers vs. invocations) |
| T20 | ✅ Done | `DeterminismTests.cs` |
| T21 | ✅ Done | `Directory.Build.props` → `2.0.0`; confirmed below |

All 21 tasks' Done-when checklists are marked complete in `tasks.md`, and the documented `>` deviation notes under T4, T10, T14, T15, T16/T17, T18, T19, T20 were independently re-derived from the actual diff (not taken on faith) — see the Spec-Anchored table and Gaps sections below for where each claim was verified or partially contested.

---

## Spec-Anchored Acceptance Criteria

| Requirement | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| WIKI-01 (every artifact under `raw/`) | All generated output rooted at `<outputRoot>/raw` | `src/Csharp2Md.Core/Topic/TopicLayout.cs:14-18` `RawRoot`; `tests/Csharp2Md.Core.Tests/Topic/TopicLayoutTests.cs:7-15`; real-binary proof `tests/Csharp2Md.Core.Tests/Cli/EndToEndTests.cs:37-60` | ✅ PASS |
| WIKI-02 (`raw/codebase/<service>/<path>.md`) | Source docs mirrored under `raw/codebase/` | `TopicLayout.cs:21-29` `CodebaseRoot`/`ServiceRoot`; `TopicLayoutTests.cs:17-39`; `EndToEndTests.cs:42-49` (`expectedDocs` == `generatedDocs`) | ✅ PASS |
| WIKI-03 (`dependencies.json`/`.mmd` at `raw/` root; `index.md` at root of indexed tree) | `dependencies.json`/`.mmd` at `raw/`; root `index.md` under `raw/codebase/` | `src/Csharp2Md.Core/Pipeline/AnalysisPipeline.cs:150-157` (`IndexWriter.WriteRootIndex(TopicLayout.CodebaseRoot(...))`, not `RawRoot`); `EndToEndTests.cs:51-57` | ✅ PASS |
| WIKI-04 (marker outside `raw/`) | `.csharp2md-output` stays at output root | `AnalysisPipeline.cs:130` (`PrepareRun` still receives `outputRoot`); `EndToEndTests.cs:60`; `tests/.../Pipeline/FrontmatterValidationTests.cs:92` (marker present alongside a run that exits 1) | ✅ PASS |
| WIKI-05 (frontmatter prefix, `---`-delimited, before `# `) | Block precedes heading, `---`-delimited | `src/Csharp2Md.Core/Rendering/RenderedDocument.cs:45-56`; `tests/.../Rendering/RenderedDocumentFrontmatterTests.cs:24-41` (`ToMarkdown_WithFrontmatter_PrependsDelimitedBlockBeforeTheHeading`); index docs: `src/Csharp2Md.Core/Output/IndexWriter.cs:78-84`, `tests/.../Output/IndexWriterTests.cs:54-60` | ✅ PASS |
| WIKI-06 (every required field present, none empty) | All 11 fields populated per schema | `tests/.../Topic/FrontmatterBuilderTests.cs:16-42` (`Build_ComposesResolvedTitleFileTypeTagsAndOptions`, asserts every field's actual value); `FrontmatterYaml.cs:94-108` (`Validate`'s required-field check); `tests/.../Topic/FrontmatterYamlTests.cs` round-trip tests | ✅ PASS |
| WIKI-07 (parses under YAML 1.2, escaped metacharacters) | Title with `:`, `"`, `#`, leading `-` survives round-trip | `src/Csharp2Md.Core/Topic/FrontmatterYaml.cs:29-32` (`WithQuotingNecessaryStrings`); `tests/.../Topic/FrontmatterYamlTests.cs:55-64` (`Render_TitleContainingYamlMetacharacters_RoundTripsIntact`, exact string `"-Order: \"Placed\" #Event"`) | ✅ PASS |
| WIKI-08 (body byte-identical below the block) | Stripped body == undecorated render, byte for byte | `tests/.../Rendering/RenderedDocumentFrontmatterTests.cs:48-60` (`ToMarkdown_WithFrontmatter_BodyBelowTheBlockIsByteIdenticalToTheUndecoratedRender`) — **confirmed discriminating**: killed by sensor Mutation 1 below | ✅ PASS |
| WIKI-09 (every rendered doc present, none dropped) | Document count under `raw/codebase/` == source document count | `tests/.../Pipeline/FrontmatterDerivationTests.cs:48-58` (`RunAsync_DocumentCountUnderCodebaseRoot_StillEqualsTheSourceDocumentCount`) | ✅ PASS |
| WIKI-10 (`topic.yaml`: slug/title/description) | Exact `slug`/`title`/generator description | `tests/.../Topic/TopicScaffoldWriterTests.cs:33-43` (`Write_TopicYaml_CarriesSlugTitleAndGeneratorDescription`, asserts `parsed["slug"] == "acme-shop"` etc.) | ✅ PASS |
| WIKI-11 (`CLAUDE.md` content) | Generator+version, schema fields, conventions, Phase-2 note | `TopicScaffoldWriterTests.cs:45-96` (four tests, each asserting specific substrings incl. every one of the 11 schema field names) | ✅ PASS |
| WIKI-12 (validation failure → stderr, continue, exit 1) | Exit code exactly `1`; every other artifact still written; every doc still generated | `src/Csharp2Md.Core/Pipeline/AnalysisPipeline.cs:43` (`ExitCode`); `tests/.../Pipeline/FrontmatterValidationTests.cs:36-93` (three tests: continues writing, surfaces path+error, exit 1 with every artifact present); CLI-level `tests/.../Cli/CliArgumentValidationTests.cs:391-414` — **confirmed discriminating**: killed by sensor Mutation 2 | ✅ PASS |
| WIKI-13 (syntax-only; degraded == healthy) | Identical `title`/`file_type` for a project with unresolved base types | `tests/.../Pipeline/FrontmatterDerivationTests.cs:38-46` (asserts `title == "PaymentsService"`, `file_type == "service"` against the real degraded `Acme.Payments` run); `tests/.../Topic/FrontmatterBuilderTests.cs:47-79` (`Build_UnresolvableBaseType_ProducesSameResultAsResolvableEquivalent`) — **confirmed discriminating**: killed by sensor Mutation 6 | ✅ PASS |
| WIKI-14 (`--topic` rejected before output touched, exit 1) | Exit `1`; output directory never created | `src/Csharp2Md.Cli/Program.cs:86-92`; `tests/.../Cli/CliArgumentValidationTests.cs:314-333` (`Run_WithInvalidTopic_ExitsOneBeforeTouchingOutputDirectory`, asserts `Directory.Exists(output)` is false) | ✅ PASS |
| WIKI-15 (default topic = slug of input dir) | `topic == "src"` for input dir `src` | `tests/.../Topic/TopicOptionsTests.cs:17-23`; CLI-level `CliArgumentValidationTests.cs:336-352` (`slug: src`) | ✅ PASS |
| WIKI-16 (default domain = `system-design`) | `domain == "system-design"` | `TopicOptionsTests.cs:25-31`; `CliArgumentValidationTests.cs:336-352` (`domain: system-design` in a real generated document) | ✅ PASS |
| WIKI-17 (summary: doc count, failure count, output path) | All three values present and correct | `Program.cs:159-164`; `tests/.../Cli/CliArgumentValidationTests.cs:360-381` (`Run_OnSuccess_ReportsDocumentCountFailureCountAndOutputTopicPath`, exact strings) | ✅ PASS |
| WIKI-18 (log timestamp + invocation) | ISO 8601 UTC timestamp, reconstructed invocation | `src/Csharp2Md.Core/Topic/RunLogWriter.cs:24,51`; `tests/.../Topic/RunLogWriterTests.cs:28-36` (exact `2026-08-15T20:30:45Z`) — **confirmed discriminating**: killed by sensor Mutation 5 | ✅ PASS |
| WIKI-19 (5 statistics) | documents, edges, services, failures, output path | `RunLogWriterTests.cs:51-64` (`Write_ReportsAllFiveStatistics`, exact values) | ✅ PASS |
| WIKI-20 (empty Phase-2 placeholder sections) | `## Graph Resolution` and `## Calibration Notes`, empty | `RunLogWriterTests.cs:66-78` (asserts exact adjacency, no stray content) | ✅ PASS |
| WIKI-21 (failures listed with path+error) | Each failing path/error line present | `RunLogWriterTests.cs:80-100` | ✅ PASS |
| WIKI-22 (log written even on exit 1) | `log.md` exists after a failing run | `RunLogWriterTests.cs:104-113`; CLI-level `CliArgumentValidationTests.cs:391-414` (`Run_WithFrontmatterValidationFailure_ExitsOneAndStillWritesLog`) | ✅ PASS |
| WIKI-23 (fixture exercises every heuristic rule) | 11/11 `file_type`, 6/6 tags, 4/4 title tiers, committed expectations table | `tests/.../Topic/FixtureExpectationsTests.cs:31-46` (expectations, verbatim from spec.md), `:82-111` (union-coverage + tier-coverage tests) | ✅ PASS |
| Determinism (Success Criteria) | Two runs byte-identical except the `log.md` timestamp line | `tests/.../Pipeline/DeterminismTests.cs:28-135` (full file-set comparison, line-by-line log diff) — **confirmed discriminating**: killed by sensor Mutation 5 (same seam) | ✅ PASS |
| P1 Independent Test #9 (`Acme.Broken` produces frontmatter) | Literal spec.md text: "`Acme.Broken` ... still produces a schema-valid frontmatter block" | No test asserts this; searched `tests/` and `fixtures/` for `Acme.Broken` — every reference (`SolutionLoaderTests.cs:48`, `DirectReferenceDetectorTests.cs:188`, `PackagingSmokeTests.cs:50`) treats it as unrestorable/contributing no documents | ⚠️ **Spec self-contradiction, not a code gap** — see Gaps §1 |

**Status**: ✅ All 24 measurable ACs covered and matched to spec-defined outcomes. One spec-text self-contradiction (P1 Independent Test #9) flagged — resolved correctly by the implementation in favor of the internally-consistent majority (Edge Cases, Assumptions, Fixture Expectations, design.md's Error Handling table), not the outlying sentence.

---

## AD-005/AD-006/AD-007 Compliance Check

- **AD-005 (no catalog cross-matching)**: not touched by this feature's diff — `GraphBuilder` is unmodified (confirmed via `git diff --stat`, no `Graph/` files listed). No regression introduced.
- **AD-006 (unconditional `raw/` layout, no compatibility mode)**: confirmed — `TopicLayout` has no flag/branch; `OutputWriter`/`IndexWriter` always receive `raw/`-rooted paths from `AnalysisPipeline.cs:150-157,208`. No `--legacy-layout` or equivalent exists in `Program.cs`.
- **AD-007 (branch discipline, semver)**: confirmed — `git branch --show-current` → `feat/llmwiki-phase1`; `git log master --oneline -3` shows master unchanged (`93ef5b4`, pre-dates this feature); `Directory.Build.props:10` → `<Version>2.0.0</Version>`; nothing pushed or tagged (out of scope for this Verifier run).

---

## Payload/Conjunction Rule Check

Spot-checked the highest-risk frontmatter fields for value assertions (not mere presence/construction):

- `title`/`file_type`/`tags`: `FixtureExpectationsTests.cs:66-79` asserts exact string equality against a committed table for 13 real documents, not "is non-null".
- `topic`/`domain`: `CliArgumentValidationTests.cs:348,352` assert exact substrings (`slug: src`, `domain: system-design`) in real generated files.
- `source_service`/`analysis_status`: `tests/.../Topic/FrontmatterTests.cs` asserts the Phase-1 constants (`null`/`"pending"`) directly on the record, not merely that the properties exist.
- `source_path`: `FrontmatterBuilderTests.cs:36` asserts the exact forward-slash string.

No field in scope was found asserted only via object-construction or truthiness.

---

## Discrimination Sensor

**Isolation**: `git worktree add ../csharp2md-verify-scratch HEAD` (never `git stash`). Baseline `git status --porcelain` captured before any sensor work; matches exactly after cleanup (see Isolation Verified below).

| # | File:line | Description | Targeted test | Killed? |
| --- | --- | --- | --- | --- |
| 1 | `src/Csharp2Md.Core/Rendering/RenderedDocument.cs:53` | Extra blank line injected before the heading when frontmatter present, breaking the byte-identical-body invariant (T12/WIKI-08) | `RenderedDocumentFrontmatterTests.ToMarkdown_WithFrontmatter_BodyBelowTheBlockIsByteIdenticalToTheUndecoratedRender` | ✅ Killed |
| 2 | `src/Csharp2Md.Core/Pipeline/AnalysisPipeline.cs:43` | `ExitCode`'s `\|\|` flipped to `&&`, conflating manifest-failure and frontmatter-failure exit conditions (WIKI-12) | `FrontmatterValidationTests.RunAsync_FrontmatterValidationFailure_ExitCodeIsOneWithEveryOtherArtifactPresent` | ✅ Killed |
| 3 | `src/Csharp2Md.Core/Topic/TitleResolver.cs:41-49` | Tier 2 (root-namespace match) and tier 3 (first-in-source-order) swapped in evaluation order (T7) | `TitleResolverTests.Resolve_ForeignNamespaceTypeDeclaredFirst_ReturnsProjectNamespaceTypeAsTierTwo` | ✅ Killed |
| 4 | `src/Csharp2Md.Core/Topic/FileTypeClassifier.cs:55-84` | `filter` rule (table position 9) moved ahead of `controller` (position 5), breaking first-rule-wins (T8) | `FileTypeClassifierTests.Classify_TypeMatchingTwoRules_ReturnsFirstInTableOrderWithWarningNamingBoth` | ✅ Killed |
| 5 | `src/Csharp2Md.Core/Topic/RunLogWriter.cs:41` | Reverted `timeProvider.GetUtcNow()` to `DateTimeOffset.UtcNow` (T17/T20) | `RunLogWriterTests.Write_TimestampComesFromTheInjectedTimeProvider_AndFormatsAsIso8601Utc` + `Write_TimestampChangesWithTheProvidedClock_NeverWithWallClockTime` | ✅ Killed (2 tests) |
| 6 | `src/Csharp2Md.Core/Pipeline/AnalysisPipeline.cs:310-318` | Frontmatter derivation downgraded to `FileType.Class` whenever `semanticModel.GetDiagnostics()` reports an error — makes classification vary with semantic-model state (WIKI-13) | `FrontmatterDerivationTests.RunAsync_DegradedProjectDocument_DerivesTheSameClassificationAHealthyProjectWould` | ✅ Killed |

Note on Mutation 6: the first attempt (gating on `semanticModel is null`) **survived**, because this codebase's "degraded" state (`PossibleMissingRestore`) still produces a non-null semantic model — Roslyn resolves the model but reports unresolved-symbol diagnostics on it (`document.SupportsSemanticModel` stays true for an unrestored project). That is itself informative: it confirms `FrontmatterBuilder`'s syntax-only contract is safe against the *literal absence* of a semantic model, but the real risk was always a component silently *reading* an available-but-degraded model. The revised mutation (gating on `GetDiagnostics().Any(Error)`) targets that real risk and was killed. The original (null-check) mutation is not counted as a 7th trial — it is recorded here for transparency since the sensor process requires showing failed attempts, not just successes.

**Sensor depth**: lightweight, expanded to 6 mutations per the task brief (above the 1-3 default tier, matching the "breaking output-format change" risk level).
**Result**: 6/6 killed — PASS ✅

**Full mutated-tree test run** (all 6 mutations applied simultaneously, for corroboration): 412 passed, 16 failed, 428 total — failures cluster exactly in the mutated areas (`RenderedDocumentFrontmatterTests`, `FrontmatterValidationTests`, `TitleResolverTests`, `FileTypeClassifierTests`, `RunLogWriterTests`, `FrontmatterDerivationTests`, plus cascading CLI/E2E tests that exercise the same code paths — e.g. `CliArgumentValidationTests.Run_WithValidManifestAndExplicitOutput_ExitsZeroAndReportsProjectCount` picked up Mutation 6's `class`-downgrade collateral damage on other fixture files). This is expected collateral from stacking 6 independent faults in one tree and does not change the per-mutation verdicts above, each of which was confirmed in isolation.

**Isolation verified**: `git worktree remove --force ../csharp2md-verify-scratch` executed; `git worktree list` shows only the real tree; `git status --porcelain` on the real tree after cleanup is byte-identical to the pre-sensor baseline (`README.md` modified; `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`, two loose docs, `AGENTS.md`, `CLAUDE.md`, `README.pt-BR.md`, `research/`, `src/Csharp2Md.Cli/Properties/` untracked — all pre-existing and out of scope).

---

## Manual Smoke Test (spec.md P1 Independent Test)

Ran the packaged CLI via `dotnet run --project src/Csharp2Md.Cli -c Release` against `fixtures/SyntheticSolution` (manifest listing `Acme.Orders`, `Acme.Payments`, `Acme.Shared.Contracts` — `Acme.Broken` excluded from the manifest as it always is in existing tests, since it cannot compile) with `--topic acme-shop`:

- Exit code `0`; console: `Wrote 13 document(s) and 11 dependency edge(s)`, `Frontmatter validation failures: 0`
- Warnings correctly named the three fixture files expected to warn: `PaymentsGrpcClient.cs` (no-rule-matched advisory), `Properties/AssemblyInfo.cs` (tier-4 fallback), `Events.cs` (no-rule-matched advisory) — matching the Fixture Expectations table and T10's documented advisory-warning behavior
- `raw/topic.yaml`, `raw/CLAUDE.md`, `raw/dependencies.json`, `raw/dependencies.mmd`, `raw/log.md` all present; `raw/codebase/` mirrors `Acme.Orders/`, `Acme.Payments/`, `Acme.Shared.Contracts/` with one `.md` per `.cs` plus `index.md` at each level
- Spot-checked `raw/codebase/Acme.Orders/OrderService.cs.md`'s frontmatter: `title: OrderService`, `file_type: service`, `tags: [async-patterns, event-driven]`, `topic: acme-shop`, `domain: system-design` — exact match to the Fixture Expectations table
- `.csharp2md-output` present at the output root, not inside `raw/`
- Temp output directory deleted after inspection (`rm -rf` on the scratchpad path only); confirmed `git status --porcelain` on the real repo tree unaffected

---

## Code Quality

| Principle | Status |
| --- | --- |
| Minimum code | ✅ — each task's diff maps 1:1 to its stated scope; the four inline pre-existing-defect fixes (T1, T15, T18, T19) are each independently justified and narrowly scoped |
| Surgical changes | ✅ |
| No scope creep | ✅ — `dotnet-nuget:convert-to-cpm`/plugin architecture/service-graph resolution correctly left out of scope per spec.md's Out of Scope table |
| Matches patterns | ✅ — `TopicOptions`/`TopicOptionsResult` follow `ManifestLoader`'s "expected error, not exception" convention; `FrontmatterBuilder`'s internal-static-class shape mirrors existing detectors |
| Spec-anchored outcome check (asserted values match spec) | ✅ — see table above |
| Per-layer Coverage Expectation met | ✅ — Topic/ has 1:1 AC-to-unit-test mapping; Pipeline/CLI integration tests cover happy + edge (degraded load) + error (validation failure, invalid topic) paths |
| Every test maps to a spec requirement | ✅ — every new test file's docstring cites a WIKI-NN or T-number |
| Documented guidelines followed | ✅ — `CLAUDE.md`'s AD-003 (no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults()`) respected: no such reference introduced in the diff (confirmed via `git diff --stat`, no changes to project references) |

---

## Edge Cases

- [x] Frontmatter validation failure → report, continue, exit 1 (WIKI-12) — `FrontmatterValidationTests.cs`
- [x] No top-level type → tier-4 fallback + warning — `TitleResolverTests.cs:82-98`
- [x] Multiple top-level types across namespaces → tier-2/tier-3 resolution — `TitleResolverTests.cs:31-79`
- [x] Records → `file_type: class` — `FixtureExpectationsTests.cs:43` (`Events.cs` → `class`)
- [x] Two+ `file_type` rules match → first wins + warning — `FileTypeClassifierTests.cs:173-185`
- [x] Non-empty output dir without marker → requires `--force` — inherited from `cli-directory-input`, unmodified by this feature (not re-verified here, out of diff scope)
- [x] Degraded-but-loading project → same frontmatter as healthy — `FrontmatterDerivationTests.cs:38-46`, `FrontmatterBuilderTests.cs:47-79`
- [x] Project cannot compile at all → no documents, no frontmatter — consistent with `AnalysisPipeline.cs:238` (`!project.SupportsCompilation` → skip), `FixtureExpectationsTests.cs:108` comment; see Gap §1 for the one spec-text inconsistency this surfaces
- [ ] Two services sharing a name → recorded in `raw/log.md` — not exercised by any test in this feature's diff (inherited P1-18 behavior; `RunLogWriter` has no code path that would record such a collision — see Gap §2)
- [x] YAML metacharacters in title → quoted/escaped — `FrontmatterYamlTests.cs:55-64`

---

## Gate Check

- **Gate command**: `dotnet build -c Release` → `dotnet format --verify-no-changes` → `dotnet test`
- **Result**: 428 passed, 0 failed, 0 skipped
- **Test count before feature**: 303 (per `tasks.md`'s recorded baseline)
- **Test count after feature**: 428
- **Delta**: +125 new tests, 0 removed
- **Skipped tests**: none
- **Failures**: none
- **Build**: `Compilação com êxito. 0 Aviso(s). 0 Erro(s).`
- **Format**: `dotnet format --verify-no-changes` exits 0

---

## Gaps (informational — do not block PASS; both are spec/scope-precision items, not implementation defects)

1. **spec.md self-contradiction: P1 Independent Test #9 vs. Edge Cases / Fixture Expectations / design.md** — Independent Test #9 reads *"Confirm `Acme.Broken` — which does not compile — still produces a schema-valid frontmatter block with `file_type: class` and a warning naming the file."* This directly contradicts spec.md's own Edge Cases section (*"IF a project cannot compile at all THEN it contributes no documents and therefore no frontmatter ... `Acme.Broken` is this case"*), its Fixture Expectations table note (*"`Acme.Broken/Broken.cs` cannot [supply tier 4], because a project that fails to compile contributes no documents"*), and design.md's Error Handling Strategy table (*"Project cannot compile at all → documents dropped, nothing else"*). The implementation correctly follows the majority, internally-consistent position — `AnalysisPipeline.cs:238` skips all documents of a project that doesn't support compilation, and no test anywhere in the diff (or the pre-existing suite) claims `Acme.Broken` produces a document. This is a spec-authoring defect discovered during verification, not a code gap; recommend correcting spec.md's Independent Test #9 line in a follow-up doc-only commit (out of this Verifier's read-only scope to fix).
2. **Two-services-sharing-a-name collision recording** — the Edge Cases table inherits P1-18's rule that a name collision "is recorded in `raw/log.md`", but no code in this feature's diff writes any such record (`RunLogWriter`'s `RunLogData` carries no collision list) and no test exercises it. This predates this feature (inherited from the v1 spec) and was not listed as one of WIKI-01..23, so it is out of this feature's requirement scope — flagged for awareness only, not a WIKI-NN failure.

Neither gap involves a `file:line` with incorrect behavior; both are documentation/scope-boundary observations. No fix tasks are being generated for either.

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| WIKI-01 .. WIKI-23 | Implementing | ✅ Verified |
| Determinism (Success Criteria) | Implementing | ✅ Verified |

---

## Summary

**Overall**: ✅ Ready

**Spec-anchored check**: 24/24 measurable ACs matched spec-defined outcomes (23 WIKI requirements + determinism); 1 spec-text self-contradiction flagged (P1 Independent Test #9), resolved correctly by the implementation, not a code defect.
**Sensor**: 6/6 mutations killed (one mutation required a revision after its first form legitimately survived — documented above, not hidden).
**Gate**: 428 passed, 0 failed, 0 skipped; build and format clean.

**What works**: The full `raw/` layout, frontmatter derivation/emission/validation pipeline, `--topic`/`--domain` CLI surface, topic scaffold, and auditable run log all work end-to-end — verified via unit tests, integration tests, a real packaged-binary smoke test against the fixture, and an adversarial mutation sensor targeting the six highest-risk invariants named in the task brief (byte-identical body, WIKI-12 exit-code distinction, title tier order, file_type rule order, TimeProvider seam, syntax-only guarantee).

**Issues found**: None that block PASS. Two informational gaps recorded above (spec self-contradiction; out-of-scope collision recording) for awareness, not remediation.

**Next steps**: None required to mark this feature done. Optional follow-up: correct spec.md's Independent Test #9 wording in a doc-only commit.
