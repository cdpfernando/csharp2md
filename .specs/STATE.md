# STATE

## Decisions

### AD-001
- **Decision**: csharp2md runs as a three-stage pipeline — Inventory (cheap, no Roslyn) → Analysis (Roslyn, sequential per service, render and write per document, then discard) → Aggregate (indexes, graph, diagram).
- **Reason**: Stage 1 must precede detection regardless, because internal-package matching and logical-name resolution both need every service known before any detector runs. Streaming Stage 2 keeps memory bounded by the catalog plus accumulated signals rather than by codebase size, which matters because the tool's stated target is a large codebase and Roslyn has documented memory/performance regressions on large solutions.
- **Trade-off**: Gave up the materialized-model architecture, which would make renderers pure functions of a complete model and make added output formats trivial. Any future output that needs whole-codebase knowledge per file (e.g. incoming-edge sections) will need a second pass or a model-building variant.
- **Scope**: All of csharp2md — pipeline shape, component boundaries, memory strategy.
- **Date**: 2026-08-14
- **Status**: superseded by AD-008

### AD-002
- **Decision**: Source files render as structural Markdown (a section per type/member, full bodies verbatim, XML docs as prose, light semantic facts), guarded by a span-coverage invariant: every source byte lands in exactly one emitted section. Semantic enrichment is an additive decorator, never woven into the renderer.
- **Reason**: Structural output gives an LLM real chunk boundaries and a human a navigable outline, which is what makes the deferred token study a meaningful comparison at all — whole-file passthrough would compare raw source against itself plus overhead. Keeping semantics optional means projects that fail to restore (an expected condition per spec) degrade to syntax-only output instead of producing broken cross-links.
- **Trade-off**: Structural rendering can silently drop usings, inter-member code, `#region`, and top-level statements. Accepted only because the span-coverage test converts that risk into a testable invariant; without that test this decision is not safe.
- **Scope**: MarkdownRenderer and any future output format derived from source files.
- **Date**: 2026-08-14
- **Status**: superseded by AD-009

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
- **Status**: superseded by AD-011

### AD-006
- **Decision**: The LLMWiki topic layout replaces the v1 flat output layout unconditionally — every run writes beneath `raw/` (`raw/codebase/` for source documents, `raw/topic.yaml`, `raw/CLAUDE.md`, `raw/dependencies.json`, `raw/dependencies.mmd`, `raw/log.md`), with no opt-in flag and no compatibility mode. `.csharp2md-output` stays at the output root. Frontmatter is injected inline as a decorator during the existing per-document write, and `--topic` / `--domain` are added to the CLI surface.
- **Reason**: One layout means one set of tests and no branch in `OutputWriter`; a flag would double the output contract for a tool with a single author. Inline injection is the only option consistent with AD-001's render-write-discard streaming — a post-hoc pass over every written file would re-read the whole output. Hardcoding `topic`/`domain` (as the original draft did, at `arquitetura-software/eshop`) would make the tool correct for exactly one codebase, so the CLI surface has to grow; this supersedes the Phase-1 Out-of-Scope row that froze it.
- **Trade-off**: A breaking change to the v1 output shape — existing end-to-end tests asserting root-level paths must be updated, and any consumer of the v1 layout breaks. User explicitly waived backward compatibility (2026-08-15). Keeping the marker outside `raw/` is a deliberate asymmetry, required because `OutputWriter.PrepareRun` reads it at the output root before deleting anything.
- **Scope**: All csharp2md output layout, the CLI option surface, and where frontmatter is produced.
- **Date**: 2026-08-15
- **Status**: superseded by AD-010

### AD-007
- **Decision**: Breaking work lands on a feature branch, never on `master`. `csharp2md-llmwiki-phase1` runs on `feat/llmwiki-phase1`, cut from `master`, carrying one atomic commit per task; it merges only after the Verifier returns PASS, and it merges through a GitHub Pull Request rather than a local merge. The output-format break is signalled by semver: `<Version>2.0.0</Version>` is introduced as part of the feature and the merge commit is tagged `v2.0.0`. Pushing the branch and opening the PR require the user's explicit go-ahead each time.
- **Reason**: `master` is identical to `origin/master`, so every commit on it is already published on GitHub, and the project packs as a dotnet tool (`PackAsTool`, `PackageId=csharp2md`). The layout migration is deliberately broken mid-sequence — the task that moves output under `raw/` invalidates path assertions that later tasks restore — so intermediate states must never reach a published branch. Semver is the only signal a `dotnet tool update` consumer gets that the output contract changed; without a `Version` property the package ships as `1.0.0` forever and the break is silent.
- **Trade-off**: A single long-lived branch delays integration until the whole feature is done, so a conflict with concurrent `master` work surfaces late. Accepted: there is no concurrent work, the repository has a single author, and the alternative — incremental merges — publishes a half-migrated layout. Tagging at merge means the tag names a commit that exists only after the PR is merged, so the version bump and the tag are not in the same commit.
- **Scope**: Branch, integration, and release marking for this feature and any future breaking change to the output contract.
- **Date**: 2026-08-15
- **Status**: active

### AD-008
- **Decision**: csharp2md retains the Inventory → Analysis → Aggregate stages but replaces render-write-discard with a validated factual-fragment lifecycle: inventory completes inertly, each project/document fragment is extracted, enriched when authorized, validated, persisted, projected to Markdown, and discarded; aggregation retains only catalogs, compact summaries, diagnostics, coverage, and persisted references. This supersedes AD-001.
- **Reason**: v3 needs one factual authority for JSON, Markdown, relations, coverage, and diagnostics without materializing a complete solution model. A per-fragment materialization preserves the existing sequential memory bound while allowing pure projectors and cross-artifact validation.
- **Trade-off**: Aggregates that need whole-codebase knowledge must stream persisted fragments or retain purpose-built summaries. The pipeline performs more small writes and needs an explicit manifest/index to navigate fragments.
- **Scope**: Pipeline lifecycle, memory strategy, fact persistence, and aggregate inputs.
- **Date**: 2026-08-17
- **Status**: active

### AD-009
- **Decision**: Validated document facts are the sole input to Markdown projection. Syntax extraction retains AD-002's exact span partition and source-byte fidelity, but semantic and detector information become facts before rendering instead of direct Markdown decorators. This supersedes AD-002.
- **Reason**: Direct enrichment lets presentation and machine data disagree. Persisting and validating one fragment before projecting it makes facts authoritative while retaining the source-fidelity invariant that made structural Markdown safe.
- **Trade-off**: The syntax extractor must store enough section/source information for projection, and renderer tests migrate from syntax-tree inputs to factual-fragment inputs.
- **Scope**: Syntax extraction, Markdown/frontmatter rendering, semantic annotations, and source fidelity.
- **Date**: 2026-08-17
- **Status**: active

### AD-010
- **Decision**: v3 keeps the unconditional `raw/` topic root and the ownership marker outside it, but replaces `raw/dependencies.json` with schema-version-2 factual fragments under `raw/facts/` and partitioned relation files. Frontmatter becomes a compact fact reference; topic, domain, and generator version exist only in topic metadata or the factual manifest. This supersedes AD-006.
- **Reason**: The v2 dependency graph and per-document topic metadata duplicate or overstate information. Partitioned validated facts support navigation, honest resolution, and deterministic downstream processing.
- **Trade-off**: The output is deliberately incompatible with v2 and requires consumers to start from `raw/facts/manifest.json`. No compatibility adapter or dual-write mode is provided.
- **Scope**: Output layout, factual/frontmatter schema, dependency artifacts, and downstream navigation.
- **Date**: 2026-08-17
- **Status**: active

### AD-011
- **Decision**: A runtime relation without a proven target keeps `target: null`, carries `unresolved_reason`, evidence, detector provenance, and `Unresolved` or `Partial` resolution. Logical names, URLs, environment keys, and discovery names are never promoted to `ServiceName` without matching evidence. Project and package references are compile-time relations only. This supersedes AD-005.
- **Reason**: Wrapping a raw logical value as a service identity creates a graph node that the analysis did not prove. v3 distinguishes observed text from resolved identity and validates the distinction structurally.
- **Trade-off**: Mermaid and relation outputs contain explicit unresolved endpoints until a future evidence-backed matcher exists, so diagrams can be less immediately connected but remain truthful.
- **Scope**: HTTP, gRPC, messaging, direct-reference relations, component graphs, and validation.
- **Date**: 2026-08-17
- **Status**: active

### AD-012
- **Decision**: `syntax-only` with untrusted input is the default and creates no executable analysis adapter. Semantic analysis requires `--trust trusted-solution`; source generators require a second explicit opt-in; analyzers never run. Trust and option validation occurs before output preparation or process creation.
- **Reason**: MSBuild evaluation can execute property functions and Roslyn extensions are executable assemblies. Without hard CPU/memory isolation, only inert source/project parsing is appropriate for arbitrary code.
- **Trade-off**: Zero-configuration runs lose semantic precision. Trusted runs expose the user to evaluated project logic and record `isolation: none`; callers must make that choice explicitly.
- **Scope**: CLI modes, analysis composition, MSBuild/Roslyn adapters, output preparation, and security tests.
- **Date**: 2026-08-17
- **Status**: active

### AD-013
- **Decision**: v3 is a modular replacement inside `Csharp2Md.Core`. `AnalysisEngine.AnalyzeAsync(AnalysisRequest, CancellationToken)` is the analysis module's only external interface; Roslyn, process, detector, storage, and projection seams are internal. Migration occurs by cuts that remove each superseded path, with no permanent v2/v3 branch and no new assembly until a second external caller justifies it.
- **Reason**: The current `AnalysisPipeline` already mixes inventory, loading, detection, rendering, validation, writing, and aggregation. Adding facts in place would spread mode checks and expose internal coordination. A deep module gives callers one operation while keeping change and tests local.
- **Trade-off**: Internal implementation remains substantial inside one project, and namespace/folder dependency discipline is not compiler-enforced across assemblies. This is accepted to avoid project/package ceremony before it provides leverage.
- **Scope**: Analysis module interface, internal seams, migration strategy, project layout, and primary test surface.
- **Date**: 2026-08-17
- **Status**: active

### AD-014
- **Decision**: v3 factual IDs use the approved `id1:<type>;key=value;...` grammar with ordered percent-encoded components, ordinal case-sensitive comparison, and no Unicode or case normalization. Resolved symbols prefer documentation-comment IDs and otherwise use a location-free canonical semantic signature; syntactic symbols use a location-free normalized declaration signature. Artifact paths are SHA-256 references of canonical Fact IDs, and a distinct-ID collision is a structural failure rather than an order-dependent suffix.
- **Reason**: Stable facts must survive absolute-root relocation and unrelated preceding edits while remaining auditable, unambiguous, portable to restrictive filesystems, and compatible with sequential fragment persistence.
- **Trade-off**: IDs are more verbose, the fallback signature and repeated-occurrence ordinal need deliberate tests, and a cryptographic reference collision fails the run instead of attempting an unstable recovery. Consumers must navigate persisted data through the manifest's artifact reference rather than deriving a filename from a readable Fact ID.
- **Scope**: All factual value types, validation, storage references, manifest navigation, semantic enrichment, detector facts, and v3 output determinism.
- **Date**: 2026-08-17
- **Status**: active

## Handoff

- **Feature**: Markdown Cleanup v3.0.1 (`.specs/features/markdown-cleanup/`) — **done.** Replaced the per-symbol/per-diagnostic `## Factual annotations` body listing with one compact `## Analysis` YAML block per document.
- **Phase / Task**: Execute — **all P1 tasks complete, feature-level validation PASSED** (round 2, after one fix→re-verify iteration). Nothing remains but the user's push/PR decision.
- **Completed this session (2026-08-19)**:
  - Spec: `.specs/features/markdown-cleanup/spec.md` (`validate_spec.py` clean, 11 EARS ACs, MDCLN-01..11).
  - Commit `9c59cf1` — `MarkdownProjector.cs`'s `AppendAnnotations` replaced with `AppendAnalysis`: heading renamed `## Factual annotations` → `## Analysis`, one fenced ` ```yaml ` block with `resolution:` (reused verbatim from `document.Header.Resolution`), `symbols:`/`relations:` maps (present `FactResolution` kinds only, fixed enum-declaration order, `{}` when empty), `diagnostics:` map (grouped by `Code` only, severity dropped, `{}` when empty). Whole-section omission guard extended to also check relations. Both approved Markdown snapshots re-approved. Frontmatter, schema, `facts.json` deliberately untouched.
  - Commit `e238572` — package version `3.0.0` → `3.0.1`: `Directory.Build.props`, a previously-hidden hardcoded `ToolVersion` literal at `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs:154` (not derived from `Directory.Build.props` — found only because the packaging smoke test's real end-to-end manifest assertion failed), and matching literals in `PackagingSmokeTests.cs`.
  - Commit `4a73b60` — mid-feature handoff (superseded by this entry).
  - **Round-1 Verifier (fresh sub-agent) → FAIL.** 10/11 ACs matched spec outcome; MDCLN-11's diagnostics-ordinal-ordering sub-clause had zero evidence (only fixture used one repeated diagnostic code), plus 2 untested spec-named edge cases (`FactResolution.NotApplicable` symbol counting, YAML-fence independence from source backtick-run length). All 3 rated "correct by inspection, not a suspected defect" — pure test-coverage gaps. Sensor 3/3 killed on the covered behaviors. Report: `.specs/features/markdown-cleanup/validation.md` (round 1, now superseded).
  - Commit `d7cd2fd` — fix task, test-only: added 3 targeted tests closing exactly those 3 gaps (`Project_AnalysisBlock_DiagnosticsAreOrderedByCodeOrdinalNotEncounterOrder`, `Project_AnalysisBlock_NotApplicableSymbolIsCountedNotDropped`, `Project_AnalysisBlock_FenceStaysPlainRegardlessOfSourceBacktickRun`); zero production-code changes. Recorded lessons L-004/L-005/L-006 in `.specs/LESSONS.md`.
  - **Round-2 Verifier (fresh sub-agent) → PASS.** 11/11 ACs, 0 spec-precision gaps, 3 new sensor mutations (distinct from round 1's) targeting exactly the 3 closed behaviors, 3/3 killed. Re-derived all 11 ACs independently rather than trusting round 1. Report: `.specs/features/markdown-cleanup/validation.md` (current). `validate_state.py markdown-cleanup` → exit 0.
  - Commit `7d76030` — records the PASS in `validation.md` and `spec.md` traceability.
  - **One disclosed, out-of-scope finding (not a `markdown-cleanup` defect, not blocking):** `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` failed intermittently (2 of 3 full-suite runs) during the round-2 gate — a pre-existing temp-file-cleanup race, file untouched by this feature, passes in isolation every time. Worth a separate ticket; not acted on here (out of this feature's scope per the Scope Guardrail).
  - Full suite: 1167 tests, clean on isolated/final runs; `dotnet build -c Release` and `dotnet format --verify-no-changes` both clean throughout.
- **In-progress** (file:line): none. Feature is done.
- **Next step**: None for this feature. Push `feat/markdown-cleanup` and open the PR (presumably to `feat/csharp2md-v3`, since that's what it was cut from and that branch is itself still unmerged to `master`) require the user's explicit go-ahead — not yet given, not yet requested. The user said mid-session they'd handle the PR themselves ("nova branch a partir dela, eu farei o pr e commit depois") but confirmed local per-task commits should still happen — which they did, 5 commits total.
- **Blockers**: None.
- **Uncommitted files**: none from this feature (working tree clean on `feat/markdown-cleanup` as of `7d76030`). Unrelated, still out of scope as before: `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`, `.specs/features/csharp2md-v3/context.md`, both dated Markdown files, `AGENTS.md`, `CLAUDE.md`, `fixtures/launch-manifest.json`, `research/`, `src/Csharp2Md.Cli/Properties/`.
- **Branch**: `feat/markdown-cleanup`, cut from `feat/csharp2md-v3`, HEAD `7d76030` (5 commits ahead of `feat/csharp2md-v3`'s `e00f191`). Not pushed to any remote.

## Historical Handoff — csharp2md v3

- **Feature**: csharp2md v3 — factual model and semantic analysis (`.specs/features/csharp2md-v3/`)
- **Phase / Task**: Execute — **all 47 tasks complete (T1-T47), feature-level validation PASSED.** Everything is committed; nothing remains but the user's push/PR decision (branch never pushed, per AD-007).
- **Completed this session (2026-08-19)**: T45 (`V3DeterminismTests` — cross-root byte-identical output, independently recomputed manifest hashes, persisted-fragment source reconstruction, approved Markdown snapshot), a pre-existing mojibake fix in `MarkdownProjector` surfaced while approving that snapshot (`884bb10`), T46 (package version 3.0.0, real packed/installed-tool assertions), T47 (README.md/README.pt-BR.md rewritten for the actual v3 surface — both had been stuck describing the stale v1 flat layout; ran the four required pre-Verifier quality skills, all clean; walked all 70 spec requirements and the 16-item Success Criteria checklist against actual task-completion state, closing a chronic staleness drift where most rows were never updated after their closing task landed). Then the mandatory independent Verifier ran two passes: the first (`.specs/features/csharp2md-v3/validation.md`) found two real, well-scoped gaps — a surviving discrimination-sensor mutant in `HttpRelationDetector.IsHttpClient` (no lookalike caught a fully-qualified-name-to-simple-name weakening) and an overstated migration-ledger claim (`MigrationLedgerTests` verifies at 12-category granularity, not per-row for all 428, though T41's evidence claimed the latter) — both fixed (`783a59a`, `5a1d026`) and independently re-verified PASS in a second pass (`e4e4aa3`): gate green, discrimination sensor 5/5 killed.
- **In-progress** (file:line): none. Feature is done.
- **Next step**: None for this feature. Push `feat/csharp2md-v3` and open the PR to `master` require the user's explicit go-ahead (AD-007) — not yet given, not yet requested.
- **Pitfalls confirmed empirically across T34-T39 (kept as reference for any future detector/relation work in this codebase, not forward-looking for this feature anymore)**:
  1. Any detector file nested under `Csharp2Md.Core.Detection.<Family>` (e.g. `.Http`, `.Grpc`, `.Messaging`, `.CompileTime`) has C#'s enclosing-namespace lookup resolve the bare name `DocumentDetectionContext` to the legacy v2 type in `Csharp2Md.Core.Detection` (still present pending T41's removal) instead of the v3 one in `Csharp2Md.Core.Detection.Contracts`, even with the right `using` present — a `using`-alias does **not** override this either. Fix: `using FactDocumentDetectionContext = Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext;` (pick a name that can't collide) and use that alias throughout the detector file; fully-qualify the same type inline in the test file wherever it imports both `Csharp2Md.Core.Detection` and `Csharp2Md.Core.Detection.Contracts` (CS0104 ambiguity there).
  2. For a reduced extension-method call `receiver.Extension(x)`, do not trust `IInvocationOperation.Instance`/`Arguments` position to find the receiver or its type — both have been observed to behave differently than the official XML docs describe depending on call shape. The robust, call-shape-independent way to get an extension method's declared receiver type is `(method.ReducedFrom ?? method).Parameters[0].Type` (works whether or not Roslyn reduced this particular call). To extract a specific named argument's value regardless of position, always match by `argument.Parameter?.Name`, never by index.
  3. A `string`/value-type argument passed to an `object`/interface-typed parameter (e.g. `AddKeyedSingleton(..., object? serviceKey)`) arrives as `IArgumentOperation.Value` being an `IConversionOperation` (boxing), whose own `.ConstantValue.HasValue` is `false` — unwrap via `conversion.Operand.ConstantValue` before falling back to "irreducible expression, mark Partial". A single-element `params` array constant is a separate case: `IArrayCreationOperation.Initializer.ElementValues[0].ConstantValue`.
  4. A `typeof(X)` argument is an `ITypeOfOperation` — read `.TypeOperand` for the `ITypeSymbol`; an unbound generic (`typeof(IRepository<>)`) has `IsUnboundGenericType: true` on that symbol.
  5. Any test fixture that inlines framework-shape stubs (fake `IServiceCollection`, fake attribute/base-class hierarchies, etc.) alongside the code under test must compile them as a **separate `SyntaxTree`** from the detection-target tree in the same `CSharpCompilation` (each tree needs its own `using` directives — see the `BodyUsings`/`FrameworkStubs` split in both `AspNetCoreDetectorTests.cs` and `DependencyInjectionDetectorTests.cs`), or the detector will legitimately also "detect" declarations inside your own stub scaffolding.
  6. Ground every non-obvious Roslyn member against `~/.nuget/packages/microsoft.codeanalysis.common/5.6.0/lib/net10.0/Microsoft.CodeAnalysis.xml` (grep it) before relying on remembered behavior — official docs and actual runtime behavior have already diverged once (pitfall 2) in this exact Roslyn/net10.0 setup.
  7. Pitfalls 2-3 (extension-method `Arguments`/`Instance` quirks, boxing conversions) are specific to **extension-method or `object`-typed-parameter call shapes** — T36's `HttpRelationDetector` needed none of them because every real `HttpClient`/`IHttpClientFactory` member it uses is a genuine instance method/property. Don't apply those workarounds reflexively; check whether the call shape is actually an extension method or boxes its argument first.
  8. (Resolved as of T41) `spec.md`'s "Requirement Traceability" table (~line 323+) had an unrelated, never-committed AD-014 identity-grammar edit sitting in its working tree since T7. It stopped reproducing once that edit was committed (by T41 at the latest) — the `git stash` workaround described here is no longer needed for this feature, but the underlying lesson stands for any future project: check `git status` on a spec file before editing its traceability table, in case an earlier task left an unrelated edit uncommitted.
  9. Comparing a **constructed** generic method's `Parameters[i].Type` (call-site type-substituted, e.g. `OrderPlaced`) against its own `TypeParameters[i]` (always unsubstituted, e.g. `TEvent`) can never succeed — they're from different substitution states. Use `method.ConstructedFrom` (or `method.OriginalDefinition`) first, then compare `Parameters`/`TypeParameters` on *that*, so both sides stay consistently unsubstituted. This broke every direct-interface-typed call in T38 silently (zero facts, no exception) until fixed; a concrete-typed call site was accidentally unaffected because `INamedTypeSymbol.GetMembers(name)` already returns unconstructed declarations.
  10. `FactValidator` (T11, `src/Csharp2Md.Core/Facts/Validation/FactValidator.cs`) only requires `Header.Evidence` non-empty and `Header.Provenance` to include a `DetectorId` for **runtime** relations (`RelationFact.IsRuntime`, true for `DependencyInjection`/`Http`/`Grpc`/`Events`, false for `CompileTime`/`Inheritance`). A `CompileTime`-partition fact may legitimately have empty evidence when it has no source-document location to point at (e.g. a project reference, which lives in project XML, not a `DocumentFact`) — don't invent fake evidence to satisfy a rule that doesn't apply. Also: `FactValidator.CompileTimeOnlyRelationKinds` already hardcodes the exact strings `"project-reference"`/`"package-reference"` (T11) — read the validator before naming new compile-time relation kinds, don't guess a name and hope it matches.
- **Blockers**: None. (Historical: this Claude Code account hit its monthly spend limit on 2026-08-18 mid-Phase-5 sub-agent batch work; resolved by finishing inline. Not relevant to resuming from here — the feature is done.)
- **Uncommitted files**: unchanged, still out of scope for this feature: `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`, `.specs/features/csharp2md-v3/context.md`, both dated Markdown files, `AGENTS.md`, `CLAUDE.md`, `research/`, and `src/Csharp2Md.Cli/Properties/`. (`README.pt-BR.md` was in this list before T47; T47 rewrote and committed it, so it's no longer uncommitted.)
- **Branch**: `feat/csharp2md-v3`, HEAD `e4e4aa3`. All 47 tasks committed, independent Verifier returned PASS after one fix→re-verify iteration (`.specs/features/csharp2md-v3/validation.md`). Nothing left except the user's push/PR decision (AD-007).

## Historical Handoff — LLMWiki Phase 1

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
