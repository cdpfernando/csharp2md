# STATE

## Decisions

### AD-001
- **Decision**: csharp2md runs as a three-stage pipeline — Inventory (cheap, no Roslyn) → Analysis (Roslyn, sequential per service, render and write per document, then discard) → Aggregate (indexes, graph, diagram).
- **Reason**: Stage 1 must precede detection regardless, because internal-package matching and logical-name resolution both need every service known before any detector runs. Streaming Stage 2 keeps memory bounded by the catalog plus accumulated signals rather than by codebase size, which matters because the tool's stated target is a large codebase and Roslyn has documented memory/performance regressions on large solutions.
- **Trade-off**: Gave up the materialized-model architecture, which would make renderers pure functions of a complete model and make added output formats trivial. Any future output that needs whole-codebase knowledge per file (e.g. incoming-edge sections) will need a second pass or a model-building variant.
- **Scope**: All of csharp2md — pipeline shape, component boundaries, memory strategy.
- **Date**: 2026-08-14
- **Status**: active

### AD-002
- **Decision**: Source files render as structural Markdown (a section per type/member, full bodies verbatim, XML docs as prose, light semantic facts), guarded by a span-coverage invariant: every source byte lands in exactly one emitted section. Semantic enrichment is an additive decorator, never woven into the renderer.
- **Reason**: Structural output gives an LLM real chunk boundaries and a human a navigable outline, which is what makes the deferred token study a meaningful comparison at all — whole-file passthrough would compare raw source against itself plus overhead. Keeping semantics optional means projects that fail to restore (an expected condition per spec) degrade to syntax-only output instead of producing broken cross-links.
- **Trade-off**: Structural rendering can silently drop usings, inter-member code, `#region`, and top-level statements. Accepted only because the span-coverage test converts that risk into a testable invariant; without that test this decision is not safe.
- **Scope**: MarkdownRenderer and any future output format derived from source files.
- **Date**: 2026-08-14
- **Status**: active

### AD-003
- **Decision**: Target `net10.0`, use `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0, do **not** reference `Microsoft.Build.*`, do **not** call `MSBuildLocator.RegisterDefaults()`, and read load failures from the `workspace.Diagnostics` property rather than the `WorkspaceFailed` event.
- **Reason**: Roslyn 4.9+ moved project loading into an out-of-process `BuildHost`, which removed the MSBuildLocator requirement that nearly every tutorial (and this project's own research doc) still describes. `WorkspaceFailed` is `[Obsolete]` in Roslyn 5.x and the repo builds with `TreatWarningsAsErrors=true`, so using it would fail the build; the `Diagnostics` property exposes the same data.
- **Trade-off**: Ties the tool to the BuildHost's runtime requirements — it must be able to resolve `dotnet` on the target machine, and whether `PackAsTool` packages the BuildHost payload correctly is unverified (tracked as a Phase-0 spike). The older MSBuildLocator route is better documented but carries the MEF-composition and assembly-resolution problems the BuildHost was built to eliminate.
- **Scope**: All Roslyn interaction and the packaged tool's runtime contract.
- **Date**: 2026-08-14
- **Status**: active

### AD-004
- **Decision**: Dependency detectors are interfaces with implementations compiled into the tool (`IDocumentDependencyDetector`, `IProjectDependencyDetector`), not a dynamically loaded plugin system.
- **Reason**: Dynamic plugins only pay off when third parties without source access need to extend the tool; only the author writes detectors. Keeping the interfaces clean preserves the option to move to dynamic loading later without a redesign.
- **Trade-off**: Adding a detector requires rebuilding and republishing the tool. Accepted given the single-author usage.
- **Scope**: Dependency detection across all current and future signal types.
- **Date**: 2026-08-14
- **Status**: active

### AD-005
- **Decision**: `GraphBuilder` (T19) resolves an HTTP/gRPC `DependencySignal`'s target by wrapping the already-resolved `RawTarget` (a literal address, an env-var/discovery name, or the raw unresolved logical name) directly as `DependencyEdge.Target : ServiceName` — it does **not** cross-match that value against `ServiceCatalog` to find the owning `ServiceDescriptor`.
- **Reason**: Neither spec.md nor design.md defines a name/address → catalog matching rule, and none is mechanically possible today — no data model stores a service's own network address, so a `HardCoded` URL has nothing to match against. The T2 fixture deliberately uses non-matching names (appsettings key `"PaymentService"` vs. catalog service `"Acme.Payments"`) to force this decision rather than let it be guessed. This mirrors the existing P2-15 rule for unpaired messaging signals (topic name stands in as target) — not a new pattern, just its extension to HTTP/gRPC. It is sufficient to pass P2's actual Independent Test in spec.md, which asks for four edges with correct types/classifications, not catalog-matched targets.
- **Trade-off**: `dependencies.json` and the Mermaid diagram show, for `HardCoded`/`Dynamic` edges, whatever raw value was resolved (a URL, an env-var name, a Docker service name) rather than necessarily a real manifest service name — honest but less immediately readable as a service graph. User confirmed deferring a real matching strategy to a later iteration ("depois pensamos em uma estratégia de match", 2026-08-14).
- **Scope**: `GraphBuilder` (T19) and any future correlation logic for HTTP/gRPC signal targets.
- **Date**: 2026-08-14
- **Status**: active

### AD-006
- **Decision**: The LLMWiki topic layout replaces the v1 flat output layout unconditionally — every run writes beneath `raw/` (`raw/codebase/` for source documents, `raw/topic.yaml`, `raw/CLAUDE.md`, `raw/dependencies.json`, `raw/dependencies.mmd`, `raw/log.md`), with no opt-in flag and no compatibility mode. `.csharp2md-output` stays at the output root. Frontmatter is injected inline as a decorator during the existing per-document write, and `--topic` / `--domain` are added to the CLI surface.
- **Reason**: One layout means one set of tests and no branch in `OutputWriter`; a flag would double the output contract for a tool with a single author. Inline injection is the only option consistent with AD-001's render-write-discard streaming — a post-hoc pass over every written file would re-read the whole output. Hardcoding `topic`/`domain` (as the original draft did, at `arquitetura-software/eshop`) would make the tool correct for exactly one codebase, so the CLI surface has to grow; this supersedes the Phase-1 Out-of-Scope row that froze it.
- **Trade-off**: A breaking change to the v1 output shape — existing end-to-end tests asserting root-level paths must be updated, and any consumer of the v1 layout breaks. User explicitly waived backward compatibility (2026-08-15). Keeping the marker outside `raw/` is a deliberate asymmetry, required because `OutputWriter.PrepareRun` reads it at the output root before deleting anything.
- **Scope**: All csharp2md output layout, the CLI option surface, and where frontmatter is produced.
- **Date**: 2026-08-15
- **Status**: active

### AD-007
- **Decision**: Breaking work lands on a feature branch, never on `master`. `csharp2md-llmwiki-phase1` runs on `feat/llmwiki-phase1`, cut from `master`, carrying one atomic commit per task; it merges only after the Verifier returns PASS, and it merges through a GitHub Pull Request rather than a local merge. The output-format break is signalled by semver: `<Version>2.0.0</Version>` is introduced as part of the feature and the merge commit is tagged `v2.0.0`. Pushing the branch and opening the PR require the user's explicit go-ahead each time.
- **Reason**: `master` is identical to `origin/master`, so every commit on it is already published on GitHub, and the project packs as a dotnet tool (`PackAsTool`, `PackageId=csharp2md`). The layout migration is deliberately broken mid-sequence — the task that moves output under `raw/` invalidates path assertions that later tasks restore — so intermediate states must never reach a published branch. Semver is the only signal a `dotnet tool update` consumer gets that the output contract changed; without a `Version` property the package ships as `1.0.0` forever and the break is silent.
- **Trade-off**: A single long-lived branch delays integration until the whole feature is done, so a conflict with concurrent `master` work surfaces late. Accepted: there is no concurrent work, the repository has a single author, and the alternative — incremental merges — publishes a half-migrated layout. Tagging at merge means the tag names a commit that exists only after the PR is merged, so the version bump and the tag are not in the same commit.
- **Scope**: Branch, integration, and release marking for this feature and any future breaking change to the output contract.
- **Date**: 2026-08-15
- **Status**: active

## Handoff

**Feature**: csharp2md + LLMWiki Phase 1 (`csharp2md-llmwiki-phase1`)
**Phase/Task**: Execute — **all 21 tasks complete (T1-T21), feature-level validation PASSED.** Everything is committed; nothing remains but the user's push/PR decision.
**Branch**: `feat/llmwiki-phase1`, cut from `master`, 21 task commits ahead (`5baabda`..`59d4757`) plus the prior planning commits. `master` is untouched and identical to `origin/master`. Nothing has been pushed.

**Completed this session**: all 21 tasks, executed as 4 sub-agent batches (`T1–T4`, `T5–T11`, `T12–T18`, `T19–T21`) per the prior session's plan, run sequentially in a fresh conversation.
- **Batch 1** (T1–T4): ManifestLoader null-services fix, `TopicOptions`, `TopicLayout`, and the breaking `raw/` layout migration. 322 tests passing.
- **Batch 2** (T5–T11): the frontmatter model, JSON schema sync test, `TitleResolver`, `FileTypeClassifier`, `TagDeriver`, `FrontmatterBuilder`, `FrontmatterYaml`. All five no-precedent Roslyn members (`FileScopedNamespaceDeclarationSyntax`, `BaseListSyntax`, `InterfaceDeclarationSyntax`, `.Modifiers`, extension-method detection) verified against official docs before use, per `tasks.md`'s binding Knowledge Verification rule. Caught a real YamlDotNet empty-scalar serialization defect before it shipped. 337 tests passing.
- **Batch 3** (T12–T18): frontmatter emission into `RenderedDocument`, wired through `AnalysisPipeline`, index frontmatter, exit-1-on-validation-failure, `TopicScaffoldWriter`, `RunLogWriter`, and the CLI `--topic`/`--domain` options. **This batch hit a monthly spend limit mid-T18** (T12–T17 landed as 6 commits from the sub-agent; T18 was finished directly in the orchestrating session). Finishing T18 surfaced and closed a real gap: `TopicScaffoldWriter`/`RunLogWriter` had been built standalone with no caller, and `Program.cs` was hardcoding exit code `0` regardless of `PipelineRunResult.ExitCode` — so WIKI-12's exit-1 contract was never actually exercised through the real binary. Both fixed inline, documented in `tasks.md` under T18. 411 tests passing after T18.
- **Batch 4** (T19–T21): fixture heuristic-coverage proof, the byte-identical-except-timestamp determinism test, and the `2.0.0` version bump. Found and fixed one more real pre-existing defect: `TagDeriver`'s dependency-injection rule only scanned invocation call sites, missing extension-method declarations never called within the same file. 428 tests passing.
- **Verifier** (fresh sub-agent, author ≠ verifier): **PASS.** 24/24 measurable ACs spec-anchored with `file:line` evidence; discrimination sensor 6/6 injected mutations killed (covering the span-coverage/byte-identical body invariant, the WIKI-12 exit-code distinction, title-tier order, file_type rule order, the `TimeProvider` seam, and the syntax-only guarantee); manual smoke test against the packaged CLI confirmed all `raw/` artifacts present and frontmatter matching the Fixture Expectations table. Two informational (non-blocking) gaps noted: a pre-existing spec.md self-contradiction about `Acme.Broken` between its own Independent Test and its Edge Cases table (implementation follows the consistent majority), and P1-18's log-collision recording being untested (inherited from v1, not a WIKI-NN requirement). Report: `.specs/features/csharp2md-llmwiki-phase1/validation.md`. `validate_state.py` confirms exit 0.

**Baseline**: **428 tests passing, 0 failing** (up from 303 at the start of Execute). `dotnet format --verify-no-changes` and `dotnet build -c Release` both clean.

**Next step**: The feature is done pending the user's go-ahead to push `feat/llmwiki-phase1` and open the PR to `master` (AD-007 requires explicit approval for each, not yet given). The `v2.0.0` tag applies to the merge commit once the PR lands, per AD-007 — it does not exist yet.

**Blockers**: None.

**Prior features**: `csharp2md` (v1) and `cli-directory-input` are both complete and independently verified — see their `validation.md` files.

## Historical Handoff

**Feature**: csharp2md (v1)
**Phase/Task**: Execute — **all 26 tasks complete (T1-T26), feature-level validation PASSED.** Only the T26 commit remains.
**Completed**: T1-T26. **283 tests passing, 0 failing.** 0 slopwatch findings. `dotnet build -c Release` → `dotnet format --verify-no-changes` → `dotnet test` all green. **AD-005 applied and verified in T19**; T26 exercises it directly (gRPC edge asserted `unresolved`, not catalog-matched). **Verifier (fresh sub-agent, author ≠ verifier) returned PASS**: 39/39 acceptance criteria evidence-backed (1 spec-precision gap on P2-14's resolution kind, resolved with documented reasoning, recorded as lesson L-001), discrimination sensor 6/6 injected mutations killed (including the two highest-risk invariants: span-coverage and sequential-workspace, both confirmed as real assertions, not comments). Report: `.specs/features/csharp2md/validation.md`.
**In-progress**: None. T26 finished directly in this session (not via sub-agent) after diagnosing and fixing 2 pre-existing T4-era tests whose `--manifest` contract went stale once T24 wired the real pipeline — see the `>` note under T26 in tasks.md for the full account. `dotnet-test:assertion-quality` and `dotnet-test:test-anti-patterns` both reviewed the new/changed T26 tests: no Critical/High findings; one Low (a temp-dir cleanup omission) found and fixed inline. The Verifier separately suggested tightening two P3-05 assertions (`Contains("3 project(s)")` also matches the degraded-summary branch, so "exits 0 even when degraded" rests on an unasserted precondition) — applied post-Verifier.
**Next step**: Feature is done pending one thing — **commit T26** (and this Handoff/spec.md traceability update). CLAUDE.md requires the user's explicit go-ahead first; message ready and validated: `feat(cli): wire end-to-end run with exit-code contract`.
**Blockers**: None currently. (Historical, resolved: this batch hit two consecutive external usage limits on sub-agent dispatch — a session limit, then a monthly spend limit — recorded in git history via this file's prior revisions if needed; not relevant to resuming from here.)
**Uncommitted files**: T26's files only — `src/Csharp2Md.Cli/Program.cs`, `tests/Csharp2Md.Core.Tests/Cli/EndToEndTests.cs` (new), `tests/Csharp2Md.Core.Tests/Cli/ProcessRunner.cs`, `tests/Csharp2Md.Core.Tests/Cli/CliBinary.cs` (new), `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs`, `tests/Csharp2Md.Core.Tests/Cli/PackagingSmokeTests.cs`, `fixtures/SyntheticSolution/Acme.Orders/PaymentsGrpcClient.cs` (new), `fixtures/SyntheticSolution/Acme.Orders/OrderService.cs`, `fixtures/SyntheticSolution/README.md`, `.specs/features/csharp2md/tasks.md`. All gated green, ready to commit once approved. Still untracked and correctly out of scope for any task: `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`, `CLAUDE.md`, `research/`, `2026-08-14-msbuildworkspace-robustez-dotnet-tool-packaging.md`, `src/Csharp2Md.Cli/Properties/launchSettings.json`, `AGENTS.md`.
**Branch**: `master`. **21 commits** through T25 (`28a41fc`..`9e601fc`, including 3 orchestrator docs/pause commits interleaved with the 18 task commits); T26 will be the 22nd once approved.

**Known gaps (final state)**:
- **Resolved**: T2 fixture's missing gRPC client call — closed in T26.
- **Resolved**: `DependencyEdge`/`DependencyGraph` 0%-coverage getters, P2-10 detected-dependency section, name→catalog matching decision (AD-005) — all resolved across T19-T26.
- **Still open, low priority, not feature-blocking**: SourceLink "no remote" warnings persist during self-analysis of the real `src/`/`tests/` projects (no git remote configured for this repo yet).

See tasks.md's per-task "Done when" checklists (all checked through T26) and the `>` notes under T2/T3/T5/T6/T7/T8/T9/T10-T19/T26 for the full list of documented deviations from design.md's original sketches (all deliberate, all reasoned, all cross-referenced into design.md itself).
