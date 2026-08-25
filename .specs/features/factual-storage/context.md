# Factual Storage Context

**Gathered:** 2026-08-25
**Spec:** `.specs/features/factual-storage/spec.md`
**Status:** Ready for execute

---

## Feature Boundary

Workstream 3 of the architecture-knowledge-engine roadmap. It gives the engine a real factual package: versioned JSON wire contracts and schemas for every Domain family, a filesystem transactional adapter that stages then publishes the manifest last, commit-time validation against those contracts, compact payload partitioning, and a Storage reader that returns Domain types.

It does not extract, classify, project Markdown, emit postings or catalogs, or compose a batch manifest. Inventory and Roslyn stay in workstream 4. Classifiers stay in workstreams 5A–5D. Retrieval projections stay in workstream 6.

---

## Implementation Decisions

### Wire completeness without extractors

- Schemas and round-trips exist now for every Domain family: all 17 fact types, all 10 observation kinds, confirmed relations, candidate links, unresolved records and open frontiers.
- Package envelopes also have schemas: manifest, coverage, run certification, diagnostics, quarantine and measurements.
- A committed package includes a byte-identical copy of `contracts/taxonomy-registry.json`.
- Empty families are legal: omit the payload shard, report count 0 on the manifest.
- Tests are the first producers. Later extractors and classifiers fill the same contracts.
- Envelope types are package contracts, not taxonomy. Domain descriptor tables stay unchanged.

### How a package lands on disk

- A filesystem adapter implementing `ITransactionalStore` is the production store. The in-memory adapter remains for tests that do not need a package.
- `analyze` requires `--output <dir>` in addition to `--solution`. Missing `--output` is an invalid invocation (exit 1).
- `--output` is the batch root. Each solution commits to a child directory keyed by that solution’s logical identity, including when N = 1. This workstream does not write a batch manifest.
- A successful commit atomically replaces the previous csharp2md package at that child. Abort leaves the last valid package byte-identical.
- The adapter creates a missing output root. It refuses if `--output` names an existing file. It refuses if the solution child exists and is not a csharp2md package (no committed manifest), and it leaves that path unchanged.
- Analysis writes only under the requested output root. ENG-16’s “no filesystem write” still holds for the in-memory adapter and for paths outside `--output`.
- ENG-45’s prohibition of `--output` is superseded. `--topic`, `--domain`, `--manifest`, `--trust`, `--include-source-generators` and `--analysis-timeout` stay absent.

### Where invalid data dies

- Storage commit is the last gate. Analysis may fail earlier later; it is not the only gate.
- Reconstructing each fact and observation through the domain’s public construction API is how the allowlist, secrets and registry membership are enforced on the wire.
- Unknown wire type/kind, identity collision, hash mismatch, absolute path, unreadable staging or I/O error is structural corruption: abort, no new manifest, last valid package preserved.
- A Structural fact or observation that fails domain construction (including the literal allowlist) also aborts.
- An Architecture, Contract, Persistence or Configuration fact, or a confirmed relation, that fails schema or domain construction is an invalid derived record: it goes to quarantine with a named diagnostic, it is omitted from canonical payloads, run certification records failure, and the remaining valid artifacts still commit.
- Unknowns, candidates and open frontiers still commit.

### Factual read-back

- `Csharp2Md.Storage` exposes a public reader that takes a committed solution-package directory and returns Domain types.
- The reader runs the same gates as commit. A failing package yields no partial snapshot.
- Analysis public surface does not grow a read port. CLI still has no project reference to Domain.
- Storage references Domain. That supersedes the bootstrap tests that forbade a Domain reference and treated Storage as an opaque byte bucket. Storage still does not classify, promote or invent knowledge.

### Compact payload primitives

- Partition payloads by fact family, observations by kind, confirmed relations by relation kind.
- No directory named for a high-cardinality identity. Bucket keys are independent of display names.
- Canonical payload is serialized once. Timestamps and runtime measurements live only in the measurements envelope.
- Catalogs, postings, Markdown, source projections and `retrieval.md` are not emitted.
- Numeric shard byte ceilings are not part of this spec.

### Agent's Discretion

None. Every gray area was decided explicitly. Mapper placement (Analysis vs Storage) and whether the output root lives on `AnalysisRequest` or the adapter constructor are left to `design.md`, under the locked boundaries: Analysis never references Storage, Domain has no JSON, Storage is the schema authority, the reader returns Domain types.

### Declined / Undiscussed Gray Areas → Assumptions

None declined. These were stated during discuss and not objected to; they are recorded in the spec’s Assumptions table:

- JSON plus committed JSON Schema files; not a database.
- In-memory adapter stays for tests that do not need a package.
- `--output` is a batch root with per-solution children and no batch manifest.
- Empty families omit shards.
- Clobber protection for non-package directories.
- Overlapping `Open` of the same package root is rejected.
- Domain taxonomy registry bytes stay identical.

---

## Specific References

The user accepted the recommended option in all four gray areas without modification. Binding references: `docs/architecture/output-and-retrieval.md` (logical package, determinism, no dir-per-identity), `docs/architecture/quality-and-security.md` (quarantine vs abort), `docs/architecture/architecture-knowledge-engine.md` (Storage module, transactional port), AD-006, AD-007, AD-013, AD-014.

---

## Deferred Ideas

- **Inventory, Roslyn binding and source fidelity** — workstream 4 `roslyn-observation-extraction`.
- **Classifiers and promotion** — workstreams 5A–5D.
- **Catalogs, postings, source locators, Markdown and retrieval scenarios** — workstream 6 `retrieval-projections`.
- **Batch manifest and proven cross-solution composition** — workstream 7 `multi-solution-composition`.
- **`validate` / `compose` verbs, performance baselines and numeric shard ceilings** — workstream 8 `generator-cli-projections-certification`.
- **Query engines, wiki, embeddings, incremental analysis** — post-generator, AD-011.
