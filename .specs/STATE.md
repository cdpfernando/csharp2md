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
**Phase/Task**: Execute — Phase 0-3 (T1-T18) complete. Next: Phase 4 (T19-T22), start with T19.
**Completed**: T1-T18, all gates green (`dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test` — 194 tests passing, up from 55 after Phase 1; 0 slopwatch findings across every phase). Batch 2 (T10-T18) additionally ran CRAP analysis at end of Phase 3 — found and fixed 3 real coverage gaps in T11's own `MarkdownRenderer`/`XmlDocProse` code before reporting — plus assertion-quality and anti-pattern checks (clean). Highest-risk item (BuildHost packaging) closed in T4; rendering-fidelity risk (span-coverage invariant) closed in T11.
**In-progress**: None.
**Next step**: T19 (GraphBuilder) — apply **AD-005**: wrap `RawTarget` as `ServiceName` directly for HTTP/gRPC edges, no catalog cross-matching. Batch 3 = Phase 4 (T19-T22) + Phase 5 (T23-T26), 8 tasks.
**Blockers**: None.
**Uncommitted files**: none from T1-T18 — all committed this session. 11 commits total on `master`: 2 bundled by phase from a prior session (`28a41fc` Phase 0, `82a5fe6` Phase 1 — T1-T9, not split per-task) + 9 atomic commits from this session (`430a2e4`..`43e06e7`, T10-T18, one per task, matching the skill's granularity). Still untracked and correctly out of scope for any task: `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`, `CLAUDE.md`, `research/`, `2026-08-14-msbuildworkspace-robustez-dotnet-tool-packaging.md`, `src/Csharp2Md.Cli/Properties/launchSettings.json` (editor/tooling artifacts, not named in any task's "Where" field).
**Branch**: `master`.

**Known gaps flagged during Phase 0-3 (not blockers, but decisions someone will need before the tasks that touch them)**:
- T2 fixture: no gRPC *client* call exists anywhere (only Payments *exposing* a gRPC service) — spec.md's P2 Independent Test narrative expects one; T2's approved Done-when checklist didn't ask for one. T16 (GrpcClientDetector) shipped without needing a fixture client call (semantic-only detection, tested via synthetic sources); flag still open for **T26**'s e2e test, which does need the fixture to exercise a real gRPC client call end-to-end.
- Real `src/`/`tests/` projects still show SourceLink "no remote" warnings during self-analysis (now that commits exist, "no commits" should be resolved; "no remote" persists — no remote configured yet).
- design.md's "detected-dependency section" (P2-10) is not yet emitted — correctly deferred to **T23** (`DependencySectionRenderer`), not a gap in T11.
- `DependencyEdge`/`DependencyGraph` getters are the only remaining 0%-coverage members as of Batch 2 — resolves once T19 consumes them.

See tasks.md's per-task "Done when" checklists (all checked through T18) and the `>` notes under T3/T5/T6/T7/T8/T9/T10-T18 for the full list of documented deviations from design.md's original sketches (all deliberate, all reasoned, all cross-referenced into design.md itself).
