# Output and retrieval

## Goal

The generated package must be self-contained and directly navigable by an LLM using ordinary file reading and search. A query engine is optional and deferred; it is not required to generate or understand the package.

## Logical package

The exact physical layout and shard sizes will be calibrated during implementation, but every output must expose these logical areas from its manifest:

```text
manifest
taxonomy registry and schemas
inventory and structural facts
observations
classified facts
confirmed relations
candidates and unknowns
contracts and revisions
coverage, certification and diagnostics
derived postings and catalogs
source and Markdown projections
retrieval guide
```

All artifacts are reachable from the manifest. Absolute paths are forbidden. Deterministic content is separated from timestamps and runtime measurements.

## Direct navigation

The package provides bounded catalogs for:

- entry points;
- boundary operations;
- components and deployment units;
- contracts;
- data stores, objects and fields;
- prioritized unknowns.

High-value identities may receive deterministic Markdown pages. High-cardinality observations and relations remain in bounded payload shards. A page contains only facts available in the authoritative payload and references the canonical identity and artifact locator.

The generator also emits compact postings for:

- outgoing and incoming relations by ID;
- callers and callees;
- contract producers and consumers;
- data readers and writers;
- unknowns and open frontiers.

Postings contain ordinals or locators, not duplicated evidence or relation payloads.

## Retrieval scenarios

`retrieval.md` explains how an agent can:

1. locate an entry point, operation, contract, symbol or data field;
2. open the canonical fact and direct relations;
3. follow `executes`, `implements-operation` and `invokes`;
4. inspect `uses-contract`, `accesses-data`, `operates-on` and `targets` effects;
5. open complete callable bodies through source locators;
6. inspect candidates and open frontiers separately;
7. stop on terminal effects, cycles, unsupported capabilities or a declared reading budget.

Flows, reverse impact, centrality and transitive callers are not persisted as facts. An LLM can derive them by following postings; an optional future query layer may automate the same traversal without changing meanings.

## Source projection

Each source document has one byte-faithful canonical projection. Symbol indexes point to document spans and sections. Architectural pages never duplicate full method bodies.

A callable locator includes document ID, span, section, signature and source hash. Once selected, a method body is retrieved completely; budgets limit expansion to additional nodes rather than silently cutting an included method.

## Markdown

Markdown is a human/LLM projection. It cannot introduce facts, relations or classifications. Repeated metadata is permitted only when validation proves it equals the factual authority. Broken links, stale locators or mismatched projected values fail projection validation.

A minimal generated `AGENTS.md` explains the manifest entry point, proof states, source retrieval and the prohibition against treating Markdown as authority. It does not duplicate the taxonomy.

## Multiple solutions

An invocation accepts `1..N` explicit solutions. Each solution produces an independent output and commit. A batch manifest references them in canonical order.

For `N > 1`, composition derives only:

- solution catalog;
- global components and deployment units;
- proven cross-solution boundary relations;
- unresolved correlation candidates.

Internal symbols, calls and persistence remain local. Shared source blobs may be content-addressed once, but semantic facts remain qualified by solution and analysis variant.

If one solution fails, completed solution outputs remain valid. A global composition is certified only when every required input is complete and compatible. Partial composition requires explicit intent and advertises incomplete scope.

## Scale constraints

- no monolithic relation or catalog file;
- no directory per high-cardinality identity;
- canonical payload serialized once;
- postings and catalogs bounded by measured byte ceilings;
- bucketed localizers independent of display names;
- input order does not affect IDs or deterministic bytes;
- navigation scenarios measure files, bytes/tokens, hops, relevant facts and noise.

The `custom` corpus establishes physical budgets. Logical semantics are frozen before those physical parameters.
