# Architecture Knowledge Engine roadmap

## Status

This roadmap replaces every prior csharp2md/LLMWiki implementation queue. It defines ordering only; it does not create a feature spec or authorize implementation by itself.

The target architecture is documented in [`docs/architecture/`](docs/architecture/README.md). Workstreams 1 through 8 are complete. Workstream 8 (final CLI, corpora, coverage gates, performance and migration completion) closed on its Verifier's iteration-3 report with two known Major gaps deferred to a follow-up workstream (AD-028 in `.specs/STATE.md`) rather than a clean PASS — see `.specs/STATE.md`'s Handoff and `generator-cli-projections-certification/context.md`'s Deferred Ideas.

## Delivery rules

- Each row becomes its own `.specs/features/<feature>/` only when explicitly started through `tlc-spec-driven`.
- Workstreams 1 through 5D are complete; the feature in progress is `retrieval-projections`.
- A feature reads the normative architecture, active `.specs/STATE.md`, its own spec and only its declared contract dependencies.
- No feature preserves the old CLI, IDs, schemas, relation kinds or output layout.
- The replacement may be functionally broken between checkpoints, but every integrated checkpoint must compile and validate its implemented invariants.

## Dependency graph

```text
knowledge-taxonomy-contract
  -> engine-bootstrap
    -> factual-storage
      -> roslyn-observation-extraction
        -> entrypoints-boundaries-contracts -----------+
        -> call-linking-flow-frontiers ----------------+
        -> persistence-knowledge ----------------------+-> retrieval-projections
        -> components-deployments-configuration -------+        |
                                                               v
                                                multi-solution-composition
                                                               |
                                                               v
                                         generator-cli-projections-certification
```

The four classifier workstreams may proceed in parallel after the observation contract is stable.

## Workstreams

| Order | Feature | State | Outcome |
| ---: | --- | --- | --- |
| 1 | `knowledge-taxonomy-contract` | Complete on `master` | Domain types, facets, relation matrix, IDs, proof states, registry and version axes |
| 2 | `engine-bootstrap` | Complete on `master` (PR #7) | New assemblies and compilable pipeline skeleton; legacy pipeline, CLI contracts and taxonomic code removed |
| 3 | `factual-storage` | Complete on `master` | Observation/fact wire contracts, schemas, transactional staging/commit, validation and compact storage primitives |
| 4 | `roslyn-observation-extraction` | Complete on `master` | Inventory, structural facts, source fidelity, Roslyn binding and the immutable observation ledger |
| 5A | `entrypoints-boundaries-contracts` | Complete on `master` (PR #10) | Entry points, HTTP/gRPC/messaging/CLI/jobs/functions, contracts and revisions |
| 5B | `call-linking-flow-frontiers` | Complete on `master` (PR #11) | Confirmed calls, in-process dispatch, polymorphism, candidates and open frontiers |
| 5C | `persistence-knowledge` | Complete on `master` (PR #12) | Data stores, objects, fields, operations, EF/SQL mappings and persistence coverage |
| 5D | `components-deployments-configuration` | Complete on `master` (PR #14) | Components, deployment units, DI/options/clients/configuration and secure overrides |
| 6 | `retrieval-projections` | In progress | Directly navigable catalogs, postings, source locators, Markdown and retrieval scenarios |
| 7 | `multi-solution-composition` | Blocked by 6 | `1..N` isolated solution outputs, batch manifest and proven global correlations |
| 8 | `generator-cli-projections-certification` | Closed (AD-028: two Major gaps deferred, see below) | Final analyze/validate/compose CLI, corpora, coverage gates, performance and migration completion |

## Completion

The replacement is complete only when:

- legacy pipeline, taxonomy, schemas, CLI and tests are gone;
- the machine-readable registry and every generated artifact agree;
- the four coverage areas and both certification types are published correctly;
- labeled corpora meet the approved precision/recall thresholds;
- `custom` completes without structural corruption and within measured scale budgets;
- generated files support the documented retrieval scenarios without a query engine;
- `1..N` solution analysis and composition are deterministic;
- the repository documents only the new contract.

Workstream 8's Execute phase (T1–T66, plus fix rounds T67–T75) closes every condition above: the legacy
pipeline and taxonomy were already gone by workstream 2; the registry drift gate stays green through the
`symbol-facet` extension; run certification and the four coverage areas publish computed content with no
`0/0` (P1 "Certified execution"); engine certification measures precision and recall against
independently authored labeled corpora with a normative-threshold gate (P1 "Engine certification"); the
certification corpus's own `analyze` completes without structural corruption and the generated
over-ceiling scale input proves the derived byte ceiling holds (T63); the documented retrieval scenarios
execute automatically against the published package with no query engine (P1 "Executable retrieval
guide"); and whole-package determinism plus batch isolation and certification are proven across runs,
absolute paths, input order and multi-solution batches (T60–T62).

The feature's Verifier ran three fix→re-verify iterations (the skill's bound); iteration 3 still found two
pre-existing Major gaps — computed invocation/contract/document-policy envelopes that never cross the
Storage boundary onto the wire (GCPC-012/016/034/088), and a fabricated self-referencing `invokes`
candidate (GCPC-018) — plus a minor fixture-reproducibility gap and a cosmetic one. Per AD-028 in
`.specs/STATE.md`, the user chose to close workstream 8 now rather than take a 4th round, deferring these
to a future workstream. `generator-cli-projections-certification/context.md`'s Deferred Ideas carries the
root cause and fix-task shape for each.

## Workstream 8 follow-up (not yet started)

Correctness debt inside the generator's own scope, deliberately deferred rather than fixed in workstream
8 (AD-028). Not a new roadmap row — starts as its own `.specs/features/<name>/` through Specify only when
explicitly picked up, per the Delivery rules above and CLAUDE.md's standing rule against pre-creating a
feature spec before its workstream starts.

- Publish the invocation-accounting, contract-accounting and document-policy envelopes onto the wire
  (GCPC-012, GCPC-016, GCPC-034, GCPC-088) — currently computed and consumed only in-memory by
  `RunCertifier`, never mapped by `DomainMapper`.
- Stop `InvokesPass.ConcreteImplementors` from publishing a candidate to a non-implementing symbol
  (GCPC-018) — a fabricated-fact defect, the same class the generator exists to eliminate.
- Make `fixtures/SyntheticSolution`'s immutability digest reproducible from a clean checkout (GCPC-117).
- Publish the ceiling's bytes-per-token ratio and the largest-artifact-per-role measurement as their own
  fields rather than leaving them only derivable (GCPC-037, GCPC-045).

Full root cause and fix-task shape (What/Where/Verify/Done-when) for each: `.specs/features/generator-cli-projections-certification/context.md`'s Deferred Ideas, and `validation.md`'s Fix Plans (iteration 3).

## Deferred after generator completion

- optional query layer;
- incremental analysis;
- snapshot diff;
- wiki/`kb`/QMD integration;
- embeddings and semantic search;
- business-rule interpretation;
- semantic support for other languages;
- dynamic plugin loading.

## Legacy disposition

This compact map preserves traceability without retaining obsolete executable specifications.

| Legacy feature/ticket | Disposition | New owner |
| --- | --- | --- |
| `csharp2md`, `csharp2md-llmwiki-phase1`, `csharp2md-v3` | Superseded; useful infrastructure selectively ported | Workstreams 2–6 |
| `cli-directory-input`, `markdown-cleanup` | Superseded | Workstreams 2 and 6–8 |
| `symbol-index` | Absorbed | Workstream 4 |
| `relation-collector`, `relation-resolver` | Superseded; semantic binding selectively ported | Workstreams 4 and 5B |
| `data-access-discovery` | Absorbed with new contracts | Workstream 5C |
| `relation-retrieval-index`, `compact-retrieval-index` | Absorbed as physical/indexing requirements | Workstreams 3 and 6 |
| `component-graph` | Superseded; component/deployment intent retained | Workstreams 5D and 6 |
| `relation-resolver-candidate-fix` | Superseded; case retained as a negative contract/event fixture | Workstream 5A |
| `detection-tree-revival` | Superseded; orphaned tree will be removed | Workstreams 4 and 5A–5D |
| `relation-resolver-p2-http-events` | Absorbed with boundary-operation semantics | Workstream 5A |
| `relation-resolver-p2-di-grpc-aspnet` | Split and absorbed | Workstreams 5A, 5B and 5D |
| `relation-resolver-p3` | Absorbed as promotion audit trail; numeric confidence rejected | Workstreams 3–5 |
| `configuration-index` | Absorbed | Workstreams 5D and 6 |
| `solution-scoped-analysis-composition` | Absorbed | Workstream 7 |
| `raw-snapshot-diff` | Deferred | Post-generator roadmap |
| Wiki ingest, flows-as-pages and business-rule pages | Out of generator | Downstream LLM/wiki layer |
