# Active state

## Status

The repository is preparing a full architectural replacement. Existing source code and wire schemas are legacy until the roadmap is executed.

Workstream 1, [`knowledge-taxonomy-contract`](features/knowledge-taxonomy-contract/spec.md), is complete, verified and on `master`.

Workstream 2, [`engine-bootstrap`](features/engine-bootstrap/spec.md), is complete, verified and merged to `master` (`8a93121`, PR #7).

Workstream 3, [`factual-storage`](features/factual-storage/spec.md), is complete, verified and on `feat/factual-storage` (`4c4948b`). Verifier report: `.specs/features/factual-storage/validation.md` (PASS, 801 tests).

Workstream 4, [`roslyn-observation-extraction`](features/roslyn-observation-extraction/spec.md), is complete, verified and on `feat/roslyn-observation-extraction` (`65fa91a`). Verifier report: `.specs/features/roslyn-observation-extraction/validation.md` (PASS, 930 tests).

Workstream 5A, [`entrypoints-boundaries-contracts`](features/entrypoints-boundaries-contracts/spec.md), is complete, verified and on `feat/entrypoints-boundaries-contracts` (`e0cf903`). Verifier report: `.specs/features/entrypoints-boundaries-contracts/validation.md` (PASS, 1019 tests). Next: classifier workstreams 5B–5D, created only when explicitly started.

Normative documentation:

- [`CONTEXT.md`](../CONTEXT.md)
- [`docs/architecture/`](../docs/architecture/README.md)
- [`architecture-knowledge-engine-roadmap.md`](../architecture-knowledge-engine-roadmap.md)

## Decisions

### AD-001 — Standardized generator-owned taxonomy

- **Decision**: csharp2md owns a machine-readable, versioned taxonomy. Structural facts, immutable observations, classified facts, confirmed relations and derived projections are separate contracts.
- **Reason**: the legacy flat relation model mixed structure, behavior, protocol and confidence, producing broad and ambiguous links.
- **Status**: active.

### AD-002 — No compatibility constraint during replacement

- **Decision**: the replacement may break CLI, output, schemas, IDs and layout. Continuity begins only at the first completed post-migration release.
- **Reason**: the product is not in use and will only be consumed after migration; compatibility work would preserve contracts scheduled for deletion.
- **Status**: active.

### AD-003 — Roslyn and trust boundary

- **Decision**: target `net10.0` and `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0; do not reference `Microsoft.Build.*` or call `MSBuildLocator.RegisterDefaults()`. Semantic analysis requires trusted input, source generators require separate opt-in, and diagnostic analyzers never run.
- **Reason**: Roslyn 4.9+ uses the out-of-process BuildHost, while MSBuild evaluation and generators can execute repository-controlled code.
- **Status**: active.

### AD-004 — Evidence before promotion

- **Decision**: extractors publish immutable observations. Versioned classifiers are the only modules that promote observations into architectural/persistence facts or confirmed causal relations, always with `derived_from` and rejected-candidate traceability.
- **Reason**: classification at extraction time made broad heuristics indistinguishable from direct evidence.
- **Status**: active.

### AD-005 — Business interpretation is downstream

- **Decision**: the generator does not produce business-rule or decision facts. It publishes complete relevant code, flows' direct technical ingredients, contracts, configuration and persistence evidence for a downstream LLM.
- **Reason**: conditions such as `balance < amount` require contextual interpretation rather than a universal static taxonomy.
- **Status**: active.

### AD-006 — Deep modules and storage seam

- **Decision**: the target assemblies are `Csharp2Md.Domain`, `Csharp2Md.Analysis`, `Csharp2Md.Storage`, `Csharp2Md.Projection` and `Csharp2Md.Cli`. Analysis exposes one high-level interface and writes through a transactional storage port; CLI contains no taxonomy or classification logic.
- **Reason**: the legacy core mixed orchestration, Roslyn, persistence, validation and presentation.
- **Status**: active.

### AD-007 — Directly navigable factual package

- **Decision**: the generator publishes a manifest-led package of facts, observations, confirmed relations, candidates, diagnostics, compact postings, Markdown and source locators. Markdown is projection, never authority.
- **Reason**: an LLM must be able to navigate generated files directly without a database or mandatory query engine.
- **Status**: active.

### AD-008 — One to many solutions, isolated semantics

- **Decision**: one invocation accepts `1..N` explicit solutions. Each solution is analyzed and committed independently; global composition contains only catalog identities and proven external relations.
- **Reason**: merging solutions into one Roslyn universe creates ambiguous compilation contexts and unbounded memory.
- **Status**: active.

### AD-009 — Coverage and certification

- **Decision**: run coverage and labeled-corpus classifier quality are independent. Mandatory areas are entry points, linked calls, contracts and persistence; precision/recall claims require ground truth.
- **Reason**: a production run cannot truthfully report recall for an unknown universe.
- **Status**: active.

### AD-010 — Proof states, not numeric confidence

- **Decision**: publish `evidence_method`, `resolution` and `frontier` as independent axes. Candidates stay outside the confirmed graph; there is no required numeric confidence score.
- **Reason**: a numeric score would imply calibration not supported by deterministic evidence.
- **Status**: active.

### AD-011 — Query and wiki layers are deferred

- **Decision**: query engines, `kb`, QMD, embeddings, wiki compilation and LLM context packaging are outside the generator replacement. A query layer may later consume the completed package without redefining taxonomy.
- **Reason**: the initial product must first produce complete, precise and directly navigable evidence.
- **Status**: active.

### AD-012 — Documentation and implementation migration

- **Decision**: the roadmap is split into bounded specs. Legacy specs, tickets and superseded decision prose are removed from the working tree; Git is their history. The first new spec is not created as part of documentation replacement.
- **Reason**: retaining executable-looking legacy plans confuses agents and spends context on contracts that will be deleted.
- **Status**: active.

### AD-013 — Domain-declared taxonomy registry, enforced at construction

- **Decision**: `Csharp2Md.Domain` declares the taxonomy as ordered descriptor tables that are the single authority for fact families, fact types, observation kinds, facet axes, relation triples, minimum evidence methods, mapping roles, proof-state axes and version axes. The same tables back construction-time rejection and are projected to a committed artifact, `contracts/taxonomy-registry.json`, guarded by a byte-comparison drift gate. Workstreams 2 through 8 validate against the registry instead of restating it.
- **Reason**: enforcement and published contract read one source, so they cannot drift; a declarative table is reviewable as a whole, unlike a taxonomy scattered across attributes; and an ordered projection makes the drift gate a deterministic byte comparison.
- **Trade-off**: the descriptor tables and the CLR record types are two representations that must agree, which costs one reflection-based bijection test and some up-front declaration code. A reflection-derived registry would have avoided that at the price of unstable emission order and a non-reviewable taxonomy.
- **Scope**: `Csharp2Md.Domain` and every consumer of the taxonomy — workstreams 2 through 8, including storage validation and all classifier workstreams.
- **Date**: 2026-08-24
- **Status**: active.

### AD-014 — Hexagonal storage port owned by Analysis

- **Decision**: The transactional storage port is declared on `Csharp2Md.Analysis`'s public surface. `Csharp2Md.Storage` implements it and may reference Analysis. Analysis never references Storage, Projection or CLI. `Csharp2Md.Projection` is a compile-only assembly until workstream 6 because the Analysis↔Projection prohibition plus the five-assembly solution list leave no legal place for a projector port.
- **Reason**: ENG-11 already places the port on Analysis; hexagonal adapters depend on application ports; putting the port in Domain would mix persistence into the taxonomy (AD-006, AD-013).
- **Trade-off**: Storage depends on Analysis, which looks inverted until the port's home is known. Workstream 6 must introduce a projector seam without a shared abstractions assembly.
- **Scope**: Analysis, Storage, Projection, CLI, and every later workstream that writes through the port or fills a pipeline stage.
- **Date**: 2026-08-25
- **Status**: active.

### AD-015 — ConfirmedRelation construction carries shape and evidence

- **Decision**: `ConfirmedRelation.Create` requires `EvidenceMethod` and, when the registered shape needs them, materialized source and target facts, and invokes `RelationShapeGuards` from that method. `FactReference` remains identity-only.
- **Reason**: identity-only `Create` left TAX-46/50/51/53 unenforceable through the public API; Domain still has no production consumers, so the break is free (AD-002).
- **Trade-off**: callers that only have identities cannot construct shape-constrained relations until they materialize facts. That is the point.
- **Scope**: `Csharp2Md.Domain` and every future classifier that constructs confirmed relations (workstreams 4, 5A–5D).
- **Date**: 2026-08-25
- **Status**: active.

### AD-016 — Storage reconstructs through Domain

- **Decision**: `Csharp2Md.Storage` and `Csharp2Md.Analysis` may reference `Csharp2Md.Domain`. Storage maps wire JSON through Domain `Create` at commit and read. Storage does not classify, promote or invent facts. CLI still has no Domain reference. The write port stays on Analysis (AD-014).
- **Reason**: a last-gate opaque byte store cannot enforce TAX-80, the registry or relation shape. Dual mappers (Analysis JSON plus Storage JSON) would drift from AD-013.
- **Trade-off**: Storage exposing Domain types on its reader is the seam workstream 6 consumes. Bootstrap tests that forbade a Storage→Domain reference are replaced.
- **Scope**: Analysis, Storage, CLI isolation tests, and every later workstream that stages snapshots or reads packages.
- **Date**: 2026-08-25
- **Status**: active.

### AD-017 — Snapshot carries package diagnostics

- **Decision**: `FactualSnapshot` includes operational diagnostics and suspected-secret evidence. Pipeline stages append them; Persistence stages the snapshot; Storage maps them into `diagnostics.json`. Stages never write the envelope themselves.
- **Reason**: workstream 3 left the diagnostics envelope empty; this workstream must name missing projects, compile failures, unsupported documents and suspected secrets in the committed package; the write port is the only legal path into that envelope. Workstreams 5A–5D will use the same slot.
- **Trade-off**: the snapshot grows beyond taxonomy records. Diagnostics stay an Analysis/Storage package contract, so Domain still has no `DiagnosticRecord` type.
- **Scope**: `FactualSnapshot`, Storage `DomainMapper`, and every later pipeline stage that records diagnostics.
- **Date**: 2026-08-25
- **Status**: active.

### AD-018 — SymbolFacet.Abstract

- **Decision**: Domain extends `SymbolFacetSet` with `Abstract`; `SymbolFactEmitter` emits it for abstract methods and interface members so 5B+ classifiers can distinguish abstract/interface targets without re-entering Roslyn.
- **Reason**: `InvokesPass` must stay Roslyn-free. A Domain facet gives classifiers a precise, testable flag instead of a fragile signature heuristic.
- **Trade-off**: a small Domain enum/table extension versus keeping Roslyn in the classifier.
- **Scope**: Csharp2Md.Domain, SymbolFactEmitter, InvokesPass and later classifiers
- **Date**: 2026-08-26
- **Status**: active.

## Standing engineering constraints

- Retrieval-led reasoning is mandatory for .NET/Roslyn work; never invent a Roslyn API.
- Source and factual content must be deterministic independently of absolute clone path and input order.
- Structural corruption aborts atomic publication; legitimate unknowns and candidates do not.
- Secrets are never duplicated into facts, observations, indexes or diagnostics.
- The tlc-spec-driven discrimination sensor remains skipped; the user runs Stryker manually. All other verifier steps remain required when feature execution begins.
- The only versioned analysis fixture is `fixtures/SyntheticSolution`. `fixtures/eShop` and `fixtures/eShopOnContainers` are local clones (gitignored). After each feature Verifier, run `LocalCorpus` analyze tests when those clones exist; skip when they do not. Never add those apps to git.

## Handoff

- **Feature**: `call-linking-flow-frontiers` — `.specs/features/call-linking-flow-frontiers/`
- **Phase / Task**: Execute complete (T1–T10). Verifier not yet run. `validation.md` does not exist.
- **Completed**: T1 `b05dd17`, T2 `bb15dd6`, T3 `e80c432`, T4 `8edc6e5`, T5 `95b1d01`, T6 `fd122ec`, T7 `976827a`, T8 `cb6b03d`, T9 `85731a1`, T10 this commit (AD-018 + Handoff + Build gate). Gate: Domain 549, Analysis 330, Storage 166, Cli 27, Projection 3 (1075, `Category!=LocalCorpus`).
- **In-progress** (file:line): none.
- **Next step**: Orchestrator dispatches the Verifier. Do not claim Verifier PASS. Discrimination sensor skipped (standing). Run LocalCorpus only if `fixtures/eShop` or `fixtures/eShopOnContainers` are present.
- **Blockers**: none. `validate_state.py` deferred to Verifier (no `validation.md` yet). `fixtures/eShop` and `fixtures/eShopOnContainers` are absent; LocalCorpus skipped. Discrimination sensor remains skipped. Carry-forward: Full gates exclude `Category=LocalCorpus`. Multi-csproj `dotnet test` hits MSB1008 — run the five test projects separately. CLI filter uses VSTest `--filter "Category!=LocalCorpus"`.
- **Uncommitted files**: unrelated `AGENTS.md` / `CLAUDE.md` / `architecture-knowledge-engine-roadmap.md` / `docs/architecture/README.md`; untracked `.specs/features/entrypoints-boundaries-contracts/design.md`.
- **Branch**: `feature/call-linking-flow-frontiers`
