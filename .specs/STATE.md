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
**Phase/Task**: Execute — Phase 0-4 (T1-T22) complete, Phase 5 in progress: T23 done, **T24-T26 not started**. Next: T24 (AnalysisPipeline orchestrator).
**Completed**: T1-T23, all gates green, **259 tests passing** (up from 194 after Phase 2+3; 0 slopwatch findings across every phase). Phase 4's CRAP gate (after T22) found and fixed a real P2-15 coverage gap in `GraphBuilder`'s role-less-messaging-signal arm (100% sequence coverage, CRAP 0.00 across all four Phase-4 components) before reporting. **AD-005 applied and verified in T19** — `GraphBuilder` wraps `RawTarget` directly, no catalog cross-matching.
**In-progress**: None. T24 had not produced any files when its sub-agent died (was still reading the components it needed to wire — `SolutionLoader`, `ServiceDiscoverer`, `MarkdownRenderer`, detectors, `GraphBuilder`, writers — before hitting the failure below).
**Next step**: Resume with a sub-agent scoped to just **T24, T25, T26** (the Phase 5 tail: `AnalysisPipeline` orchestrator, `RunReporter`, CLI end-to-end wiring). Same brief as the original Batch 3 dispatch applies unchanged — AD-005 is already applied (T19 done), the no-commit override still applies, and **T26 still needs the T2 fixture extended with a gRPC client call** (see gap below) before its Independent Test can be made executable.
**Blockers**: The prior sub-agent attempt at this batch died mid-T24 with `API error: You've hit your session limit · resets 1:30am (America/Sao_Paulo)`. Not a code or design blocker — external usage limit. User chose to wait for the reset rather than retry immediately or have the orchestrator implement T24-T26 inline. Resume any time after **2026-08-14 01:30 America/Sao_Paulo**.
**Uncommitted files**: none belonging to any task — T1-T23 fully committed. Still untracked and correctly out of scope for any task: `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`, `CLAUDE.md`, `research/`, `2026-08-14-msbuildworkspace-robustez-dotnet-tool-packaging.md`, `src/Csharp2Md.Cli/Properties/launchSettings.json`, and a new **`AGENTS.md`** (appeared during the failed Batch 3 run — an apparent verbatim mirror of `CLAUDE.md`'s content, not referenced by any task or by application code, only mentioned in skill reference templates; harmless, left alone, unexplained origin worth a glance if it recurs).
**Branch**: `master`. **16 commits total**: 2 bundled by phase from a prior session (`28a41fc` Phase 0, `82a5fe6` Phase 1 — T1-T9) + 14 atomic commits from this session, one per task (`430a2e4`..`43e06e7` for T10-T18, `694b8ff` docs, `d545be1`..`7df9b66` for T19-T23).

**Known gaps (updated)**:
- **Still open**: T2 fixture has no gRPC *client* call anywhere (only `Acme.Payments` *exposing* a gRPC service). T26's own "Done when" requires making spec.md's P2 Independent Test executable, which names a gRPC call among the four dependency signals to verify — so **T24-T26's worker must extend the fixture** (add an `Acme.Orders → Acme.Payments` gRPC client call) before T26 can pass. Precedent for extending T2 from a later task already exists (T3 did it).
- **Resolved**: `DependencyEdge`/`DependencyGraph` 0%-coverage getters — consumed and covered by T19/T20/T21.
- **Resolved**: P2-10 "detected-dependency section" — implemented by T23 (`DependencySectionRenderer` + `RenderedDocument.DependencySection`).
- **Still open, low priority**: SourceLink "no remote" warnings persist during self-analysis (commits now exist, so "no commits" is resolved; no remote is configured yet).

See tasks.md's per-task "Done when" checklists (all checked through T23) and the `>` notes under T3/T5/T6/T7/T8/T9/T10-T19 for the full list of documented deviations from design.md's original sketches (all deliberate, all reasoned, all cross-referenced into design.md itself).
