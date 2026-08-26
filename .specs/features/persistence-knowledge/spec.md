# Persistence Knowledge Specification

## Problem Statement

Workstream 4 fills the observation ledger and workstream 5A promoted architecture and contract facts, but the `Persistence` fact family is still empty: no `DataStore`, `DataObject`, `DataField` or `DataOperation` fact is ever produced, and the `accesses-data`, `operates-on` and `maps-to` relations have no producer. An LLM reading the package cannot answer "which tables does this flow read, what does it write, and which CLR type maps to which physical table". This workstream introduces the persistence classifier: it promotes EF Core and raw-SQL data access from the immutable ledger into persistence facts with confirmed relations, conventional-mapping candidates and unresolved records.

The ledger cannot support that promotion as it stands. `DataAccess` observations carry an empty payload, `Assignment` observations carry an empty payload, and `Invocation` payloads capture only route and client-name literals — so the operation kind, the entity type, `ToTable("order_headers")` and `HasColumnName("order_status")` never reach a classifier. Closing that gap is part of this workstream.

## Goals

- [ ] Enrich `DataAccess`, `Assignment` and `Invocation` observation payloads so persistence evidence reaches the ledger, and teach `ConfigurationDetector` to recognize `GetConnectionString`.
- [ ] Classify one `DataStore` per `DbContext`-derived type, named by its proven connection-string key when C# evidence binds one and by its CLR type name otherwise.
- [ ] Classify `DataObject` facts from exposed entity sets and from raw-SQL statement targets, with `ExplicitConfirmation` mapping state where a fluent `ToTable` proves the physical table and `ConventionalCandidate` where only convention is available.
- [ ] Classify `DataField` facts at column granularity from LINQ filters and projections, tracked writes, explicit `HasColumnName` calls and raw-SQL column lists.
- [ ] Classify `DataOperation` facts and emit confirmed `accesses-data` and `operates-on` relations with full evidence chains.
- [ ] Emit confirmed `maps-to` relations for explicitly configured object and field mappings, and `CandidateLink` records for conventional ones.
- [ ] Extend `fixtures/SyntheticSolution` so its hand-written SQL is actually executed through EF's raw-SQL APIs and its `DbContext` is registered against an observable configuration key.
- [ ] Publish persistence run-coverage counts into the snapshot diagnostics envelope.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| `ConfigurationBinding` facts and `configured-by` relations | Owned by workstream 5D; 5C reads the key literal from a `Configuration` observation but mints no configuration fact |
| Reading `appsettings.json`, `.env` or any non-C# configuration file | Non-C# adapter owned by 5D; 5C uses only C#-observable configuration access |
| Non-relational store technologies (`document`, `key-value`, `cache`) | No fixture coverage and no classifier rule; `DataStoreTechnology` stays `relational` or is not minted |
| `DataObjectForm` values `view`, `collection`, `key-space`, `cache-region` | Same reason; only `table` and `unknown` are produced |
| Dapper, ADO.NET, `SqlCommand`, `IDbConnection` data access | No fixture coverage; EF Core and EF raw-SQL only |
| A general SQL grammar or parser | A bounded leading-keyword reader is specified instead (PK-17, PK-22); anything outside it stays unresolved |
| EF migrations, `OnModelCreating` conventions other than `ToTable`/`HasColumnName`, value converters, owned types | No fixture coverage; each would need its own classifier rule and ground truth |
| Navigation properties, foreign keys, indexes, primary keys, relationships | Not representable in the registry's persistence fact family |
| `invokes` confirmed relations and call linking | Owned by workstream 5B |
| Entry points, boundary operations, contracts | Owned by workstream 5A and already complete |
| `belongs-to`, `included-in`, components, deployment units, DI grouping | Owned by workstream 5D; 5C reuses the `Component` facts 5A already mints and creates none |
| `maps-to` with `contract-implementation` or `serialization-binding` roles | Owned by workstreams 5D and 6 |
| Source projections, catalogs, postings, Markdown | Owned by workstream 6 |
| The `run_certification` document and the `persistence_coverage` gate threshold | Owned by workstream 8; 5C publishes the raw counts only |
| Changes to Domain descriptor tables or `contracts/taxonomy-registry.json` bytes | Workstream 1 is closed |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here. Nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Hand-written SQL evidence | The fixture's `OrderSqlQueries` is rewritten so each of its six statements is executed inline through `FromSqlRaw` / `FromSqlInterpolated` / `ExecuteSqlRaw`, with the literal at the call site | User decision. A SQL string returned from a method and never executed is text-shaped evidence; an execution call site makes the access semantically bound, which `accesses-data` requires | y |
| `DataStore` identity | Technology from the `DbContext` base type; name is the connection-string key when a `Configuration` observation proves one bound to that context, and the context's fully-qualified type name otherwise | User decision. Keeps 5C inside C#-observable evidence, gives the physical name when it is proven, and lets 5D add `configured-by` later without changing the identity | y |
| Field granularity | Full column granularity: every entity property reached by a LINQ filter or projection, every tracked-write target, every explicit `HasColumnName` and every raw-SQL column identifier becomes a `DataField` | User decision. The fixture was authored for this ("one filtered column, three projected ones"; "a tracked write whose property name matches two exposed entities") | y |
| Ledger payload gap | `DataAccessDetector`, `AssignmentDetector` and the invocation payload builder are extended inside this workstream; `ConfigurationDetector` learns `GetConnectionString` | User decision. AD-004 forbids a classifier re-reading Roslyn, so the evidence must reach the ledger. Observation identity includes the payload, so `DataAccess` and `Assignment` identities change — permitted by AD-002, and no existing test asserts an empty payload for either kind | y |
| Raw-SQL target resolution | The `operates-on` target of a raw-SQL access is resolved from the SQL statement's own target identifier, never from the receiver's entity type | A raw statement names its own table explicitly; inferring through the entity mapping would make two evidence sources compete for one target. The fixture's `Orders` (SQL) and `order_headers` (EF `ToTable`) therefore stay two distinct data objects | n |
| Name-similarity merging | `DataObject` facts are never merged because their names are similar; `Orders` and `order_headers` remain separate facts under the same store | The taxonomy forbids promotion from name or prefix similarity. Merging them would be exactly that | n |
| Bracketed SQL identifiers | `[Orders]` is unquoted deterministically to `Orders`; square-bracket delimiting is part of the bounded reader | T-SQL delimited identifiers are a closed, reducible grammar. Refusing them would be arbitrary, and the interpolated-table case still exercises the unresolved path. The fixture comment that calls this unreadable is superseded legacy narrative | n |
| Unproven schema name | `DataObject.SchemaName` is the literal `unknown` when no schema is proven | `StructuralLiteral` rejects empty canonical text, and inventing `dbo` would assert a provider default the code does not state | n |
| `SaveChanges` semantics | `SaveChanges` / `SaveChangesAsync` never mints its own `DataOperation`; it confirms the tracked `Assignment` observations in the same owning callable as `update` operations | `SaveChanges` is a flush, not an access. Minting an operation for it would produce an operation with no resolvable target | n |
| Stored procedures | `EXEC <name>` mints a `DataObject` with form `unknown` and `ConventionalCandidate` mapping state, plus a `DataOperation` of kind `execute` | `DataOperation.Create` requires a target `FactReference`, so dropping the target would drop the operation entirely. `form=unknown` is the honest facet | n |
| `DataOperation` identity | `DataOperation.Create(target, operation, mappingState)` carries no symbol, so two callables performing the same operation on the same object share one `DataOperation` fact, reached by two `accesses-data` relations | Domain is closed (workstream 1). The registry's documentary `identity_components` list names `symbol`, but the shipped ID grammar does not, and sharing is semantically coherent for a many-to-one `accesses-data` | n |
| Tracked-write ambiguity | `order.Amount = amount` binds semantically to `Order.Amount`; the fact that `OrderLine` also declares `Amount` creates no ambiguity | The fixture comment describing this as a two-entity match is legacy name-based narrative. Semantic binding resolves it | n |
| Classifier identity format | `csharp2md.classifier.persistence-ef` and `csharp2md.classifier.persistence-sql`, both version 1 | Matches 5A's `csharp2md.classifier.{area}` pattern; the two rule sets version independently | n |
| Pass registration | One `PersistencePass` appended to `ClassificationAndPromotionStage`'s pass list after `ContractPass` and before `RelationPass` | 5A's composable stage is the declared extension point (EBC-27). Persistence facts must exist before any later relation pass observes them | n |
| Raw SQL text in payloads | Observation payloads carry the parsed `sql-operation`, `sql-target` and `sql-columns` entries, never the statement text | The statement string is an unbounded literal that may carry credentials; the parsed fields are the allowlisted structural literals the security rules permit | n |
| Multi-valued payload entries | Column and field lists are one payload entry holding ordinal-sorted names joined by `\|` | `NormalizedPayload.Create` throws on a duplicate payload key, so one entry per column is impossible. Ordinal sorting keeps the observation identity order-independent, and `\|` survives `FactIdGrammar.RequireCanonicalText` | n |
| Coverage publication | Persistence coverage is published as a `DiagnosticRecord` in the snapshot diagnostics envelope with numerator, denominator and each unresolved occurrence's owner id | AD-017 makes the diagnostics envelope the pipeline's operational channel; the certification document is workstream 8's | n |

**Open questions:** none — all resolved or logged above.

---

## Implicit-requirement dimensions sweep

Large scope, so every dimension resolves to a requirement or an explicit exclusion.

| Dimension | Coverage |
| --- | --- |
| Input validation and bounds | PK-16, PK-17, PK-25, PK-34 — the bounded SQL reader accepts a closed keyword set and rejects everything else; `StructuralLiteral` and `FactGuards` reject empty or malformed names at construction; `SELECT *` yields no field |
| Failure and partial-failure states | PK-34, PK-35, PK-37 — an unresolvable SQL target, an unresolvable entity type and an unrecognized statement each produce an unresolved record or a diagnostic, never a confirmed edge with an invented target |
| Idempotency, retry, duplicate handling | PK-40, PK-41 — re-running the classifier over the same ledger produces identical facts, relations, candidates and unresolved records; shared `DataOperation` identities deduplicate through `SnapshotAccumulator.AddFact` |
| Auth boundaries and rate limits | N/A because the engine is a local in-process tool with no network surface and opens no database connection |
| Concurrency and ordering | PK-40, PK-41 — observation processing order does not affect classifier output; every identity is payload-derived, and emitted collections are ordered by canonical id |
| Data lifecycle and expiry | N/A because generated packages are operator-owned files |
| Observability | PK-46, PK-47 — persistence coverage numerator, denominator and each unresolved occurrence's owner reach the diagnostics envelope |
| External-dependency failure | N/A because the classifier consumes the in-memory accumulator, not Roslyn, the filesystem or a database |
| State-transition integrity | PK-38, PK-39, PK-45 — the persistence pass registers into 5A's composable stage without modifying its passes, the stage's counts include persistence output, and the in-memory adapter path stays equivalent |

---

## User Stories

### P1: Persistence evidence reaches the ledger ⭐ MVP

**User Story**: As a persistence classifier, I want the operation kind, entity type, mapping literals and configuration key in the observation payloads so that I can promote persistence facts without re-reading Roslyn.

**Why P1**: AD-004 forbids a classifier from re-deriving evidence from source. With today's empty payloads no persistence classification is possible at all.

**Acceptance Criteria**:

1. WHEN `DataAccessDetector` observes a data access THEN the emitted `DataAccess` observation SHALL carry a payload entry `operation` whose value is one of `read`, `insert`, `update`, `delete`, `execute`, `unknown`. (PK-01)
2. WHEN a data access binds to a receiver of type `DbSet<TEntity>` THEN the `DataAccess` payload SHALL carry a payload entry `entity-type` holding `TEntity`'s fully-qualified name. (PK-02)
3. WHEN a data access binds to a receiver that is or derives from `DbContext` THEN the `DataAccess` payload SHALL carry a payload entry `context-type` holding that context's fully-qualified name. (PK-03)
4. WHEN a data access invokes `FromSqlRaw`, `FromSqlInterpolated`, `ExecuteSqlRaw` or `ExecuteSqlInterpolated` with a constant statement argument THEN the `DataAccess` payload SHALL carry `sql-operation` and `sql-target` entries derived from the bounded reader, a `sql-columns` entry holding the statement's column identifiers ordinal-sorted and joined by `|`, and SHALL NOT carry the statement text. (PK-04)
5. WHEN a data access is a LINQ operator applied over a `DbSet<TEntity>` THEN the `DataAccess` payload SHALL carry a single `field-names` entry holding the ordinal-sorted, `|`-joined names of the entity properties reached by that operator's lambda arguments. (PK-05)
6. WHEN `AssignmentDetector` observes an assignment to an entity property THEN the emitted `Assignment` observation SHALL carry payload entries `entity-type` and `field-name`. (PK-06)
7. WHEN an invocation binds to `ToTable(string)`, `HasColumnName(string)`, `Entity<T>()` or `Property(...)` on the EF model-builder surface THEN the `Invocation` payload SHALL carry, respectively, a `table-name` literal, a `field-name` literal, an `entity-type` literal, and the `entity-type` of the enclosing `Entity<T>()` call. (PK-07)
8. WHEN an invocation binds to `GetConnectionString(name)` on an `IConfiguration` receiver with a constant argument THEN `ConfigurationDetector` SHALL emit a `Configuration` observation whose `key` entry holds that name. (PK-08)
9. The persistence payload entries SHALL NOT contain a connection string, password, token, certificate or authorization value; any such value SHALL be replaced by suspected-secret evidence with a redacted excerpt. (PK-09)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert the `DataAccess` observation for `OrderWrites.PlaceOrder` carries `operation=insert` and `entity-type=Acme.Orders.Data.Order`; assert the `DataAccess` observation for the executed `SELECT` carries `sql-operation=read`, `sql-target=Orders` and no statement text; assert the `Assignment` observation for `PayOrder` carries `field-name=Status`; assert a `Configuration` observation carries `key=OrdersDb`.

---

### P1: Data store classification ⭐ MVP

**User Story**: As an LLM, I want a `DataStore` fact per unit of work so that every data object has a store to hang from and I can see which physical store a component talks to.

**Why P1**: `DataObject.Create` requires a store `FactReference`. No store means no persistence facts at all.

**Acceptance Criteria**:

1. WHEN the ledger names a `DbContext`-derived type — as the `container` of a `DbSet<T>` property `Symbol` fact, or as the `context-type` payload entry of a `DataAccess` observation — THEN the classifier SHALL create exactly one `DataStore` fact for that type with technology `relational`. (PK-10)
2. WHEN a `Configuration` observation in the analyzed solution proves a connection-string key whose owning callable also references that `DbContext` type THEN the `DataStore`'s name SHALL be that configuration key. (PK-11)
3. IF no such configuration key is proven THEN the `DataStore`'s name SHALL be the `DbContext` type's fully-qualified name. (PK-12)
4. WHEN a project contains no `DbContext`-derived type THEN the classifier SHALL NOT create a `DataStore` for that project. (PK-13)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert exactly one `DataStore` exists, with technology `relational` and name `OrdersDb`; remove the configuration call in a unit-level ledger and assert the name falls back to `Acme.Orders.Data.OrderDbContext`; assert `Acme.Shared.Contracts` contributes no `DataStore`.

---

### P1: Data object classification ⭐ MVP

**User Story**: As an LLM, I want a `DataObject` per table so that I can see which physical tables exist and whether their names are proven or merely conventional.

**Why P1**: Data objects are the target of every `operates-on` relation and the parent of every field.

**Acceptance Criteria**:

1. WHEN a `DbContext`-derived type exposes a `DbSet<TEntity>` member THEN the classifier SHALL create one `DataObject` under that context's `DataStore` with form `table`. (PK-14)
2. WHEN an `Invocation` observation proves `Entity<TEntity>().ToTable(name)` for that entity type THEN the `DataObject`'s table name SHALL be `name` and its mapping state SHALL be `ExplicitConfirmation`. (PK-15)
3. IF no `ToTable` call is proven for an exposed entity type THEN the `DataObject`'s table name SHALL be the `DbSet` member's name and its mapping state SHALL be `ConventionalCandidate`. (PK-16)
4. The `DataObject`'s schema name SHALL be the literal `unknown` when no schema is proven. (PK-17)
5. WHEN the bounded SQL reader resolves a statement target identifier THEN the classifier SHALL create a `DataObject` under the executing context's `DataStore` with that identifier as its table name and mapping state `ConventionalCandidate`. (PK-18)
6. WHEN the bounded SQL reader resolves a target identifier enclosed in square brackets THEN the classifier SHALL use the unbracketed identifier as the table name. (PK-19)
7. The classifier SHALL NOT merge two `DataObject` facts because their table names are similar, share a prefix, or differ only by pluralization. (PK-20)
8. WHEN the bounded SQL reader resolves an `EXEC` statement naming a procedure THEN the classifier SHALL create a `DataObject` with form `unknown`, that procedure's name, and mapping state `ConventionalCandidate`. (PK-21)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert a `DataObject` with table name `order_headers` and mapping state `ExplicitConfirmation`; assert a `DataObject` with table name `OrderLines` and mapping state `ConventionalCandidate`; assert a separate `DataObject` named `Orders` from the raw SQL, not merged with `order_headers`; assert a `DataObject` named `usp_RebuildOrderTotals` with form `unknown`; assert the `DELETE FROM [Orders]` access resolves to the existing `Orders` object.

---

### P1: Data field classification and mapping ⭐ MVP

**User Story**: As an LLM, I want column-level facts and mapping relations so that I can answer which columns a flow reads or writes and which CLR property backs a physical column.

**Why P1**: Column granularity is the difference between "this flow touches orders" and "this flow reads `order_status`".

**Acceptance Criteria**:

1. WHEN a `DataAccess` payload's `field-names` entry names an entity property reached by a LINQ filter or projection over an entity set THEN the classifier SHALL create a `DataField` under that entity's `DataObject` for each such name. (PK-22)
2. WHEN an `Assignment` observation names an entity property and the same owning callable contains a `SaveChanges` data access THEN the classifier SHALL create a `DataField` under that entity's `DataObject`. (PK-23)
3. WHEN a `sql-columns` payload entry names a column identifier THEN the classifier SHALL create a `DataField` under the statement's resolved `DataObject`. (PK-24)
4. WHEN an `Invocation` observation proves `Property(...).HasColumnName(name)` for an entity property THEN that property's `DataField` SHALL use `name` as its field name and `ExplicitConfirmation` as its mapping state. (PK-25)
5. IF no `HasColumnName` call is proven for a property THEN its `DataField` SHALL use the CLR property name as its field name and `ConventionalCandidate` as its mapping state. (PK-26)
6. WHEN a raw-SQL statement's column list is the wildcard `*` THEN the classifier SHALL NOT create any `DataField` for that statement. (PK-27)
7. WHEN a `DataObject`'s mapping state is `ExplicitConfirmation` THEN the package SHALL contain a confirmed `maps-to` relation from the entity's `Symbol` to that `DataObject` with mapping role `data-object-mapping` and evidence method `configured`. (PK-28)
8. WHEN a `DataField`'s mapping state is `ExplicitConfirmation` THEN the package SHALL contain a confirmed `maps-to` relation from the property's `Symbol` to that `DataField` with mapping role `data-field-mapping` and evidence method `configured`. (PK-29)
9. IF a `DataObject` or `DataField` mapping state is `ConventionalCandidate` THEN the classifier SHALL create a `CandidateLink` of kind `maps-to` from the CLR `Symbol` to that fact and SHALL NOT create a confirmed `maps-to` relation for it. (PK-30)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert a `DataField` named `order_status` with `ExplicitConfirmation` under `order_headers` and a confirmed `maps-to` from `Order.Status`; assert `DataField` facts named `Id` and `Amount` with `ConventionalCandidate` and `CandidateLink` records rather than confirmed relations; assert `SELECT * FROM {tableName}` produces no field; assert `INSERT INTO Orders (Id, Status, Amount)` produces three fields under the SQL-derived `Orders` object.

---

### P1: Data operations and access relations ⭐ MVP

**User Story**: As an LLM, I want each data access expressed as a `DataOperation` reachable from the callable that performs it so that I can trace a flow from an entry point down to the rows it touches.

**Why P1**: `accesses-data` and `operates-on` are the only edges that connect the code graph to the persistence graph.

**Acceptance Criteria**:

1. WHEN a `DataAccess` observation resolves to both an operation kind and a target `DataObject` THEN the classifier SHALL create a `DataOperation` fact carrying that target, that operation kind, and the target's mapping state. (PK-31)
2. WHEN a `DataOperation` is created THEN the package SHALL contain a confirmed `accesses-data` relation from the observation's owning callable `Symbol` to that `DataOperation`, with evidence method `semantic`, a classifier identity of `csharp2md.classifier.persistence-ef` or `csharp2md.classifier.persistence-sql` version 1, and a `derived_from` chain naming the originating observation. (PK-32)
3. WHEN a `DataOperation` is created THEN the package SHALL contain a confirmed `operates-on` relation from that operation to its target `DataObject`. (PK-33)
4. WHEN a `DataAccess` or tracked-write occurrence resolves one or more `DataField` facts THEN the package SHALL contain a confirmed `operates-on` relation from the `DataOperation` to each of those fields. (PK-34)
5. The classifier SHALL derive a data access's operation kind from this closed table and from no other signal: `Add`/`AddAsync` on an entity set → `insert`; a LINQ operator over an entity set → `read`; a `SELECT` statement → `read`; an `INSERT` statement → `insert`; an `UPDATE` statement → `update`; a `DELETE` statement → `delete`; an `EXEC` statement → `execute`; anything else → `unknown`. (PK-35)
6. WHEN a callable contains an `Assignment` observation on an entity property and a `SaveChanges` data access THEN the classifier SHALL create a `DataOperation` of kind `update` on that entity's `DataObject`. (PK-36)
7. The classifier SHALL NOT create a `DataOperation` for a `SaveChanges` or `SaveChangesAsync` access that has no tracked assignment and no entity-set operation in the same owning callable. (PK-37)
8. WHEN two callables perform the same operation kind on the same `DataObject` THEN the package SHALL contain one shared `DataOperation` fact reached by one `accesses-data` relation per callable. (PK-38)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert `OrderQueries.GetOrder` produces a `read` operation on `order_headers` with `accesses-data` and `operates-on` relations; assert `OrderWrites.PlaceOrder` produces an `insert`; assert `PayOrder` and `Reprice` each produce an `update` reached by their own `accesses-data` relation while sharing one `DataOperation`; assert the executed `EXEC` produces an `execute` operation on `usp_RebuildOrderTotals`.

---

### P1: Unresolved evidence and negative cases ⭐ MVP

**User Story**: As an LLM, I want unresolvable data access recorded as unresolved rather than guessed, and persistence-shaped code that touches no store to produce nothing.

**Why P1**: The zero-tolerance gates forbid a confirmed relation resting on a name. Without explicit unresolved records the package overstates certainty or silently drops accesses.

**Acceptance Criteria**:

1. IF a raw-SQL statement's target identifier is an interpolation hole or any non-constant expression THEN the classifier SHALL create an `UnresolvedRecord` of kind `operates-on` with cause `InsufficientEvidence` and SHALL NOT create a `DataObject` for it. (PK-39)
2. IF a raw-SQL statement's leading keyword is outside the bounded reader's set THEN the classifier SHALL create an `UnresolvedRecord` of kind `accesses-data` with cause `NoCandidateFound`. (PK-40)
3. IF a `DataAccess` observation's entity type cannot be resolved to a `Symbol` fact THEN the classifier SHALL create an `UnresolvedRecord` of kind `accesses-data` with cause `NoCandidateFound` and SHALL NOT create a `DataOperation`. (PK-41)
4. WHEN a type's name, namespace or folder suggests persistence but it performs no data access THEN the classifier SHALL create no persistence fact, relation, candidate or unresolved record for it. (PK-42)
5. The `PersistencePass` SHALL be registered in the existing `ClassificationAndPromotionStage` pass list without modifying any workstream 5A classifier pass. (PK-43)
6. WHEN Classification and Promotion completes THEN its `StageResult` fact and relation counts SHALL include the persistence pass's output. (PK-44)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert `SelectAllFrom` produces an `UnresolvedRecord` and no `DataObject`; assert `OrderRepository` produces no persistence record of any kind despite its name, folder and namespace; assert 5A's pass list is unchanged and the stage's counts grew.

---

### P1: Determinism, security and invariants ⭐ MVP

**User Story**: As an operator, I want persistence output to be deterministic and to preserve every workstream 4 and 5A invariant.

**Why P1**: Two credential literals sit directly in the analyzed persistence code. A leak here is a zero-tolerance gate failure.

**Acceptance Criteria**:

1. IF two clones of the same tree are analyzed THEN every persistence fact identity, relation, candidate and unresolved record SHALL be equal. (PK-45)
2. IF the same solution is analyzed twice THEN every canonical payload file SHALL be byte-identical. (PK-46)
3. The package SHALL NOT contain an absolute filesystem path in any persistence fact, relation, candidate, unresolved record or diagnostic. (PK-47)
4. Canonical package payloads SHALL NOT contain the `OrderSqlQueries.ConnectionString` value or the `appsettings.json` `OrdersDb` connection string; each SHALL appear only as suspected-secret evidence with a redacted excerpt. (PK-48)
5. The public surface of `Csharp2Md.Analysis` SHALL NOT expose any type from `Microsoft.CodeAnalysis` through the persistence classifier, and `Csharp2Md.Cli` SHALL continue to declare no project reference to `Csharp2Md.Domain`. (PK-49)
6. WHERE the in-memory adapter is used the persistence classifier SHALL still produce facts and relations and SHALL create no files. (PK-50)

**Independent Test**: Analyze the fixture from two working directories and assert identity equality on persistence output. Commit twice and assert canonical bytes. Grep every canonical payload for both fixture passwords and assert absence. Run with the in-memory adapter and assert no filesystem writes.

---

### P2: Persistence run coverage

**User Story**: As an operator, I want the run to report how much of the recognized data access it actually resolved so that I can judge the package before workstream 8's certification exists.

**Why P2**: The roadmap assigns persistence coverage to this workstream, but the certification envelope that consumes it belongs to workstream 8. Publishing the raw counts now is enough.

**Acceptance Criteria**:

1. WHEN Classification and Promotion completes THEN the snapshot diagnostics envelope SHALL carry a record naming the count of recognized data-access occurrences and the count of those that resolved to both an operation kind and a target. (PK-51)
2. WHEN a recognized data-access occurrence does not resolve THEN the coverage diagnostic SHALL name that occurrence's owning symbol identity. (PK-52)
3. The coverage diagnostic SHALL NOT report a percentage or a pass/fail verdict. (PK-53)

**Independent Test**: Analyze `Acme.Orders.slnx`; read `diagnostics.json`; assert a persistence coverage record whose denominator equals the number of `DataAccess` observations and whose numerator excludes the interpolated-table access; assert the unresolved occurrence's owner id is named; assert no percentage appears.

---

## Edge Cases

- IF a `DbContext`-derived type exposes no `DbSet` member and no data access names it THEN no `DataStore` SHALL be created for it, because no ledger record mentions it (PK-10, PK-13).
- IF `ToTable` is called with a non-constant argument THEN the entity's `DataObject` SHALL fall back to the conventional name with `ConventionalCandidate` mapping state (PK-16).
- IF `HasColumnName` is called with a non-constant argument THEN the property's `DataField` SHALL fall back to the CLR property name with `ConventionalCandidate` mapping state (PK-26).
- IF the same entity type is exposed by two `DbSet` members on one context THEN each member SHALL produce its own `DataObject` and neither SHALL be merged (PK-14, PK-20).
- WHEN a LINQ chain projects into an anonymous type THEN each entity property reached inside that projection SHALL still produce a `DataField` (PK-22).
- IF a tracked assignment targets a property of a type that is not an exposed entity THEN no `DataField` and no `update` operation SHALL be created (PK-23, PK-42).
- WHEN two `DataObject` facts would carry the same store, form, schema, name and mapping state THEN `SnapshotAccumulator` SHALL deduplicate them without reporting structural corruption (PK-38).
- IF a persistence fact identity collides with a structurally different fact THEN `SnapshotAccumulator` SHALL report structural corruption (existing workstream 4 invariant).

---

## Requirement Traceability

Each requirement gets a unique ID for tracking across design, tasks, and validation.

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| PK-01 | P1: Persistence evidence reaches the ledger | - | Pending |
| PK-02 | P1: Persistence evidence reaches the ledger | - | Pending |
| PK-03 | P1: Persistence evidence reaches the ledger | - | Pending |
| PK-04 | P1: Persistence evidence reaches the ledger | - | Pending |
| PK-05 | P1: Persistence evidence reaches the ledger | - | Pending |
| PK-06 | P1: Persistence evidence reaches the ledger | - | Pending |
| PK-07 | P1: Persistence evidence reaches the ledger | - | Pending |
| PK-08 | P1: Persistence evidence reaches the ledger | - | Pending |
| PK-09 | P1: Persistence evidence reaches the ledger | - | Pending |
| PK-10 | P1: Data store classification | - | Pending |
| PK-11 | P1: Data store classification | - | Pending |
| PK-12 | P1: Data store classification | - | Pending |
| PK-13 | P1: Data store classification | - | Pending |
| PK-14 | P1: Data object classification | Phase 5 (T19) | Implementing |
| PK-15 | P1: Data object classification | - | Pending |
| PK-16 | P1: Data object classification | - | Pending |
| PK-17 | P1: Data object classification | - | Pending |
| PK-18 | P1: Data object classification | Phase 5 (T19) | Implementing |
| PK-19 | P1: Data object classification | - | Pending |
| PK-20 | P1: Data object classification | Phase 6 (T23) | Implementing |
| PK-21 | P1: Data object classification | - | Pending |
| PK-22 | P1: Data field classification and mapping | Phase 5 (T19) | Implementing |
| PK-23 | P1: Data field classification and mapping | - | Pending |
| PK-24 | P1: Data field classification and mapping | - | Pending |
| PK-25 | P1: Data field classification and mapping | - | Pending |
| PK-26 | P1: Data field classification and mapping | - | Pending |
| PK-27 | P1: Data field classification and mapping | - | Pending |
| PK-28 | P1: Data field classification and mapping | Phase 5 (T20) | Implementing |
| PK-29 | P1: Data field classification and mapping | Phase 5 (T20) | Implementing |
| PK-30 | P1: Data field classification and mapping | Phase 5 (T21) | Implementing |
| PK-31 | P1: Data operations and access relations | Phase 5 (T19) | Implementing |
| PK-32 | P1: Data operations and access relations | Phase 5 (T20) | Implementing |
| PK-33 | P1: Data operations and access relations | Phase 5 (T20) | Implementing |
| PK-34 | P1: Data operations and access relations | Phase 5 (T20) | Implementing |
| PK-35 | P1: Data operations and access relations | - | Pending |
| PK-36 | P1: Data operations and access relations | - | Pending |
| PK-37 | P1: Data operations and access relations | - | Pending |
| PK-38 | P1: Data operations and access relations | Phase 6 (T23) | Implementing |
| PK-39 | P1: Unresolved evidence and negative cases | Phase 4 (T18) | Implementing |
| PK-40 | P1: Unresolved evidence and negative cases | Phase 4 (T18) | Implementing |
| PK-41 | P1: Unresolved evidence and negative cases | Phase 4 (T18) | Implementing |
| PK-42 | P1: Unresolved evidence and negative cases | Phase 4 (T18), Phase 6 (T23) | Implementing |
| PK-43 | P1: Unresolved evidence and negative cases | Phase 5 (T22) | Implementing |
| PK-44 | P1: Unresolved evidence and negative cases | Phase 5 (T22) | Implementing |
| PK-45 | P1: Determinism, security and invariants | Phase 6 (T24) | Implementing |
| PK-46 | P1: Determinism, security and invariants | Phase 6 (T24, T26) | Implementing |
| PK-47 | P1: Determinism, security and invariants | Phase 6 (T24) | Implementing |
| PK-48 | P1: Determinism, security and invariants | Phase 6 (T25) | Implementing |
| PK-49 | P1: Determinism, security and invariants | Phase 6 (T25) | Implementing |
| PK-50 | P1: Determinism, security and invariants | Phase 6 (T25) | Implementing |
| PK-51 | P2: Persistence run coverage | Phase 4 (T18), Phase 6 (T23) | Implementing |
| PK-52 | P2: Persistence run coverage | Phase 4 (T18) | Implementing |
| PK-53 | P2: Persistence run coverage | Phase 4 (T18) | Implementing |

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 53 total, 0 mapped to tasks, 53 unmapped ⚠️ (mapping happens in the Tasks phase)

---

## Fixture changes

`fixtures/SyntheticSolution` is the only versioned analysis fixture and this workstream extends it. Three changes, each required by an approved decision:

1. **Raw-SQL execution surface.** `DbSet<T>` gains `FromSqlRaw(string)` and `FromSqlInterpolated(FormattableString)` stand-ins; `DbContext` gains `ExecuteSqlRaw(string)`. These mirror the existing local stand-ins for `DbContext`, `DbSet`, `ModelBuilder` and `WebApplication`.
2. **Executed statements.** `OrderSqlQueries` is rewritten so each of its six statement shapes is executed at the call site with its literal inline, instead of being returned as a string. The `ConnectionString` field stays, unchanged, as the document's credential-absence probe.
3. **Configuration-bound context.** An `IConfiguration` stand-in with `GetConnectionString(string)` is added and the composition root registers `OrderDbContext` against `GetConnectionString("OrdersDb")`, so the store's physical name is C#-observable.

`OrderRepository`, `OrderDbContext`, `OrderConfiguration`, `appsettings.json` and every non-persistence fixture file are unchanged. Superseded legacy comments in the touched files are rewritten to describe the current contract.

---

## Success Criteria

How we know the feature is successful:

- [ ] `analyze --solution fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx --output <dir>` writes a package with non-zero `Persistence` family facts and non-zero `accesses-data`, `operates-on` and `maps-to` relations
- [ ] Exactly one `DataStore` exists, named `OrdersDb`, technology `relational`
- [ ] `order_headers` and `order_status` are `ExplicitConfirmation` facts with confirmed `maps-to` relations; `OrderLines`, `Id` and `Amount` are `ConventionalCandidate` facts with `CandidateLink` records
- [ ] The raw-SQL `Orders` object and the EF-mapped `order_headers` object coexist unmerged
- [ ] `SelectAllFrom` produces an `UnresolvedRecord` and no `DataObject`
- [ ] `OrderRepository` produces no persistence record of any kind
- [ ] Neither fixture password appears in any canonical payload
- [ ] Two clone paths produce identical persistence output and byte-identical canonical payloads
- [ ] `diagnostics.json` carries persistence coverage counts with no percentage
- [ ] After the Verifier, `LocalCorpus` tests run when `fixtures/eShop` or `fixtures/eShopOnContainers` exist, and are skipped when they do not
