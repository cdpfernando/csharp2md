# Architecture Knowledge Engine roadmap

## Status

This roadmap replaces every prior csharp2md/LLMWiki implementation queue. It defines ordering only; it does not create a feature spec or authorize implementation by itself.

The target architecture is documented in [`docs/architecture/`](docs/architecture/README.md). Existing code and schemas remain legacy until their owning workstream replaces them.

## Delivery rules

- Each row becomes its own `.specs/features/<feature>/` only when explicitly started through `tlc-spec-driven`.
- The first feature, `knowledge-taxonomy-contract`, has not been created yet.
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
| 1 | `knowledge-taxonomy-contract` | Planned; spec not created | Domain types, facets, relation matrix, IDs, proof states, registry and version axes |
| 2 | `engine-bootstrap` | Blocked by 1 | New assemblies and compilable pipeline skeleton; legacy pipeline, CLI contracts and taxonomic code removed |
| 3 | `factual-storage` | Blocked by 2 | Observation/fact wire contracts, schemas, transactional staging/commit, validation and compact storage primitives |
| 4 | `roslyn-observation-extraction` | Blocked by 3 | Inventory, structural facts, source fidelity, Roslyn binding and the immutable observation ledger |
| 5A | `entrypoints-boundaries-contracts` | Blocked by 4 | Entry points, HTTP/gRPC/messaging/CLI/jobs/functions, contracts and revisions |
| 5B | `call-linking-flow-frontiers` | Blocked by 4 | Confirmed calls, in-process dispatch, polymorphism, candidates and open frontiers |
| 5C | `persistence-knowledge` | Blocked by 4 | Data stores, objects, fields, operations, EF/SQL mappings and persistence coverage |
| 5D | `components-deployments-configuration` | Blocked by 4 | Components, deployment units, DI/options/clients/configuration and secure overrides |
| 6 | `retrieval-projections` | Blocked by 5A–5D | Directly navigable catalogs, postings, source locators, Markdown and retrieval scenarios |
| 7 | `multi-solution-composition` | Blocked by 6 | `1..N` isolated solution outputs, batch manifest and proven global correlations |
| 8 | `generator-cli-projections-certification` | Blocked by 7 | Final analyze/validate/compose CLI, corpora, coverage gates, performance and migration completion |

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
