# Active state

## Status

Workstreams 1 through 8 have been executed. Workstream 8,
[`generator-cli-projections-certification`](features/generator-cli-projections-certification/spec.md),
closed with the gaps recorded by AD-028. The follow-up
[`analysis-publication-resilience`](features/analysis-publication-resilience/spec.md) closed with a PASS
for its separate publication-resilience scope; it did not reopen the AD-028 gaps. The current
implementation is the operational baseline. The next incompatible replacement is only a proposal; its
implementation has not started.

Each completed feature's spec and Verifier report are the authority on its scope, verdict, test count and
accepted criteria. The Handoff at the end of this file records the latest execution state.

Normative documentation:

- [`CONTEXT.md`](../CONTEXT.md) defines the product language.
- [`docs/specs/pacote-conhecimento-util-e-confiavel.md`](../docs/specs/pacote-conhecimento-util-e-confiavel.md) defines the proposed next product contract.
- This file owns active decisions and execution state.

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

### AD-019 — Projection runs inside the commit, over the wire document

- **Decision**: `Csharp2Md.Storage` declares the projector port and invokes it inside `Commit()`, after `PackageValidator.Validate` and before `PackagePublisher.ToPublicationOrder`. `Csharp2Md.Projection` implements it and references Storage and Domain. Projections are fragments of the same atomic publication; a projection-validation failure raises `PublicationRejectedException` and preserves the prior package byte-identical.
- **Reason**: the projector must see the mapped wire document to cite real canonical keys and shard ordinals, which is what makes a posting resolve in one hop instead of a linear scan. AD-007 requires every artifact reachable from the manifest, so a second post-commit write would leave a window with an incomplete manifest.
- **Trade-off**: changing a projection requires re-running the analysis; there is no `project-only` path. Closes the seam AD-014 deferred to this workstream, at the cost of Analysis staying unable to influence projection.
- **Scope**: `Csharp2Md.Storage`, `Csharp2Md.Projection`, and workstreams 7 and 8.
- **Date**: 2026-08-26
- **Status**: active.

### AD-020 — Symbol carries a declaration locator

- **Decision**: `Symbol` gains a declaration locator (document reference, source span, source hash), filled by `SymbolFactEmitter` from the declaration's own syntax span. When a symbol has several declaring syntax references, the ordinally first one wins.
- **Reason**: catalogs, postings and Markdown all link into source through this locator, and "once selected, a method body is retrieved completely" cannot be honoured without an exact declaration span.
- **Trade-off**: a Domain extension mid-programme, in the AD-018 mould. It is not an identity component, so `contracts/taxonomy-registry.json` stays byte-identical and the AD-013 drift gate is unaffected.
- **Scope**: `Csharp2Md.Domain`, `SymbolFactEmitter`, and every projection that links into source.
- **Date**: 2026-08-26
- **Status**: active.

### AD-021 — Byte-faithful means faithful outside declared redactions

- **Decision**: the `source/` projection reproduces the original bytes exactly, except inside suspected-secret spans, which are replaced by the fixed 17-byte marker `[REDACTED-SECRET]` regardless of the span's length. A redacted artifact declares `redacted`, the ordinal-sorted redacted spans, the sha256 of the original bytes and the sha256 of the published bytes.
- **Reason**: `quality-and-security.md` calls the source projection sensitive and also says redaction occurs only in projections. Redacting silently would break the hash contract; not redacting would publish secrets inside the package.
- **Trade-off**: fidelity is broken by design, but explicitly and auditably. Individual secret values are still never hashed, and the fixed-length marker does not leak the secret's length.
- **Scope**: `Csharp2Md.Projection`, package consumers, and workstream 8's certification.
- **Date**: 2026-08-26
- **Status**: active.

### AD-022 — Source bytes reach the store through a reader, not the snapshot

- **Decision**: `ITransactionalStore.Open` takes an `ISourceDocumentReader` alongside the solution key, and `StagedFragment` gains a lazy form so staging materializes one document at a time. `FactualSnapshot` stays pure data and carries no source bytes. The store requests each document at most once and does not retain it.
- **Reason**: `BoundSolution` is disposed only after `Commit()` returns, so putting the bytes in the snapshot would hold a second full copy of the source alongside Roslyn's. Re-reading from disk inside the store would make `InMemoryTransactionalStore` depend on the filesystem to produce the same artifacts.
- **Trade-off**: the write port grows a non-projection member, so the "the port does not change" invariant becomes "the port gains no projection member". In exchange the peak stays at one document and the snapshot stays inspectable in isolation.
- **Scope**: `Csharp2Md.Analysis` write port, `Csharp2Md.Storage`, and workstreams 7 and 8.
- **Date**: 2026-08-26
- **Status**: active.

### AD-023 — Package layout is planned before it is written

- **Decision**: `LayoutPlanner` computes every artifact key, every record ordinal, the intern tables and the shard split from the validated wire document, before payload writing, projection, manifest building or the reader touch it. All four consume that one plan. `FactualPackageReader` becomes manifest-driven.
- **Reason**: a citation must be shard-aware at the moment it is minted; patching it afterwards is the two-ordinal indirection the audit criticized (design.md's Architecture Overview seam 2).
- **Scope**: `Csharp2Md.Storage.Mapping.LayoutPlanner`, `PackagePublisher`, `ManifestBuilder`, `PublishedPackageView`, `FactualPackageReader`.
- **Date**: 2026-09-10.
- **Status**: active.

### AD-024 — Run coverage, certification and accounting travel on `FactualSnapshot`

- **Decision**: run coverage, the run-certification status and the invocation/contract accounting ledgers are produced by Analysis (`ValidationAndCoverageStage`, pipeline index 4) and ship on `FactualSnapshot`. Storage maps them into `coverage.json` and `run-certification.json` and never computes them.
- **Reason**: only Analysis knows the denominators — recognized occurrences, framework exclusions, policy decisions; Storage computing them would make Storage classify, which AD-006 forbids. Extends AD-017's precedent for diagnostics.
- **Scope**: `Csharp2Md.Analysis.Pipeline.ValidationAndCoverageStage`, `FactualSnapshot`, `Csharp2Md.Storage.Mapping.DomainMapper`.
- **Date**: 2026-09-10.
- **Status**: active.

### AD-025 — `validate` re-hydrates and re-runs the publication-time validators; no second auditor

- **Decision**: `validate` re-hydrates a published package through `FactualPackageReader` and re-runs the exact validators `Commit()` already runs (`PackageValidator`, `ProjectionValidator`). There is no separate package auditor.
- **Reason**: one definition of "valid" cannot drift from itself, and a rule added for publication is enforced on `validate` for free. Detection strength is proven by a corpus of deliberately corrupted packages (one per defect class) rather than by a second implementation.
- **Trade-off**: a validator defect invisible to `Commit()` is equally invisible to `validate` — the accepted cost of removing the separate auditor, mitigated by the corrupted-package corpus.
- **Scope**: `Csharp2Md.Storage.FactualPackageReader`, `Csharp2Md.Storage.Validation.PackageValidator`, `ProjectionValidator`, `Csharp2Md.Cli.CommandFactory`.
- **Date**: 2026-09-10.
- **Status**: active.

### AD-026 — `fixtures/CertificationCorpus` joins `fixtures/SyntheticSolution` as a versioned fixture

- **Decision**: `fixtures/CertificationCorpus` is a second versioned analysis fixture, amending the standing constraint that `fixtures/SyntheticSolution` is the only one. `fixtures/SyntheticSolution` stays byte-identical — the new corpus is a sibling tree, not a modification of the existing one.
- **Reason**: D-01. A new, versioned, C#-only corpus reproduces every audit regression (B1–B5, I1–I5) and runs in CI as the mandatory gate, while the eShop/eShopOnContainers clones stay optional (`LocalCorpus`) since they are gitignored and unreproducible in CI.
- **Scope**: `fixtures/CertificationCorpus`, the standing engineering constraints below, every task in `generator-cli-projections-certification` that reads the corpus.
- **Date**: 2026-09-10.
- **Status**: active.

### AD-027 — `derived_from` carries only the evidence that justifies its own promotion

- **Decision**: a structural relation's evidence chain cites the observations that justify that specific promotion, not every observation inside the owning document. `ContainsRelationEmitter`'s per-promotion `EvidenceScope` replaces the document-wide chain it built before.
- **Reason**: design.md F1 measured a `contains` edge citing 449 unrelated observations across `invocation`, `data-access`, `assignment` and `type-usage` — 96.3% of the audited `contains.json`'s bytes and, worse, a correctness defect: an LLM reading `derived_from` was actively misled about what justified the edge.
- **Scope**: `Csharp2Md.Analysis.Classification.EvidenceScope`, `ContainsRelationEmitter`.
- **Date**: 2026-09-10.
- **Status**: active.

### AD-028 — `generator-cli-projections-certification` closes with two known Major gaps deferred to a follow-up workstream

- **Decision**: the feature closes on iteration 3's FAIL report (`validation.md`) rather than taking a 4th fix→re-verify round. The four gaps iteration 2 routed (GCPC-004, GCPC-038, GCPC-057, GCPC-061, GCPC-069/070) are confirmed genuinely closed. The two Major gaps iteration 3's independent re-derivation surfaced — GCPC-012/016/034/088 (three computed accounting/policy envelopes never cross the Storage boundary onto the wire) and GCPC-018 (`InvokesPass.ConcreteImplementors` publishes a fabricated self-referencing candidate) — plus the Minor (GCPC-117 fixture-digest reproducibility) and Cosmetic (GCPC-037/045 derivable-not-published fields) gaps, are explicitly deferred, not silently dropped: each is recorded as a Deferred Idea in `context.md` with root cause and fix-task shape, and `spec.md`'s Requirement Traceability table is updated from `Verified` to `⚠️ Deferred` for every affected ID (GCPC-012, GCPC-016, GCPC-018, GCPC-034, GCPC-037, GCPC-045, GCPC-088, GCPC-117, GCPC-120). GCPC-116 is corrected the other way, from its stale `⚠️ Partial` to `Verified` — F6 closed it for real.
- **Reason**: `validate.md`'s fix→re-verify loop is bounded to 3 iterations before escalating to the user (`sub-agents.md`'s Verifier section, `validate.md` step 8); iteration 3 was that bound. Presented with the choice (authorize a 4th round / accept with a spec amendment / split into a follow-up feature and close), the user chose the third: stop iterating on this feature now, track the remainder as future work.
- **Trade-off**: `python3 .claude/skills/tlc-spec-driven/scripts/validate_state.py generator-cli-projections-certification` will keep exiting 1 for this feature — `validation.md`'s verdict stays the true, unedited iteration-3 FAIL (evidence integrity: the Verifier's own report is never rewritten to a false PASS), so this feature can never pass the skill's deterministic completion gate as-is. That is accepted: the gate's purpose (don't silently call undone work done) is served by the honest FAIL plus this decision record, not defeated by it. A future workstream that wants to close the remaining gaps starts as its own feature (Specify → ... → Execute → Verifier). The standing project rule against pre-creating an executable feature before its workstream starts still applies, so no `.specs/features/<name>/` is created by this decision alone.
- **Scope**: `generator-cli-projections-certification` (closed), its `spec.md`/`context.md`/`validation.md`, and any future workstream that picks up GCPC-012/016/018/034/037/045/088/117.
- **Date**: 2026-09-11.
- **Status**: active.

### AD-029 — Third versioned fixture `fixtures/PublicationResilience`

- **Decision**: `fixtures/PublicationResilience` joins `fixtures/SyntheticSolution` and `fixtures/CertificationCorpus` as a versioned analysis fixture. It is a sibling tree. `fixtures/SyntheticSolution` stays byte-identical. `fixtures/CertificationCorpus` labeled denominators stay untouched: this tree carries no engine-certification labels.
- **Reason**: `analysis-publication-resilience` must prove three publication defects through default-budget `analyze` without silently moving labeled precision/recall (AD-026) or mutating the immutability digest of SyntheticSolution (GCPC-117).
- **Trade-off**: a third committed C# tree costs review and CI time. Putting the cases inside `CertificationCorpus` would have been smaller and would have changed labeled denominators. Generating the disposition volume at test time would have avoided git size but would not be the "versioned fixture" APR-33 requires.
- **Scope**: `analysis-publication-resilience`, the standing engineering-constraint fixture list, and every later workstream that must not treat this tree as labeled ground truth.
- **Date**: 2026-09-14
- **Status**: active.

## Standing engineering constraints

- Retrieval-led reasoning is mandatory for .NET/Roslyn work; never invent a Roslyn API.
- Source and factual content must be deterministic independently of absolute clone path and input order.
- Structural corruption aborts atomic publication; legitimate unknowns and candidates do not.
- Secrets are never duplicated into facts, observations, indexes or diagnostics.
- The tlc-spec-driven discrimination sensor remains skipped; the user runs Stryker manually. All other verifier steps remain required when feature execution begins.
- The versioned analysis fixtures are `fixtures/SyntheticSolution`, `fixtures/CertificationCorpus` (AD-026), and `fixtures/PublicationResilience` (AD-029). `fixtures/SyntheticSolution` stays byte-identical. `fixtures/CertificationCorpus` is the labeled engine-certification tree; `fixtures/PublicationResilience` is an unlabeled publication-regression sibling and is not a labeled-denominator source. `fixtures/eShop` and `fixtures/eShopOnContainers` are local clones (gitignored). After each feature Verifier, run `LocalCorpus` analyze tests when those clones exist; skip when they do not (the skip also applies when the clone directory exists but the expected `.sln`/`.slnx` path inside it is missing). Never add those apps to git.

## Handoff

- **Feature**: `analysis-publication-resilience` — Execute closed PASS. `validate_state.py` 0 errors.
- **Phase / Task**: Post-Verifier LocalCorpus (clones present: eShop, eShopOnContainers, Pitstop). Discrimination sensor standing skip.
- **Completed**: T1–T13 (`a0a9598` … `b717b5c`). Verifier PASS, 42/42 ACs, gate 2134. Lesson L-028 from APR-41 spec-precision.
- **In-progress** (file:line): none.
- **Next step**: commit `validation.md`, `design.md`, lessons, and STATE.md when the user asks. LocalCorpus (optional): 12 passed, 6 failed — see last run.
- **Blockers**: none. APR-41 isolated Pitstop rows wrap each `.csproj` in a temp `.slnx` (clone has one `pitstop.sln`). Recorded as spec-precision, not a FAIL.
- **Carry-forward**: Full gates exclude `Category=LocalCorpus`. Test floor 2134 (Domain 575, Analysis 864, Storage 396, Cli 65, Projection 234).
- **Uncommitted files**: `.specs/STATE.md`; `.specs/LESSONS.md`; `.specs/lessons.json`; `docs/specs/reducao-complexidade-acidental.md`; `?? .specs/features/analysis-publication-resilience/design.md`; `?? .specs/features/analysis-publication-resilience/validation.md`.
- **Branch**: `feature/generator-cli-projections-certification`.
