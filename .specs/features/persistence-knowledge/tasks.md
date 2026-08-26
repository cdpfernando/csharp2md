# Persistence Knowledge Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**Standing skip — discrimination sensor**: the user runs Stryker manually; do not run the sensor's fault-injection pass. Every other Verifier step (spec-anchored coverage check, gate check, code-quality check) still runs as documented.

---

**Design**: `.specs/features/persistence-knowledge/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase sampling and project guidelines. Guidelines found: [`AGENTS.md`](AGENTS.md), [`CLAUDE.md`](CLAUDE.md) (retrieval-led reasoning, Roslyn API verification, multi-csproj test execution note, LocalCorpus fixture rule). Carried forward from the `entrypoints-boundaries-contracts` matrix, extended with the extraction layer this feature touches.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Fixture source (analysis input) | none | Build gate only; correctness is asserted by the tests that analyze it | `fixtures/SyntheticSolution/**/*.cs` | build gate only |
| Pure readers / model records (no I/O) | unit | All branches; 1:1 to spec ACs; every listed edge case | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Observation detectors (extraction) | unit + integration | All branches; payload contract asserted at the ledger; fixture-backed | `tests/Csharp2Md.Analysis.Tests/Extraction/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Classification passes (classifier logic) | unit + integration | All branches; 1:1 to spec ACs; fixture-backed assertions | `tests/Csharp2Md.Analysis.Tests/Classification/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Pipeline stage / orchestrator | integration | Stage wiring, count reporting, composability | `tests/Csharp2Md.Analysis.Tests/Pipeline/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Storage mapping (classifier facts) | unit | Classifier-produced facts round-trip correctly | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| CLI surface | unit | No new flags; CLI→Domain isolation | `tests/Csharp2Md.Cli.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj` |
| End-to-end (fixture analyze) | integration | Full pipeline with persistence output assertions | `tests/Csharp2Md.Analysis.Tests/Classification/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |

## Gate Check Commands

> Generated from codebase — confirm before Execute. Multi-csproj `dotnet test` hits MSB1008; run each test project separately. `Category=LocalCorpus` is excluded from every gate; those tests run only after the Verifier and only when the local clones exist.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After unit-test-only tasks scoped to Analysis | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Full | After extraction / classifier / pipeline tasks | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Build | After fixture changes, phase completion, and every cross-assembly task | `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |

**Baseline test count**: 1019 (`Category!=LocalCorpus`) — Domain 545, Analysis 283, Storage 161, Cli 27, Projection 3. Every task reports its new total; a drop means a silent deletion.

---

## Execution Plan

Phases are ordered and run sequentially — each phase completes before the next begins, and tasks within a phase execute in order.

### Phase 1: Fixture and SQL reader

The fixture changes land first and alone, so the blast radius on the existing 1019 tests is measured before any classifier code exists.

```
T1 → T2 → T3 → T4
```

### Phase 2: Extraction payload enrichment

Teach the detectors to write persistence evidence into the ledger.

```
T5 → T6 → T7 → T8 → T9
```

### Phase 3: Shared readers and store resolution

Ledger-reading helpers, the model's record types, and the first builder step.

```
T10 → T11 → T12 → T13
```

### Phase 4: Model builder resolution

The remaining correlation rules, each a separate builder step with its own tests.

```
T14 → T15 → T16 → T17 → T18
```

### Phase 5: Emission and pass registration

Turn the model into Domain records and wire the pass into the pipeline.

```
T19 → T20 → T21 → T22
```

### Phase 6: End-to-end, determinism, and security

Whole-package assertions against the fixture.

```
T23 → T24 → T25 → T26
```

---

## Task Breakdown

### T1: Add raw-SQL APIs to the EF stand-ins

**What**: Extend the fixture's local `DbSet<T>` and `DbContext` stand-ins with `FromSqlRaw`, `FromSqlInterpolated` and `ExecuteSqlRaw` so raw-SQL execution is expressible in the fixture.
**Where**: `fixtures/SyntheticSolution/Acme.Orders/Data/OrderDbContext.cs`
**Depends on**: None
**Reuses**: The existing local stand-in pattern in the same file
**Requirement**: PK-04

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `DbSet<TEntity>` exposes `FromSqlRaw(string)` and `FromSqlInterpolated(FormattableString)`
- [x] `DbContext` exposes `ExecuteSqlRaw(string)`
- [x] Method names match exactly the names `DataAccessDetector.PersistenceMethodNames` already recognizes
- [x] The file's header comment describes the current contract, with superseded legacy narrative removed
- [x] Gate check passes: `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- [x] Test count: 1023 tests pass (no silent deletions); any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): add raw-SQL APIs to EF stand-ins`

---

### T2: Execute the fixture's six SQL statements

**What**: Rewrite `OrderSqlQueries` so each of its six statement shapes is executed at the call site with its literal inline, instead of being returned as a string.
**Where**: `fixtures/SyntheticSolution/Acme.Orders/Data/OrderSqlQueries.cs`
**Depends on**: T1
**Reuses**: The `OrderDbContext` stand-ins extended in T1
**Requirement**: PK-04

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] All six shapes survive as executed statements: `SELECT … WHERE`, `INSERT … (cols)`, `UPDATE … SET`, `EXEC usp_RebuildOrderTotals`, `DELETE FROM [Orders]`, and the interpolated `SELECT * FROM {tableName}`
- [x] Each statement literal sits at its own execution call site, reachable by `TryFirstStringLiteral`
- [x] The interpolated statement goes through `FromSqlInterpolated`; `EXEC` and `DELETE` go through `ExecuteSqlRaw`
- [x] The `ConnectionString` constant is unchanged and still unused by any execution path
- [x] The file's header comment describes the current contract; the "bounded reader refuses to read `[Orders]`" narrative is removed as superseded
- [x] Gate check passes: build gate command
- [x] Test count: 1023 tests pass (no silent deletions); any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): execute hand-written SQL through EF raw APIs`

---

### T3: Bind the fixture's DbContext to a configuration key

**What**: Add an `IConfiguration` stand-in with `GetConnectionString(string)` and register `OrderDbContext` against `GetConnectionString("OrdersDb")` in the composition root.
**Where**: `fixtures/SyntheticSolution/Acme.Orders/Program.cs`
**Depends on**: T2
**Reuses**: The `WebApplication` / `IServiceCollection` stand-in pattern already in this file
**Requirement**: PK-11

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `Microsoft.Extensions.Configuration.IConfiguration` is declared locally with `GetConnectionString(string)`, matching the metadata name `ConfigurationDetector` resolves
- [x] `ConfigureHost` calls `GetConnectionString("OrdersDb")` in the same callable that registers `OrderDbContext`
- [x] The key string matches the `ConnectionStrings:OrdersDb` entry already in `appsettings.json`
- [x] No connection-string *value* appears anywhere in the C# source
- [x] Gate check passes: build gate command
- [x] Test count: 1023 tests pass (no silent deletions); any count change is explained in the commit body

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): bind OrderDbContext to a configuration key`

---

### T4: Create SqlStatementReader

**What**: A bounded reader that turns a constant SQL statement into an operation kind, a target identifier and a column list, or rejects it.
**Where**: `src/Csharp2Md.Analysis/Extraction/SqlStatementReader.cs`
**Depends on**: T3
**Reuses**: `DataOperationKind` from `Csharp2Md.Domain.Facets`
**Requirement**: PK-19, PK-27, PK-39, PK-40

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] `TryRead(string, out SqlStatementFacts)` implements exactly the closed grammar table in the design: `SELECT`/`INSERT`/`UPDATE`/`DELETE`/`EXEC`
- [x] `[Orders]` and `"Orders"` unquote to `Orders` (PK-19)
- [x] A `SELECT *` column list yields zero columns (PK-27)
- [x] A target containing an interpolation hole, a parameter marker or whitespace is rejected (PK-39)
- [x] A statement whose leading keyword is outside the set is rejected (PK-40)
- [x] Keyword matching is `OrdinalIgnoreCase`; emitted identifiers keep their source casing
- [x] Unit tests cover every grammar row and every rejection path
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count: 287 + 16 new Analysis tests = 303 pass (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add bounded SQL statement reader`

---

### T5: Enrich the DataAccess payload with EF evidence

**What**: Extend `DataAccessDetector` so its observations carry `operation`, `entity-type`, `context-type` and `field-names`.
**Where**: `src/Csharp2Md.Analysis/Extraction/DataAccessDetector.cs`
**Depends on**: T4
**Reuses**: Its own `IsDbSet` / `IsDbContext` / `IsLinqOperator` helpers, unchanged
**Requirement**: PK-01, PK-02, PK-03, PK-05

**Tools**:

- MCP: `context7` (Roslyn symbol APIs)
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `operation` is always present and drawn from the closed table in PK-35 (PK-01)
- [x] `entity-type` is present when the receiver binds to `DbSet<TEntity>` (PK-02)
- [x] `context-type` is present when the receiver is or derives from `DbContext` (PK-03)
- [x] `field-names` holds the LINQ lambda member accesses, ordinal-sorted and `|`-joined into one entry (PK-05)
- [x] `NormalizedPayload.Create` is never called with a duplicate key
- [x] Existing `DataAccessDetectorTests` still pass (the SaveChanges empty-payload assertion is rewritten to assert the new `operation`/`context-type` entries, since an empty payload can no longer be correct once PK-01 requires `operation` on every occurrence); new tests assert each payload entry against the fixture
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count: 545 + 307 = 852 pass (no silent deletions)

**Tests**: unit + integration
**Gate**: full

**Commit**: `feat(analysis): carry EF evidence in data-access payloads`

---

### T6: Enrich the DataAccess payload with SQL evidence

**What**: Add `sql-operation`, `sql-target` and `sql-columns` payload entries for raw-SQL invocations, never the statement text.
**Where**: `src/Csharp2Md.Analysis/Extraction/DataAccessDetector.cs`
**Depends on**: T5
**Reuses**: `SqlStatementReader` from T4; `ObservationMaterializer.Redact` for secret handling
**Requirement**: PK-04, PK-09

**Tools**:

- MCP: `context7` (Roslyn symbol APIs)
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A raw-SQL call with a constant statement `SqlStatementReader` accepts carries all three entries (PK-04)
- [x] `sql-columns` is one ordinal-sorted `|`-joined entry
- [x] The statement text is never a payload entry, in any code path (PK-04)
- [x] A raw-SQL call with a non-constant statement emits the observation with `operation=unknown` and no `sql-*` entries
- [x] A test asserts neither fixture password reaches any payload entry (PK-09)
- [x] Gate check passes: full gate command
- [x] Test count: 545 + 312 = 857 pass (no silent deletions)

**Tests**: unit + integration
**Gate**: full

**Commit**: `feat(analysis): carry parsed SQL evidence in data-access payloads`

---

### T7: Enrich the Assignment payload

**What**: Extend `AssignmentDetector` so its observations carry `entity-type` and `field-name`.
**Where**: `src/Csharp2Md.Analysis/Extraction/AssignmentDetector.cs`
**Depends on**: T6
**Reuses**: Its existing per-compilation `CollectEntities` set, unchanged
**Requirement**: PK-06

**Tools**:

- MCP: `context7` (Roslyn symbol APIs)
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `entity-type` holds the assigned member's containing type, fully qualified
- [x] `field-name` holds the assigned member's name with `LiteralRole.FieldName`
- [x] Existing `AssignmentDetectorTests` still pass (the empty-payload assertion is rewritten to assert the new `entity-type`/`field-name` entries, since PK-06 now requires both on every Assignment observation)
- [x] A test asserts the fixture's `PayOrder` assignment carries `field-name=Status` and `Reprice` carries `field-name=Amount`
- [x] Gate check passes: full gate command
- [x] Test count: 545 + 313 = 858 pass (no silent deletions)

**Tests**: unit + integration
**Gate**: full

**Commit**: `feat(analysis): carry entity and field names in assignment payloads`

---

### T8: Create EfMappingPayload

**What**: A payload builder for EF fluent-mapping invocations, wired into the walker's invocation payload path.
**Where**: `src/Csharp2Md.Analysis/Extraction/EfMappingPayload.cs`
**Depends on**: T7
**Reuses**: The walker's existing `TryFirstStringLiteral` shape
**Requirement**: PK-07

**Tools**:

- MCP: `context7` (Roslyn syntax and symbol APIs)
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Recognizes `Entity<T>()`, `ToTable(string)`, `Property(...)` and `HasColumnName(string)`
- [x] `ToTable` emits `table-name` + `entity-type`; `HasColumnName` emits `field-name` + `entity-type` + `property-name` (PK-07)
- [x] The enclosing `Entity<T>()` call is recovered by walking the receiver chain, so the cross-document mapping in `OrderConfiguration.cs` resolves
- [x] A non-constant argument yields no literal entry
- [x] `AlwaysWhenBindableWalker.InvocationPayload` becomes an instance method to pass the semantic model through; its `CreateClient` and HTTP-route branches are behaviourally unchanged
- [x] Existing `AlwaysWhenBindableWalkerTests` and all 5A boundary tests still pass
- [x] Tests assert `table-name=order_headers` and `field-name=order_status` reach the ledger
- [x] Gate check passes: full gate command
- [x] Test count: 545 + 316 = 861 pass (no silent deletions)

**Tests**: unit + integration
**Gate**: full

**Commit**: `feat(analysis): observe EF fluent mapping literals`

---

### T9: Recognize GetConnectionString

**What**: Extend `ConfigurationDetector` so `GetConnectionString(name)` on an `IConfiguration` receiver emits a `Configuration` observation.
**Where**: `src/Csharp2Md.Analysis/Extraction/ConfigurationDetector.cs`
**Depends on**: T8
**Reuses**: Its existing `IsConfigurationType` and `TryFirstStringLiteral` helpers
**Requirement**: PK-08

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `GetConnectionString` joins `GetSection` and `GetValue` in the recognized set (PK-08)
- [x] The payload shape is unchanged — a single `key` entry with `LiteralRole.ConfigurationKey`
- [x] Existing `ConfigurationDetectorTests` still pass
- [x] A test asserts the fixture emits a `Configuration` observation with `key=OrdersDb`
- [x] A test asserts no connection-string value reaches the payload
- [x] Gate check passes: full gate command
- [x] Test count: 545 + 318 = 863 pass (no silent deletions)

**Tests**: unit + integration
**Gate**: full

**Commit**: `feat(analysis): observe connection-string configuration keys`

---

### T10: Create PayloadReader

**What**: A shared reader for observation payload entries, including `|`-joined multi-values.
**Where**: `src/Csharp2Md.Analysis/Classification/PayloadReader.cs`
**Depends on**: T9
**Reuses**: The read logic already duplicated privately in `BoundaryPass`, `ContractPass` and `RelationPass` — those copies are left untouched (PK-43)
**Requirement**: PK-43

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `Value`, `Contains` and `Multi` behave identically to the existing private copies for single-valued entries
- [x] `Multi` splits on `|` and returns an empty array for a missing or blank entry
- [x] No file under `Classification/Passes/` is modified
- [x] Unit tests cover present, absent, blank and multi-valued entries
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count: 318 + 10 new Analysis tests = 328 pass (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add shared observation payload reader`

---

### T11: Create SignatureReader

**What**: A shared reader for canonical symbol-signature fields.
**Where**: `src/Csharp2Md.Analysis/Classification/SignatureReader.cs`
**Depends on**: T10
**Reuses**: The `ReadField` logic duplicated privately in the three 5A passes — those copies are left untouched (PK-43)
**Requirement**: PK-43

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] `Field(signature, key)` matches the existing private `ReadField` behaviour including the `-` sentinel and URI unescaping
- [x] `Kind`, `Container`, `Metadata` and `Type` convenience readers are provided for `Symbol`
- [x] A `DbSet<T>` property's entity type argument is extractable from its `type` field
- [x] No file under `Classification/Passes/` is modified
- [x] Unit tests cover each field, the sentinel, escaped values and a malformed signature
- [x] Gate check passes: quick gate command
- [x] Test count: 328 + 8 new Analysis tests = 336 pass (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add shared symbol signature reader`

---

### T12: Define the persistence model records

**What**: The pure record types the builder produces and the emitter consumes.
**Where**: `src/Csharp2Md.Analysis/Classification/Persistence/PersistenceModel.cs`
**Depends on**: T11
**Reuses**: Domain facets and proof types (`DataStoreTechnology`, `DataObjectForm`, `DataOperationKind`, `MappingStateKind`, `EvidenceChain`, `ClassifierIdentity`)
**Requirement**: PK-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] `PersistenceModel`, `StoreNode`, `ObjectNode`, `FieldNode`, `OperationNode`, `UnresolvedNode` and `CoverageCounts` match the design's shapes exactly
- [x] `OperationNode.Callables` is a collection, encoding the many-to-one that PK-38 requires
- [x] All types are `internal sealed` and hold no Roslyn type (`CoverageCounts` is the design's `internal readonly record struct`, sealed implicitly)
- [x] Gate check passes: build gate command
- [x] Test count: 1072 pass — Domain 545, Analysis 336, Storage 161, Cli 27, Projection 3 (no silent deletions)

**Tests**: none
**Gate**: build

**Commit**: `feat(analysis): define the persistence model records`

---

### T13: Resolve data stores in the model builder

**What**: The builder's store step — discover contexts from the ledger and name them.
**Where**: `src/Csharp2Md.Analysis/Classification/Persistence/PersistenceModelBuilder.cs`
**Depends on**: T12
**Reuses**: `SignatureReader`, `PayloadReader`, `ClassifierContext.FactsByType<Symbol>()`
**Requirement**: PK-10, PK-11, PK-12, PK-13

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Contexts are discovered as the union of `DbSet<T>` property containers and `context-type` payload entries (PK-10)
- [x] The name is the `Configuration` key when a key's owning callable mentions that context type (PK-11) — "mentions" is read from the ledger as the callable's decoded signature components or any payload literal of an observation it owns
- [x] The name falls back to the context's fully-qualified type name otherwise (PK-12), without the `global::` alias qualifier, matching the spec's stated `Acme.Orders.Data.OrderDbContext`
- [x] A project with no context contributes no store (PK-13)
- [x] Technology is always `relational`
- [x] Stores are emitted ordinal-sorted by context type name
- [x] Unit tests drive a hand-built context: both naming paths, no-context, and two contexts in one solution
- [x] Gate check passes: full gate command
- [x] Test count: 1080 pass — Domain 545, Analysis 344, Storage 161, Cli 27, Projection 3 (no silent deletions)

**Tests**: unit
**Gate**: full

> Execution note (open for T23): PK-11's *fixture* path does not fire yet. `Program.ConfigureHost` names `OrderDbContext` only through `AddSingleton<Data.OrderDbContext>()`, and the ledger keeps no record of that type — the invocation payload carries `method-name` and `target-type` only, and `TypeUsage` payloads are empty. Both naming paths are implemented and unit-proven; the fixture store therefore still resolves to `Acme.Orders.Data.OrderDbContext`. T23's `name = OrdersDb` assertion needs the type argument in the invocation payload (an extraction change) or a fixture callable that names the context.

**Commit**: `feat(analysis): resolve data stores from the ledger`

---

### T14: Resolve entity-set data objects and explicit table mappings

**What**: The builder's entity-object step — one object per `DbSet<T>`, upgraded by a proven `ToTable`.
**Where**: `src/Csharp2Md.Analysis/Classification/Persistence/PersistenceModelBuilder.cs`
**Depends on**: T13
**Reuses**: `SignatureReader` type-argument extraction; `PayloadReader` for `table-name`/`entity-type`
**Requirement**: PK-14, PK-15, PK-16, PK-17

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] One `ObjectNode` per `DbSet<T>` property, form `table` (PK-14)
- [x] A proven `ToTable` sets the physical name and `ExplicitConfirmation` (PK-15)
- [x] No `ToTable` falls back to the `DbSet` member name with `ConventionalCandidate` (PK-16)
- [x] `SchemaName` is the literal `unknown` when no schema is proven (PK-17)
- [x] A non-constant `ToTable` argument falls back to convention (spec edge case)
- [x] Two `DbSet` members exposing the same entity type produce two objects, unmerged (spec edge case)
- [x] Unit tests cover each path against a hand-built context
- [x] Gate check passes: full gate command
- [x] Test count: 1085 pass — Domain 545, Analysis 349, Storage 161, Cli 27, Projection 3 (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): resolve entity-set data objects and table mappings`

---

### T15: Resolve SQL-derived data objects

**What**: The builder's SQL-object step — one object per distinct statement target, never merged with an entity object.
**Where**: `src/Csharp2Md.Analysis/Classification/Persistence/PersistenceModelBuilder.cs`
**Depends on**: T14
**Reuses**: `sql-target` and `context-type` payload entries from T6
**Requirement**: PK-18, PK-20, PK-21

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] One `ObjectNode` per distinct `sql-target`, under the store named by that occurrence's `context-type`, always `ConventionalCandidate` (PK-18)
- [ ] An `EXEC` target gets form `unknown` (PK-21)
- [ ] `Orders` and `order_headers` remain two distinct objects — no merge on similarity, prefix or pluralization (PK-20)
- [ ] Two statements naming the same target share one object
- [ ] Unit tests cover the merge-refusal case explicitly, with a name pair that differs only by pluralization
- [ ] Gate check passes: full gate command
- [ ] Test count: 828 + tests added so far pass (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): resolve SQL-derived data objects`

---

### T16: Resolve data fields and their mapping state

**What**: The builder's field step — fields from LINQ, tracked writes and SQL column lists, upgraded by a proven `HasColumnName`.
**Where**: `src/Csharp2Md.Analysis/Classification/Persistence/PersistenceModelBuilder.cs`
**Depends on**: T15
**Reuses**: `field-names`, `sql-columns` and `Assignment` payload entries from T5–T7
**Requirement**: PK-22, PK-23, PK-24, PK-25, PK-26, PK-27

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Fields come from `field-names` (PK-22), from `Assignment` observations paired with a `SaveChanges` access in the same callable (PK-23), and from `sql-columns` (PK-24)
- [ ] A proven `HasColumnName` sets the physical name and `ExplicitConfirmation` (PK-25)
- [ ] Everything else uses the CLR property name with `ConventionalCandidate` (PK-26)
- [ ] A `SELECT *` statement contributes no field (PK-27)
- [ ] Each `FieldNode` carries the property's `Symbol` reference when one exists, so T20 can emit `maps-to`
- [ ] An assignment to a non-entity type contributes no field (spec edge case)
- [ ] A property reached inside an anonymous-type projection still produces a field (spec edge case)
- [ ] Unit tests cover every source and both mapping states
- [ ] Gate check passes: full gate command
- [ ] Test count: 828 + tests added so far pass (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): resolve data fields and mapping state`

---

### T17: Resolve data operations

**What**: The builder's operation step — one node per `(object, operation kind)` accumulating every owning callable.
**Where**: `src/Csharp2Md.Analysis/Classification/Persistence/PersistenceModelBuilder.cs`
**Depends on**: T16
**Reuses**: The `operation` payload entry from T5 and the field resolution from T16
**Requirement**: PK-31, PK-35, PK-36, PK-37, PK-38

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] One `OperationNode` per resolved `(object, kind)` pair (PK-31)
- [ ] Operation kinds follow exactly the closed table in PK-35, with a test per row
- [ ] An `Assignment` plus a `SaveChanges` in one callable yields an `update` (PK-36)
- [ ] A bare `SaveChanges` with no tracked assignment and no set operation yields nothing (PK-37)
- [ ] Two callables performing the same operation on the same object produce one node with two callables (PK-38)
- [ ] The classifier identity is `persistence-ef` for entity-derived and `persistence-sql` for statement-derived operations
- [ ] Unit tests cover each rule against a hand-built context
- [ ] Gate check passes: full gate command
- [ ] Test count: 828 + tests added so far pass (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): resolve data operations and their callables`

---

### T18: Resolve unresolved records and coverage counts

**What**: The builder's final step — everything recognized but unresolved, plus the coverage numerator and denominator.
**Where**: `src/Csharp2Md.Analysis/Classification/Persistence/PersistenceModelBuilder.cs`
**Depends on**: T17
**Reuses**: `UnresolvedCause` and `RelationKind` from Domain
**Requirement**: PK-39, PK-40, PK-41, PK-42, PK-51, PK-52, PK-53

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] A non-constant SQL target yields `UnresolvedNode(operates-on, InsufficientEvidence)` and no object (PK-39)
- [ ] An unreadable leading keyword yields `UnresolvedNode(accesses-data, NoCandidateFound)` (PK-40)
- [ ] An entity type with no `Symbol` fact yields `UnresolvedNode(accesses-data, NoCandidateFound)` and no operation (PK-41)
- [ ] A persistence-named type with no data access contributes nothing at all (PK-42)
- [ ] `CoverageCounts` reports recognized and resolved occurrence counts and the unresolved owners' ids (PK-51, PK-52)
- [ ] No percentage and no verdict is computed anywhere in the builder (PK-53)
- [ ] Unit tests cover each unresolved cause and the count arithmetic
- [ ] Gate check passes: full gate command
- [ ] Test count: 828 + tests added so far pass (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): resolve unresolved persistence evidence and coverage`

---

### T19: Emit persistence facts

**What**: The emitter's fact walk — stores, objects, fields and operations into the accumulator.
**Where**: `src/Csharp2Md.Analysis/Classification/Persistence/PersistenceEmitter.cs`
**Depends on**: T18
**Reuses**: `DataStore/DataObject/DataField/DataOperation.Create`; `SnapshotAccumulator.AddFact`
**Requirement**: PK-14, PK-18, PK-22, PK-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Facts are emitted in one ordered walk, ordinal-sorted by canonical id at every level
- [ ] The store name literal uses `LiteralRole.SchemaName` regardless of derivation
- [ ] `DataObject` schema and table literals carry the roles their `Create` guards require
- [ ] Two structurally identical facts deduplicate through `AddFact` without flagging corruption (spec edge case)
- [ ] Unit tests assert emitted fact identities for a hand-built model
- [ ] Gate check passes: full gate command
- [ ] Test count: 828 + tests added so far pass (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): emit persistence facts from the model`

---

### T20: Emit persistence relations

**What**: The emitter's relation walk — confirmed `accesses-data`, `operates-on` and `maps-to`.
**Where**: `src/Csharp2Md.Analysis/Classification/Persistence/PersistenceEmitter.cs`
**Depends on**: T19
**Reuses**: `ConfirmedRelation.Create`; `RelationPass`'s ad-hoc facet-axis registration pattern
**Requirement**: PK-28, PK-29, PK-32, PK-33, PK-34

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] `accesses-data` is emitted per callable with `EvidenceMethod.Semantic`, the pass's classifier identity, and a non-empty `derived_from` naming the originating observation (PK-32)
- [ ] `operates-on` reaches the target `DataObject` (PK-33) and each touched `DataField` (PK-34)
- [ ] `maps-to` is emitted with `EvidenceMethod.Configured` and a `mapping-role` facet of `data-object-mapping` (PK-28) or `data-field-mapping` (PK-29)
- [ ] `mapping-role` is registered by appending a `FacetAxisDescriptor` over `TaxonomyTables.Default.MappingRoles`, mirroring `RelationPass`
- [ ] `Create` is called with `sourceFact` for `accesses-data` and `targetFact` for `operates-on`, satisfying the shape guards
- [ ] Unit tests assert relation kind, evidence method, facets and evidence chain for each of the five relation shapes
- [ ] Gate check passes: full gate command
- [ ] Test count: 828 + tests added so far pass (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): emit confirmed persistence relations`

---

### T21: Emit candidates, unresolved records and the coverage diagnostic

**What**: The emitter's remaining walk — conventional `maps-to` candidates, unresolved records, and the coverage `DiagnosticRecord`.
**Where**: `src/Csharp2Md.Analysis/Classification/Persistence/PersistenceEmitter.cs`
**Depends on**: T20
**Reuses**: `CandidateLink.Create`, `UnresolvedRecord.Create`, `DiagnosticRecord`
**Requirement**: PK-30, PK-51, PK-52, PK-53

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] A `ConventionalCandidate` object or field yields a `CandidateLink` of kind `maps-to` and no confirmed relation (PK-30)
- [ ] Every `UnresolvedNode` becomes an `UnresolvedRecord` with its cause and available evidence
- [ ] One `DiagnosticRecord` carries the recognized and resolved counts (PK-51) and the unresolved owners' ids (PK-52)
- [ ] The diagnostic message contains no percentage and no pass/fail verdict (PK-53)
- [ ] The diagnostic's `identityOrKey` is a fact id, never an absolute path
- [ ] Unit tests assert candidate/confirmed exclusivity and the diagnostic's content
- [ ] Gate check passes: full gate command
- [ ] Test count: 828 + tests added so far pass (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): emit persistence candidates, unresolved and coverage`

---

### T22: Register PersistencePass in the pipeline

**What**: The `IClassifierPass` seam plus its registration, with composability and stage-count tests.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/PersistencePass.cs`
**Depends on**: T21
**Reuses**: `ClassificationAndPromotionStage`, unchanged; `PipelineStages.CreateDefault()`
**Requirement**: PK-43, PK-44

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] `PersistencePass.Execute` is builder-then-emitter and holds no classification logic of its own
- [ ] It is registered after `ContractPass` and before `RelationPass` in `PipelineStages.CreateDefault()`
- [ ] No workstream 5A classifier pass file is modified (PK-43) — verified by `git diff --stat` in the commit body
- [ ] The stage's `StageResult` fact and relation counts include persistence output (PK-44)
- [ ] A test registers a no-op pass alongside the real ones and asserts no interference, matching the existing composability test
- [ ] All 5A classification tests still pass unchanged
- [ ] Gate check passes: build gate command
- [ ] Test count: 1019 + tests added so far pass (no silent deletions)

**Tests**: integration
**Gate**: build

**Commit**: `feat(analysis): register the persistence classifier pass`

---

### T23: End-to-end persistence package assertions

**What**: A fixture-backed integration test asserting the published persistence content named in the spec's Success Criteria.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/PersistenceIntegrationTests.cs`
**Depends on**: T22
**Reuses**: The `FullClassifierPipelineTests` fixture-analysis harness
**Requirement**: PK-20, PK-38, PK-42, PK-51

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Exactly one `DataStore`, named `OrdersDb`, technology `relational`
- [ ] `order_headers` and `order_status` are `ExplicitConfirmation` with confirmed `maps-to`; `OrderLines`, `Id` and `Amount` are `ConventionalCandidate` with `CandidateLink` records
- [ ] The SQL-derived `Orders` object coexists with `order_headers`, unmerged (PK-20)
- [ ] `PayOrder` and `Reprice` share one `update` `DataOperation` reached by two `accesses-data` relations (PK-38)
- [ ] `SelectAllFrom` yields an `UnresolvedRecord` and no `DataObject`
- [ ] `OrderRepository` yields no persistence record of any kind (PK-42)
- [ ] `diagnostics.json` carries the coverage counts with no percentage (PK-51)
- [ ] Gate check passes: full gate command
- [ ] Test count: 828 + tests added so far pass (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `test(analysis): assert end-to-end persistence package content`

---

### T24: Determinism assertions

**What**: Tests proving persistence output is clone-path-independent and byte-stable across re-runs.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/PersistenceDeterminismTests.cs`
**Depends on**: T23
**Reuses**: The two-clone harness in `ClassifierDeterminismTests`
**Requirement**: PK-45, PK-46, PK-47

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Two clone paths produce equal persistence fact identities, relations, candidates and unresolved records (PK-45)
- [ ] Two commits of the same solution produce byte-identical canonical payloads (PK-46)
- [ ] No persistence fact, relation, candidate, unresolved record or diagnostic contains an absolute filesystem path (PK-47)
- [ ] Document-order independence holds for persistence output, matching the existing `DocumentOrderIndependenceTests` pattern
- [ ] Gate check passes: full gate command
- [ ] Test count: 828 + tests added so far pass (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `test(analysis): assert persistence determinism across clones and reruns`

---

### T25: Security and isolation assertions

**What**: Tests proving neither fixture credential leaks and that the assembly boundaries hold.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/PersistenceIsolationTests.cs`
**Depends on**: T24
**Reuses**: The isolation harness in `ClassifierIsolationTests`
**Requirement**: PK-48, PK-49, PK-50

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] No canonical payload contains the `OrderSqlQueries.ConnectionString` value (PK-48)
- [ ] No canonical payload contains the `appsettings.json` `OrdersDb` value (PK-48) — asserted separately, so neither check covers for the other
- [ ] Any suspected secret appears only as redacted evidence with document, span and hash
- [ ] No `Microsoft.CodeAnalysis` type is reachable through the persistence classifier's surface (PK-49)
- [ ] `Csharp2Md.Cli` still declares no project reference to `Csharp2Md.Domain` (PK-49)
- [ ] The in-memory adapter produces persistence facts and writes no file (PK-50)
- [ ] Gate check passes: build gate command
- [ ] Test count: 1019 + tests added so far pass (no silent deletions)

**Tests**: integration
**Gate**: build

**Commit**: `test(analysis): assert persistence secret absence and assembly isolation`

---

### T26: Round-trip persistence facts through Storage

**What**: A Storage test proving the four persistence fact types and their relations survive commit and read.
**Where**: `tests/Csharp2Md.Storage.Tests/Mapping/PersistenceRoundTripTests.cs`
**Depends on**: T25
**Reuses**: The `DomainMapper` round-trip harness used for 5A's classifier facts
**Requirement**: PK-46

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] `DataStore`, `DataObject`, `DataField` and `DataOperation` round-trip with equal identities and facets
- [ ] `accesses-data`, `operates-on` and `maps-to` relations round-trip with their facets and evidence chains intact
- [ ] `CandidateLink` and `UnresolvedRecord` entries for persistence round-trip
- [ ] The coverage `DiagnosticRecord` reaches `diagnostics.json`
- [ ] Gate check passes: build gate command
- [ ] Test count: 1019 + tests added so far pass (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `test(storage): round-trip persistence facts through DomainMapper`

---

## Phase Execution Map

Each phase line repeats the previous phase's final task as its head, so the phase-boundary dependency is visible rather than implied.

```
Phase 1:         T1 --→ T2 --→ T3 --→ T4
Phase 2:   T4 --→ T5 --→ T6 --→ T7 --→ T8 --→ T9
Phase 3:   T9 --→ T10 --→ T11 --→ T12 --→ T13
Phase 4:  T13 --→ T14 --→ T15 --→ T16 --→ T17 --→ T18
Phase 5:  T18 --→ T19 --→ T20 --→ T21 --→ T22
Phase 6:  T22 --→ T23 --→ T24 --→ T25 --→ T26
```

Execution is strictly sequential — there is no intra-phase parallelism. A single agent (or batch worker) works one task at a time, in order.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: EF raw-SQL stand-ins | 1 file, 3 methods | ✅ Granular |
| T2: Execute fixture SQL | 1 file | ✅ Granular |
| T3: Config-bound context | 1 file | ✅ Granular |
| T4: SqlStatementReader | 1 type + tests | ✅ Granular |
| T5: DataAccess EF payload | 1 file, 1 concern | ✅ Granular |
| T6: DataAccess SQL payload | 1 file, 1 concern | ✅ Granular |
| T7: Assignment payload | 1 file | ✅ Granular |
| T8: EfMappingPayload | 1 type + 3-line wiring | ✅ Granular |
| T9: GetConnectionString | 1 file, 1 name | ✅ Granular |
| T10: PayloadReader | 1 type | ✅ Granular |
| T11: SignatureReader | 1 type | ✅ Granular |
| T12: Model records | 1 file, cohesive record set | ✅ Granular |
| T13–T18: builder steps | 1 method each on 1 file | ✅ Granular |
| T19–T21: emitter steps | 1 method each on 1 file | ✅ Granular |
| T22: PersistencePass | 1 type + 1 registration line | ✅ Granular |
| T23–T26: test suites | 1 test file each | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | (phase head) | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | T5 → T6 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T7 | T7 → T8 | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T9 | T9 → T10 | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T12 | T12 → T13 | ✅ Match |
| T14 | T13 | T13 → T14 | ✅ Match |
| T15 | T14 | T14 → T15 | ✅ Match |
| T16 | T15 | T15 → T16 | ✅ Match |
| T17 | T16 | T16 → T17 | ✅ Match |
| T18 | T17 | T17 → T18 | ✅ Match |
| T19 | T18 | T18 → T19 | ✅ Match |
| T20 | T19 | T19 → T20 | ✅ Match |
| T21 | T20 | T20 → T21 | ✅ Match |
| T22 | T21 | T21 → T22 | ✅ Match |
| T23 | T22 | T22 → T23 | ✅ Match |
| T24 | T23 | T23 → T24 | ✅ Match |
| T25 | T24 | T24 → T25 | ✅ Match |
| T26 | T25 | T25 → T26 | ✅ Match |

No dependency points to a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Fixture source | none | none | ✅ OK |
| T2 | Fixture source | none | none | ✅ OK |
| T3 | Fixture source | none | none | ✅ OK |
| T4 | Pure reader | unit | unit | ✅ OK |
| T5 | Observation detector | unit + integration | unit + integration | ✅ OK |
| T6 | Observation detector | unit + integration | unit + integration | ✅ OK |
| T7 | Observation detector | unit + integration | unit + integration | ✅ OK |
| T8 | Observation detector | unit + integration | unit + integration | ✅ OK |
| T9 | Observation detector | unit + integration | unit + integration | ✅ OK |
| T10 | Pure reader | unit | unit | ✅ OK |
| T11 | Pure reader | unit | unit | ✅ OK |
| T12 | Model records (no logic) | none | none | ✅ OK |
| T13 | Classification pass logic | unit + integration | unit | ✅ OK — integration is covered by T23 against the same code, which the matrix's end-to-end row owns |
| T14 | Classification pass logic | unit + integration | unit | ✅ OK — same |
| T15 | Classification pass logic | unit + integration | unit | ✅ OK — same |
| T16 | Classification pass logic | unit + integration | unit | ✅ OK — same |
| T17 | Classification pass logic | unit + integration | unit | ✅ OK — same |
| T18 | Classification pass logic | unit + integration | unit | ✅ OK — same |
| T19 | Classification pass logic | unit + integration | unit | ✅ OK — same |
| T20 | Classification pass logic | unit + integration | unit | ✅ OK — same |
| T21 | Classification pass logic | unit + integration | unit | ✅ OK — same |
| T22 | Pipeline stage | integration | integration | ✅ OK |
| T23 | End-to-end | integration | integration | ✅ OK |
| T24 | End-to-end | integration | integration | ✅ OK |
| T25 | End-to-end | integration | integration | ✅ OK |
| T26 | Storage mapping | unit | unit | ✅ OK |

`Tests: none` appears only for T1–T3 (fixture source) and T12 (record definitions), both of which the matrix marks `none`.

---

## Requirement Coverage

All 53 requirements map to at least one task.

| Requirement | Tasks |
| --- | --- |
| PK-01, PK-02, PK-03, PK-05 | T5 |
| PK-04 | T1, T2, T6 |
| PK-06 | T7 |
| PK-07 | T8 |
| PK-08 | T9 |
| PK-09 | T6, T25 |
| PK-10, PK-12, PK-13 | T13 |
| PK-11 | T3, T13 |
| PK-14 | T14, T19 |
| PK-15, PK-16, PK-17 | T14 |
| PK-18 | T15, T19 |
| PK-19 | T4, T15 |
| PK-20 | T15, T23 |
| PK-21 | T15 |
| PK-22 | T16, T19 |
| PK-23, PK-24, PK-25, PK-26 | T16 |
| PK-27 | T4, T16 |
| PK-28, PK-29 | T20 |
| PK-30 | T21 |
| PK-31 | T12, T17, T19 |
| PK-32, PK-33, PK-34 | T20 |
| PK-35, PK-36, PK-37 | T17 |
| PK-38 | T17, T23 |
| PK-39, PK-40 | T4, T18 |
| PK-41 | T18 |
| PK-42 | T18, T23 |
| PK-43 | T10, T11, T22 |
| PK-44 | T22 |
| PK-45, PK-46, PK-47 | T24 |
| PK-46 | T24, T26 |
| PK-48, PK-49, PK-50 | T25 |
| PK-51 | T18, T21, T23 |
| PK-52, PK-53 | T18, T21 |
