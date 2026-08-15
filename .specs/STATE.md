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

## Handoff

**Feature**: csharp2md (v1)
**Phase/Task**: Execute — Phase 0 (T1-T4) and Phase 1 (T5-T9) complete. Next: Phase 2 (T10-T13), start with T10.
**Completed**: T1-T9, all gates green (`dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test` — 55 tests passing, 0 slopwatch findings). Highest-risk item (BuildHost packaging) closed in T4.
**In-progress**: None — T9 finished cleanly, no partial work.
**Next step**: T10 (domain model) — but `ServiceName`, `PackageId`, and `ResolutionKind` were already pulled forward into T6/T9 (forward-dependency fixes; see notes on those tasks in tasks.md). T10 should only add the remaining types: `DependencyKind`, `CommunicationType`, `MessagingRole`, `SourceLocation`, `DependencySignal`, `DependencyEdge` — skip redefining the three that already exist.
**Blockers**: None.
**Uncommitted files**: Everything — **no commit has been made in this repo at all** (0 commits, fresh `git init` state). CLAUDE.md requires asking the user before every commit; none has been requested yet for T1-T9.
**Branch**: `master` (per gitStatus at session start).

**Known gaps flagged during Phase 0+1 (not blockers, but decisions someone will need before the tasks that touch them)**:
- T9: `ServiceNameResolver` classifies (`HardCoded`/`Dynamic`/`Unresolved`) but does not resolve a logical name to a specific `ServiceDescriptor` — design.md's sketch implied it should, but neither spec.md nor design.md defines how a resolved name/address maps back to a catalog entry (my own T2 fixture uses non-matching names on purpose: appsettings key `"PaymentService"` vs. catalog service `"Acme.Payments"`). T15/T16 (HttpClientDetector/GrpcClientDetector) will need this decision.
- T2 fixture: no gRPC *client* call exists anywhere (only Payments *exposing* a gRPC service) — spec.md's P2 Independent Test narrative expects one; T2's actual approved Done-when checklist didn't ask for one. Flag if T16/T26 need it.
- Real `src/`/`tests/` projects (not just the fixture) still show SourceLink "no remote"/"no commits" warnings as `Kind == Failure` when self-analyzed by `SolutionLoader` — resolves once the repo has its first commit + a remote (fixture already patched around this via its own `Directory.Build.props`; the main projects weren't, since patching them would mean shipping with SourceLink permanently disabled).

See tasks.md's per-task "Done when" checklists (all checked through T9) and the `>` notes under T3/T5/T6/T7/T8/T9 for the full list of documented deviations from design.md's original sketches (all deliberate, all reasoned, all cross-referenced into design.md itself).
