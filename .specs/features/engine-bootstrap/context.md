# Engine Bootstrap Context

**Gathered:** 2026-08-25
**Spec:** `.specs/features/engine-bootstrap/spec.md`
**Status:** Ready for design

---

## Feature Boundary

Workstream 2 of the architecture-knowledge-engine roadmap. It creates the target assembly topology (`Csharp2Md.Analysis`, `Csharp2Md.Storage`, `Csharp2Md.Projection`, plus a rewritten `Csharp2Md.Cli`) around the completed `Csharp2Md.Domain`, wires an executable eight-stage pipeline skeleton behind one analysis facade and a transactional storage port, and removes the legacy pipeline, CLI contract and taxonomic code.

It contains no Roslyn, no MSBuild, no wire schema and no on-disk package format. Inventory and Roslyn binding belong to workstream 4; wire contracts, physical storage and commit belong to workstream 3.

---

## Implementation Decisions

### How alive the skeleton is

- A **walking skeleton**, not a compile-only one: `analyze` executes all eight pipeline stages end to end for every requested solution and exits 0.
- Every stage is a stub that produces zero facts, zero observations and zero relations, and says so in the run summary.
- Publication goes through a real transactional storage port backed by an **in-memory adapter**. Nothing is written to disk.
- Rejected: writing a provisional manifest to disk, because that would define a package layout workstream 3 owns and would likely rewrite.
- Rejected: a compile-only skeleton, because a skeleton that never executes cannot validate the seam invariants the roadmap requires of an integrated checkpoint.
- Rejected: pulling a real Inventory stage forward, because the roadmap assigns inventory to workstream 4.
- The invariants this skeleton exists to prove are therefore: stage order, stage substitutability, cancellation, staging/commit/abort through the port, manifest-published-last ordering, `1..N` solution isolation, order-independent determinism, and assembly-boundary purity.

### Legacy removal strategy

- `src/Csharp2Md.Core`, `tests/Csharp2Md.Core.Tests`, `benchmarks/Csharp2Md.RetrievalIndex.Benchmarks`, `schemas/` and the old CLI body are all deleted in this feature.
- A committed **port ledger** records, for each removed area, its former path, a one-line description of what it did, and the last commit SHA in which it existed — so workstreams 4 through 6 can retrieve the Roslyn, MSBuild-evaluation and source-fidelity code they want to port without trawling history blind.
- Rejected: quarantining Core under a `legacy/` folder, because 15,000 lines of dead code stays visible to agents and greps.
- Rejected: porting infrastructure forward now, because that pulls Roslyn into workstream 2.

### Provisional CLI surface

- One verb, `analyze`, with a repeatable `--solution <path>` requiring at least one value. Nothing else.
- Every option the in-memory skeleton cannot honor is omitted rather than parsed-and-ignored: each later workstream adds the options it can actually implement.
- The legacy markdown/topic-era surface (`--topic`, `--domain`, `--manifest`, `--output`, `--trust`, `--include-source-generators`, `--analysis-timeout`) is gone.
- `validate` and `compose` wait for workstream 8.
- The project stays packaged as a dotnet tool with command name `csharp2md`.
- Exit codes: `0` for a completed run, including one that reports unknowns, candidates or open frontiers; `1` for an invalid invocation; `2` for a structural failure that aborted publication. With `1..N` solutions the process exit code reflects the worst individual outcome, and one solution failing never aborts the batch.

### Legacy test and asset disposition

- All 1,093 legacy test attributes are deleted with `Csharp2Md.Core.Tests`. They assert contracts AD-002 explicitly breaks, and keeping any of them would keep a Roslyn dependency in workstream 2.
- The port ledger names the **Roslyn sanitation probes** and the **CLI security-boundary tests** specifically, as items a later workstream must re-establish rather than rediscover the need for.
- `fixtures/SyntheticSolution` survives as input data for workstream 4 onward.
- `schemas/*.json` and the retrieval-index benchmark project are deleted; workstreams 3 and 6 establish their own.
- `Directory.Packages.props` is pruned of versions that lose their last consumer (the Roslyn packages and YamlDotNet). Fixture-only packages stay, because the fixtures that reference them survive.
- `.slopwatch/`, `.testagent/`, `.idea/` and `artifacts/` are untouched — not product code.
- Deleting the legacy suite also disposes of the known-failing `MigrationLedgerTests`, so this feature closes with a fully green solution.

### Carried-forward limitation from workstream 1

- `ConfirmedRelation.Create`'s signature is **widened now** with optional shape-carrying parameters, so `RelationShapeGuards.RequireCallableIfNeeded`, `.RequireLegalTargetShape` and `.RequireSufficientEvidence` gain production call sites and TAX-46, TAX-50, TAX-51 and TAX-53 become reachable through the domain's public relation-construction API.
- Rationale: the domain has zero production consumers today, so the change costs no migration. Deferring it makes workstream 4 or 5B pay for it.
- Rejected: formalizing a caller contract instead, which leaves enforcement to convention.
- Rejected: carrying it forward untouched.
- `contracts/taxonomy-registry.json` must stay byte-identical through this change — the descriptor tables are not being altered, only the construction API.

### Agent's Discretion

None. Every gray area was decided explicitly.

### Declined / Undiscussed Gray Areas → Assumptions

None declined. Five low-stakes items were stated as assumptions and not objected to; all five are recorded in the spec's Assumptions & Open Questions table:

- the CLI stays packaged as a dotnet tool named `csharp2md`;
- `fixtures/SyntheticSolution` survives;
- `schemas/` and the benchmark project are deleted;
- `Directory.Packages.props` is pruned to consumers that remain;
- the exit-code contract is 0 / 1 / 2 as described above.

---

## Specific References

The user accepted the recommended option in all five gray areas without modification, so there are no "I want it like X" overrides. The binding references are the repository's own normative documents: `CONTEXT.md`, `docs/architecture/architecture-knowledge-engine.md` (pipeline stages, module boundaries), `docs/architecture/quality-and-security.md` (degradation vs. abort), `docs/architecture/output-and-retrieval.md` (manifest-last publication), and decisions AD-002, AD-003, AD-006 and AD-008 in `.specs/STATE.md`.

---

## Deferred Ideas

- **Physical package layout and wire schemas** — workstream 3 `factual-storage`.
- **Real Inventory, including authorized-root enforcement and symlink-escape rejection** — workstream 4 `roslyn-observation-extraction`.
- **Roslyn semantic analysis, the BuildHost path and re-established Roslyn viability probes** — workstream 4.
- **`validate` and `compose` verbs, the full option surface and CLI security-boundary tests** — workstream 8 `generator-cli-projections-certification`, with the security-boundary tests possibly earlier alongside real Inventory.
- **Retrieval-index benchmarking** — workstream 6 `retrieval-projections`, once there is something to project.
