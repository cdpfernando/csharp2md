# Active state

## Status

The repository is preparing a full architectural replacement. Existing source code and wire schemas are legacy until the roadmap is executed.

Workstream 1, [`knowledge-taxonomy-contract`](features/knowledge-taxonomy-contract/spec.md), is the first replacement feature. Its spec, design and task breakdown are approved; execution has not started.

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

## Standing engineering constraints

- Retrieval-led reasoning is mandatory for .NET/Roslyn work; never invent a Roslyn API.
- Source and factual content must be deterministic independently of absolute clone path and input order.
- Structural corruption aborts atomic publication; legitimate unknowns and candidates do not.
- Secrets are never duplicated into facts, observations, indexes or diagnostics.
- The tlc-spec-driven discrimination sensor remains skipped; the user runs Stryker manually. All other verifier steps remain required when feature execution begins.

## Handoff

- **Feature**: `knowledge-taxonomy-contract` — `.specs/features/knowledge-taxonomy-contract/`
- **Phase / Task**: Tasks complete and approved; Execute not started. No task is in progress and no implementation file exists yet.
- **Completed**: Specify (spec.md, 91 requirements, closure gate clean), Discuss (context.md, 4 decisions), Design (design.md, approved), AD-013 recorded, Tasks (tasks.md, 54 tasks across 11 phases, all 91 requirements mapped, `validate_tasks.py` clean).
- **In-progress** (file:line): none.
- **Next step**: Run Execute starting at T1. The 54 tasks pack into nine task-budgeted batches on whole-phase boundaries — `P1+P2` (5), `P3` (4), `P4` (7), `P5` (6), `P6` (5), `P7` (6), `P8` (7), `P9` (7), `P10+P11` (7) — so present the sub-agent offer before dispatching.
- **Blockers**: none. The four former spec assumptions are now fixed by the approved tasks: registry path `contracts/taxonomy-registry.json` (T49), test project `tests/Csharp2Md.Domain.Tests` (T2), all five version axes starting at 1 (T22), identity determinism proven by synthetic-input unit tests (T9).
- **Task-phase refinements to design.md** (deliberate, not deviations): `FacetAxes` owns the CLR-to-wire pairing and axis value lists and the registry declares its axis descriptors from them, breaking the design's mutual Facets↔Registry dependency; `TaxonomyTables` is a copyable value so TAX-88/89 and TAX-90 can be proven against an injected table; requirement traceability is mechanical via a `[Trait("Requirement", "TAX-nn")]` convention gated by T54.
- **Standing quality gate**: every phase-end task (T2, T5, T9, T16, T22, T27, T33, T40, T47, T51, T54) runs `dotnet-skills:slopwatch` over that phase's changes, by user request.
- **Uncommitted files**: `.specs/STATE.md`, `architecture-knowledge-engine-roadmap.md`, `.specs/features/knowledge-taxonomy-contract/spec.md`, `.specs/features/knowledge-taxonomy-contract/context.md`, `.specs/features/knowledge-taxonomy-contract/design.md`, `.specs/features/knowledge-taxonomy-contract/tasks.md`
- **Branch**: `codex/architecture-knowledge-engine-docs`
