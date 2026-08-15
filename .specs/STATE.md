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

## Handoff

**Feature**: csharp2md (v1)
**Phase/Task**: Execute — Phase 0-4 complete, Phase 5: T23, T24, T25 done. **Only T26 remains** (CLI end-to-end wiring — the last task of the entire feature).
**Completed**: T1-T25, all gates green, **279 tests total (277 passing, 2 failing — see In-progress)** — the 277 passing baseline before T26's partial work stood at 273 (T24) then 279 (T25's 6 added). 0 slopwatch findings through T25. **AD-005 applied and verified in T19.**
**In-progress**: T26 is **mid-flight and currently broken** — a sub-agent got partway through before hitting the blocker below. Working tree (uncommitted, not staged) has: `src/Csharp2Md.Cli/Program.cs` modified, `tests/Csharp2Md.Core.Tests/Cli/ProcessRunner.cs` modified, `tests/Csharp2Md.Core.Tests/Cli/CliBinary.cs` new, `fixtures/SyntheticSolution/Acme.Orders/PaymentsGrpcClient.cs` new (the gRPC-client fixture extension — done), `fixtures/SyntheticSolution/Acme.Orders/OrderService.cs` and `fixtures/SyntheticSolution/README.md` modified to wire it in. **2 tests currently fail**: `CliArgumentValidationTests.Run_WithValidArguments_ExitsZeroAndReportsProjectCount` and `PackagingSmokeTests.PackedTool_RunFromOutsideRepo_LoadsFixtureSolutionAndReportsProjectCount`, both with `Manifest is not valid JSON: '<' is an invalid start of a value` — looks like the CLI is receiving something other than the manifest content mid-refactor of `Program.cs`. Do not commit this state as-is.
**Next step**: Resume T26 only. Diagnose and fix the 2 failing tests first (`Program.cs`'s current state vs. what `CliBinary.cs`/`ProcessRunner.cs` expect it to do — read both before changing either), then finish T26's remaining "Done when" items (all still unchecked in tasks.md), run the Build gate, mark tasks.md, and report back. After T26 is gated green, the orchestrator dispatches the mandatory Verifier — not the T26 worker itself.
**Blockers**: Two consecutive external usage-limit failures on sub-agent dispatch, different in kind: (1) a session limit that would have reset ~1:30am America/Sao_Paulo — waited it out; (2) immediately after, **a monthly spend limit** (`You've hit your monthly spend limit · raise it at claude.ai/settings/usage`), which does **not** reset on a timer — it requires the user to raise it manually at that URL. This is why T26 is left mid-flight rather than retried again automatically. Not a code or design blocker.
**Uncommitted files**: T26's in-progress/broken files listed above (do not commit until fixed and gated). Still untracked and correctly out of scope for any task: `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`, `CLAUDE.md`, `research/`, `2026-08-14-msbuildworkspace-robustez-dotnet-tool-packaging.md`, `src/Csharp2Md.Cli/Properties/launchSettings.json`, `AGENTS.md` (apparent verbatim mirror of `CLAUDE.md`, unexplained origin, harmless, not referenced by any task).
**Branch**: `master`. **18 commits total**: 2 bundled by phase from a prior session (T1-T9) + 16 atomic commits from this session, one per task, `430a2e4`..`cba424a` (T10-T25).

**Known gaps (updated)**:
- **Resolved**: T2 fixture's missing gRPC client call — `PaymentsGrpcClient.cs` added to `Acme.Orders` during the (interrupted) T26 attempt, wired into `OrderService.cs`. Verify it still compiles and is actually exercised once T26 resumes.
- **Resolved**: `DependencyEdge`/`DependencyGraph` 0%-coverage getters, P2-10 detected-dependency section — see prior entries, unchanged.
- **Still open, low priority**: SourceLink "no remote" warnings persist during self-analysis (no remote configured yet).

See tasks.md's per-task "Done when" checklists (all checked through T25) and the `>` notes under T3/T5/T6/T7/T8/T9/T10-T19 for the full list of documented deviations from design.md's original sketches (all deliberate, all reasoned, all cross-referenced into design.md itself).
