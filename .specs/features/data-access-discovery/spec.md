# Database Access Discovery Specification

## Problem Statement

csharp2md's factual output describes types, members and code-to-code relations, but says nothing about what
the code does to a database. A reader of the generated wiki cannot answer "who writes `Orders.Status`?",
"which tables does this service touch?", or "is this table read-only?" — the persistence layer is the one
part of a .NET service that the current pipeline renders as ordinary method bodies with no semantics
attached. Every downstream goal that depends on it (change-impact analysis, per-table wiki pages, a
knowledge graph that connects a handler to a column) is blocked on this stage not existing.

## Goals

- [ ] A member that reads or writes a database produces a persistence relation naming the operation
      (`read` / `insert` / `update` / `delete` / `execute`), the target database object, and the columns it
      touched — under the default syntax-only analysis mode, not only in trusted mode.
- [ ] A table, view, procedure or column whose name is proven by a source literal or explicit configuration
      becomes a real node with a stable fact ID, so incoming edges can be hung on it; a name that is only
      inferred never becomes a node.
- [ ] Every persistence claim states how it was known — `Exact` for configured, `Heuristic` for convention,
      `Candidate` for ambiguous, `Unresolved` for unreadable — and no detected access is ever dropped
      because its target could not be resolved.
- [ ] Zero credentials, connection-string values, or secret text appear anywhere in the output.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Connecting to a database, executing queries, or reading a live schema | Brief §37 excludes it; the stage is a pure function of source text, which is what keeps it runnable offline and deterministic. |
| Data analysis, sensitive-data inference, or any database mutation | Brief §37. Out of the tool's purpose entirely. |
| Migrations impact analysis, query performance, query plans | Brief §37 defers all three to later work; each needs schema or runtime information this stage refuses to gather. |
| NHibernate, Marten, MongoDB, CosmosDB, and custom-repository strategies | Brief §4 states the initial implementation focuses on SQL/relational. The architecture must admit them later; this feature does not build them. |
| A global cross-document `DatabaseMappingResolver` second pass | Mirrors the `RelationCollector` / `RelationResolver` split already established by AD-015: this feature shapes the output so a resolver can attach later without re-running collection. |
| `wiki/data/` and `wiki/entities/` page generation (brief §33) | Consumes this feature's facts; it is a projection concern, not a discovery concern. |
| A third-party SQL parser dependency | User decision: keep the packaged dotnet tool's dependency tree at its current size and its failure behaviour deterministic. Coverage beyond the bounded tokenizer is future work. |
| Mutation testing / the Verifier's discrimination sensor | Standing project preference, reaffirmed for this feature: no mutants are injected at any point. The Verifier still runs and still performs the spec-anchored outcome check against DAD-01..DAD-38 with `file:line` evidence — only the automated mutation sub-step is skipped. Stryker is run manually by the user when wanted. |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here - nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Analysis mode the stage requires | Syntax-first: the collector runs unconditionally under the default `syntax-only` / untrusted mode; a `SemanticModel`, when trusted mode produced one, only refines what syntax already found | AD-012 makes `syntax-only` the default, and AD-015 already established this exact shape for `RelationCollector`; a stage that needed trusted mode would produce nothing on a default run | y |
| Where persistence relations are written | A new `data` relation partition, serialized to `raw/facts/relations/data.json` alongside the existing seven partitions, at schema version 2 | Reuses `CanonicalAggregateWriter`'s existing per-partition file loop and `FactStore`'s partition mapping; a parallel `facts/database/` tree (brief §30) would duplicate machinery the pipeline already has | n — final layout confirmed in Design |
| Where persistence nodes are written | Node facts ride in the per-document fragment the way `SymbolFact` does, and are aggregated into `raw/facts/database.json` at schema version 2 | A table is referenced from many documents, so the per-document fragment is the natural producer and the aggregate is the natural place to see the catalogue once | n — final layout confirmed in Design |
| Brief §12's seven resolution states vs. the existing `FactResolution` enum | Reuse the existing enum: `configured`/`exact` maps to `Exact`, `convention` to `Heuristic`, an ambiguous match to `Candidate`, `syntactic` to `Syntactic`, and `dynamic`/`unresolved` to `Unresolved` with an `unresolved_reason` naming which | AD-014 and the merge/rank machinery are built on the existing enum; growing it again for two states that map cleanly onto existing members would ripple into `FactMerger`, projections and approved snapshots for no behavioural gain | y |
| Brief §13's operation set vs. brief §6's relation kinds | The relation kind carries the coarse direction (`reads`, `writes`, `executes`, `accesses`), and every access relation carries an `operation` detail with the precise value (`read`, `insert`, `update`, `delete`, `execute`, `unknown`) | Coarse kinds keep "which tables does this service write?" a one-predicate query; the detail keeps the precision brief §13 asks for. Emitting `inserts`/`updates`/`deletes` as separate kinds would force every consumer to enumerate a growing kind list | n — wire shape finalized in Design |
| A persisted entity's representation | A persisted entity is an existing C# symbol plus a `maps-to` relation, not a new node kind | The entity already has a `SymbolFactId`; minting a second identity for the same declaration would split its incoming edges across two nodes | y |
| Aggregate handling of a node emitted by several documents | The aggregate contains exactly one entry per node id, merging evidence and keeping the highest-confidence resolution | Per-fragment duplicate detection is already scoped per document (`FactValidator.ValidateDuplicates`), so cross-document repetition is expected and must be reconciled at aggregation, not treated as a structural failure | y |
| SQL text preserved on an unresolved access | Preserved as a `sql` relation detail, truncated to 2000 characters | Brief §16 requires the SQL be kept as evidence; `Evidence` is a source span, not text, so the text needs a detail. The cap bounds a pathological generated-SQL literal without losing the ordinary case | y |
| Fixture coverage for the new shapes | The synthetic fixture solution gains EF Core configuration, LINQ query, write, and literal-SQL documents; the existing `OrderDbContext.cs` stand-in is extended, not replaced | `fixtures/SyntheticSolution` currently holds a `DbContext`/`DbSet` stand-in with no configuration, no queries and no SQL, so none of P1's criteria are exercisable against it as it stands | y |
| Attributing a property assignment to an entity in syntax-only mode | Match the assigned property's name against the properties of entities exposed by `DbContext`s discovered in the same analysis run: exactly one match gives `Heuristic`; more than one gives `Candidate` with `target_id` null; no match emits nothing | Syntax alone cannot prove the receiver's type. Refusing to emit would lose brief §8's central scenario; emitting at `Exact` would lie. This is precisely the distinction `Heuristic` and `Candidate` exist to record | y |

**Open questions:** none - all resolved or logged above.

---

## Implicit-Requirement Dimensions Sweep

| Dimension | Resolution |
| --- | --- |
| Input validation & bounds | DAD-16 (SQL detail truncation cap) |
| Failure / partial-failure states | DAD-14 (an unresolvable access is still emitted), DAD-18 (analyser failure is a diagnostic, not a run failure) |
| Idempotency / retry / duplicate handling | DAD-19 (the aggregate holds one entry per node id), DAD-20 (byte-identical output across runs) |
| Auth boundaries & rate limits | N/A because the stage has no callers, no network surface, and no privileged operation — it reads source files already inside the analysis scope |
| Concurrency / ordering | N/A because analysis is sequential per project and per document (AD-008); DAD-20 covers the only ordering guarantee that matters here, output determinism |
| Data lifecycle / expiry | N/A because the whole output tree is regenerated per run by `OutputWriter.PrepareRun`; no persistence fact outlives its run |
| Observability | DAD-18 (diagnostics), DAD-38 (discovery metrics) |
| External-dependency failure | N/A because P1 adds no external dependency: the SQL analyser is internal and no database is contacted (brief §37) |
| State-transition integrity | N/A because the stage is a pure function from source text to facts; it holds no state across documents beyond a read-only entity/mapping catalogue |

---

## User Stories

### P1: EF Core persistence model and access ⭐ MVP

**User Story**: As someone reading a generated wiki for a .NET service, I want every EF Core entity, its
table, its columns, and every member that reads or writes them recorded as facts, so that I can ask which
code touches `Orders` and who changes `Orders.Status` without opening the source.

**Why P1**: EF Core is the dominant .NET persistence path and the only source that yields the full chain the
brief demands — detect access, resolve resource, map columns — including the configured-versus-convention
split that the whole honesty model rests on.

**Acceptance Criteria**:

1. WHEN a document declares a type whose base list names `DbContext` THEN the system SHALL emit one
   `exposes` relation per `DbSet<TEntity>` property that type declares, sourced at the declaring type's
   symbol id, carrying `TEntity`'s simple name as the relation's `target_text` detail.
2. WHEN a `DbSet<TEntity>` property is discovered and the analysed source contains no `ToTable`
   configuration for `TEntity` THEN the system SHALL emit a `maps-to` relation from `TEntity`'s symbol id
   with `target_id` null, `target_text` equal to the `DbSet` property's name, and resolution `Heuristic`.
3. WHEN the analysed source contains an `Entity<TEntity>()` chain ending in `ToTable` with a string-literal
   argument THEN the system SHALL emit a database-object node bearing that literal's value with kind
   `table` and resolution `Exact`, and a `maps-to` relation from `TEntity`'s symbol id whose `target_id` is
   that node's id.
4. IF an entity has both a convention-derived and an explicitly configured table mapping THEN the system
   SHALL emit only the explicitly configured `maps-to` relation for that entity.
5. WHEN a `Property(x => x.P)` chain ending in `HasColumnName` with a string-literal argument is present
   THEN the system SHALL emit a database-column node bearing that literal's value, owned by the entity's
   mapped database object, and a `maps-property-to-column` relation from `P`'s symbol id to that node with
   resolution `Exact`.
6. WHEN a property of a mapped entity has no `HasColumnName` configuration THEN the system SHALL emit a
   `maps-property-to-column` relation from that property's symbol id with `target_id` null, `target_text`
   equal to the property's own name, and resolution `Heuristic`.
7. WHEN a member body reads a `DbSet` property of a discovered `DbContext` THEN the system SHALL emit a
   `reads` relation sourced at the enclosing member's symbol id, carrying an `operation` detail of `read`
   and targeting the entity's mapped database object.
8. WHEN such a `DbSet` read flows into a LINQ chain that references entity properties outside a `Where`
   lambda THEN the system SHALL emit one `reads-column` relation per referenced property, each carrying a
   `usage` detail of `read`.
9. WHEN such a `DbSet` read flows into a `Where` invocation whose lambda references entity properties THEN
   the system SHALL emit one `filters-by` relation per referenced property, each carrying a `usage` detail
   of `filter`.
10. WHEN a member body invokes `Add`, `AddAsync`, `AddRange`, `Update`, `UpdateRange`, `Remove` or
    `RemoveRange` on a discovered `DbSet` property THEN the system SHALL emit a `writes` relation from the
    enclosing member whose `operation` detail is `insert` for the `Add` family, `update` for the `Update`
    family, and `delete` for the `Remove` family.
11. WHEN a member body assigns a property whose name matches exactly one property of an entity exposed by a
    discovered `DbContext` and that same member invokes `SaveChanges` or `SaveChangesAsync` THEN the system
    SHALL emit a `writes-column` relation from that member to the property's mapped column, carrying a
    `usage` detail of `write` and resolution `Heuristic`.
12. IF such an assigned property name matches properties on more than one exposed entity THEN the system
    SHALL emit the `writes-column` relation with `target_id` null, the observed `receiver.property` text as
    `target_text`, and resolution `Candidate`.
13. The system SHALL attach `Evidence` carrying the document id, the normalized relative path, and
    one-based start and end line and column to every persistence node and every persistence relation it
    emits.
14. IF a detected access cannot be resolved to a target THEN the system SHALL still emit the access
    relation, with `target_id` null, a populated `unresolved_reason`, and the observed target text
    preserved as a `target_text` detail.
15. The system SHALL never write a connection-string value, password, token, or other credential text into
    any fact, relation detail, or diagnostic it produces.
16. The system SHALL truncate any SQL text it preserves as a relation detail to at most 2000 characters.
17. The system SHALL derive persistence classification only from observed persistence API usage, and SHALL
    NOT treat a type name, file name, or namespace as evidence of database access.
18. IF the persistence analyser throws while inspecting a document THEN the system SHALL record an analysis
    diagnostic naming that document and continue with the remaining documents, leaving the run's exit code
    unchanged.
19. WHEN the same database-object or database-column node is emitted from more than one document THEN the
    aggregate output SHALL contain exactly one entry for that node id, merging the contributing evidence
    and retaining the highest-confidence resolution.
20. The system SHALL produce byte-identical persistence facts across two runs over the same unchanged
    input.

**Independent Test**: Run the CLI in default (syntax-only, untrusted) mode over the fixture solution
extended with an EF Core configuration and query document; assert `raw/facts/relations/data.json` contains
the `maps-to`, `reads`, `reads-column`, `filters-by` and `writes-column` relations with the expected
resolutions, and that `raw/facts/database.json` lists the configured table and column nodes.

---

### P1: Literal SQL statements yield targets, columns, or a preserved unresolved access

**User Story**: As someone reading a generated wiki, I want SQL written as a string in C# to be analysed for
its operation, its target object and its columns, so that hand-written SQL is as visible in the graph as
EF Core is — and so that SQL the tool cannot read is preserved rather than silently discarded.

**Why P1**: Hand-written SQL sits beside EF Core in most real services, and it is the branch that exercises
the honesty guarantee hardest: brief §17's interpolated-table case is exactly where a naive implementation
invents a destination.

**Acceptance Criteria**:

21. WHEN a string literal's first significant token is `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `MERGE`,
    `EXEC` or `CALL`, compared case-insensitively, THEN the system SHALL emit one access relation sourced at
    the enclosing member's symbol id whose `operation` detail is derived from that verb.
22. WHEN such a statement names its target object as a plain identifier the analyser can read — after
    `FROM`, `INTO`, `UPDATE`, `DELETE FROM`, `MERGE INTO`, `EXEC` or `CALL` — THEN the system SHALL emit a
    database-object node for that identifier with resolution `Exact` and set the relation's `target_id` to
    that node's id.
23. WHERE the statement verb is `EXEC` or `CALL` the system SHALL set the emitted node's kind to
    `procedure`, and for every other recognised verb the system SHALL set the node's kind to `unknown`.
24. WHEN an `INSERT INTO <object> (<column-list>)` column list is present THEN the system SHALL emit one
    `writes-column` relation per listed column, each carrying a `usage` detail of `write`.
25. WHEN an `UPDATE <object> SET <column> = ...` assignment list is present THEN the system SHALL emit one
    `writes-column` relation per assigned column, each carrying a `usage` detail of `write`.
26. WHEN a `WHERE` clause compares a plain column identifier against a literal or a parameter reference THEN
    the system SHALL emit a `filters-by` relation for that column carrying a `usage` detail of `filter`.
27. IF the SQL text is an interpolated string, a concatenation, or any expression the analyser cannot reduce
    to a single literal THEN the system SHALL emit the access relation with `target_id` null, `target_text`
    of `dynamic-table`, an `unresolved_reason` naming dynamic SQL, and resolution `Unresolved`, and SHALL
    NOT emit a database-object node for it.
28. IF the analyser recognises a statement's verb but cannot read its target object THEN the system SHALL
    emit the access relation with `target_id` null, resolution `Unresolved`, and the statement text
    preserved as a `sql` detail.

**Independent Test**: Add a fixture document containing one readable `SELECT ... WHERE`, one
`INSERT INTO ... (...)`, one `UPDATE ... SET ... WHERE`, one `EXEC usp_X`, and one interpolated
`$"SELECT * FROM {tableName}"`; assert each produces the operation, node, column and resolution above, and
that the interpolated case produces an `Unresolved` access with no node.

---

### P2: Dapper and ADO.NET call sites

**User Story**: As someone analysing a service that predates or bypasses EF Core, I want Dapper and raw
ADO.NET call sites recognised as database access, so that those services are not silently reported as having
no persistence at all.

**Why P2**: These paths reuse P1's SQL analyser wholesale, so they are additive rather than foundational —
but a service built on Dapper produces nothing at all until they ship.

**Acceptance Criteria**:

29. WHEN a member invokes `Query`, `QueryAsync`, `QuerySingle`, `QuerySingleAsync`, `QueryFirst` or
    `QueryFirstAsync` with a SQL argument THEN the system SHALL emit a `reads` relation whose target and
    columns come from analysing that argument as SQL.
30. WHEN a member invokes `Execute` or `ExecuteAsync` with a SQL argument THEN the system SHALL emit an
    access relation whose `operation` detail comes from the analysed SQL's verb, defaulting to `unknown`
    when the verb is unrecognised.
31. WHEN a member assigns a `CommandText` property THEN the system SHALL analyse the assigned value as SQL
    and emit the resulting access relation sourced at that member.
32. WHILE a command's `CommandType` is set to `CommandType.StoredProcedure` within the same member, the
    system SHALL treat that command's `CommandText` value as a procedure name rather than as a SQL
    statement, emit a database-object node of kind `procedure`, and emit an `executes` relation to it.
33. WHEN a command's parameters are added with string-literal names THEN the system SHALL record each
    parameter name as a detail on the emitted access relation.

**Independent Test**: A fixture document using Dapper `QueryAsync`, Dapper `ExecuteAsync`, and an ADO.NET
`SqlCommand` with `CommandType.StoredProcedure` produces the three expected relations with correct
operations and a `procedure`-kind node.

---

### P2: Extended column usage

**User Story**: As someone doing change-impact analysis, I want to distinguish a column used in a join or a
grouping from one merely projected, so that "where is `CustomerId` used?" separates structural use from
display use.

**Why P2**: The four remaining usages need whole-LINQ-chain traversal and a more precise SQL analyser than
the MVP's; the P1 vocabulary already answers the questions the brief opens with.

**Acceptance Criteria**:

34. WHEN a LINQ chain or SQL statement uses a column in a join condition THEN the system SHALL emit that
    column's access relation with a `usage` detail of `join`.
35. WHEN a LINQ chain or SQL statement uses a column in an ordering, grouping, or aggregate position THEN
    the system SHALL emit that column's access relation with a `usage` detail of `order`, `group`, or
    `aggregate` respectively.

**Independent Test**: A fixture query with a join, an `OrderBy`, a `GroupBy` and a `Sum` produces one
relation per column with the matching usage value.

---

### P3: Connections and database identity

**User Story**: As someone mapping a system's data topology, I want to see which connection a `DbContext`
binds to and which provider it uses, so that I can tell two contexts pointing at different databases apart —
without any credential leaving the source.

**Why P3**: Valuable for topology, but nothing in P1 or P2 depends on it, and brief §19 warns that a
connection name is not proof of a database's identity, so the payoff is deliberately modest.

**Acceptance Criteria**:

36. WHEN a `UseSqlServer`, `UseNpgsql` or `UseMySql` invocation references a configuration key rather than a
    literal value THEN the system SHALL emit a connection node named by that key, a `connects-to` relation
    from the configuring `DbContext`'s symbol id, and a `provider` detail naming the provider.
37. The system SHALL leave a connection node's database target `unknown` unless a configuration entry in the
    analysed source proves which database it denotes.

**Independent Test**: A fixture `Program.cs` registering
`UseSqlServer(configuration.GetConnectionString("OrdersDb"))` produces a connection node named `OrdersDb`
with provider `sqlserver`, a `connects-to` relation from `OrderDbContext`, and no connection-string value
anywhere in the output.

---

### P3: Discovery metrics

**User Story**: As someone judging whether the discovery stage actually worked on my codebase, I want counts
of what it found and what it failed to resolve, so that silent under-detection is visible instead of looking
like a codebase with no database.

**Why P3**: Diagnostic value only; every requirement above is verifiable without it.

**Acceptance Criteria**:

38. WHEN an analysis run completes THEN the system SHALL report counts of discovered contexts, mapped
    entities, mapped objects, mapped columns, read operations, write operations, procedures, dynamic
    queries, and unresolved mappings.

**Independent Test**: A run over the fixture solution reports non-zero counts matching the fixture's known
contents, including a non-zero unresolved count for the interpolated-SQL document.

---

## Edge Cases

- IF a `DbSet` property is declared on a type that does not derive from `DbContext` THEN the system SHALL
  NOT emit `exposes` or `maps-to` relations for it.
- IF an assigned property name matches no property of any entity exposed by a discovered `DbContext` THEN
  the system SHALL emit no `writes-column` relation for that assignment.
- IF a `ToTable` or `HasColumnName` argument is not a string literal THEN the system SHALL treat the mapping
  as unconfigured and fall back to the convention mapping at resolution `Heuristic`.
- IF a document contains no persistence API usage at all THEN the system SHALL emit no persistence facts for
  it and SHALL NOT record a diagnostic.
- WHEN a string literal begins with a SQL verb but is used as a message, log format, or comparison value
  rather than passed to a database API THEN the system SHALL still emit the access relation at resolution
  `Syntactic`, because refusing it would require proving a receiver type that syntax cannot see.
- IF the analysed solution declares more than one `DbContext` exposing the same entity type THEN the system
  SHALL emit one `exposes` relation per context, and SHALL resolve the entity's table mapping from
  configuration when present and otherwise emit one convention `maps-to` per distinct `DbSet` property name.

---

## Requirement Traceability

Each requirement gets a unique ID for tracking across design, tasks, and validation.

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| DAD-01 | P1: EF Core persistence model and access | T27 | Verified |
| DAD-02 | P1: EF Core persistence model and access | T27 | Verified |
| DAD-03 | P1: EF Core persistence model and access | T27 | Verified |
| DAD-04 | P1: EF Core persistence model and access | T27 | Verified |
| DAD-05 | P1: EF Core persistence model and access | Design | Pending |
| DAD-06 | P1: EF Core persistence model and access | Design | Pending |
| DAD-07 | P1: EF Core persistence model and access | Design | Pending |
| DAD-08 | P1: EF Core persistence model and access | Design | Pending |
| DAD-09 | P1: EF Core persistence model and access | Design | Pending |
| DAD-10 | P1: EF Core persistence model and access | Design | Pending |
| DAD-11 | P1: EF Core persistence model and access | Design | Pending |
| DAD-12 | P1: EF Core persistence model and access | Design | Pending |
| DAD-13 | P1: EF Core persistence model and access | Design | Pending |
| DAD-14 | P1: EF Core persistence model and access | Design | Pending |
| DAD-15 | P1: EF Core persistence model and access | Design | Pending |
| DAD-16 | P1: EF Core persistence model and access | Design | Pending |
| DAD-17 | P1: EF Core persistence model and access | Design | Pending |
| DAD-18 | P1: EF Core persistence model and access | Design | Pending |
| DAD-19 | P1: EF Core persistence model and access | Design | Pending |
| DAD-20 | P1: EF Core persistence model and access | Design | Pending |
| DAD-21 | P1: Literal SQL statements | Design | Pending |
| DAD-22 | P1: Literal SQL statements | Design | Pending |
| DAD-23 | P1: Literal SQL statements | Design | Pending |
| DAD-24 | P1: Literal SQL statements | Design | Pending |
| DAD-25 | P1: Literal SQL statements | Design | Pending |
| DAD-26 | P1: Literal SQL statements | Design | Pending |
| DAD-27 | P1: Literal SQL statements | Design | Pending |
| DAD-28 | P1: Literal SQL statements | Design | Pending |
| DAD-29 | P2: Dapper and ADO.NET call sites | - | Pending |
| DAD-30 | P2: Dapper and ADO.NET call sites | - | Pending |
| DAD-31 | P2: Dapper and ADO.NET call sites | - | Pending |
| DAD-32 | P2: Dapper and ADO.NET call sites | - | Pending |
| DAD-33 | P2: Dapper and ADO.NET call sites | - | Pending |
| DAD-34 | P2: Extended column usage | - | Pending |
| DAD-35 | P2: Extended column usage | - | Pending |
| DAD-36 | P3: Connections and database identity | - | Pending |
| DAD-37 | P3: Connections and database identity | - | Pending |
| DAD-38 | P3: Discovery metrics | - | Pending |

**ID format:** `DAD-[NUMBER]`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 38 total, 0 mapped to tasks, 38 unmapped ⚠️

---

## Success Criteria

How we know the feature is successful:

- [ ] A default-mode (`syntax-only`, untrusted) run over the extended fixture solution produces a non-empty
      `raw/facts/relations/data.json` and a `raw/facts/database.json` listing every configured table and
      column.
- [ ] For the brief §7 scenario, the output answers "which columns does `GetOrder` read?" with `Id`,
      `Status` and `Amount`, and "which column does it filter by?" with `Id`.
- [ ] For the brief §8 scenario, `PayOrder` appears as a writer of the column that `Order.Status` maps to.
- [ ] Every emitted persistence fact carries evidence, and a search of the whole output tree for the
      fixture's connection-string value returns nothing.
- [ ] The interpolated-SQL document produces an access at resolution `Unresolved` with no invented target
      and its SQL preserved.
- [ ] Two consecutive runs over unchanged input produce byte-identical persistence output.
