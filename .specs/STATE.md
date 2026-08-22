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

### AD-015
- **Decision**: `RelationCollector` (feature `relation-collector`) produces relation facts for `calls`, `creates`, `inherits`, `implements`, `references`, `publishes`, `subscribes`, `handles`, `http-client`, `http-call` by wiring directly into the live streaming pipeline — `SyntaxFactExtractor.Extract` (syntax-only candidates, always runs) and `TrustedSemanticProjectProcessor.BindDocuments` (best-effort semantic refinement, trusted mode only), merged by `FactMerger`'s existing same-identity/`ResolutionRank` logic — rather than through the pre-existing `Detection`/`DetectorHost`/`IDocumentFactDetector` abstraction described by AD-004.
- **Reason**: Designing this feature surfaced that `DetectorHost` and all six of its registered detectors (`MessagingRelationDetector`, `HttpRelationDetector`, `GrpcRelationDetector`, `CompileTimeReferenceDetector`, `DependencyInjectionDetector`, `AspNetCoreDetector`) are never invoked by `AnalysisEngine.AnalyzeAsync` — confirmed by grep, zero references outside `Detection/*`. `relations.json` has been empty in every real run regardless of `SemanticModel` availability, not only in the partially-broken-binding case the feature was originally reported against. Finishing `DetectorHost`'s wiring (constructing `DocumentDetectionContext`/`SolutionAnalysisIndex` for real, which itself has a chicken-and-egg dependency on relations that don't exist yet) is materially bigger than this feature's scope; the user explicitly deferred it after being shown the trade-off.
- **Trade-off**: Two code paths now exist for "how a relation fact gets produced" — this feature's kinds live in `Analysis/Syntax` + `Analysis/Relations`, while `GrpcRelationDetector`/`CompileTimeReferenceDetector`/`DependencyInjectionDetector`/`AspNetCoreDetector` remain in `Detection/*`, still unwired, still orphaned. `DetectorHost` and those four detectors are not removed (still used/tested in isolation) but their fate — finish wiring them the same way, port them to the new pattern, or retire them — is an explicit open question for a future feature, not resolved here.
- **Scope**: Relation-fact production for `RelationCollector`'s ten kinds; the `Detection`/`DetectorHost` abstraction's future is out of scope for this decision.
- **Date**: 2026-08-19
- **Status**: partially superseded by AD-018 (relation enrichment merge point only; the pass-one wiring stands)

### AD-016
- **Decision**: Persistence discovery introduces a new fact family (`DatabaseObjectFact`, `DatabaseColumnFact`) whose identity is minted **only** from a name proven by a source string literal or explicit configuration (`ToTable("tb_order")`, `HasColumnName("order_status")`, a table name the SQL tokenizer read out of a literal statement). A name reached by EF convention, by interpolated or concatenated SQL, or by any other inference never mints a node: its relation keeps `target_id: null`, carries the observed `target_text`, an `unresolved_reason` where applicable, and a non-`Exact` resolution (`Heuristic` for convention, `Candidate` for ambiguous, `Unresolved` for unreadable).
- **Reason**: The knowledge graph needs real nodes to hang incoming edges on — without them there is no per-table wiki page and no change-impact query, which are the stated payoffs of the whole stage. But AD-011's guarantee (never promote an unproven name to an identity) is what makes the output trustworthy. Splitting on "was this name proven by a literal?" satisfies both: a literal in source is evidence in exactly the sense AD-011 requires, while a convention is a claim about a target, not proof the target exists.
- **Trade-off**: Two representations now exist for the same conceptual table — a real node when configuration proves its name, and a `target_text` string when only convention suggests it. A consumer asking "which tables exist?" must read both and understand that the second set is unconfirmed. The alternative (mint from convention too) would have made the catalogue look complete while being partly invented.
- **Scope**: The persistence fact family and its relations. Extends AD-011's discipline beyond `ServiceName` to a new node family; supersedes nothing.
- **Date**: 2026-08-20
- **Status**: active

### AD-017
- **Decision**: The factual fragment schema moves from version 3 to version 4 to admit `database_objects` and `database_columns`. `FactualJsonSerializer.SchemaVersion` and `schemas/facts.schema.json`'s `const` move together; the aggregate envelopes (`raw/facts/relations/*.json`, `coverage.json`, `diagnostics.json`, `manifest.json`) stay at version 2.
- **Reason**: `facts.schema.json` is strict (`additionalProperties: false`, every array in `required`), so adding fact kinds is necessarily a breaking change for any consumer validating against it. Recording the break as a version bump is what `symbol-index` already did going 2→3 when it extended `SymbolFact`; following the same practice keeps the fragment schema's version an honest signal instead of letting the contract drift silently.
- **Trade-off**: Every fragment-reading consumer must be updated in lockstep with the tool, and the two version lines (fragment at 4, aggregates at 2) must be kept mentally distinct. Keeping fragments at 3 and extending the schema quietly would have avoided the churn at the cost of making the version number meaningless.
- **Scope**: The factual fragment wire contract and `schemas/facts.schema.json`. Partially supersedes AD-010's schema-version statement as it applies to fragments; AD-010's output-layout decisions are otherwise untouched.
- **Date**: 2026-08-20
- **Status**: active

### AD-018
- **Decision**: `RelationCollector` emits `RawRelation` **claims** rather than facts. A `RelationClaimAccumulator` buffers them together with the `DocumentExtent`s their evidence will be validated against, and `RelationResolver` is the only component that mints a `RelationFact` — in pass two, once `SymbolIndex` exists. Document fragments no longer carry relations; every relation in a run lives in one solution-level fragment. The factual fragment schema moves from version 4 to version 5.
- **Reason**: `SymbolIndex` is built after the document loop completes, so a relation persisted during the loop can never have been resolved against it. Every alternative either lets a resolved aggregate contradict an unresolved fragment, or rewrites artifacts already written to content-addressed paths. Making the collector produce claims is what `data-access-discovery` already does with `RawDatabaseClaim` (documented there as "never serialized"), and it makes "the resolver is the sole writer of `RelationFact`" structural instead of a convention that a later contributor can quietly break.
- **Trade-off**: Resolution context (receiver text, member name, argument types, declaration bindings) is held in memory for the whole run and never appears in the output, so a wrong edge cannot be diagnosed from the artifacts alone — only from a re-run. The alternative, serializing that context as `RelationDetail` entries, was rejected because details feed the identity fingerprint and would have churned every `relation_id`. A set of existing tests and snapshots that assert relations inside document fragments must be rewritten to the new location.
- **Scope**: Relation production and persistence, and the factual fragment wire contract. Partially supersedes AD-015: relation enrichment is merged at the claim level by `FactResolutionAlgebra.Stronger` instead of at the fact level by `FactMerger`'s same-identity/`ResolutionRank` logic. The pass-one wiring AD-015 chose (`SyntaxFactExtractor.Extract` plus `TrustedSemanticProjectProcessor.BindDocuments`, not `DetectorHost`) is unchanged, as is AD-015's statement about the orphaned `Detection/` tree.
- **Date**: 2026-08-21
- **Status**: active

### AD-019
- **Decision**: A relation carries a `ResolutionMethod` (`exact`, `candidate`, `syntactic`, `configured`, `convention`, `dynamic`, `heuristic`, `unresolved`) alongside — not instead of — its header's `FactResolution`. No part of a fact's identity may derive from its resolution outcome, so resolution state is a first-class field and never a `RelationDetail`.
- **Reason**: `FactResolution` answers "how proven is this fact?" and is aggregated across every fact family by `FactResolutionAlgebra`; `ResolutionMethod` answers "by what route did we reach this target?" and is meaningful only on a relation. Extending `FactResolution` with `Configured`/`Convention`/`Dynamic` would drag those states into every symbol, document and project header and would supersede AD-016's convention-to-`Heuristic` mapping for no gain. The field-not-detail rule is mechanically forced: `RelationFactId` is minted from a fingerprint of the details, so a resolution-bearing detail would change a relation's identity every time its resolution improved.
- **Trade-off**: Two resolution vocabularies now coexist on one fact, and a consumer must learn which question each answers. Collapsing them into one enum would have been conceptually tidier at the cost of a breaking change across every fact family.
- **Scope**: The relation fact's wire contract and the identity rule for all fact families. Conforms to AD-011 and AD-016: a target is still proven only by a semantic binding, an index match, or a source literal.
- **Date**: 2026-08-21
- **Status**: active

### AD-020
- **Decision**: `relation-resolver` does not attempt to make `raw/dependencies.mmd` carry a real edge, even though spec.md's own P1 Success Criteria and T30's Done-when list both name it. `RelationResolverEndToEndTests` proves the other five P1 Independent Tests plus that `dependencies.mmd` is written and non-empty; it does not assert an edge.
- **Reason**: Two independent, pre-existing gaps make an edge unreachable regardless of how well relations resolve. First, no production code path anywhere constructs a `ComponentFact` (`grep -rln "new ComponentFact(" src/Csharp2Md.Core/` returns nothing) — the entire `Detection/` tree that would presumably produce one is orphaned from `AnalyzeAsync`, as AD-015 already documented. Second, and separately, `RelationProjector.Mermaid` builds `componentByProject` keyed by `ComponentIndexEntry.ProjectIds` (project-shaped `FactId`s) but looks candidate edges up by `relation.SourceId`/`TargetId` (symbol- or document-shaped `FactId`s) — a key-shape mismatch that would still miss every lookup even if `ComponentFact`s existed. design.md's Reuse table assumed `Mermaid`'s existing `TargetId is not null` filter was the only blocker and would "start passing" once relations resolve; that assumption is false on both counts. Confirmed by a real end-to-end run over `fixtures/SyntheticSolution`: `raw/dependencies.mmd` is `"flowchart LR\n"` with zero edges even with real, resolved relation targets in place. User confirmed (2026-08-21, in response to an explicit AskUserQuestion) to drop the edge assertion and record this as an open finding rather than expand scope to fix `Mermaid`'s lookup or wire up component detection.
- **Trade-off**: spec.md's P1 Success Criteria line ("a non-empty `raw/dependencies.mmd` with at least one edge outside the `data` partition") stays unmet by this feature. A future feature must both wire some producer of `ComponentFact` (finishing or replacing the orphaned `Detection/` tree per AD-015) and fix `RelationProjector.Mermaid`'s project-vs-symbol/document key mismatch before that criterion is achievable.
- **Scope**: `RelationResolverEndToEndTests`'s coverage of spec.md's dependencies.mmd Success Criterion only. Does not touch `RelationProjector`, `Detection/`, or component detection. Extends AD-015's open question about the orphaned `Detection/` tree with a second, independent blocker in `RelationProjector.Mermaid` itself.
- **Date**: 2026-08-21
- **Status**: active

## Handoff

- **Feature**: `component-graph` (`.specs/features/component-graph/`) — **planned, not started.** Specify,
  Design and Tasks are all complete and **user-approved** (2026-08-22). Execute has not begun; no production
  code has been written for it and nothing has been committed.
- **Phase / Task**: Execute, about to start at **T1**. 0 of 17 tasks done.
- **Branch**: still `feat/relation-resolver`, HEAD `69a9006`, stacked on `feat/data-access-discovery` (which
  is stacked on `master`). Neither branch is pushed to any remote; no PR opened. Pushing/opening a PR
  requires explicit user go-ahead per AD-007 — not requested or given. **No branch has been cut for
  `component-graph`** — decide that with the user before T1 (prior features each got their own stacked
  branch; AD-007 requires a feature branch, and this feature carries a `refactor(projection)!` break at T9).
- **In-progress** (file:line): none.
- **Next step**: run Execute from T1. The user chose **"approve, I'll execute later"**, so do not start
  without them asking. The sub-agent offer was presented and **not yet answered** — 17 tasks pack into 3
  batches (Phases 1+2 = T1-T8, Phases 3+4 = T9-T15, Phase 5 = T16-T17); one-worker-per-phase (5 workers) and
  inline execution were both offered as alternatives. Ask which before dispatching anything.
- **Blockers**: None.
- **Two pipeline fixes committed this session** (2026-08-22), both pre-existing working-tree changes the
  user asked to land before Execute begins. Neither changes output; both were verified against the full
  gate before committing:
  - `fe3448e` `perf(facts): serialize a fragment straight to UTF-8 bytes` — `FactualJsonSerializer` no
    longer materializes the payload as a UTF-16 string before encoding it. AD-018 puts every relation in
    one solution-level fragment, which can grow large enough that the intermediate string fails to
    allocate. CRLF normalization moved to an in-place byte pass.
  - `69a9006` `perf(analysis): bound aggregate projection to the pass-two fragments` — `AnalysisEngine`'s
    `validatedFragments` retained one fragment per document and per project for the whole run, so peak
    memory grew with codebase size. **That directly contradicted AD-008**, whose stated purpose is a
    pipeline bounded by catalogs and aggregates rather than by how much source it read; the fix restores
    that guarantee rather than establishing a new one, so no new `AD-NNN` is warranted. The replacement
    `aggregateFragments` builder is scoped to pass two, and both aggregate projectors now read one
    explicitly scoped collection instead of the same growing builder at two different points in the run.
    Per-document and per-project fragments are still persisted and still reach the manifest through
    `storedFragments`.

  `component-graph`'s design.md quotes `aggregateFragments` in its `AnalysisEngine` wiring snippet; that
  identifier is now in `HEAD`, so T2 and T11 apply as written.
- **Gate at close**: `dotnet build -c Release` clean, `dotnet format --verify-no-changes` clean,
  `dotnet test` 1850/1851 — the single failure is the pre-existing documented
  `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` flake, unchanged
  from the previous session's baseline and unrelated to either commit.
- **Uncommitted files — untracked**: the same long-standing paths carried by every prior handoff —
  `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`, `.specs/features/csharp2md-v3/context.md`,
  `.specs/features/data-access-discovery/context.md` and `design.md`,
  `.specs/features/relation-collector/context.md` and `design.md`, two dated Markdown files, `AGENTS.md`,
  `CLAUDE.md`, `fixtures/launch-manifest.json`, `research/`, `src/Csharp2Md.Cli/Properties/`. Now also
  `.specs/features/component-graph/` (spec.md, design.md, tasks.md).

## Planning Record — Component Graph (2026-08-22)

- **What it closes**: AD-020. A live CLI run over `fixtures/SyntheticSolution` during Design confirmed
  `raw/dependencies.mmd` is exactly `"flowchart LR\n"` — zero nodes, zero edges — while the same run resolves
  **15 cross-project relations** and **11 relations into proven database objects**. Both of AD-020's blockers
  were re-verified rather than taken on trust: `grep` finds no `new ComponentFact(` outside one test file,
  and `RelationProjector.Mermaid`'s `componentByProject` is keyed by project-shaped `FactId`s while relation
  endpoints are symbol-, document- or database-shaped.
- **Spec**: 35 requirements. P1 `COMP-01`..`COMP-32` across four stories (components exist; deduped edges
  render; database objects are nodes; omissions are counted). P2 `COMP-40`/`COMP-41` (service-level rollup)
  and P3 `COMP-50` (placeholder nodes for unresolved endpoints) deliberately out of the task list.
  `validate_spec.py` exits 0.
- **User decisions (AskUserQuestion, all three recorded as confirmed assumptions in spec.md)**: one component
  per **project** (not per service); edges **deduped** by (source, target, partition, kind) with a count;
  **database objects are nodes** alongside projects. Service granularity was additionally ruled out by
  evidence — `Acme.Shared.Contracts` belongs to both `.slnx` files, which trips
  `RelationProjector`'s one-project-one-component invariant on this repo's own fixture.
- **Design**: `ComponentFragmentBuilder` (mints one `ComponentFact` per `ProjectFact`), a pure
  `GraphNodeIndex` (endpoint `FactId` → node, resolved by walking facts, never by parsing id text — AD-014),
  and a new `ComponentGraphProjector` owning `dependencies.mmd` and `components.md`. `RelationProjector`
  narrows to its partition job. Architecture chosen by the user over two alternatives.
- **No schema bump**: `schemas/facts.schema.json` already lists `components` in `required` and `"component"`
  in `fact_kind`. `FactualJsonSerializer.SchemaVersion` stays at **5**. Verified by reading the schema.
- **Two corrections the live run forced into the spec**: every `ProjectFact` reports `syntactic` in the
  default syntax-only mode, so a component's resolution **mirrors its project's** rather than being a
  hardcoded `Exact`; and `Acme.DoesNotExist` gets a `ProjectFact` despite the file being absent, so the run
  yields **5** components, not 4.
- **Critical trap recorded in design.md's Risks table**: `RelationProjector.Project` is invoked inline inside
  the `snapshot` constructor at `AnalysisEngine.cs:269`, *after* `CoverageProjector.Project` at line 263, so
  any diagnostic a projector emits from there never reaches `diagnostics.json`. This is the same defect the
  relation-resolver session found in `RelationFragmentBuilder` (T23). T11 therefore requires asserting
  `C2M-CG-001` against the **written file**, not the projector's return value.
- **Tasks**: 17 tasks, 5 phases. `validate_tasks.py` exits 0 (2 expected `Tests: none` warnings on the two
  document-only tasks). One design amendment was made during breakdown and written back into design.md:
  `ComponentGraphProjection` also carries `ImmutableArray<ComponentGraphEdge> Edges`, so edge selection (T4)
  is verifiable without the renderer (T5).
- **Standing skip**: the Verifier's discrimination sensor is skipped for this feature, as for every prior
  one, recorded in `tasks.md`'s header.

## Historical Handoff — RelationResolver

- **Feature**: RelationResolver (`.specs/features/relation-resolver/`) — **done.** Turns the pipeline's
  relation stubs into proven edges: `RelationResolver` is now the sole reachable writer of `RelationFact`,
  reading the run's complete `SymbolIndex` against every buffered `RawRelation` claim in a pass-two stage,
  through a fixed five-strategy chain (`ExistingTarget` → `DatabaseRelation` → `ReceiverType` → `SymbolIndex`
  → `Unresolved`).
- **Phase / Task**: Execute — **all 32 tasks complete across 6 phases**; feature-level validation **PASSED**
  on the first Verifier pass (two Minor spec-precision gaps found and fixed immediately after, before this
  handoff was written — see below). Discrimination sensor skipped by standing user request (same as
  `symbol-index`/`relation-collector`/`data-access-discovery`), recorded in `tasks.md`'s header.
- **Branch**: `feat/relation-resolver`, cut from `feat/data-access-discovery` (the user's explicit choice —
  `AskUserQuestion`, "stacked, not from master"), HEAD `dea05f5`. 34 commits. Not pushed to any remote.
- **Specify/Design/Tasks (prior session, 2026-08-21)**: spec.md — 49 requirements, P1 `RELR-01`..`RELR-39`
  across five stories, P2 `RELR-40`..`RELR-45` and P3 `RELR-46`..`RELR-49` deliberately out of this task
  list's scope. design.md — five-strategy chain, claim-based collector-to-resolver seam (mirrors
  `RawDatabaseClaim`, never serialized). tasks.md — 32 tasks, 6 phases. User chose "one sub-agent per phase"
  for execution.
- **Execution (this session)**: Phases 1-3 (T1-T14) ran via dispatched sub-agents as planned. **Phase 4's
  sub-agent hit the account's monthly API spend limit mid-T15 and terminated.** The user's only instruction
  was "continue" — interpreted (matching this same project's own documented precedent, e.g.
  data-access-discovery's Phase 5 interruption) as authorization to keep implementing directly in the main
  session rather than retry sub-agent dispatch. Phases 4, 5 and 6 (T15-T32) were completed this way, each
  task still following the full implement → gate → atomic-commit cycle.
- **Eight real production bugs found and fixed during wiring (T23)**, each via direct CLI/`dotnet test`
  runs rather than assumption: `RelationClaimAccumulator`'s "no claim → no extent" rule dropped
  cross-referenced documents for pure-SQL claims; `DatabaseMappingResolver`'s `ProducerMethod` was
  hardcoded to `Configured` regardless of the claim's actual proof strength; `DatabaseRelationStrategy`'s
  generic fallback hardcoded `Unresolved` instead of deriving from `ShapeConfidence`;
  `FactStore.MapRelation` never read `RelationFact.Method`/`Candidates` at all, so every persisted relation
  showed `"exact"` regardless of truth; `RelationFragmentBuilder.Build`'s diagnostics were validator-only,
  so the resolver's own `C2M-RELR-*` codes never reached `diagnostics.json`. Full detail in the T23 commit
  body (`dd94e76`).
- **AD-020 (new decision this session)**: spec.md's Success Criterion "a non-empty `raw/dependencies.mmd`
  with at least one edge outside the `data` partition" is **not met**, by explicit user decision after being
  shown the trade-off (`AskUserQuestion`). Two independent, pre-existing gaps make it unreachable regardless
  of resolution quality: no production path anywhere constructs a `ComponentFact` (the `Detection/` tree
  that would is still orphaned, per AD-015), and separately `RelationProjector.Mermaid`'s
  `componentByProject` lookup is keyed by project-shaped `FactId`s while relation `Source`/`TargetId`s are
  symbol- or document-shaped, so it would still miss even if a `ComponentFact` existed. Confirmed by a real
  run: `raw/dependencies.mmd` is `"flowchart LR\n"` with zero edges even with real, resolved relation
  targets in place. `RelationResolverEndToEndTests.cs`'s corresponding test proves only that the file is
  written and non-empty, not that it has an edge — this is the honest, narrower claim. **Closing this gap
  for real needs a future feature**: wire some `ComponentFact` producer and fix `Mermaid`'s key mismatch.
- **Verifier (fresh sub-agent, author != verifier) → PASS.** All 39 P1 acceptance criteria independently
  re-derived and confirmed against real `file:line` assertions; two T32 commit-body citations were wrong and
  corrected (RELR-10, RELR-25 — the underlying behaviour was still covered elsewhere, so not gaps). Two
  Minor spec-precision gaps found: RELR-05 (`ReceiverTypeStrategy` forwards `ArgumentCount`/`ArgumentTypes`
  into the lookup but no test exercised non-default values) and RELR-20 ("preserve the claim's
  `FactProvenance`" was asserted with a vacuous `NotEmpty`, since `RawRelation` carries no provenance field
  by design). Both fixed same-session (`dea05f5`) with a genuine overload-discriminating test and a pinned
  `DetectorId` assertion, respectively; re-verified green. One cosmetic stale comment
  (`FactualJsonContracts.cs`, pointed at a `SPEC_DEVIATION` note T32's rewrite had already removed) also
  fixed. `.specs/features/relation-resolver/validation.md` is the persisted report, updated in place to
  reflect the post-fix state; `validate_state.py relation-resolver` exits 0. Four lessons distilled to
  `.specs/lessons.json` (L-007..L-010).
- **Gate at close**: `dotnet build -c Release` clean, `dotnet format --verify-no-changes` clean,
  `dotnet test` 1850/1851 passing (the one failure is the pre-existing documented
  `DotnetMsBuildEvaluatorTests` flake, unrelated to this feature — carried unchanged across every session
  that has touched this branch). Test count 859 → 968 `[Fact]`/`[Theory]` attributes (+109).
- **Not decided**: whether to push the two stacked branches and open a PR (needs explicit user go-ahead
  per AD-007); what to work on next.

## Historical Handoff — Database Access Discovery

- **Feature**: Database Access Discovery (`.specs/features/data-access-discovery/`) — **done.** A stage that
  maps every interaction the analysed code has with a database: EF Core entities/tables/columns and literal
  SQL, emitted as persistence nodes (`raw/facts/database.json`) plus a new `data` relation partition, all
  under the default syntax-only mode.
- **Phase / Task**: Execute — **all 36 tasks complete across 7 phases, feature-level validation PASSED**
  after one fix→re-verify iteration (a first Verifier pass FAILed on 3 items; all 3 were fixed and a second
  independent Verifier pass returned PASS, 28/28 P1 acceptance criteria substantiated). Discrimination
  sensor skipped for this feature by the user's standing request (same as `relation-collector`/
  `symbol-index`), recorded in `tasks.md`'s header — the user will run Stryker manually.
- **Branch**: `feat/data-access-discovery`, cut from `master` at `67bbbe0`, HEAD `4ea8cfc`. 41 commits.
  Not pushed to any remote.
- **Completed this session (2026-08-20)**, executed as 7 sequential phase-batch sub-agents (one per phase,
  the user's chosen packing), each following implement.md's per-task cycle:
  - **Phase 0** (`0fa984d`..`6907fd4`, T1-T3) — the three pre-existing pipeline defects the prior Design
    session found: `RelationProjector` had no production caller so every relation partition was written
    empty; `CanonicalAggregateWriter` listed 6 of 7 partitions so `structural.json` was never written; a
    multi-project run crashed in `CoverageProjector` on a project reached by more than one path. All three
    fixed with a failing-test-first cycle. One pre-existing test that asserted the defect itself
    (`EndToEndTests`, `"entries": []`) was rewritten *stricter*, not deleted.
  - **Phase 1** (`c7a1f8f`..`9b9e572`, T4-T10) — the persistence fact family: `DatabaseObjectKind`/
    `DatabaseOperation`/`ColumnUsage` enums, `DatabaseObjectFactId`/`DatabaseColumnFactId` (AD-014
    grammar), `DatabaseObjectFact`/`DatabaseColumnFact`, `FactValidator` support, JSON contracts, and a
    **breaking schema bump to version 4** (`feat(facts)!:`, one snapshot re-approved line-by-line, 4 version
    assertions renamed to reflect the new value).
  - **Phase 2** (`3857c83`..`00e7b6e`, T11-T15) — the collector seam: `RawDatabaseClaim`, `IDataAccessAnalyzer`,
    `DataAccessCollector` with per-analyzer failure isolation (`C2M-DA-001`, partial work discarded,
    cancellation never converted into a diagnostic), `DatabaseClaimAccumulator` with canonical ordering,
    wired into `SyntaxFactExtractor.Extract`'s existing walk.
  - **Phase 3** (`c440edd`..`7bd4a80`, T16-T22) — `EfCoreAnalyzer`: DbContext/DbSet discovery, `ToTable`/
    `HasColumnName` configuration, reads/filters/writes as claims. One stray out-of-scope file
    (`launchSettings.json`) was accidentally staged by a `git add -A` and untracked again in a follow-up
    commit — its blob remains in one commit's history, working tree is correct.
  - **Phase 4** (`6fbb437`..`7a7272b`, T23-T26) — `SqlStatementReader` (an internal bounded tokenizer, no
    new package) plus `SqlTextAnalyzer`. The DAD-15 credential guard (withholds statement text when a
    literal credential value is assigned) originates here, later found incomplete — see below.
  - **Phase 5** (`55d4c8c`..`403d3c5`, T27-T32) — `DatabaseMappingResolver` (entity→object, property→column,
    accesses→relations, configured-over-convention precedence), `DatabaseFragmentBuilder`, pass-2 wiring
    into `AnalysisEngine`, `DatabaseAggregateProjector` writing `raw/facts/database.json`. **This batch's
    sub-agent was interrupted by an infrastructure error (API spend-limit) after committing T27-T31**; the
    orchestrator reconciled state (found T32's implementation and tests already written but uncommitted,
    an interrupted run — same pattern as a prior session's `symbol-index` T11), independently ran the gate,
    and closed T32 as its own commit (`403d3c5`).
  - **Phase 6** (`2196149`..`20856f2`, T33-T36) — extended `fixtures/SyntheticSolution` with EF Core
    configuration/queries and literal/dynamic SQL, then proved both of `spec.md`'s P1 Independent Tests
    verbatim end to end and flipped all 28 P1 `DAD-NN` traceability rows to `Verified`. One deliberate
    deviation from T34's literal wording, disclosed inline: "no credential text anywhere in the fixture"
    was inverted to "the fixture *does* carry two credentials, on purpose" — read literally, T34's
    criterion would have made DAD-15 vacuous (nothing to leak, nothing proven).
  - **First Verifier (fresh sub-agent) → FAIL.** 27/28 P1 ACs substantiated; `DAD-15` was not. Ran the CLI
    over the fixture and found `Password=inline-fixture-secret` inside `symbols[].signature`,
    `symbols[].symbol_id`, `symbols[].header.id` and `documents[].symbol_ids[]` in
    `raw/facts/document/*.json` — synthesised **fact** fields, not rendered source. Root cause pre-dates
    this feature: `SyntaxFactExtractor.DeclarationSignature` joins every token of a member's declaration
    verbatim up to its body, so a field initializer like a hardcoded connection-string `const` flows
    straight into the symbol's signature and the id derived from it. T34's fixture (a `const string
    ConnectionString` in `OrderSqlQueries.cs`) was the first thing in this codebase to exercise that
    pre-existing leak against a real credential. Also flagged, non-blocking: `DAD-18`'s "leaving the run's
    exit code unchanged" clause had no assertion at any layer (true by inspection only); two spec-precision
    gaps (`MERGE`'s operation, the SQL access relation's own resolution) were confirmed genuine, not
    defects.
  - **Fix iteration** (`ecff901`, `e49bbd7`, `a9626f6`) — because the second Verifier sub-agent dispatch
    also hit the same infrastructure error, the orchestrator implemented the three fixes directly rather
    than keep retrying: (1) extracted `SqlTextAnalyzer`'s existing credential-shape predicate into a shared
    `CredentialText.Carries` helper and applied it in `DeclarationSignature` to redact a credential-bearing
    string-literal token to `"<redacted>"` before it becomes part of a signature or the id derived from it
    — narrow by construction, proven with tests that an ordinary literal and a `SET Password = @password`
    parameterized literal are untouched; (2) threaded a new internal `dataAccessAnalyzers` constructor seam
    through `AnalysisEngine` (mirroring the existing `onSymbolIndexBuilt` pattern) so a test could inject a
    throwing analyzer at the real `AnalyzeAsync` level and assert `ExitCode == 0`, closing DAD-18's gap;
    (3) wording-only `spec.md` edits stating `MERGE`'s `update` operation and the SQL relation's `Syntactic`
    resolution explicitly.
  - **Second Verifier — also hit the infrastructure error mid-run.** The orchestrator ran the
    re-verification directly instead: re-derived all evidence fresh (re-ran the CLI over the fixture and
    walked the output JSON field-by-field rather than trusting the fix commits' own summaries; re-read
    every changed test file at its current line numbers, since the DAD-15 fix shifted later citations in
    `DataAccessDiscoveryEndToEndTests.cs` by ~35 lines) and confirmed **PASS**: all 28 P1 criteria
    substantiated, gate clean, `validate_state.py data-access-discovery` exit 0. This departs from strict
    author≠verifier separation for the fix-and-reverify step only (both were done by the orchestrating
    session, not a fresh sub-agent) — disclosed to the user at the time as a consequence of repeated
    sub-agent dispatch failures, not a silent shortcut. Report: `.specs/features/data-access-discovery/validation.md`.
  - **One pre-existing, disclosed, order-dependent flaky test, confirmed unrelated to this feature** (same
    known flake disclosed in every prior feature's handoff below): `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml`
    (temp-directory cleanup race under xUnit parallel execution) — 1/1 passing in isolation, file untouched
    by this feature's diff.
- **In-progress** (file:line): none. Feature is done.
- **Next step**: None for this feature's implementation. Two things remain, both requiring the user: (1)
  run Stryker manually for the mutation-testing pass deferred from the automated Verifier; (2) push
  `feat/data-access-discovery` and open the PR — not yet given, not yet requested, per the standing
  blast-radius rule. Worth a look later, not blocking: `SyntaxFactExtractor.DeclarationSignature`'s
  credential redaction now covers this feature's fixture case, but is a general fix — any codebase with a
  different secret-shaped literal pattern the `CredentialText` predicate doesn't recognise (its key list is
  `password`/`pwd`/`accountkey`/`sharedaccesskey`/`accesstoken`) would still leak into a symbol signature.
- **Blockers**: None.
- **Uncommitted files**: none from this feature (working tree clean on `feat/data-access-discovery` as of
  `4ea8cfc`, aside from the same long-standing untracked paths listed below, plus one pre-existing
  line-ending-only working-copy modification to `DatabaseFragmentBuilder.cs` that carries no content diff).
  `context.md` and `design.md` for this feature stay untracked, matching this repo's established
  convention (Design-phase artifacts; only `spec.md`, `tasks.md`, and `validation.md` are committed by
  Execute). Unrelated, still out of scope: `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`,
  `.specs/features/csharp2md-v3/context.md`, `.specs/features/relation-collector/context.md` and
  `design.md`, both dated Markdown files, `AGENTS.md`, `CLAUDE.md`, `fixtures/launch-manifest.json`,
  `research/`, `src/Csharp2Md.Cli/Properties/`.

## Historical Handoff — SymbolIndex

- **Feature**: SymbolIndex (`.specs/features/symbol-index/`) — **done.** A queryable, name/qualified-name/member/method-lookup index over every `SymbolFact` a run produces, backed by `FrozenDictionary`s, with explicit ambiguity reporting (`FindCandidates`), build-time diagnostics for duplicate/dangling/ambiguous symbols, and metrics — wired into `AnalysisEngine.AnalyzeAsync` via an internal test-seam callback (no public `AnalysisResult` change).
- **Phase / Task**: Execute — **all 11 tasks complete (T1-T11), feature-level validation PASSED** (single pass, no fix→re-verify iteration needed). Discrimination sensor skipped for this feature by the user's standing request (same as `relation-collector`/`markdown-cleanup`), recorded in `tasks.md`'s header — the user will run Stryker manually.
- **Completed this session (2026-08-20)**: This session resumed mid-feature. T1-T6 (`TypeNameNormalizer`, `SymbolFact` identity fields on both the syntax-only and semantic-enrichment paths, `FactValidator`'s `ContainingSymbolId` check, the schema-version-3 JSON contract, and `SymbolIndex`/`SymbolIndexBuilder`'s core `GetById`/`FindByName`/`FindByQualifiedName`/`FindMembers` surface) were already committed on `feat/symbol-index` (`b976e0f`..`8dd9332`) from a prior session. On resuming, a **background sub-agent's prior worktree** (`.claude/worktrees/agent-aae601327d8804b00`, branch `worktree-agent-aae601327d8804b00`) was discovered still checked out and ahead: it had already completed and committed T7-T10 (`FindMethods`/`MethodLookup` argument-count-and-type ranking, `FindCandidates`/`SymbolLookupResult` priority-tier ambiguity, build-time `duplicated-symbol-id`/`invalid-containing-symbol`/`ambiguous-symbol-lookup` diagnostics, `SymbolIndexMetrics`/`IndexedSymbolKind`) as commits `d98d4bf`..`117c4b3`, and had T11's implementation and its integration test (`SymbolIndexEndToEndTests.cs`) already **written but uncommitted** — an interrupted run. Independently re-verified before trusting any of it: `dotnet build -c Release` clean, `dotnet format --verify-no-changes` clean, `dotnet test` 1379/1379 passing (including the new e2e tests), and `AnalysisEngine.cs`'s diff confirmed no `AnalysisResult` shape change. Then, via the `tlc-spec-driven` skill: closed T11 as its own atomic commit (`2527d47`, wiring `SymbolIndexBuilder` into `AnalysisEngine.AnalyzeAsync` through a new `internal Action<SymbolIndex>? onSymbolIndexBuilt` constructor parameter, mirroring the existing `FragmentValidationFunc`/`IAnalysisEngineObserver` test-seam pattern), flipped all 23 `SYMIDX-NN` traceability rows to `Verified`, fast-forward-merged the worktree branch into `feat/symbol-index` (`8dd9332`..`2527d47`, clean ff, no divergence), dispatched a fresh independent Verifier sub-agent, and committed its report (`15f4f38`).
- **Independent Verifier (fresh sub-agent, author ≠ verifier) → PASS.** 23/23 `SYMIDX-NN` ACs spec-anchored with `file:line` evidence. Gate: 1378/1379 passed — the 1 failure (`DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml`) is a pre-existing, unrelated temp-directory race, independently reproduced on the pre-feature baseline (`6cfb491`) in a disposable worktree and confirmed passing 12/12 in isolation — not a symbol-index regression. Test delta: +170 new tests, all passing. `dotnet build -c Release` 0 warnings/errors, `dotnet format --verify-no-changes` clean. Report: `.specs/features/symbol-index/validation.md`. `validate_state.py symbol-index` → exit 0.
- **One disclosed, non-blocking finding, independently re-verified by the Verifier (not taken on trust) — a spec-precision gap, not a defect:** T11's literal Done-when wording named `PaymentsService`/`AuthorizePayment` (from `fixtures/SyntheticSolution`) as the pair that should show `Resolution = Exact` in trusted-solution mode. That pair cannot bind on this fixture — `PaymentsService` derives from the gRPC-generated `Payments.PaymentsBase` and `AuthorizePayment` takes `Grpc.Core` parameter types the fixture compilation never references (no generated stub exists under `obj/`), so both correctly bind to error symbols and are indexed `Unresolved`. The task's SYMIDX-23/Goal-3 requirement (Exact resolution genuinely surfaces through the live trusted-mode index) is proven instead via `SwaggerOperationDefaultsFilter`/`Apply` — a type/member that does bind — while a dedicated test explicitly pins `PaymentsService`'s real `Unresolved`/`ContainsErrorSymbol=true` outcome so the spec-named symbols aren't silently skipped. Escalated in `tasks.md` rather than forced; left unchecked with its reasoning inline, not silently marked done. No lesson recorded — reasoned correctly and honestly disclosed, not a mistake to avoid next time.
- **One pre-existing, disclosed, order-dependent flaky test, confirmed unrelated to this feature (untouched file, reproduces on the unmodified pre-feature baseline too):** `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` (temp-file-cleanup race under xunit's parallel execution) — same known flake already disclosed in `relation-collector`'s and `markdown-cleanup`'s handoffs below. Not acted on here, per the Scope Guardrail.
- **In-progress** (file:line): none. Feature is done.
- **Next step**: None for this feature's implementation. Two things remain, both requiring the user: (1) run Stryker manually for the mutation-testing pass they deferred from the automated Verifier; (2) push `feat/symbol-index` (cut from `feat/relation-collector`'s tip `6cfb491`) and open the PR — not yet given, not yet requested, per the standing blast-radius rule. Optional housekeeping, not blocking: two stale worktrees (`.claude/worktrees/agent-a0a81a01f5cd5dfcb` and `agent-aae601327d8804b00`) and their branches are now fully superseded by the fast-forward merge and can be pruned (`git worktree remove` + `git branch -d`) whenever convenient — left alone this session since removing another session's worktree wasn't asked for.
- **Blockers**: None.
- **Uncommitted files**: none from this feature (working tree clean on `feat/symbol-index` as of `15f4f38`, aside from the same long-standing untracked paths listed below). Unrelated, still out of scope as before: `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`, `.specs/features/csharp2md-v3/context.md`, `.specs/features/relation-collector/context.md` and `design.md`, both dated Markdown files, `AGENTS.md`, `CLAUDE.md`, `fixtures/launch-manifest.json`, `research/`, `src/Csharp2Md.Cli/Properties/`.
- **Branch**: `feat/symbol-index`, cut from `feat/relation-collector` at `6cfb491`, HEAD `15f4f38`. Not pushed to any remote.

## Historical Handoff — RelationCollector

- **Feature**: RelationCollector (`.specs/features/relation-collector/`) — **done.** Closes the reported `relations: []` defect: `calls`, `creates`, `inherits`, `implements`, `references`, `publishes`, `subscribes`, `handles`, `http-client`, `http-call` relation facts are now produced in both syntax-only (default) and trusted-semantic mode, per AD-015.
- **Phase / Task**: Execute — **all 16 tasks complete (T1-T16), feature-level validation PASSED** (single pass, no fix→re-verify iteration needed). Nothing remains but the user's push/PR decision and their own planned manual Stryker run (the automated discrimination sensor was skipped for this feature by the user's explicit standing request, recorded in `tasks.md`'s header).
- **Completed this session (2026-08-19)**, executed as 3 sequential phase-batch sub-agents (Batch 1 = Phases 1+2 / T1-T5, Batch 2 = Phase 3 / T6-T10, Batch 3 = Phases 4+5 / T11-T16), each following implement.md's per-task cycle (implement → gate → atomic commit):
  - **Batch 1** (`c9d7df8`..`bcbb9aa`): `FactResolution.Heuristic`/`Candidate` + `FactMerger.ResolutionRank` (T1), `RelationPartition.Structural` + `RelationProjector` wiring (T2), `FactValidator`'s evidence/provenance gate loosened from `IsRuntime` to compile-time-only-kind exclusion (T3), `SyntacticRelationCandidate` extended with a real evidence span + `FactResolution` and broadened `OwnerId` to `FactId` (T4), `RelationNoiseFilter` shared BCL/framework exclusion predicate (T5). This batch's process was interrupted once mid-T4 by a harness restart; resumed the same sub-agent from its preserved transcript rather than restarting — it correctly picked up the in-flight diff and finished T4+T5 without redoing T1-T3. Final `FactResolution` order: `Exact, Partial, Syntactic, Heuristic, Candidate, Unresolved, NotApplicable`. T1-T3 each needed touching 1-2 files beyond their literal task `Where` list as unavoidable direct consequences (an exhaustive switch, the schema-sync contract test) — not scope creep, confirmed by the Verifier.
  - **Batch 2** (`7cb2221`..`965bfe5`): all 5 syntax-only candidate-detection passes in `SyntaxFactExtractor.Extract` — `inherits`/`implements` split via position + `I`-prefix heuristic (T6), `publishes`/`subscribes`/`handles` (T7), `http-client`/`http-call` (T8), `calls`/`creates` via `RelationNoiseFilter` (T9), `references` from the already-computed `RelevantTypeReferences` (T10). T9 fixed a real dispatch bug surfaced against a spec.md edge case (a `Publish`/`Subscribe`-named call with no extractable target was falling through to the `calls` fallback instead of emitting nothing).
  - **Batch 3** (`c11a04f`..`9ea6d81`): `RelationCollector.CreateFacts` candidate→fact materialization (T11), wired into `AnalysisEngine.AnalyzeProjectAsync` for syntax-only mode (T12), `RelationCollector.Refine` semantic enrichment sharing `RelationFactId`s with `CreateFacts` (T13), wired into `TrustedSemanticProjectProcessor.BindDocuments` for trusted mode (T14), retired `MessagingRelationDetector`/`HttpRelationDetector` + their tests now that the replacement path is proven (T15), end-to-end fixture proof against `fixtures/SyntheticSolution` closing all 17 RELC-NN requirement IDs to `Status: Verified` in `spec.md`'s traceability table (T16).
  - **Independent Verifier (fresh sub-agent, author ≠ verifier) → PASS.** 17/17 ACs spec-anchored with `file:line` evidence (exact target_text values, exact resolution enum members, exact partition mapping — not presence-only checks), 0 spec-precision gaps. Discrimination sensor skipped per the standing user request (not run, not applicable to this PASS). Gate: 1207 passed, 0 unexpected failures. Independently re-confirmed (not taken on trust): both retired detectors and their tests are fully deleted (`git grep` clean outside doc comments); the fixture scenario that originally reported `relations: []` now produces real relations (`V3DeterminismTests`' re-approved snapshot: `relation_count: 0 → 9`). `dotnet build -c Release` 0 warnings/errors, `dotnet format --verify-no-changes` clean. Report: `.specs/features/relation-collector/validation.md`. `validate_state.py relation-collector` → exit 0.
  - **One disclosed, non-blocking finding from the Verifier:** `RelationNoiseFilter.IsFrameworkType(ITypeSymbol)` (T5's semantic-path method) is dead production code — called only from its own unit test. `design.md`/T13 described `Refine` applying it to `calls`/`creates` semantic filtering, but the shipped `Refine` (`RelationCollector.cs:80-109`) only processes `inherits`/`implements` and inferred-publish discovery. Does not fail any AC — RELC-16 only mandates the syntax-only predicate (`IsLikelyFrameworkType`), which is correctly wired. Left as an explicit open follow-up: either wire `IsFrameworkType` into `Refine`'s `calls`/`creates` handling, or strike the design.md sentence and delete the unused method. No lesson recorded for this (doesn't fit any of `lessons.py`'s five grounded signal types — a clean PASS with no AC gap).
  - **Two pre-existing, disclosed, order-dependent flaky tests, confirmed unrelated to this feature (untouched files, pass 1/1 in isolation, reproduce on an unmodified baseline too):** `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` (temp-file-cleanup race) and `SourceGeneratorAdapterTests.FailingGenerator_RetainsPreGeneratorCompilationAndAddsScopedDiagnostic`. Not acted on here, per the Scope Guardrail.
  - **Post-Verifier fix, found by the user running the packaged tool against their own real codebase (not a fixture):** `RelationCollector.CreateFacts` crashed with `ArgumentException: The value must be a canonical single-line value. (Parameter 'claimFingerprint')` on an `http-call` candidate whose non-literal `route` argument was a multi-line object-initializer expression (`SyntaxFactExtractor` captures such arguments' full source text verbatim). `ClaimFor` (`RelationCollector.cs`) joined that raw text straight into the fingerprint passed to `RelationFactId.Create`, and `FactIdGrammar.RequireCanonicalText` rejects `\r`/`\n`/`\t`/double-spaces/leading-trailing whitespace. Fix: `ClaimFor` now runs its joined string through a new `Canonicalize` helper (collapse any whitespace run to one space, then trim) before it becomes the `claimFingerprint` — the persisted `route` detail's `Value` is intentionally left as the original raw text; only the identity fingerprint is normalized. Regression test added: `RelationCollectorTests.CreateFacts_HttpCallRouteSpansMultipleLines_CanonicalizesTheClaimFingerprintInsteadOfThrowing`. Commit: `<pending>`. **Not yet investigated further** (deliberately deferred, per the user's own request for the fastest fix first): whether a non-literal `route` should capture something narrower than the full argument expression text in the first place (T8's detection) is still open — flagged for a future look, not resolved here.
- **In-progress** (file:line): none. Feature is done; the post-Verifier fix above is a follow-up bug fix, not new scope.
- **Next step**: None for this feature's implementation. Two things remain, both requiring the user: (1) run Stryker manually for the mutation-testing pass they deferred from the automated Verifier; (2) push `feat/relation-collector` (cut from `feat/markdown-cleanup`'s tip `4a6783a`) and open the PR — not yet given, not yet requested, per the standing blast-radius rule (push/PR/deploy always need explicit per-action go-ahead regardless of prior approvals). Optionally worth a look later: whether `http-call`'s `route` capture should be narrowed for non-literal arguments (see the post-Verifier fix note above).
- **Blockers**: None.
- **Uncommitted files**: `.specs/STATE.md` (this handoff update, plus the still-pending AD-015 append from Design — already present in the working tree before this session started). Unrelated, still out of scope: `.agents/`, `.claude/`, `.cursor/`, `.windsurf/`, `.specs/features/csharp2md-v3/context.md`, `.specs/features/relation-collector/context.md` and `design.md` (untracked — Design-phase artifacts, never committed this session; only `spec.md`, `tasks.md`, and the new `validation.md` were committed/added by Execute), both dated Markdown files, `AGENTS.md`, `CLAUDE.md`, `fixtures/launch-manifest.json`, `research/`, `src/Csharp2Md.Cli/Properties/`.
- **Branch**: `feat/relation-collector`, cut from `feat/markdown-cleanup` at `4a6783a`, HEAD `9ea6d81` plus the pending post-Verifier fix commit. Not pushed to any remote.

## Historical Handoff — Markdown Cleanup v3.0.1

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
