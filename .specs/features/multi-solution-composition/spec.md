# Multi-Solution Composition Specification

## Problem Statement

`AnalysisEngine.AnalyzeAsync` already accepts `1..N` solutions, analyzes each one as an independent semantic unit and commits each package atomically. What it does not do is produce a batch. After the loop finishes there is no entry point above the per-solution packages, no record that the solutions were analyzed together, and nothing connecting `OrderPaidIntegrationEvent` published in `Ordering.sln` to the handler consuming it in `Catalog.sln` — even though both sides already publish that event type as a `BoundaryOperation.ProtocolOperationKey`, and both packages already carry the artifact key and ordinal that locate it.

The per-solution output is also not reproducible. `FilesystemTransactionalStore.Open` derives the package directory from `sha256(absolute solution path)`, so the same two solutions analyzed from `d:\work\repo` and `/home/x/repo` land in differently named directories. A batch manifest built on those names would inherit the same defect, and the manifest `solution_key` currently differs between the filesystem store (a path hash) and the in-memory store (the raw key).

This workstream adds the batch layer named in `architecture-knowledge-engine.md`'s pipeline: a batch manifest, a clone-independent package directory per solution, proven cross-solution correlations derived only from keys the committed packages already publish, unresolved correlation candidates for the pairings that are not provable, and grouped global catalogs that never invent shared identity. Composition derives; it never promotes (AD-004, AD-008).

## Goals

- [ ] Publish `batch-manifest.json` at the output root for every invocation, `N = 1` included, listing every requested solution in canonical order with its identity, package directory and publication status.
- [ ] Derive each package directory from the solution identity instead of the absolute solution path, so a batch is byte-reproducible across clones and across `--solution` argument order.
- [ ] Publish the manifest `solution_key` as the solution identity, identically from the filesystem store and the in-memory store.
- [ ] Publish proven cross-solution boundary relations from exact equality of a key both packages already publish: messaging by protocol operation key, contracts by contract identity.
- [ ] Publish unresolved correlation candidates for HTTP outbound/inbound pairs, whose destination is not statically provable.
- [ ] Publish grouped global catalogs of components, deployment units and external systems that keep every identity qualified by its owning solution and declare shared identity as not proven.
- [ ] Declare incomplete scope explicitly when a requested solution fails, leaving every committed package untouched.
- [ ] Derive the whole composition from bounded per-solution contributions, never from a second full copy of any package.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Merging solutions into one `Compilation`, `SymbolIndex` or monolithic internal graph | AD-008; ambiguous compilation contexts and unbounded memory |
| Cross-solution symbol, call, data-store, data-object or data-field correlation | `output-and-retrieval.md`: internal symbols, calls and persistence remain local |
| Promoting any cross-solution correlation into a `ConfirmedRelation`, candidate link or fact in any package | User decision: composition is derived projection only; AD-004 keeps promotion inside versioned classifiers |
| Proving an HTTP destination by traversing `ConfigurationBinding` base addresses to another solution's deployment unit | User decision: raises the proof bar past exact key equality; recorded as a deferred idea for workstream 8 |
| Merging two `Component` or `DeploymentUnit` identities into one global identity | User decision: shared identity is never asserted without proof |
| Content-addressed shared source blobs across solutions | `output-and-retrieval.md` states this as MAY; it is a physical calibration owned by workstream 8 |
| The final `analyze` / `validate` / `compose` CLI surface, new flags such as `--allow-partial-composition`, corpora and coverage-gate thresholds | Owned by workstream 8; this workstream adds no CLI option |
| A `compose` path that recomposes committed packages without re-analysis | Follows from running composition inside `analyze`; recorded as a deferred idea for workstream 8 |
| Engine certification, run certification beyond the existing status field, precision/recall claims | Owned by workstream 8 (AD-009) |
| Markdown pages, postings or a retrieval guide for the batch level | The batch manifest is itself the entry point; batch-level projection beyond the named artifacts is workstream 8's |
| Calibrated byte ceilings for composition shards | Same freeze as workstream 6: logical semantics now, physical parameters in workstream 8 |
| New fact families, fact types, observation kinds, facet axes or relation triples | Workstream 1 is closed; `Targets: BoundaryOperation → BoundaryOperation` already exists in the registry |
| Parallel or incremental analysis of the solutions in a batch, caching, batch-to-batch diff | Deferred after generator completion |
| A query engine, `kb`, QMD, embeddings, wiki compilation, business-rule interpretation | Deferred downstream concerns (AD-005, AD-011) |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here. Nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Composition trigger | `AnalysisEngine.AnalyzeAsync` runs composition after the last per-solution `Commit()`, through a batch member on the storage port; the store writes `batch-manifest.json` and `composition/` at the output root. One invocation is one batch | User decision. Keeps the per-solution commit atomic and independent, adds no CLI surface, and leaves the workstream 8 `compose` command free to be designed later | y |
| Status of a cross-solution correlation | A derived projection entry citing both fact identities and both packages' locators. No `ConfirmedRelation`, candidate link or fact is created anywhere | User decision. AD-004 is untouched: nothing outside a versioned classifier promotes, and `Csharp2Md.Projection` keeps its "does not promote knowledge" contract | y |
| Proof bar for a proven correlation | Exact equality of a key both packages already publish: messaging `ProtocolOperationKey` outbound in one solution and inbound in another, and identical `contract:schema-key=…` identity across solutions | User decision. Both keys are published verbatim by the 5A/5B classifiers, so equality is checkable without new inference | y |
| HTTP correlation | Always a correlation candidate, never a proven relation, because `DestinationScope` is a named-client string that does not prove which solution serves the route | User decision, and consistent with the bar above | y |
| Package directory naming | `s-<first 32 hex chars of sha256(solution identity)>`, replacing `s-<…sha256(absolute path)>`. The `.lock` and `.staging` siblings follow the same stem | User decision to make the batch reproducible. A hash keeps the directory filesystem-safe for `.sln` names containing spaces or non-ASCII characters; the batch manifest maps identity to directory in one hop. AD-002 authorizes the output-layout break | y |
| Manifest `solution_key` | The solution identity string, published identically by `FilesystemTransactionalStore` and `InMemoryTransactionalStore` | Removes the current divergence (path hash versus raw key) and keeps the value clone-independent. `Path.GetFullPath` stays an internal lookup key, never a published value | n |
| Batch for `N = 1` | `batch-manifest.json` and the composition artifacts are published for a single solution too, with zero cross-solution entries | User decision. A consumer gets one entry point and one reading path instead of having to detect which of two layouts it received | y |
| Partial batch | The composition is published over the committed solutions, the batch manifest declares `complete: false`, names the missing solutions and the reason, and declares itself not certifiable. No new CLI flag | User decision. `output-and-retrieval.md` requires completed solution outputs to stay valid and partial composition to advertise incomplete scope; declaring it in the artifact is that advertisement | y |
| Global component and deployment-unit catalog | Identities stay qualified by solution; equal canonical names are grouped for reading only, and each group declares `shared_identity: not-proven` | User decision. `output-and-retrieval.md`: semantic facts remain qualified by solution and analysis variant | y |
| Composition input | Each `Commit()` yields a bounded contribution — solution identity, components, deployment units, external systems, contracts, and boundary operations with direction, protocol, operation key, destination scope, HTTP method, route and artifact locator. Composition reads nothing else | Agent default. Keeps the batch step catalog-sized rather than fact-sized and honours "no monolithic relation or catalog file"; re-reading committed packages would double the I/O and reintroduce a full-document peak | n |
| Composition artifact keys | `batch-manifest.json` at the root, plus `composition/cross-solution-relations.json`, `composition/shared-contracts.json`, `composition/correlation-candidates.json`, `composition/components-and-deployment-units.json`, `composition/external-systems.json` | Agent default. Mirrors the flat `catalogs/` naming workstream 6 established, so shard splitting and validation reuse the same machinery | n |
| Empty composition artifacts | An artifact with zero entries is not published, matching the per-solution rule that a run with no facts publishes no projection | Agent default, following the convention established in workstream 6 (`b164a3b`) | n |
| Shard splitting | Composition artifacts split into ordinal-suffixed shards under the same configurable byte ceiling as workstream 6's projections | Agent default. One rule for all bounded artifacts, and workstream 8 calibrates one number | n |
| Stale package directories | A directory under the output root that no requested solution maps to is left untouched and is not referenced from the batch manifest | Agent default. Deleting it would destroy a valid package the caller may still hold; referencing it would claim it belongs to this batch | n |
| Batch write failure | A failure while writing the batch manifest or composition leaves every committed per-solution package intact and reports a non-zero exit; it does not roll back committed packages | Agent default. Per-solution atomicity is the guarantee `output-and-retrieval.md` gives; a batch-wide rollback would contradict it | n |

**Open questions:** none - all resolved or logged above.

---

## User Stories

### P1: Deterministic batch output ⭐ MVP

**User Story**: As an agent reading a generated package, I want one invocation over `1..N` solutions to publish a single batch entry point with clone-independent package directories, so that I can find every solution in the batch and get the same bytes on any machine.

**Why P1**: Every other story addresses artifacts inside the batch. Without a reproducible batch entry point there is nothing to address, and a batch manifest built on path-derived directory names would be irreproducible by construction.

**Acceptance Criteria** (each line is one EARS pattern):

1. WHEN an analysis invocation finishes THEN the system SHALL publish `batch-manifest.json` at the output root.
2. The system SHALL derive each solution's package directory name from that solution's identity and never from the solution's absolute path.
3. The system SHALL publish in `batch-manifest.json` one entry per requested solution carrying the solution identity, the solution file name, the package directory name and the publication status `committed` or `unpublished`.
4. The system SHALL order `batch-manifest.json` solution entries by solution identity, ordinal ascending.
5. WHEN the same solutions are analyzed from two different parent directories THEN the system SHALL produce byte-identical `batch-manifest.json` content and identical package directory names.
6. WHEN the same solutions are passed in a different `--solution` order THEN the system SHALL produce byte-identical `batch-manifest.json` and byte-identical composition artifacts.
7. The system SHALL publish `solution_key` in each per-solution `manifest.json` as that solution's identity, with `FilesystemTransactionalStore` and `InMemoryTransactionalStore` producing the same value for the same solution.
8. WHEN exactly one solution is requested THEN the system SHALL publish `batch-manifest.json` with one entry and publish no cross-solution relation, shared-contract or correlation-candidate entry.
9. IF a requested solution ends with publication status `unpublished` THEN the system SHALL publish that solution's `batch-manifest.json` entry with status `unpublished` and its failing stage name, and SHALL leave every committed solution's package directory unmodified.
10. WHILE at least one requested solution is unpublished, the system SHALL publish `batch-manifest.json` with `complete` set to `false` and `incomplete_scope_reason` set to `solution-unpublished`.
11. WHEN every requested solution is committed THEN the system SHALL publish `batch-manifest.json` with `complete` set to `true` and no `incomplete_scope_reason`.
12. IF two requested solutions resolve to the same solution identity THEN the system SHALL reject the invocation before any solution is analyzed, with a message naming both requested paths.
13. IF the output root contains a package directory that no requested solution maps to THEN the system SHALL leave that directory unmodified and SHALL NOT reference it from `batch-manifest.json`.
14. WHEN the output root already contains a `batch-manifest.json` from an earlier invocation THEN the system SHALL replace it with the current invocation's batch manifest.
15. IF writing `batch-manifest.json` or any composition artifact fails THEN the system SHALL leave every committed per-solution package directory unmodified and SHALL report a non-zero exit code.

**Independent Test**: Analyze two fixture solutions into an empty output directory, then analyze the same two after copying them under a different parent directory; assert both runs produce the same package directory names and byte-identical `batch-manifest.json`.

---

### P2: Proven cross-solution correlations

**User Story**: As an agent tracing a flow that leaves one solution, I want proven cross-solution boundary relations and explicitly unproven correlation candidates, so that I can follow `OrderPaidIntegrationEvent` from its publisher into its handler in another solution without guessing.

**Why P2**: This is the workstream's factual outcome. It ships after P1 because every entry addresses a package directory and cites an artifact key and ordinal inside it.

**Acceptance Criteria**:

1. WHEN an outbound messaging boundary operation in one solution has a protocol operation key equal to that of an inbound messaging boundary operation in a different solution THEN the system SHALL publish one `composition/cross-solution-relations.json` entry of relation kind `targets` from the outbound operation to the inbound operation.
2. The system SHALL publish for every cross-solution relation entry the source fact identity, the source solution identity, the source artifact key and ordinal, the target fact identity, the target solution identity, the target artifact key and ordinal, and the matched key.
3. WHEN one outbound messaging boundary operation matches inbound messaging boundary operations in more than one other solution THEN the system SHALL publish one entry per matched pair.
4. The system SHALL NOT publish a cross-solution relation entry whose source and target operations belong to the same solution.
5. IF an outbound messaging boundary operation's protocol operation key matches no inbound messaging boundary operation in any other solution THEN the system SHALL publish no cross-solution relation entry and no correlation candidate for it.
6. WHEN a contract identity appears in the committed packages of two or more different solutions THEN the system SHALL publish one `composition/shared-contracts.json` entry carrying that contract identity and, for each owning solution, the solution identity, artifact key and ordinal.
7. WHEN an outbound HTTP boundary operation's HTTP method and route joined by a single space equal the protocol operation key of an inbound HTTP boundary operation in a different solution THEN the system SHALL publish one `composition/correlation-candidates.json` entry carrying both fact identities, both solution identities, the matched key and the outbound operation's destination scope.
8. The system SHALL NOT publish any outbound HTTP boundary operation pairing as a `composition/cross-solution-relations.json` entry.
9. The system SHALL NOT write any fact, observation, confirmed relation, candidate link or quarantine record into any per-solution package as a result of composition.
10. The system SHALL derive every composition artifact only from per-solution contributions containing the solution identity, components, deployment units, external systems, contracts, and boundary operations with direction, protocol, operation key, destination scope, HTTP method, route and artifact locator, and SHALL read no symbol, observation, confirmed relation, candidate or source byte during composition.
11. The system SHALL order cross-solution relation entries and correlation candidates by source solution identity, then source fact identity, then target solution identity, then target fact identity, ordinal ascending.
12. The system SHALL order `composition/shared-contracts.json` entries by contract identity, ordinal ascending, and each entry's owning solutions by solution identity, ordinal ascending.
13. WHERE a composition artifact would exceed the configured byte ceiling, the system SHALL split it into ordinal-suffixed shards using the same shard-splitting rule as the per-solution projections.
14. IF a composition artifact would contain zero entries THEN the system SHALL NOT publish that artifact and SHALL NOT reference it from `batch-manifest.json`.

**Independent Test**: Analyze a two-solution fixture where one publishes an integration event the other handles, plus an HTTP client call whose route matches the other's endpoint; assert `composition/cross-solution-relations.json` contains exactly the messaging pair and `composition/correlation-candidates.json` contains exactly the HTTP pair.

---

### P3: Grouped global catalogs

**User Story**: As an agent orienting itself in a batch, I want a global catalog of components, deployment units and external systems that keeps every identity qualified by its solution, so that I can see the whole batch's topology without being told two same-named services are the same thing.

**Why P3**: Navigational convenience over identities already reachable through each solution's own catalogs. The batch is usable without it.

**Acceptance Criteria**:

1. The system SHALL publish `composition/components-and-deployment-units.json` listing every component and deployment unit from every committed solution, each entry carrying the fact identity, the owning solution identity, the artifact key and the ordinal.
2. The system SHALL group entries in `composition/components-and-deployment-units.json` by canonical name and order the groups by canonical name, then entries within a group by solution identity, ordinal ascending.
3. WHEN a canonical name is carried by identities from two or more different solutions THEN the system SHALL publish that group with `shared_identity` set to `not-proven`.
4. The system SHALL NOT merge two component identities or two deployment-unit identities into a single global identity.
5. The system SHALL publish `composition/external-systems.json` in the same grouped, unmerged form as `composition/components-and-deployment-units.json`.

**Independent Test**: Analyze two fixture solutions that each declare a deployment unit named `ordering-api`; assert the global catalog reports one group holding two solution-qualified identities with `shared_identity: not-proven`.

---

## Edge Cases

- IF every requested solution ends unpublished THEN the system SHALL publish `batch-manifest.json` with `complete` set to `false`, every entry carrying status `unpublished`, and publish no composition artifact.
- IF a committed solution's contribution carries no component, deployment unit, external system, contract or boundary operation THEN the system SHALL publish `batch-manifest.json` and omit every composition artifact that would be empty.
- WHEN two committed solutions carry the same fact identity for a contract THEN the system SHALL publish one shared-contract entry rather than one entry per solution.
- IF an outbound messaging boundary operation and an inbound messaging boundary operation with the same protocol operation key belong to the same solution THEN the system SHALL publish no composition entry, leaving that pairing to the solution's own confirmed relations.
- IF a solution identity appears in a contribution but its package directory is absent from the output root THEN the system SHALL treat that solution as unpublished in `batch-manifest.json`.
- WHEN the output root does not exist at invocation time THEN the system SHALL create it and publish the batch manifest and composition artifacts into it.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| MSC-01 | P1: Deterministic batch output | Design | Pending |
| MSC-02 | P1: Deterministic batch output | Design | Verified |
| MSC-03 | P1: Deterministic batch output | Design | Implementing |
| MSC-04 | P1: Deterministic batch output | Design | Pending |
| MSC-05 | P1: Deterministic batch output | Design | Implementing |
| MSC-06 | P1: Deterministic batch output | Design | Pending |
| MSC-07 | P1: Deterministic batch output | Design | Verified |
| MSC-08 | P1: Deterministic batch output | Design | Pending |
| MSC-09 | P1: Deterministic batch output | Design | Pending |
| MSC-10 | P1: Deterministic batch output | Design | Pending |
| MSC-11 | P1: Deterministic batch output | Design | Pending |
| MSC-12 | P1: Deterministic batch output | Design | Verified |
| MSC-13 | P1: Deterministic batch output | Design | Pending |
| MSC-14 | P1: Deterministic batch output | Design | Pending |
| MSC-15 | P1: Deterministic batch output | Design | Pending |
| MSC-16 | P2: Proven cross-solution correlations | Design | Implementing |
| MSC-17 | P2: Proven cross-solution correlations | Design | Implementing |
| MSC-18 | P2: Proven cross-solution correlations | Design | Verified |
| MSC-19 | P2: Proven cross-solution correlations | Design | Verified |
| MSC-20 | P2: Proven cross-solution correlations | Design | Verified |
| MSC-21 | P2: Proven cross-solution correlations | Design | Pending |
| MSC-22 | P2: Proven cross-solution correlations | Design | Pending |
| MSC-23 | P2: Proven cross-solution correlations | Design | Pending |
| MSC-24 | P2: Proven cross-solution correlations | Design | Pending |
| MSC-25 | P2: Proven cross-solution correlations | Design | Verified |
| MSC-26 | P2: Proven cross-solution correlations | Design | Pending |
| MSC-27 | P2: Proven cross-solution correlations | Design | Pending |
| MSC-28 | P2: Proven cross-solution correlations | Design | Pending |
| MSC-29 | P2: Proven cross-solution correlations | Design | Pending |
| MSC-30 | P3: Grouped global catalogs | Design | Pending |
| MSC-31 | P3: Grouped global catalogs | Design | Pending |
| MSC-32 | P3: Grouped global catalogs | Design | Pending |
| MSC-33 | P3: Grouped global catalogs | Design | Pending |
| MSC-34 | P3: Grouped global catalogs | Design | Pending |
| MSC-35 | Edge cases | Design | Pending |
| MSC-36 | Edge cases | Design | Pending |
| MSC-37 | Edge cases | Design | Pending |
| MSC-38 | Edge cases | Design | Verified |
| MSC-39 | Edge cases | Design | Verified |
| MSC-40 | Edge cases | Design | Pending |

**ID mapping:** MSC-01..15 are P1 acceptance criteria 1..15 in order; MSC-16..29 are P2 acceptance criteria 1..14 in order; MSC-30..34 are P3 acceptance criteria 1..5 in order; MSC-35..40 are the Edge Cases in order.

**ID format:** `MSC-[NUMBER]`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 40 total, 0 mapped to tasks, 40 unmapped ⚠️

---

## Success Criteria

- [ ] Analyzing the same two solutions from two different parent directories produces identical package directory names and byte-identical `batch-manifest.json`.
- [ ] Reordering the `--solution` arguments produces byte-identical batch and composition artifacts.
- [ ] A two-solution fixture in which one publishes an integration event the other handles yields exactly one `targets` cross-solution relation entry, resolvable to both operations in one hop each through the published artifact key and ordinal.
- [ ] An HTTP outbound/inbound route match across solutions yields a correlation candidate and no cross-solution relation.
- [ ] With one solution failing, every committed package directory is byte-identical to the same run without the failure, and the batch manifest declares `complete: false` with `incomplete_scope_reason: solution-unpublished`.
- [ ] No per-solution package gains a fact, observation, confirmed relation, candidate link or quarantine record from composition.
- [ ] Composition reads no symbol, observation, confirmed relation, candidate or source byte.
- [ ] `validate_spec.py multi-solution-composition` exits clean, and the full test suite passes with `Category=LocalCorpus` excluded.
