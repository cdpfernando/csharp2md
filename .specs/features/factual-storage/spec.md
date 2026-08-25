# Factual Storage Specification

## Problem Statement

Workstream 2 proved staging, commit, abort and manifest-last ordering against an in-memory port. Nothing yet defines the wire, writes a navigable package, or refuses a corrupt one. Workstreams 4 through 6 cannot fill extractors, classifiers or projectors until observation and fact bytes, schemas, commit-time gates and factual read-back exist as one contract.

## Goals

- [ ] Publish versioned JSON Schemas and round-trip every Domain family plus the package envelopes, with empty families legal.
- [ ] Ship a filesystem transactional adapter that stages, validates, and publishes the manifest last, preserving the last valid package on abort.
- [ ] Make Storage the last gate: schema, registry, identity, hash, path and domain-construction checks, with quarantine for invalid derived records and abort for structural corruption.
- [ ] Expose a Storage reader that returns Domain types from a committed solution package.
- [ ] Require `analyze --output` and write a schema-valid empty package from the stub pipeline, touching no path outside that root.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Roslyn, MSBuild, Inventory, authorized-root enforcement, source fidelity | Owned by workstream 4 `roslyn-observation-extraction` |
| Extractors, classifiers, promotion rules, framework support | Owned by workstreams 4 and 5A–5D; this feature supplies the contracts they serialize into |
| Catalogs, postings, source locators, Markdown, `retrieval.md` | Owned by workstream 6 `retrieval-projections` |
| Batch manifest and proven cross-solution composition | Owned by workstream 7 `multi-solution-composition` |
| `validate` and `compose` verbs, labeled-corpus certification, performance baselines, numeric shard byte ceilings | Owned by workstream 8 `generator-cli-projections-certification` |
| Query engines, `kb`, QMD, embeddings, wiki compilation | Deferred by AD-011 |
| Incremental analysis, snapshot diff, SQLite or any database as factual authority | Explicit non-goals; AD-007 requires a file package |
| Changes to Domain descriptor tables or `contracts/taxonomy-registry.json` bytes | Workstream 1 is closed; wire schemas are the first publication of `schema_version` 1 |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here. Nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Wire completeness without extractors | Schemas and round-trips for every Domain family and the package envelopes now; tests are the first producers | Confirmed with the user; later workstreams must not invent serialization | y |
| How a package lands on disk | Filesystem adapter is production; `analyze` requires `--output` | Confirmed with the user; this workstream exists to make the package real, and bootstrap omitted options the skeleton could not honor | y |
| Where invalid data dies | Storage commit is the last gate | Confirmed with the user; a byte bucket would let a buggy caller publish an unreadable package | y |
| Factual read-back | Storage exposes a reader that returns Domain types; Analysis public surface does not | Confirmed with the user; Projection and later CLI need read-back without growing an Analysis read port | y |
| Wire format | JSON with committed JSON Schema files under `contracts/` | Stated during discuss and not objected to; AD-007 requires files an LLM can open, and the architecture already names schemas | y |
| In-memory adapter | Retained for tests that do not need a package; it writes no files | Stated during discuss and not objected to; pipeline unit tests must not depend on disk | y |
| Output layout | `--output` is the batch root; each solution uses a child directory keyed by logical solution identity, including when N = 1; no batch manifest | Stated during discuss and not objected to; a stable per-solution layout lets workstream 7 add a root manifest later without moving packages | y |
| Empty families | Omit the payload shard; the manifest reports count 0 | Stated as an assumption and not objected to; empty arrays would bloat the package without adding navigation | y |
| Envelope vs taxonomy | Manifest, coverage, run certification, diagnostics, quarantine and measurements are package contracts, not Domain types | Stated as an assumption and not objected to; AD-013 keeps Domain as taxonomy | y |
| Domain reference from Storage | Storage references Domain; the bootstrap tests that forbade that reference and treated Storage as an opaque byte bucket are superseded; Storage still does not classify or promote | Implied by the reader and last-gate decisions the user confirmed; reconstruction through Domain `Create` is the allowlist/secret gate | y |
| `--output` vs ENG-45 | ENG-45’s prohibition of `--output` is superseded; the other names on that list stay forbidden | Implied by the CLI decision the user confirmed | y |
| ENG-16 filesystem writes | In-memory runs still write nothing; filesystem runs write only under the requested output root | Constrains ENG-16 rather than deleting it; a snapshot of the working tree still passes when `--output` is outside it | y |
| Non-package clobber | Refuse to write a solution child that exists without a committed csharp2md manifest; leave that path unchanged | Stated as an assumption and not objected to; atomic replace must not delete the user’s unrelated files | y |
| Overlapping sessions | Reject a second `Open` of the same solution package root while another session has neither committed nor aborted | Stated as an assumption and not objected to; two writers would make abort-preserves-last-valid untestable | y |
| Timestamps | Timestamps and runtime measurements live only in the measurements envelope | Follows `docs/architecture/output-and-retrieval.md`; canonical payload bytes must be deterministic | y |
| Registry bytes | `contracts/taxonomy-registry.json` remains byte-identical | Wire schemas publish `schema_version` 1 already declared by `TaxonomyVersions.Initial`; descriptor tables do not change | y |
| Quarantine vs abort split | Invalid derived records (Architecture, Contract, Persistence, Configuration facts, and confirmed relations that fail schema or Domain construction) go to quarantine and the rest commits; identity collision, hash mismatch, absolute path, allowlist/secret failure, unreadable staging and I/O errors abort | Follows `docs/architecture/quality-and-security.md`; not a new capability | y |
| Mapper and output-root placement | Left to `design.md`, constrained by Analysis never referencing Storage, Domain having no JSON, Storage owning schemas, and the reader returning Domain types | No user-visible fork once those boundaries hold | n |
| JSON Schema dialect and shard file names | Left to `design.md` | Physical names are not product behavior; logical partitions are | n |
| Numeric shard byte ceilings | Out of this spec | Architecture freezes logical semantics before measured physical budgets; workstream 8 owns the budgets | n |

**Open questions:** none - all resolved or logged above.

---

## Implicit-requirement dimensions sweep

Large scope, so every dimension resolves to a requirement or an explicit exclusion.

| Dimension | Coverage |
| --- | --- |
| Input validation and bounds | STOR-20, STOR-21, STOR-47, STOR-48 — `--output` is required; an existing file and a non-package child directory are refused by name |
| Failure and partial-failure states | STOR-18, STOR-23, STOR-25, STOR-31, STOR-57 — abort preserves the last valid package; I/O errors abort that solution only; one failed solution does not block the others |
| Idempotency, retry, duplicate handling | STOR-17, STOR-27, STOR-44, STOR-45 — atomic replace of an owned package; identity collision aborts; staging order and a second commit of the same graph are byte-identical |
| Auth boundaries and rate limits | N/A because the engine is a local in-process tool with no network surface |
| Concurrency and ordering | STOR-16, STOR-19, STOR-56, STOR-59 — manifest last, per-solution children, independent commits, overlapping `Open` rejected |
| Data lifecycle and expiry | STOR-06, STOR-17, STOR-18, STOR-58 — empty families omitted; success replaces; abort preserves; cancelled staging is deleted. TTL and archival are N/A because generated packages are operator-owned files |
| Observability | STOR-23, STOR-25, STOR-26, STOR-36, STOR-55 — failing gate, artifact key, unregistered value, I/O error and unpublished solutions are named; CLI exit 2 on structural abort |
| External-dependency failure | STOR-23 — the only external dependency this feature adds is the filesystem; permission and disk-full failures abort and preserve the last valid package. No network |
| State-transition integrity | STOR-16, STOR-31, STOR-33, STOR-60, STOR-61 — commit publishes manifest last or not at all; unknowns still commit; a session commits at most once; staging after commit is rejected |

---

## User Stories

### P1: Versioned wire contracts for every Domain family ⭐ MVP

**User Story**: As a workstream 4–6 author, I want committed JSON Schemas for every Domain family and the package envelopes so that I serialize into a contract I do not invent.

**Why P1**: Without the wire, every later stage would ship a private JSON shape and Storage could not validate.

**Acceptance Criteria**:

1. The Storage assembly SHALL publish a JSON Schema for each registered fact type: Solution, Project, Document, Symbol, Component, DeploymentUnit, EntryPoint, BoundaryOperation, ExternalSystem, Contract, ContractBinding, ContractRevision, DataStore, DataObject, DataField, DataOperation and ConfigurationBinding. (STOR-01)
2. The Storage assembly SHALL publish a JSON Schema for each registered observation kind: invocation, object-creation, type-usage, base-type, attribute-usage, assignment, configuration, route-declaration, message-operation and data-access. (STOR-02)
3. The Storage assembly SHALL publish JSON Schemas for ConfirmedRelation, CandidateLink, UnresolvedRecord and OpenFrontier. (STOR-03)
4. The Storage assembly SHALL publish JSON Schemas for the package envelopes manifest, coverage, run_certification, diagnostics, quarantine and measurements. (STOR-04)
5. WHEN a package is committed THEN it SHALL contain a copy of `contracts/taxonomy-registry.json` whose bytes equal the committed registry file. (STOR-05)
6. WHEN a fact family, observation kind or relation kind has zero records THEN the package SHALL omit that payload shard and the manifest SHALL report count 0 for it. (STOR-06)
7. The committed JSON Schema files SHALL live under `contracts/` and SHALL declare `schema_version` 1. (STOR-07)
8. IF a committed schema file differs from the schema emitted by the serializer contract THEN the drift gate test SHALL fail naming the differing file. (STOR-08)
9. WHEN the registry emitter runs after this feature THEN `contracts/taxonomy-registry.json` SHALL remain byte-identical to its committed content. (STOR-09)

**Independent Test**: Enumerate the registered fact types, observation kinds and relation records against the committed schema directory; commit an empty graph and assert omitted shards plus manifest counts of 0; run both drift gates.

---

### P1: Canonical round-trip through Domain construction ⭐ MVP

**User Story**: As a consumer of a generated package, I want every stored fact and observation to round-trip through Domain construction so that the wire cannot represent a state the taxonomy rejects.

**Why P1**: The last gate is only as strong as reconstruction. An opaque byte store would make TAX-80 and the registry unenforceable on disk.

**Acceptance Criteria**:

1. WHEN a valid Domain fact, observation, confirmed relation, candidate link, unresolved record or open frontier is committed and then read THEN the reader SHALL return a value equal to the original under Domain equality. (STOR-10)
2. The `Csharp2Md.Storage` project SHALL declare a project reference to `Csharp2Md.Domain`. (STOR-11)
3. The `Csharp2Md.Storage` assembly SHALL NOT classify, promote or invent facts, observations or relations. (STOR-12)
4. WHEN Storage accepts a fact or observation payload at commit THEN it SHALL reconstruct that record through the domain’s public construction API. (STOR-13)

**Independent Test**: Commit one fixture per family and kind, read it back, and assert Domain equality; assert Storage references Domain; assert no classifier/promoter type exists in Storage; assert an out-of-allowlist literal is rejected by commit, not stored.

---

### P1: Filesystem transactional publication ⭐ MVP

**User Story**: As an operator, I want a successful analysis to publish a package on disk with the manifest last so that an interrupted run never leaves a mixed old/new tree.

**Why P1**: AD-007 is a navigable file package. The in-memory adapter cannot be that package.

**Acceptance Criteria**:

1. The `Csharp2Md.Storage` assembly SHALL provide a filesystem adapter that implements the transactional storage port. (STOR-14)
2. The `Csharp2Md.Storage` assembly SHALL retain the in-memory adapter that implements the transactional storage port. (STOR-15)
3. WHEN a filesystem commit succeeds THEN the manifest SHALL be the last published artifact in that solution package. (STOR-16)
4. WHEN a filesystem commit succeeds against a directory that already holds a csharp2md package THEN the adapter SHALL replace that package atomically so no observer can read a mix of old and new artifacts. (STOR-17)
5. IF a filesystem session aborts or a commit fails THEN every artifact from the last successful commit for that solution SHALL remain byte-identical. (STOR-18)
6. WHEN one or more solutions are committed under one output root THEN each solution SHALL occupy a child directory keyed by its logical solution identity. (STOR-19)
7. IF the solution child path exists and does not contain a committed csharp2md manifest THEN the adapter SHALL refuse to write and SHALL leave that path unchanged. (STOR-20)
8. IF the output root path names an existing file THEN the adapter SHALL refuse to write naming that path. (STOR-21)
9. WHEN the output root directory does not exist THEN the adapter SHALL create it. (STOR-22)
10. IF the adapter cannot create, write or replace files because of an I/O error THEN it SHALL abort, name the error, and leave the last valid package unchanged. (STOR-23)
11. WHERE the in-memory adapter is used the system SHALL create no files and no directories. (STOR-24)

**Independent Test**: Commit twice to the same child and assert atomic replace plus byte-identical preservation on a forced abort; refuse a non-package child and a file-as-root; create a missing root; assert the in-memory adapter still writes nothing.

---

### P1: Commit-time structural gates ⭐ MVP

**User Story**: As a downstream reader, I want commit to refuse a corrupt package so that I never have to guess whether a file is authority.

**Why P1**: The quality document’s abort vs quarantine split is the difference between a broken package and a degraded but readable one.

**Acceptance Criteria**:

1. IF a staged payload fails its JSON Schema THEN commit SHALL abort as structural corruption and SHALL name the artifact key. (STOR-25)
2. IF a staged payload names a fact type, observation kind or relation kind that is absent from the taxonomy registry THEN commit SHALL abort as structural corruption naming the unregistered value. (STOR-26)
3. IF two facts in one solution package share one identity string THEN commit SHALL abort as structural corruption naming the identity. (STOR-27)
4. IF a content hash in a payload does not match the bytes it claims to cover THEN commit SHALL abort as structural corruption naming the identity. (STOR-28)
5. IF a canonical payload contains an absolute filesystem path THEN commit SHALL abort as structural corruption naming the field. (STOR-29)
6. IF a Structural fact or an observation cannot be reconstructed through the domain’s public construction API THEN commit SHALL abort as structural corruption naming the identity. (STOR-30)
7. WHEN commit aborts for structural corruption THEN no new manifest SHALL be published for that session. (STOR-31)
8. IF an Architecture, Contract, Persistence or Configuration fact or a confirmed relation fails schema or domain construction THEN commit SHALL write that record to quarantine with a named diagnostic, SHALL omit it from canonical payloads, SHALL mark run certification failed, and SHALL commit the remaining valid artifacts. (STOR-32)
9. WHEN a run produces unknowns, candidates or open frontiers but no structural corruption THEN the filesystem adapter SHALL commit the package. (STOR-33)

**Independent Test**: Drive each abort fixture (schema, unregistered value, identity collision, bad hash, absolute path, allowlist failure) and assert Unpublished plus preserved prior package; drive one invalid derived fact among valid structural facts and assert quarantine plus a committed manifest; commit a candidate-only graph.

---

### P1: Factual package reader ⭐ MVP

**User Story**: As a workstream 6 author, I want to read a committed package back into Domain types so that projection does not parse JSON by convention.

**Why P1**: A write-only store would force every consumer to re-implement the wire.

**Acceptance Criteria**:

1. The `Csharp2Md.Storage` assembly SHALL expose a public reader that accepts a committed solution-package directory and returns Domain facts, observations, confirmed relations, candidate links, unresolved records and open frontiers. (STOR-34)
2. The `Csharp2Md.Analysis` public surface SHALL NOT include the factual reader. (STOR-35)
3. IF the reader is given a directory that fails any abort-class commit-time gate THEN it SHALL reject the read naming the failing gate and SHALL return no partial snapshot. (STOR-36)
4. IF the reader is given a path that is not a csharp2md package THEN it SHALL reject the read naming the path. (STOR-37)
5. WHEN the reader loads a package that contains quarantined records THEN it SHALL return the valid Domain snapshot and SHALL expose the quarantine records separately from confirmed facts and relations. (STOR-38)

**Independent Test**: Read a committed fixture package and assert Domain equality with the written graph; point the reader at Analysis exported types and assert absence; reject a truncated package and a random directory with the path or gate named.

---

### P1: Compact payload layout ⭐ MVP

**User Story**: As an LLM opening the package with ordinary file reads, I want bounded shards keyed independently of display names so that I am not forced through a directory per symbol or one monolithic relation file.

**Why P1**: `output-and-retrieval.md` freezes these layout invariants before workstream 6 adds postings. Putting them off would let tests grow a layout workstream 6 then has to break.

**Acceptance Criteria**:

1. The package SHALL NOT contain a directory whose name is a fact identity or an observation identity. (STOR-39)
2. Payload artifacts SHALL be partitioned by fact family, observations by observation kind, and confirmed relations by relation kind. (STOR-40)
3. Shard and bucket keys SHALL be independent of display names and translated labels. (STOR-41)
4. A canonical payload record SHALL be serialized once; other package files SHALL reference it by identity or locator rather than duplicating its bytes. (STOR-42)
5. IF an artifact carries a timestamp or a runtime measurement THEN it SHALL live only in the measurements envelope, not in a canonical payload file. (STOR-43)
6. WHEN the same Domain graph is committed twice THEN every canonical payload file SHALL be byte-identical. (STOR-44)
7. WHEN the same fragments are staged in two different orders THEN the committed canonical payload files SHALL be byte-identical. (STOR-45)
8. The package SHALL NOT emit catalogs, postings, Markdown pages, source projections or a retrieval guide. (STOR-46)

**Independent Test**: Commit a graph with several families and kinds; assert partition files and the absence of identity-named directories, duplicated payload bytes, timestamps in canonical files, and any posting/Markdown/source file.

---

### P1: CLI `--output` and a valid empty package ⭐ MVP

**User Story**: As an operator, I want `analyze --solution … --output <dir>` to write a schema-valid package even while extractors are still stubs so that the tool honors every option it exposes.

**Why P1**: A filesystem adapter that only tests can reach is not production. The stub pipeline must emit a legal empty package or workstream 4 inherits a lying contract.

**Acceptance Criteria**:

1. The `analyze` verb SHALL require `--output <dir>` and at least one `--solution`. (STOR-47)
2. IF `analyze` is invoked with no `--output` option THEN the CLI SHALL exit with code 1 and write a message naming the missing option to standard error. (STOR-48)
3. WHEN `analyze` completes with no structural failure THEN the CLI SHALL write a schema-valid package for every requested solution under the output root and SHALL exit with code 0. (STOR-49)
4. WHEN the bootstrapped pipeline produces zero facts, zero observations and zero relations THEN each written package SHALL be schema-valid, SHALL include the taxonomy-registry copy and a manifest, and SHALL report count 0 for every family. (STOR-50)
5. WHEN `analyze` writes a package THEN no file or directory outside the requested output root SHALL be created, modified or deleted. (STOR-51)
6. The CLI SHALL NOT expose an option named `--topic`, `--domain`, `--manifest`, `--trust`, `--include-source-generators` or `--analysis-timeout`. (STOR-52)
7. The `Csharp2Md.Cli` project SHALL continue to declare no project reference to `Csharp2Md.Domain`. (STOR-53)
8. WHEN a run reports unknowns, candidates or open frontiers but no structural failure THEN the CLI SHALL write the package and SHALL exit with code 0. (STOR-54)
9. IF a structural failure aborts publication for any requested solution THEN the CLI SHALL exit with code 2. (STOR-55)

**Independent Test**: Invoke the built tool with a missing `--output`, a valid `--output` on the synthetic fixture, a forced structural failure, and a snapshot of the working tree excluding the output root.

---

### P1: Session isolation and cleanup ⭐ MVP

**User Story**: As an operator analyzing several solutions, I want each package isolated and leftover staging gone so that one failure cannot corrupt another solution or leave a half-written tree.

**Why P1**: AD-008 isolation is already true in memory. It has to remain true on disk, including cancellation.

**Acceptance Criteria**:

1. WHEN more than one solution is requested THEN each SHALL commit or abort independently under its own child directory. (STOR-56)
2. IF one solution’s commit aborts THEN every remaining solution SHALL still be analyzed and, on success, committed. (STOR-57)
3. IF a session is aborted or cancelled THEN the adapter SHALL leave no staging directory and no partial artifact for that solution. (STOR-58)
4. IF a second session opens the same solution package root while another session on that root has neither committed nor aborted THEN the adapter SHALL reject the second open naming the root. (STOR-59)
5. WHEN a store session has already committed THEN a further `Commit` on that session SHALL be rejected. (STOR-60)
6. WHEN a store session has already committed THEN a further `Stage` on that session SHALL be rejected. (STOR-61)

**Independent Test**: Run two solutions with the second forced to abort; assert the first package is valid and the second child is unchanged or absent; cancel mid-stage and assert no staging residue; assert overlapping `Open` and double `Commit` are named rejections.

---

## Edge Cases

- IF `--output` is omitted THEN the CLI SHALL exit 1 naming `--output` (STOR-48).
- IF `--output` names an existing file THEN the adapter SHALL refuse naming that path (STOR-21).
- IF a solution child exists without a csharp2md manifest THEN the adapter SHALL refuse and leave it unchanged (STOR-20).
- IF disk-full or permission errors occur during staging THEN commit SHALL abort, name the I/O error, and preserve the last valid package (STOR-23).
- IF two facts collide on one identity THEN commit SHALL abort naming the identity (STOR-27).
- IF a classified fact fails construction THEN it SHALL be quarantined and the rest SHALL commit (STOR-32).
- IF a canonical payload contains an absolute path THEN commit SHALL abort naming the field (STOR-29).
- WHEN N = 1 THEN the package SHALL still occupy a child directory under the output root (STOR-19).
- WHEN the pipeline emits zeros THEN the package SHALL still be schema-valid with count 0 (STOR-50).
- WHEN cancellation arrives mid-session THEN no staging residue SHALL remain (STOR-58).
- WHERE the in-memory adapter is used no files SHALL be created (STOR-24).

---

## Requirement Traceability

Each requirement gets a unique ID for tracking across design, tasks, and validation.

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| STOR-01 | P1: Versioned wire contracts for every Domain family | Execute (T14) | Implementing |
| STOR-02 | P1: Versioned wire contracts for every Domain family | Execute (T14) | Implementing |
| STOR-03 | P1: Versioned wire contracts for every Domain family | Execute (T14) | Implementing |
| STOR-04 | P1: Versioned wire contracts for every Domain family | Execute (T14) | Implementing |
| STOR-05 | P1: Versioned wire contracts for every Domain family | Execute (T15) | Implementing |
| STOR-06 | P1: Versioned wire contracts for every Domain family | Execute (T16) | Implementing |
| STOR-07 | P1: Versioned wire contracts for every Domain family | Execute (T13) | Implementing |
| STOR-08 | P1: Versioned wire contracts for every Domain family | Execute (T14) | Implementing |
| STOR-09 | P1: Versioned wire contracts for every Domain family | Execute (T15) | Implementing |
| STOR-10 | P1: Canonical round-trip through Domain construction | Execute (T17) | Implementing |
| STOR-11 | P1: Canonical round-trip through Domain construction | Execute (T4) | Implementing |
| STOR-12 | P1: Canonical round-trip through Domain construction | Execute (T4) | Implementing |
| STOR-13 | P1: Canonical round-trip through Domain construction | Execute (T16) | Implementing |
| STOR-14 | P1: Filesystem transactional publication | Tasks | In Tasks |
| STOR-15 | P1: Filesystem transactional publication | Execute (T27) | Implementing |
| STOR-16 | P1: Filesystem transactional publication | Execute (T28) | Implementing |
| STOR-17 | P1: Filesystem transactional publication | Tasks | In Tasks |
| STOR-18 | P1: Filesystem transactional publication | Tasks | In Tasks |
| STOR-19 | P1: Filesystem transactional publication | Tasks | In Tasks |
| STOR-20 | P1: Filesystem transactional publication | Tasks | In Tasks |
| STOR-21 | P1: Filesystem transactional publication | Tasks | In Tasks |
| STOR-22 | P1: Filesystem transactional publication | Tasks | In Tasks |
| STOR-23 | P1: Filesystem transactional publication | Tasks | In Tasks |
| STOR-24 | P1: Filesystem transactional publication | Execute (T28) | Implementing |
| STOR-25 | P1: Commit-time structural gates | Execute (T20) | Implementing |
| STOR-26 | P1: Commit-time structural gates | Execute (T21) | Implementing |
| STOR-27 | P1: Commit-time structural gates | Execute (T22) | Implementing |
| STOR-28 | P1: Commit-time structural gates | Execute (T22) | Implementing |
| STOR-29 | P1: Commit-time structural gates | Execute (T23) | Implementing |
| STOR-30 | P1: Commit-time structural gates | Execute (T24) | Implementing |
| STOR-31 | P1: Commit-time structural gates | Tasks | In Tasks |
| STOR-32 | P1: Commit-time structural gates | Execute (T25) | Implementing |
| STOR-33 | P1: Commit-time structural gates | Execute (T26) | Implementing |
| STOR-34 | P1: Factual package reader | Tasks | In Tasks |
| STOR-35 | P1: Factual package reader | Execute (T5) | Implementing |
| STOR-36 | P1: Factual package reader | Tasks | In Tasks |
| STOR-37 | P1: Factual package reader | Tasks | In Tasks |
| STOR-38 | P1: Factual package reader | Tasks | In Tasks |
| STOR-39 | P1: Compact payload layout | Tasks | In Tasks |
| STOR-40 | P1: Compact payload layout | Tasks | In Tasks |
| STOR-41 | P1: Compact payload layout | Tasks | In Tasks |
| STOR-42 | P1: Compact payload layout | Execute (T19) | Implementing |
| STOR-43 | P1: Compact payload layout | Tasks | In Tasks |
| STOR-44 | P1: Compact payload layout | Execute (T6) | Implementing |
| STOR-45 | P1: Compact payload layout | Tasks | In Tasks |
| STOR-46 | P1: Compact payload layout | Tasks | In Tasks |
| STOR-47 | P1: CLI `--output` and a valid empty package | Tasks | In Tasks |
| STOR-48 | P1: CLI `--output` and a valid empty package | Tasks | In Tasks |
| STOR-49 | P1: CLI `--output` and a valid empty package | Tasks | In Tasks |
| STOR-50 | P1: CLI `--output` and a valid empty package | Execute (T28) | Implementing |
| STOR-51 | P1: CLI `--output` and a valid empty package | Tasks | In Tasks |
| STOR-52 | P1: CLI `--output` and a valid empty package | Tasks | In Tasks |
| STOR-53 | P1: CLI `--output` and a valid empty package | Tasks | In Tasks |
| STOR-54 | P1: CLI `--output` and a valid empty package | Tasks | In Tasks |
| STOR-55 | P1: CLI `--output` and a valid empty package | Tasks | In Tasks |
| STOR-56 | P1: Session isolation and cleanup | Tasks | In Tasks |
| STOR-57 | P1: Session isolation and cleanup | Tasks | In Tasks |
| STOR-58 | P1: Session isolation and cleanup | Tasks | In Tasks |
| STOR-59 | P1: Session isolation and cleanup | Execute (T29) | Implementing |
| STOR-60 | P1: Session isolation and cleanup | Execute (T29) | Implementing |
| STOR-61 | P1: Session isolation and cleanup | Execute (T29) | Implementing |

**ID format:** `STOR-[NUMBER]`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 61 total, 61 mapped to tasks, 0 unmapped

---

## Success Criteria

- [ ] `csharp2md analyze --solution <fixture> --output <dir>` exits 0 and writes a schema-valid empty package (manifest last, taxonomy-registry copy, every family count 0) only under `<dir>`.
- [ ] Omitting `--output` exits 1 naming the option. A structural abort exits 2 and leaves the last valid package byte-identical.
- [ ] One fixture per Domain family round-trips through commit and the Storage reader under Domain equality.
- [ ] Abort-class fixtures (schema, collision, bad hash, absolute path, allowlist) publish no new manifest. An invalid derived fact lands in quarantine and the rest commits.
- [ ] Two staging orders of the same graph produce byte-identical canonical payloads. No identity-named directories. No postings or Markdown.
- [ ] Storage references Domain and does not classify. Analysis public surface has no reader. CLI has no Domain project reference.
- [ ] Every requirement ID STOR-01 through STOR-61 has at least one test asserting a spec-defined outcome.
