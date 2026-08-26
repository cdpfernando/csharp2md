# Retrieval Projections Context

**Gathered:** 2026-08-26
**Spec:** `.specs/features/retrieval-projections/spec.md`
**Status:** Ready for design

---

## Feature Boundary

Workstream 6 fills the pipeline's declared `Retrieval Projection` responsibility: the committed package gains bounded catalogs, compact postings, callable source locators, byte-faithful source projections, deterministic Markdown pages for architecture identities, a generated `AGENTS.md` and a `retrieval.md` guide — so an LLM can navigate the package with ordinary file reading and search, with no query engine (AD-011).

Projections never introduce facts, relations or classifications (AD-007). Every repeated value is proven equal to the authoritative payload before it is published.

---

## Implementation Decisions

### Projector seam and commit atomicity

- Projection runs **inside `Commit()`**, after `DomainMapper.ToWire` and `PackageValidator.Validate` produce the wire document, and before `PackagePublisher.ToPublicationOrder` writes staging.
- `Csharp2Md.Storage` declares the projector port. `Csharp2Md.Projection` implements it and references `Csharp2Md.Storage` and `Csharp2Md.Domain`.
- `Csharp2Md.Analysis` is not touched by the seam: no new port, no `IStoreSession` method, no reference to Projection. The Analysis→Projection prohibition in AD-014 stays intact.
- The projector reads the **wire view**, so postings and Markdown links cite the real canonical key and the real shard ordinal. One hop from a posting to the evidence.
- Projections join the **same atomic publication**. A projection-validation failure raises `PublicationRejectedException`, the session aborts, and the previous package is preserved unchanged.
- Accepted cost: changing a projection requires re-running the analysis. Re-projecting a committed package without re-analysis is not supported in this workstream.
- The pipeline's slot-6 `RetrievalProjectionStub` is left in place — the eight declared stage names are load-bearing in `PipelineOrchestrator`.

### Source projection and secret redaction

- Every document in the analyzed inventory — C# documents and the configuration documents 5D reads — is copied into `source/`, byte-faithful except where redaction applies.
- Paths under `source/` are relative and derived from the `DocumentId`. No absolute path, and no dependency on the clone location.
- Suspected secrets are **redacted in place and the redaction is declared**. The redacted span is replaced by a fixed-length marker; the artifact declares `redacted: true`, the redacted spans, the sha256 of the **original** bytes (provenance, matching `DocumentDto.ContentSha256`) and the sha256 of the **published** bytes.
- "Byte-faithful" is therefore redefined precisely: identical to the original except inside declared spans. The break is explicit and auditable, never silent.
- Individual secret values are never hashed and never reproduced, in line with the standing security rule. `FactualSnapshot.SuspectedSecrets` already carries document, span, hash and redacted excerpt, so no new detection is needed.

### Callable declaration locators

- `Csharp2Md.Domain` gains a declaration locator on the `Symbol` fact — document, span and source hash. `SymbolFactEmitter` fills it from Roslyn, where the information already sits.
- This does **not** change `contracts/taxonomy-registry.json`: the registry records only `identity_components`, and `Symbol` identity stays `project` + `signature`. The byte-comparison drift gate is unaffected.
- Rejected: deriving spans from the union of observation locators (approximate; a callable with no observations gets no span, and the signature and braces fall outside — it would violate "once selected, a method body is retrieved completely").
- Rejected: a new `declaration` observation kind (changes the registry, and floods `observations/` with one record per symbol).

### Markdown surface

- Pages are generated for **architecture identities**: entry point, boundary operation, component, deployment unit, contract, data store and data object.
- Each page carries the fact's own facets, its direct confirmed relations, and links into catalogs, postings and `source/`. Every repeated value cites the canonical key and ordinal it came from.
- No page per callable, per symbol, per observation or per individual relation — that would collide with "no directory per high-cardinality identity".
- `retrieval.md` and the generated `AGENTS.md` ship in this workstream; they are the "retrieval scenarios" outcome of roadmap row 6.

### Catalogs, postings and physical budgets

- Catalog inventory is exactly the `output-and-retrieval.md` list: entry points; boundary operations; components and deployment units; contracts; data stores, objects and fields; prioritized unknowns.
- Posting inventory is exactly its list: outgoing and incoming relations by ID; callers and callees; contract producers and consumers; data readers and writers; unknowns and open frontiers.
- Postings carry artifact key plus ordinal — never duplicated evidence or relation payloads.
- This workstream freezes the **logical semantics and the shape of sharding**: deterministic bucket keys, canonical ordering, one shard per bucket, a configurable ceiling with a declared default, and a test proving a shard splits when the ceiling is exceeded.
- The **calibrated numbers** come from the `custom` corpus in workstream 8. `output-and-retrieval.md` is explicit: logical semantics freeze before the physical parameters.

### Agent's Discretion

- The exact bucket-key function and shard file-naming scheme, provided it is deterministic, independent of display names, and independent of input order.
- The internal JSON shape of each catalog and posting artifact, provided it satisfies the stated acceptance criteria.
- The default ceiling value, since workstream 8 recalibrates it.

### Declined / Undiscussed Gray Areas → Assumptions

None declined. All four gray areas were discussed and resolved. The remaining open items are recorded in the spec's Assumptions & Open Questions table — chiefly how source bytes reach `Csharp2Md.Storage`, which is a design-phase decision, not a product one.

---

## Specific References

- `output-and-retrieval.md` is treated as normative for the catalog inventory, the posting inventory, the retrieval scenarios and the scale constraints.
- AD-014 explicitly deferred the projector seam to this workstream; the decision above closes it.
- AD-018 is the precedent for extending Domain mid-programme with a justified decision record.
- AD-017 is the precedent for the pipeline appending non-taxonomy content into the publication envelope.

---

## Deferred Ideas

- Re-projecting a committed package without re-analysis. Attractive while tuning Markdown, but incompatible with the chosen atomicity. Revisit in workstream 8 if the analyze/validate/compose CLI wants a `project-only` mode.
- Calibrated byte ceilings and shard thresholds — workstream 8, from the `custom` corpus.
- Cross-solution catalogs and the batch manifest — workstream 7.
- The `run_certification` document and coverage gate thresholds — workstream 8.
