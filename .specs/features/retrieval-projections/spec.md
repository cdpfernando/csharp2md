# Retrieval Projections Specification

## Problem Statement

Workstreams 4 through 5D fill the package with facts, observations, confirmed relations, candidates and diagnostics, but nothing in it is navigable. `PackagePublisher` emits a manifest, the registry, coverage, diagnostics, measurements, run-certification, five fact shards, per-kind observation shards, per-relation shards, candidates, unresolved, frontiers and quarantine — and nothing else. There is no `source/`, no `catalogs/`, no `postings/`, no `markdown/` and no `retrieval.md`. An LLM asked "who calls `OrderService.PlaceAsync`, and what does that method actually do" must scan whole relation shards linearly and then find the source outside the package, because `SymbolDto` publishes a signature and a hash but no span.

`Csharp2Md.Projection` is still a single `AssemblyMarker.cs`, and AD-014 explicitly left the projector seam for this workstream to invent. This workstream fills that assembly and the package's navigation layer: bounded catalogs, compact postings, callable declaration locators, byte-faithful source projections, deterministic Markdown pages for architecture identities, a generated `AGENTS.md` and a `retrieval.md` guide — so the documented retrieval scenarios work with ordinary file reading and search, with no query engine (AD-011).

Projections are derived, never authoritative (AD-007). Every value a projection repeats must be proven equal to the authoritative payload before publication, and a projection that cannot be proven consistent aborts the commit rather than shipping a broken package.

## Goals

- [ ] Declare a projector port on `Csharp2Md.Storage`, implemented by `Csharp2Md.Projection`, invoked inside `Commit()` over the mapped wire document so projections join the same atomic publication.
- [ ] Extend `Symbol` with a declaration locator (document, span, source hash) filled by `SymbolFactEmitter`, without changing `contracts/taxonomy-registry.json` bytes.
- [ ] Publish one byte-faithful `source/` artifact per analyzed document, with suspected-secret spans redacted by a fixed-length marker and the redaction declared with both hashes.
- [ ] Publish the six catalogs and five posting families named in `output-and-retrieval.md`, each entry citing a canonical artifact key and an ordinal rather than duplicating evidence.
- [ ] Publish deterministic Markdown pages for architecture identities that introduce no facts and cite the origin of every repeated value.
- [ ] Publish `retrieval.md` covering the seven documented retrieval scenarios and a generated `AGENTS.md` that explains the manifest entry point without duplicating the taxonomy.
- [ ] Validate every projection link, ordinal, locator and repeated value before staging is written, and abort the whole publication when validation fails.
- [ ] Freeze the logical semantics and the shape of shard splitting behind a configurable byte ceiling, leaving the calibrated numbers to workstream 8.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Re-projecting a committed package without re-analysis | Incompatible with the chosen atomicity; recorded as a deferred idea for the workstream 8 CLI |
| Calibrated byte ceilings and shard thresholds | `output-and-retrieval.md` freezes logical semantics before physical parameters; the `custom` corpus is workstream 8's |
| Cross-solution catalogs, batch manifest, global composition | Owned by workstream 7 |
| The final `analyze` / `validate` / `compose` CLI surface, corpora and coverage gate thresholds | Owned by workstream 8; this workstream changes no CLI command |
| The `run_certification` document beyond the status field already published | Owned by workstream 8 |
| A query engine, `kb`, QMD, embeddings, wiki compilation, business-rule interpretation | Deferred downstream concerns (AD-005, AD-011) |
| Flows, reverse impact, centrality and transitive callers as persisted artifacts | `output-and-retrieval.md` forbids persisting them; an agent derives them by following postings |
| Markdown pages per callable, symbol, observation or individual relation | Collides with "no directory per high-cardinality identity" |
| New fact families, fact types, observation kinds, facet axes or relation triples | Workstream 1 is closed; this workstream adds a non-identity field to `Symbol` only |
| New classification or promotion of any kind | Projections derive; only versioned classifiers promote (AD-004) |
| Incremental projection, snapshot diff, caching between runs | Deferred after generator completion |
| Redaction of anything other than `FactualSnapshot.SuspectedSecrets` spans | Detection is workstream 4's; this workstream consumes the evidence it already produces |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here. Nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Projector seam | The port is declared by `Csharp2Md.Storage` and invoked inside `Commit()` after `PackageValidator.Validate`; `Csharp2Md.Projection` implements it and references Storage and Domain | User decision. The projector sees the mapped wire document, so postings cite real canonical keys and ordinals — one hop from posting to evidence instead of a linear scan. Analysis is untouched, so the AD-014 prohibition holds | y |
| Commit atomicity | Projections are fragments in the same publication; a projection failure raises `PublicationRejectedException`, aborts staging and leaves the prior package byte-identical | User decision. AD-007 requires every artifact reachable from the manifest; a second write would leave a window with an incomplete manifest | y |
| Re-projection cost | Changing a projection requires re-running the analysis; no `project-only` path exists | User decision, accepted cost of the atomicity choice | y |
| Source copy coverage | Every document in the analyzed inventory — C# plus the configuration documents 5D reads — is copied under `source/` | `output-and-retrieval.md`: "each source document has one byte-faithful canonical projection". Copying only fact-bearing documents would break locators into files that carry only structural facts | y |
| Source artifact keys | Derived from the owning project and the document's repository-relative path, never from an absolute path or the clone location | Absolute paths are forbidden and published bytes must be clone-path independent | y |
| Secret handling in the copy | The suspected span is replaced by a fixed-length ASCII marker; the artifact declares `redacted: true`, the ordinal-sorted redacted spans, the sha256 of the original bytes and the sha256 of the published bytes | User decision. Resolves the literal tension between "the byte-faithful source projection is treated as sensitive" and "redaction occurs only in projections/retrieval". The break in fidelity is explicit and auditable, never silent | y |
| Meaning of "byte-faithful" | Identical to the original bytes outside declared redaction spans | Follows from the decision above; stated so acceptance criteria are unambiguous | y |
| Redaction marker | The fixed 17-byte ASCII sequence `[REDACTED-SECRET]`, regardless of the redacted span's original length | Fixed length so the published bytes do not leak the secret's length. Hashing or length-preserving substitution would both leak | n |
| Callable locators | `Symbol` gains a declaration locator (document reference, span, source hash) filled by `SymbolFactEmitter` from the declaration's own syntax span | User decision. Roslyn already holds it; deriving from observation spans is approximate and would violate "once selected, a method body is retrieved completely" | y |
| Registry stability | Adding the locator leaves `contracts/taxonomy-registry.json` byte-identical | Verified: the registry records only `identity_components`, and `Symbol` identity is `project` plus `signature`. The AD-013 drift gate is unaffected | y |
| Multiple declaring references | When a symbol has more than one declaring syntax reference, the published locator is the one whose document id, start line and start column sort first ordinally | Partial types and partial methods are real. Picking ordinally keeps the choice deterministic and clone-path independent | n |
| Source bytes reaching Storage | `ITransactionalStore.Open` takes an `ISourceDocumentReader` alongside the solution key, and `StagedFragment` gains a lazy form so `WriteStaging` materializes one document at a time. `FactualSnapshot` stays pure data and carries no bytes | User decision. `Csharp2Md.Storage` cannot reopen the clone, and re-reading from disk would make `InMemoryTransactionalStore` depend on the filesystem to produce the same artifacts. Putting the bytes in the snapshot would hold a second full copy of the source alongside Roslyn's, because `BoundSolution` is disposed only after `Commit()` returns. A reader keeps the peak at one document and keeps the snapshot inspectable in isolation | y |
| Reader retention | The store requests each document's bytes at most once and does not retain them after the fragment is written | The whole point of the reader is bounding the peak; retaining what it returns would reintroduce the second copy | y |
| Markdown surface | Pages for entry point, boundary operation, component, deployment unit, contract, data store and data object; nothing per callable, symbol, observation or relation | User decision. This is what "high-value identities" means, and it keeps the file count bounded | y |
| Retrieval guide ownership | `retrieval.md` and the generated `AGENTS.md` ship in this workstream | "Retrieval scenarios" is an explicit outcome of roadmap row 6, and workstream 8 already carries CLI, corpora, coverage gates and performance | y |
| Catalog and posting inventory | Exactly the lists in `output-and-retrieval.md`, with nothing added and nothing dropped | The document is normative; inventing a catalog would be scope creep and dropping one would leave a documented scenario unsupported | y |
| Physical budgets | This workstream freezes the semantics and the shape of sharding — deterministic bucket keys, canonical ordering, one shard per bucket, a configurable ceiling with a declared default — and defers the calibrated numbers to workstream 8 | User decision, and `output-and-retrieval.md` states it directly: "logical semantics are frozen before those physical parameters" | y |
| Default ceiling value | 1 MiB per catalog or posting artifact | A placeholder large enough that `fixtures/SyntheticSolution` never splits by accident and small enough to be exceeded by a synthetic over-ceiling test. Workstream 8 recalibrates it | n |
| Unknown prioritization | The unknowns catalog ranks by the count of confirmed relations whose source or target is the unresolved record's owner, descending, then by fact id ordinal | "Prioritized unknowns" needs a computable definition. Relation degree is derivable from the published payload alone and needs no new evidence | n |
| Pipeline slot 6 | `RetrievalProjectionStub` stays in place and stays a no-op | The eight declared stage names are load-bearing in `PipelineOrchestrator`'s arity and name checks; removing one breaks it for no benefit, since projection now runs inside Storage | n |
| Empty publication | A run that publishes no facts publishes the manifest and the registry and no projection artifact | Matches the existing empty-commit behaviour, where absent shards are omitted rather than published empty | n |
| Markdown link form | Links are relative artifact keys plus an ordinal reference in a trailing comment, not anchors into JSON | JSON has no anchor grammar. A key plus ordinal is exactly what the postings carry and what projection validation can check | n |

**Open questions:** none — all resolved or logged above.

---

## Implicit-requirement dimensions sweep

| Dimension | Resolution |
| --- | --- |
| Input validation & bounds | RP-42, RP-43, RP-45 — a projection citing a missing artifact, an out-of-range ordinal or an out-of-bounds span aborts the publication |
| Failure / partial-failure states | RP-04 — projection failure aborts the whole commit and preserves the prior package byte-identical; there is no partially projected package |
| Idempotency / retry / duplicate handling | RP-46 — two runs over the same input produce byte-identical projection artifacts, so a retry after a failed commit converges on the same bytes |
| Auth boundaries & rate limits | N/A because analysis is local, publishes to the local filesystem and sends nothing outward |
| Concurrency / ordering | RP-48 — input order does not affect projection bytes; the existing per-solution lock in `FilesystemTransactionalStore` already serializes concurrent publications and this workstream adds no second writer |
| Data lifecycle / expiry | N/A because the package is fully rewritten on each commit; there is no retained or expiring projection state |
| Observability | RP-42 through RP-45 — a projection-validation abort names the offending artifact key, ordinal, page or locator in the rejection detail, on the existing `PublicationRejectedException` channel |
| External-dependency failure | N/A because projection reads only the in-memory wire document and the staged source bytes; it makes no external call |
| State-transition integrity | RP-02, RP-03 — projection runs at exactly one point in the commit sequence, after validation and before ordering, and cannot observe a half-mapped document |

---

## User Stories

### P1: Projector seam and atomic publication ⭐ MVP

**User Story**: As the engine, I want projection to run inside the commit over the mapped wire document, so that projections cite real artifact keys and ordinals and can never ship half-written.

**Why P1**: Every other story publishes through this seam. Nothing else can be built until it exists.

**Acceptance Criteria**:

1. RP-01 — The system SHALL declare the projector port on `Csharp2Md.Storage`'s public surface, implemented by `Csharp2Md.Projection`.
2. RP-02 — WHEN `Commit()` runs THEN the system SHALL invoke the projector after `PackageValidator.Validate` returns and before `PackagePublisher.ToPublicationOrder` orders the artifacts.
3. RP-03 — WHEN the projector returns fragments THEN the system SHALL publish them in the same atomic publication as the factual artifacts, with every fragment listed in `manifest.json`.
4. RP-04 — IF the projector throws or projection validation fails THEN the system SHALL raise `PublicationRejectedException`, delete the staging directory, and leave the previously committed package byte-identical.
5. RP-05 — The system SHALL NOT add a `Csharp2Md.Analysis` reference to `Csharp2Md.Projection`, and SHALL NOT add any projector member to `ITransactionalStore` or `IStoreSession`. The source-document reader that `Open` accepts is not a projector member.
6. RP-06 — WHERE no projector is supplied to the store the system SHALL commit the factual artifacts alone and publish no projection artifact.
7. RP-57 — WHEN the store materializes a `source/` fragment THEN it SHALL request that document's bytes from the source-document reader at most once, and SHALL NOT retain them after the fragment is written.

**Independent Test**: Commit a snapshot through `FilesystemTransactionalStore` with a projector returning one fragment; assert the fragment is on disk and named in `manifest.json`. Commit again with a projector that throws; assert `PublicationRejectedException`, no staging directory left behind, and the prior package bytes unchanged. Commit a third time through a counting reader and assert each document was requested exactly once.

---

### P1: Source projection and secret redaction ⭐ MVP

**User Story**: As an LLM reading the package, I want each analyzed document present in the package itself, so that I can open a complete method body without access to the original clone.

**Why P1**: Source locators are worthless without the source, and roadmap row 6 names source locators as an outcome.

**Acceptance Criteria**:

7. RP-07 — WHEN a document is in the analyzed inventory THEN the system SHALL publish exactly one artifact under `source/` whose bytes equal the original document's bytes outside declared redaction spans.
8. RP-08 — The system SHALL derive every `source/` artifact key from the document's owning project and repository-relative path, and SHALL NOT include an absolute path or any clone-location-dependent segment.
9. RP-09 — WHEN a `SuspectedSecretEvidence` span falls inside a document THEN the system SHALL replace exactly those bytes in the published artifact with the fixed 17-byte marker `[REDACTED-SECRET]`, regardless of the span's original length.
10. RP-10 — WHEN a document has at least one redacted span THEN the system SHALL declare `redacted` as true, the redacted spans in ordinal order, the sha256 of the original bytes and the sha256 of the published bytes.
11. RP-11 — WHILE a document has no suspected-secret span the system SHALL publish bytes whose sha256 equals the `ContentSha256` already published for that document in `facts/structural.json`.
12. RP-12 — The system SHALL NOT publish a secret value, a hash of an individual secret value, or an unredacted secret excerpt in any `source/` artifact, catalog, posting, Markdown page or manifest entry.

**Independent Test**: Analyze `fixtures/SyntheticSolution`, whose configuration carries a suspected secret. Assert the secret's bytes appear in no published artifact, that the marker occupies its span, that the artifact declares both hashes, and that a clean C# document's published sha256 equals its `ContentSha256`.

---

### P1: Callable declaration locators ⭐ MVP

**User Story**: As an LLM, I want each symbol to carry the span of its own declaration, so that I can read one method completely without reading its whole file.

**Why P1**: Catalogs, postings and Markdown all link into source through this locator.

**Acceptance Criteria**:

13. RP-13 — The system SHALL extend the `Symbol` fact with a declaration locator carrying a document reference, a source span and a source hash.
14. RP-14 — WHEN `SymbolFactEmitter` emits a `Symbol` for a declaration in an analyzed document THEN it SHALL populate that locator from the declaration's own syntax span.
15. RP-15 — WHEN the declaration locator is published THEN the span SHALL cover the complete declaration including its signature and body, such that the bytes in that span form a syntactically complete member.
16. RP-16 — IF a symbol has more than one declaring syntax reference THEN the system SHALL publish the locator whose document id, start line and start column sort first ordinally.
17. RP-17 — The system SHALL leave `contracts/taxonomy-registry.json` byte-identical to its committed content.

**Independent Test**: Analyze the fixture, take the published locator for a known method, slice exactly that span out of the matching `source/` artifact, and assert the slice parses as a complete member declaration whose name matches the symbol's signature.

---

### P1: Bounded catalogs ⭐ MVP

**User Story**: As an LLM, I want a bounded catalog per identity kind, so that locating an entry point, operation, contract or data field is one file read.

**Why P1**: Catalogs are scenario 1 of the retrieval guide — every other scenario starts by locating an identity.

**Acceptance Criteria**:

18. RP-18 — The system SHALL publish catalogs for entry points; boundary operations; components and deployment units; contracts; data stores, data objects and data fields; and prioritized unknowns.
19. RP-19 — WHEN a catalog entry is published THEN it SHALL carry the identity's fact id, the canonical key of the artifact holding its authoritative payload, and that entry's zero-based ordinal within the artifact.
20. RP-20 — WHEN an agent resolves a catalog entry THEN reading the cited artifact at the cited ordinal SHALL yield the fact whose id the entry claimed.
21. RP-21 — WHERE a fact family produced no facts the system SHALL omit that catalog artifact entirely rather than publish it empty.
22. RP-22 — The system SHALL order catalog entries by ordinal comparison of the entry's fact id.
23. RP-23 — WHEN the unknowns catalog is published THEN it SHALL rank entries by the count of confirmed relations whose source or target is the unresolved record's owner, descending, and by fact id ordinal within an equal count.
24. RP-24 — A catalog entry SHALL NOT contain a value absent from the artifact it cites.

**Independent Test**: Analyze the fixture, then for every entry in every published catalog, open the cited artifact, index it at the cited ordinal, and assert the fact id matches. Assert a family with no facts has no catalog file.

---

### P1: Compact postings ⭐ MVP

**User Story**: As an LLM, I want postings that answer "who calls this", "who produces this contract" and "who writes this table" in one hop, so that traversal does not require scanning relation shards.

**Why P1**: Postings are what make the documented traversal scenarios possible without a query engine.

**Acceptance Criteria**:

25. RP-25 — The system SHALL publish postings for outgoing and incoming relations by fact id; callers and callees; contract producers and consumers; data readers and writers; and unknowns and open frontiers.
26. RP-26 — WHEN a posting entry names a relation THEN it SHALL carry the canonical key of the relation artifact and that relation's zero-based ordinal within it, and SHALL NOT carry the relation's payload or its evidence chain.
27. RP-27 — WHEN an agent resolves a posting entry THEN reading the cited artifact at the cited ordinal SHALL yield the relation whose source and target the posting claimed.
28. RP-28 — The system SHALL derive callers and callees postings only from confirmed `invokes` relations.
29. RP-29 — WHERE candidates, unresolved records or open frontiers exist the system SHALL publish them only in their own postings and catalogs, and SHALL NOT merge them into the confirmed postings.
30. RP-30 — The system SHALL order posting groups by ordinal comparison of the subject fact id, and entries within a group by artifact key and then ordinal.

**Independent Test**: Analyze the fixture and assert the callers posting for a known callee lists exactly the callers `relations/confirmed/invokes.json` contains for it, that every cited ordinal resolves to that relation, and that no candidate appears among them.

---

### P1: Markdown pages for architecture identities ⭐ MVP

**User Story**: As a human or LLM reader, I want a readable page per architecture identity, so that I can understand a boundary operation without assembling it from four JSON files.

**Why P1**: Markdown is a named outcome of roadmap row 6.

**Acceptance Criteria**:

31. RP-31 — The system SHALL publish one Markdown page per entry point, boundary operation, component, deployment unit, contract, data store and data object.
32. RP-32 — WHEN a page is published THEN it SHALL state the identity's fact id, its facet values, its direct confirmed relations, and links into the catalogs, postings and `source/` artifacts holding the underlying evidence.
33. RP-33 — A Markdown page SHALL NOT state a fact, relation, facet value or classification absent from the authoritative payload.
34. RP-34 — WHEN a page reproduces a value from a payload THEN it SHALL cite the canonical key and ordinal that value came from.
35. RP-35 — The system SHALL NOT publish a Markdown page per callable, symbol, observation or individual relation.
36. RP-36 — WHEN the same input is analyzed twice THEN the system SHALL produce byte-identical Markdown pages.

**Independent Test**: Analyze the fixture, parse every published page, and assert each stated facet value and relation is present in the artifact at the ordinal the page cites, and that no page names an identity absent from the facts.

---

### P1: Retrieval guide and generated AGENTS.md ⭐ MVP

**User Story**: As an LLM opening the package for the first time, I want a guide telling me where to start and how to traverse, so that I do not have to reverse-engineer the layout.

**Why P1**: "Retrieval scenarios" is an explicit outcome of roadmap row 6, and without it the package's navigability is untestable in practice.

**Acceptance Criteria**:

37. RP-37 — The system SHALL publish `retrieval.md` documenting each of the seven retrieval scenarios in `docs/architecture/output-and-retrieval.md`, naming the artifacts each scenario reads.
38. RP-38 — The system SHALL publish a generated `AGENTS.md` explaining the manifest as the entry point, the proof-state axes, how to retrieve source, and that Markdown is a projection and never authority.
39. RP-39 — The generated `AGENTS.md` SHALL NOT restate the taxonomy's fact types, observation kinds, facet axes or relation triples.
40. RP-40 — The system SHALL list both artifacts in `manifest.json` and SHALL NOT write an absolute path into either.

**Independent Test**: Analyze the fixture and assert `retrieval.md` names, for each of the seven scenarios, at least one artifact key present in the same publication, and that `AGENTS.md` contains no taxonomy enumeration.

---

### P1: Projection validation ⭐ MVP

**User Story**: As the engine, I want every projection link, ordinal, locator and repeated value checked before anything is written, so that a broken package is never published.

**Why P1**: `output-and-retrieval.md` requires that broken links, stale locators and mismatched projected values fail projection validation, and AD-007 requires that projections never contradict the authority.

**Acceptance Criteria**:

41. RP-41 — WHEN the projector returns fragments THEN the system SHALL validate every link, ordinal, locator and repeated value before any staging file is written.
42. RP-42 — IF a projection cites an artifact key absent from the same publication THEN the system SHALL abort the publication and name the offending key in the rejection detail.
43. RP-43 — IF a projection cites an ordinal outside the bounds of the cited artifact's array THEN the system SHALL abort the publication and name the artifact key and the ordinal in the rejection detail.
44. RP-44 — IF a value reproduced in a Markdown page differs from the corresponding value in the authoritative payload THEN the system SHALL abort the publication and name the page and the value in the rejection detail.
45. RP-45 — IF a source locator's span falls outside the bounds of the published `source/` artifact it addresses THEN the system SHALL abort the publication and name the locator in the rejection detail.

**Independent Test**: Drive `Commit()` with a stub projector emitting, in turn, a missing artifact key, an out-of-range ordinal, a mismatched Markdown value and an out-of-bounds span. Assert each aborts, each rejection detail names the offender, and the prior package is byte-identical after every attempt.

---

### P1: Determinism, security and assembly isolation ⭐ MVP

**User Story**: As a maintainer, I want projections to be reproducible and correctly layered, so that the package's bytes are trustworthy and the architecture does not erode.

**Why P1**: Determinism and isolation are standing engineering constraints, and every prior workstream has asserted them.

**Acceptance Criteria**:

46. RP-46 — WHEN the same solution is analyzed twice THEN the system SHALL produce byte-identical projection artifacts.
47. RP-47 — WHEN the same repository is analyzed from two different absolute paths THEN the system SHALL produce byte-identical projection artifacts.
48. RP-48 — WHEN the same solution's documents are presented in a different order THEN the system SHALL produce byte-identical projection artifacts.
49. RP-49 — The system SHALL NOT publish an absolute path in any projection artifact.
50. RP-50 — The system SHALL reference only `Csharp2Md.Storage` and `Csharp2Md.Domain` from `Csharp2Md.Projection`, SHALL NOT reference `Csharp2Md.Projection` from `Csharp2Md.Analysis`, and SHALL NOT reference `Csharp2Md.Domain` from `Csharp2Md.Cli`.
51. RP-51 — WHERE a run publishes no facts the system SHALL publish the manifest and the registry and SHALL publish no projection artifact.

**Independent Test**: Analyze the fixture twice from two copies at different absolute paths with shuffled document order, and assert every projection artifact's bytes are identical across all runs. Assert the assembly reference set by reflection over the built assemblies.

---

### P2: Bounded artifacts and shard shape

**User Story**: As a maintainer facing a real repository, I want catalogs and postings to split deterministically once they grow, so that the package never contains a monolithic catalog file.

**Why P2**: `fixtures/SyntheticSolution` never approaches any ceiling, so this is provable on a synthetic over-ceiling input but not demonstrable on the versioned fixture. The calibrated numbers arrive in workstream 8.

**Acceptance Criteria**:

52. RP-52 — The system SHALL expose a configurable byte ceiling for catalog and posting artifacts with a declared default of 1 MiB.
53. RP-53 — WHEN a catalog or posting would exceed the ceiling THEN the system SHALL split it into shards addressed by a deterministic bucket key.
54. RP-54 — The system SHALL derive bucket keys from the entry's fact id and SHALL NOT derive them from a display name.
55. RP-55 — WHEN an artifact is split THEN the system SHALL list every resulting shard in `manifest.json`.
56. RP-56 — WHEN an artifact is split THEN the system SHALL preserve every entry's content unchanged and SHALL alter only which artifact hosts it.

**Independent Test**: Project a synthetic wire document large enough to exceed a lowered ceiling; assert the catalog splits, every shard is in the manifest, the union of shard entries equals the unsplit entry set, and a re-run produces the same shard assignment.

---

## Edge Cases

- IF a document's bytes are not valid UTF-8 THEN the system SHALL publish them unchanged as opaque bytes and SHALL NOT attempt textual normalization.
- IF two documents in different projects share a repository-relative path THEN the system SHALL publish both under distinct artifact keys qualified by owning project.
- IF a suspected-secret span covers an entire document THEN the system SHALL publish an artifact consisting solely of the marker and SHALL declare the redaction.
- IF two suspected-secret spans overlap THEN the system SHALL merge them into one redacted span before substitution, so that no marker is emitted inside another marker.
- IF a fact appears in the wire document but in no catalog's family THEN the system SHALL publish it in the shards only, and this SHALL NOT fail projection validation.
- WHEN a confirmed relation's source and target are the same fact THEN the system SHALL list it in both the outgoing and the incoming postings for that fact.
- IF an unresolved record's owner participates in no confirmed relation THEN the system SHALL rank it last in the unknowns catalog rather than omit it.
- WHEN a symbol is declared in a document that produced no `Document` fact THEN the system SHALL emit no declaration locator for it, matching the emitter's existing owning-project requirement.
- IF the wire document contains no architecture facts THEN the system SHALL publish no Markdown page and SHALL still publish `retrieval.md` and `AGENTS.md`.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| RP-01 | P1: Projector seam and atomic publication | Design | Pending |
| RP-02 | P1: Projector seam and atomic publication | Design | Pending |
| RP-03 | P1: Projector seam and atomic publication | Design | implemented |
| RP-04 | P1: Projector seam and atomic publication | Design | Pending |
| RP-05 | P1: Projector seam and atomic publication | Design | Pending |
| RP-06 | P1: Projector seam and atomic publication | Design | Pending |
| RP-07 | P1: Source projection and secret redaction | Design | Pending |
| RP-08 | P1: Source projection and secret redaction | Design | Pending |
| RP-09 | P1: Source projection and secret redaction | Design | Pending |
| RP-10 | P1: Source projection and secret redaction | Design | Pending |
| RP-11 | P1: Source projection and secret redaction | Design | Pending |
| RP-12 | P1: Source projection and secret redaction | Design | Pending |
| RP-13 | P1: Callable declaration locators | Design | Pending |
| RP-14 | P1: Callable declaration locators | Design | Pending |
| RP-15 | P1: Callable declaration locators | Design | Pending |
| RP-16 | P1: Callable declaration locators | Design | Pending |
| RP-17 | P1: Callable declaration locators | Design | Pending |
| RP-18 | P1: Bounded catalogs | Design | Pending |
| RP-19 | P1: Bounded catalogs | Design | implemented |
| RP-20 | P1: Bounded catalogs | Design | implemented |
| RP-21 | P1: Bounded catalogs | Design | Pending |
| RP-22 | P1: Bounded catalogs | Design | Pending |
| RP-23 | P1: Bounded catalogs | Design | Pending |
| RP-24 | P1: Bounded catalogs | Design | Pending |
| RP-25 | P1: Compact postings | Design | Pending |
| RP-26 | P1: Compact postings | Design | implemented |
| RP-27 | P1: Compact postings | Design | implemented |
| RP-28 | P1: Compact postings | Design | Pending |
| RP-29 | P1: Compact postings | Design | Pending |
| RP-30 | P1: Compact postings | Design | Pending |
| RP-31 | P1: Markdown pages for architecture identities | Design | Pending |
| RP-32 | P1: Markdown pages for architecture identities | Design | Pending |
| RP-33 | P1: Markdown pages for architecture identities | Design | Pending |
| RP-34 | P1: Markdown pages for architecture identities | Design | Pending |
| RP-35 | P1: Markdown pages for architecture identities | Design | Pending |
| RP-36 | P1: Markdown pages for architecture identities | Design | Pending |
| RP-37 | P1: Retrieval guide and generated AGENTS.md | Design | Pending |
| RP-38 | P1: Retrieval guide and generated AGENTS.md | Design | Pending |
| RP-39 | P1: Retrieval guide and generated AGENTS.md | Design | Pending |
| RP-40 | P1: Retrieval guide and generated AGENTS.md | Design | Pending |
| RP-41 | P1: Projection validation | Design | Pending |
| RP-42 | P1: Projection validation | Design | Pending |
| RP-43 | P1: Projection validation | Design | Pending |
| RP-44 | P1: Projection validation | Design | Pending |
| RP-45 | P1: Projection validation | Design | Pending |
| RP-46 | P1: Determinism, security and assembly isolation | Design | implemented |
| RP-47 | P1: Determinism, security and assembly isolation | Design | Pending |
| RP-48 | P1: Determinism, security and assembly isolation | Design | implemented |
| RP-49 | P1: Determinism, security and assembly isolation | Design | Pending |
| RP-50 | P1: Determinism, security and assembly isolation | Design | Pending |
| RP-51 | P1: Determinism, security and assembly isolation | Design | Pending |
| RP-52 | P2: Bounded artifacts and shard shape | - | Pending |
| RP-53 | P2: Bounded artifacts and shard shape | - | Pending |
| RP-54 | P2: Bounded artifacts and shard shape | - | Pending |
| RP-55 | P2: Bounded artifacts and shard shape | - | Pending |
| RP-56 | P2: Bounded artifacts and shard shape | - | Pending |
| RP-57 | P1: Projector seam and atomic publication | Design | Pending |

**ID format:** `RP-[NUMBER]`

**Coverage:** 57 total, 0 mapped to tasks (the Tasks phase has not run), 0 unmapped.

---

## Decisions to record in STATE.md

| ID | Decision |
| --- | --- |
| AD-019 | Projection runs inside `Commit()` over the wire document. The projector port is declared by `Csharp2Md.Storage` and implemented by `Csharp2Md.Projection`; projections join the same atomic publication, and a projection failure aborts it. Closes the seam AD-014 deferred to this workstream. |
| AD-020 | `Symbol` carries a declaration locator (document, span, source hash) filled by `SymbolFactEmitter`. It is not an identity component, so `contracts/taxonomy-registry.json` stays byte-identical. |
| AD-022 | The transactional store takes a source-document reader at `Open` and materializes `source/` fragments lazily, one document at a time. The snapshot carries no source bytes, so the package never holds a second full copy of the source alongside Roslyn's. |
| AD-021 | The `source/` projection is byte-faithful outside declared redaction spans. Suspected-secret spans are replaced by a fixed-length marker and the redaction is declared with the original and published hashes. |

---

## Success Criteria

- [ ] An agent can answer "who calls X", "what contract does operation Y accept" and "which table does Z write" by reading at most three files, each read resolving through a cited artifact key and ordinal.
- [ ] Every callable reachable from a catalog can be read completely from a `source/` artifact using only its published locator.
- [ ] No suspected secret's bytes appear anywhere in the published package, and every redaction is declared with both hashes.
- [ ] Every link in every Markdown page, catalog and posting resolves to an artifact in the same publication, enforced before staging is written.
- [ ] Two runs from different clone paths with shuffled input order produce byte-identical projection artifacts.
- [ ] The full gate passes with no regression against the 1342 tests already on `master`.
