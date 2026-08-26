# Persistence Knowledge Validation

**Date**: 2026-08-26
**Spec**: `.specs/features/persistence-knowledge/spec.md`
**Diff range**: `343a248^..7b064e2` (plus `ee40071` Storage PK- prefix allowlist)
**Verifier**: independent sub-agent (author ≠ verifier)
**Result**: PASS

T1–T26 each have a Conventional Commit on `feature/persistence-knowledge` (`343a248` … `7b064e2`). `tasks.md` Done-when boxes are all checked. Header still says “Draft”; the checkboxes and commits are the completion evidence.

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1 | Done | `343a248` fixture raw-SQL stand-ins |
| T2 | Done | `0f50d87` execute six SQL shapes |
| T3 | Done | `f80f0a8` bind OrderDbContext to OrdersDb |
| T4 | Done | `012a4c8` SqlStatementReader |
| T5 | Done | `508800d` DataAccess EF payload |
| T6 | Done | `2a92d9a` DataAccess SQL payload |
| T7 | Done | `7ed4bab` Assignment payload |
| T8 | Done | `1ff91cf` EfMappingPayload |
| T9 | Done | `4309b0d` GetConnectionString |
| T10 | Done | `d0b4521` PayloadReader |
| T11 | Done | `77c5f1f` SignatureReader |
| T12 | Done | `07aa088` persistence model records |
| T13 | Done | `9f7cedd` resolve data stores |
| T14 | Done | `afa2d2c` entity-set objects |
| T15 | Done | `976d609` SQL-derived objects |
| T16 | Done | `e293d79` fields and mapping state |
| T17 | Done | `efa3775` data operations |
| T18 | Done | `1c908a0` unresolved and coverage |
| T19 | Done | `d8b263a` emit facts |
| T20 | Done | `467af9f` emit relations |
| T21 | Done | `0662d07` candidates, unresolved, coverage diagnostic |
| T22 | Done | `59c71a7` register PersistencePass |
| T23 | Done | `8318697` e2e package assertions |
| T24 | Done | `250fe87` determinism |
| T25 | Done | `8e2b4c5` secrets and isolation |
| T26 | Done | `7b064e2` Storage round-trip |

`ee40071` is a Storage coverage-scan allowlist for the `PK-` trait prefix, not a numbered task. No blocked or partial tasks.

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| WHEN DataAccessDetector observes a data access THEN payload `operation` is one of `read`, `insert`, `update`, `delete`, `execute`, `unknown` (PK-01) | PlaceOrder `insert`; SaveChanges `unknown`; every DataAccess has an `operation` key | `DataAccessDetectorTests.cs:67` `operation`/`insert`; `:44-46` `operation`/`unknown`; `:122-124` `Assert.All` key `operation` | PASS |
| WHEN access binds to `DbSet<TEntity>` THEN `entity-type` is TEntity FQN (PK-02) | `global::Acme.Orders.Data.Order` | `DataAccessDetectorTests.cs:70-72` `entity-type` == `global::Acme.Orders.Data.Order` | PASS |
| WHEN access binds to a `DbContext` (or derived) THEN `context-type` is that FQN (PK-03) | `global::Acme.Orders.Data.OrderDbContext` | `DataAccessDetectorTests.cs:47-51` `context-type` == `global::Acme.Orders.Data.OrderDbContext` | PASS |
| WHEN FromSqlRaw/FromSqlInterpolated/ExecuteSqlRaw/ExecuteSqlInterpolated has a constant statement THEN `sql-operation`, `sql-target`, ordinal-sorted `\|`-joined `sql-columns`, and no statement text (PK-04) | SELECT: `sql-operation=read`, `sql-target=Orders`, `sql-columns=Id\|Status`, no `SELECT` text; DELETE unquoted `Orders` | `DataAccessDetectorTests.cs:142-148` those entries and `Assert.DoesNotContain` SELECT; `:165-166` delete/`Orders` | PASS |
| WHEN a LINQ operator over `DbSet<TEntity>` THEN one `field-names` entry, ordinal-sorted, `\|`-joined (PK-05) | Where `Id`; Select `Amount\|Id\|Status`; both `operation=read` | `DataAccessDetectorTests.cs:86` `field-names`/`Id`; `:107` `Amount\|Id\|Status`; `:89` and `:109-111` `read` | PASS |
| WHEN AssignmentDetector observes an entity-property assignment THEN `entity-type` and `field-name` (PK-06) | PayOrder `Status` on Order; Reprice `Amount` | `AssignmentDetectorTests.cs:37-46` entity-type Order, `field-name=Status` role FieldName; `:70-76` Amount | PASS |
| WHEN invocation binds to ToTable / HasColumnName / Entity\<T\> / Property THEN table-name / field-name / entity-type / enclosing Entity\<T\> entity-type (PK-07) | `table-name=order_headers`; `field-name=order_status`; Entity payload `entity-type=Order` | `EfMappingPayloadTests.cs:25-34` table-name/entity-type; `:50-61` field-name/entity-type/property-name Status; `:70-77` Entity invocations with entity-type Order | PASS |
| WHEN GetConnectionString(name) on IConfiguration with a constant argument THEN Configuration observation `key` is that name (PK-08) | `key=OrdersDb`, role ConfigurationKey | `ConfigurationDetectorTests.cs:60-63` key/role/`OrdersDb` | PASS |
| Persistence payload entries SHALL NOT contain connection string, password, token, certificate, or authorization values; secrets redacted (PK-09) | Fixture passwords and `Server=` absent from payloads | `DataAccessDetectorTests.cs:230-231` both fixture secrets; `ConfigurationDetectorTests.cs:79-81` those plus `Server=` | PASS |
| WHEN ledger names a DbContext-derived type (DbSet container or `context-type`) THEN exactly one relational DataStore (PK-10) | One store, technology Relational; DbSet+payload still one store | `PersistenceModelBuilderTests.cs:32-35` Single + Relational; `:65` one FQN; `PersistenceIntegrationTests.cs:22-24` Single OrdersDb/relational | PASS |
| WHEN a Configuration key's owning callable also references that DbContext THEN store name is that key (PK-11) | Name `OrdersDb` | `PersistenceModelBuilderTests.cs:79` and `:93` `Assert.Equal("OrdersDb", ...)`; `PersistenceIntegrationTests.cs:23` `OrdersDb` | PASS |
| IF no such key is proven THEN store name is the DbContext FQN without `global::` (PK-12) | `Acme.Orders.Data.OrderDbContext` | `PersistenceModelBuilderTests.cs:35` and `:107` that name | PASS |
| WHEN a project contains no DbContext-derived type THEN no DataStore (PK-13) | Empty stores | `PersistenceModelBuilderTests.cs:120` `Assert.Empty(model.Stores)` | PASS |
| WHEN DbContext exposes `DbSet<TEntity>` THEN one DataObject form `table` under that store (PK-14) | Form Table; e2e `order_headers` form table | `PersistenceModelBuilderTests.cs:157` `DataObjectForm.Table`; `PersistenceEmitterTests.cs:43-46` store/form/table; `PersistenceIntegrationTests.cs:30` `table` | PASS |
| WHEN Entity\<T\>().ToTable(name) is proven THEN table name is `name` and mapping `ExplicitConfirmation` (PK-15) | `order_headers` / ExplicitConfirmation | `PersistenceModelBuilderTests.cs:175-176`; `PersistenceIntegrationTests.cs:27-29` | PASS |
| IF no ToTable (or non-constant) THEN table name is the DbSet member name, mapping `ConventionalCandidate` (PK-16) | `OrderLines` conventional; non-constant ToTable falls back to `Orders` | `PersistenceModelBuilderTests.cs:190-191` OrderLines/ConventionalCandidate; `:207-208` Orders/ConventionalCandidate; `PersistenceIntegrationTests.cs:39-42` | PASS |
| DataObject schema name is literal `unknown` when no schema is proven (PK-17) | `unknown` | `PersistenceModelBuilderTests.cs:158` `Assert.Equal("unknown", dataObject.SchemaName)` | PASS |
| WHEN bounded reader resolves a statement target THEN DataObject under executing context, that identifier, `ConventionalCandidate` (PK-18) | SQL `Orders` conventional, distinct from `order_headers` | `PersistenceModelBuilderTests.cs:247-249`; `PersistenceIntegrationTests.cs:33-37` | PASS |
| WHEN target is square-bracketed THEN unbracketed identifier is the table name (PK-19) | `[Orders]` → `Orders` | `SqlStatementReaderTests.cs:60-64` `Assert.Equal("Orders", facts.Target)`; `DataAccessDetectorTests.cs:166` payload `sql-target=Orders` | PASS |
| SHALL NOT merge DataObjects for similar / prefix / pluralization names (PK-20) | `Orders` and `order_headers` unmerged; two DbSets unmerged | `PersistenceModelBuilderTests.cs:351`; `:376`; `PersistenceIntegrationTests.cs:37` `Assert.NotEqual` identities | PASS |
| WHEN EXEC names a procedure THEN DataObject form `unknown`, that name, `ConventionalCandidate` (PK-21) | `usp_RebuildOrderTotals`, form Unknown, ConventionalCandidate | `PersistenceModelBuilderTests.cs:293-295` | PASS |
| WHEN `field-names` names entity properties THEN a DataField per name under that entity DataObject (PK-22) | Status field; anonymous projection Amount/Id/Status | `PersistenceModelBuilderTests.cs:396-399`; `:421` | PASS |
| WHEN Assignment names an entity property and same callable has SaveChanges THEN DataField under that entity DataObject (PK-23) | Status field with SaveChanges; none without; none for unexposed type | `PersistenceModelBuilderTests.cs:437-438`; `:455` empty; `:471` empty fields | PASS |
| WHEN `sql-columns` names identifiers THEN DataField per column under the statement DataObject (PK-24) | Amount, Id, Status under SQL Orders | `PersistenceModelBuilderTests.cs:493` | PASS |
| WHEN Property(...).HasColumnName(name) is proven THEN field name is `name`, mapping `ExplicitConfirmation` (PK-25) | `order_status` / ExplicitConfirmation | `PersistenceModelBuilderTests.cs:540-542`; `PersistenceIntegrationTests.cs:44-48` | PASS |
| IF no HasColumnName (or non-constant) THEN CLR property name, `ConventionalCandidate` (PK-26) | Status/Id/Amount conventional | `PersistenceModelBuilderTests.cs:397-398`; `:560-561`; `PersistenceIntegrationTests.cs:50-57` | PASS |
| WHEN column list is `*` THEN no DataField for that statement (PK-27) | Zero columns from reader; builder empty fields; emitter no DataField | `SqlStatementReaderTests.cs:29` `Assert.Empty`; `PersistenceModelBuilderTests.cs:515`; `PersistenceEmitterTests.cs:624` | PASS |
| WHEN DataObject mapping is ExplicitConfirmation THEN confirmed `maps-to` from entity Symbol, role `data-object-mapping`, evidence `configured` (PK-28) | maps-to headers, facet data-object-mapping, Configured | `PersistenceEmitterTests.cs:356-362`; `PersistenceIntegrationTests.cs:60-65` | PASS |
| WHEN DataField mapping is ExplicitConfirmation THEN confirmed `maps-to` from property Symbol, role `data-field-mapping`, evidence `configured` (PK-29) | maps-to order_status, facet data-field-mapping, Configured | `PersistenceEmitterTests.cs:400-405`; `PersistenceIntegrationTests.cs:66-71` | PASS |
| IF mapping is ConventionalCandidate THEN CandidateLink kind maps-to and no confirmed maps-to (PK-30) | OrderLines/Id/Amount candidates; not in confirmed maps-to | `PersistenceEmitterTests.cs:473-479`; `PersistenceIntegrationTests.cs:74-85` | PASS |
| WHEN DataAccess resolves kind and target DataObject THEN DataOperation with that target, kind, and target mapping state (PK-31) | Read on order_headers, ExplicitConfirmation | `PersistenceEmitterTests.cs:122-124`; `PersistenceModelBuilderTests.cs:635-636` Read | PASS |
| WHEN DataOperation is created THEN confirmed `accesses-data` from owning callable, evidence semantic, classifier `persistence-ef` or `persistence-sql` v1, derived_from names the observation (PK-32) | Kind AccessesData; classifier id/version; DerivedFrom DataAccess | `PersistenceEmitterTests.cs:218-225` kind/source/target/EfIdentity/DerivedFrom DataAccess; `:448` SqlIdentity | PASS |
| WHEN DataOperation is created THEN confirmed `operates-on` to the target DataObject (PK-33) | Source operation, target data object | `PersistenceEmitterTests.cs:266-268` | PASS |
| WHEN access/tracked-write resolves DataFields THEN confirmed `operates-on` to each field (PK-34) | Two field relations Amount and Status | `PersistenceEmitterTests.cs:322-324`; `PersistenceModelBuilderTests.cs:637` FieldNames | PASS |
| Operation kind from the closed table only (PK-35) | SQL/entity-set kinds; `save`/`unknown`/null → Unknown | `PersistenceModelBuilderTests.cs:586-589` theory; `:607-609` insert/read + persistence-ef; `:616-618` Unknown | PASS |
| WHEN callable has entity-property Assignment and SaveChanges THEN DataOperation kind `update` on that entity DataObject (PK-36) | Update, field Status, callable PayOrder | `PersistenceModelBuilderTests.cs:654-656`; `PersistenceIntegrationTests.cs:87-97` shared update, PayOrder/Reprice accesses-data | PASS |
| SHALL NOT create a DataOperation for bare SaveChanges with no tracked assignment and no entity-set operation (PK-37) | Empty operations | `PersistenceModelBuilderTests.cs:671` `Assert.Empty` operations | PASS |
| WHEN two callables perform the same kind on the same DataObject THEN one shared DataOperation and one accesses-data per callable (PK-38) | One update, two callables; e2e two accesses-data | `PersistenceModelBuilderTests.cs:690-694`; `PersistenceIntegrationTests.cs:90-97` `Assert.Equal(2, updateAccesses.Length)` | PASS |
| IF raw-SQL target is interpolation/non-constant THEN UnresolvedRecord kind `operates-on`, cause `InsufficientEvidence`, no DataObject for it (PK-39) | SelectAllFrom operates-on/InsufficientEvidence; no `{` table | `PersistenceModelBuilderTests.cs:710-714`; `PersistenceIntegrationTests.cs:100-107`; `SqlStatementReaderTests.cs:109` reject `{tableName}` | PASS (spec-precision: see note) |
| IF leading keyword is outside the bounded set THEN UnresolvedRecord kind `accesses-data`, cause `NoCandidateFound` (PK-40) | MERGE rejected; context-only unknown → AccessesData/NoCandidateFound | `SqlStatementReaderTests.cs:134-136` `Assert.False`; `PersistenceModelBuilderTests.cs:728-731` | PASS (spec-precision: see note) |
| IF entity type cannot be resolved to a Symbol THEN UnresolvedRecord kind `accesses-data`, cause `NoCandidateFound`, no DataOperation (PK-41) | AccessesData/NoCandidateFound; empty operations | `PersistenceModelBuilderTests.cs:745-748` | PASS |
| WHEN a type's name/namespace/folder suggests persistence but it performs no data access THEN no persistence record (PK-42) | OrderRepository absent from persistence identities | `PersistenceModelBuilderTests.cs:763-767`; `PersistenceIntegrationTests.cs:109-117` `Assert.DoesNotContain` OrderRepository | PASS |
| PersistencePass SHALL register in ClassificationAndPromotionStage without modifying 5A classifier passes (PK-43) | Name Persistence; CreateDefault order Contract → Persistence → Relation; 5A pass files untouched in diff | `PersistencePassTests.cs:28`; `:55-57` contract < persistence < relation; `PipelineStages.cs:18-23` | PASS |
| WHEN Classification completes THEN StageResult fact/relation counts include persistence output (PK-44) | FactCount/RelationCount equal persistence facts/relations; classification count ≥ persistence shard sizes | `PersistencePassTests.cs:96-97`; `:135-140` | PASS |
| IF two clones of the same tree are analyzed THEN persistence identities are equal (PK-45) | Equal facts/relations/candidates/unresolved; also document-order stable | `PersistenceDeterminismTests.cs:67-70`; `:117-121` | PASS |
| IF the same solution is analyzed twice THEN canonical payloads are byte-identical (PK-46) | SequenceEqual per persistence canonical key; Storage round-trip equals original facts/relations | `PersistenceDeterminismTests.cs:99-102`; `PersistenceRoundTripTests.cs:40-43` | PASS |
| Package SHALL NOT contain an absolute filesystem path in persistence content (PK-47) | Clone root / slash / JSON-escaped forms absent | `PersistenceDeterminismTests.cs:72-73` and `:105-107` `AssertNoAbsolutePath` | PASS |
| Canonical payloads SHALL NOT contain OrderSqlQueries.ConnectionString or appsettings OrdersDb values; only redacted suspected-secret evidence (PK-48) | Both secrets absent; excerpts contain `***` or `[REDACTED]` | `PersistenceIsolationTests.cs:46`; `:56`; `:84-88` | PASS |
| Csharp2Md.Analysis SHALL NOT expose Microsoft.CodeAnalysis through the persistence classifier; Cli SHALL not reference Domain (PK-49) | PersistencePass/Emitter/Builder not exported; no CodeAnalysis member types; Domain absent from CLI ProjectReference | `PersistenceIsolationTests.cs:103-105`; `:120-122`; `:145` | PASS |
| WHERE in-memory adapter is used, classifier still produces facts/relations and creates no files (PK-50) | File-set hash unchanged; DataStores/DataObjects non-empty; accesses-data present | `PersistenceIsolationTests.cs:161`; `:170-174` | PASS |
| WHEN Classification completes THEN diagnostics envelope names recognized and resolved data-access counts (PK-51) | Code `persistence-coverage`; message names both counts | `PersistenceEmitterTests.cs:600-612`; `PersistenceIntegrationTests.cs:123-125`; `PersistenceModelBuilderTests.cs:788-789` 3 recognized / 2 resolved | PASS |
| WHEN a recognized occurrence does not resolve THEN coverage diagnostic names that occurrence's owning symbol identity (PK-52) | Unresolved owner id in message / CoverageCounts | `PersistenceEmitterTests.cs:607`; `PersistenceModelBuilderTests.cs:790` | PASS |
| Coverage diagnostic SHALL NOT report a percentage or a pass/fail verdict (PK-53) | No `%` / percent / pass\|fail\|verdict; CoverageCounts members are raw counts only | `PersistenceEmitterTests.cs:608-610`; `PersistenceIntegrationTests.cs:126-128`; `PersistenceModelBuilderTests.cs:802` | PASS |

**Status**: All 53 ACs covered. 1 spec-precision gap flagged (PK-39/PK-40 derivation; see note).

PK-39/PK-40 spec-precision: the spec distinguishes interpolation-hole vs unread leading keyword. After extraction, both shapes can reach the classifier as `operation=unknown` without `sql-*` entries. `PersistenceModelBuilder.cs:84-88` derives kind/cause from what the occurrence reached (object-but-unnamed-op → `operates-on`/`InsufficientEvidence`; no object → `accesses-data`/`NoCandidateFound`). Tests still assert the specified UnresolvedRecord kind/cause for the fixture interpolated-table case (`SelectAllFrom`) and the unread-keyword/context-only case. No `// SPEC_DEVIATION` marker. Not scored as an uncovered AC.

PK-04 empty `sql-columns`: `DataAccessDetector.cs:293-297` omits the entry when the column list is empty (`DELETE`/`EXEC`/`SELECT *`). `StructuralLiteral` rejects empty canonical text, so an empty `sql-columns` value is not Domain-feasible. SELECT with columns is asserted (`Id|Status`). Same Domain constraint pattern as EBC-06; not scored as an uncovered AC.

EvidenceMethod.Semantic on PK-32 is a Create-time guard. Tests assert classifier identity, source/target, and `derived_from`, plus `ConfirmedRelation.Create(..., EvidenceMethod.Semantic)` equality. PK-28/PK-29 e2e DTOs assert `EvidenceMethod == "Configured"`.

T23 added unused `OrderDbContext? context = null` on `ConfigureHost` so PK-11's fixture callable names the context in its signature (`Program.cs:44-51`). Independent Test still holds: exactly one DataStore named `OrdersDb`. Unit tests cover both the signature path and the payload-`context-type` path. Legitimate fixture-callable fix, not scope creep.

---

## Discrimination Sensor

| Mutation | File:line | Description | Killed? |
| -------- | --------- | ----------- | ------- |
| — | — | Not run | SKIPPED |

**Sensor depth**: skipped
**Result**: SKIPPED (standing user request for csharp2md, same as prior features including `entrypoints-boundaries-contracts`). No git worktree, no file mutation, no Stryker.

Static gap analysis only (`dotnet-test:test-gap-analysis` step 4 without 4b live mutation):

- Flipping PK-39/PK-40 kind/cause on the fixture `SelectAllFrom` payload (`entity-type` present, no `sql-target`) would fail `PersistenceIntegrationTests.cs:100-104` (`operates-on`/`InsufficientEvidence`) and `PersistenceModelBuilderTests.cs:710-712`. The unread-keyword synthetic (context-type only) would fail `:728-731`. Unverified (static reasoning).
- Removing `AddEnclosingEntityType` from the `Property(...)` arm in `EfMappingPayload.cs:50-53` would likely survive `EfMappingPayloadTests` because `Entity<T>()` still emits `entity-type` and `HasColumnName` walks the enclosing Entity itself. Unverified (static reasoning); not treated as an uncovered AC (PK-07 Independent Test does not name Property; HasColumnName/ToTable/Entity are asserted).
- PK-01 `Assert.All` only checks that an `operation` key exists; a rogue value such as `save` on an untested access would survive that test. Closed-set membership is asserted by PK-35 `OperationKind` (`PersistenceModelBuilderTests.cs:616-618`) and by the specific `insert`/`read`/`unknown` extraction tests. Unverified (static reasoning).
- `EvidenceMethod.Semantic` on `accesses-data` is construction-killed by Domain `RequireSufficientEvidence` (taxonomy minimum Semantic), same pattern as EBC-07.

---

## Interactive UAT Results

Not performed. This feature is backend/CLI infrastructure; automated checks are sufficient per validate.md.

---

## Code Quality

| Principle | Status |
| --------- | ------ |
| Minimum code | PASS |
| Surgical changes | PASS |
| No scope creep | PASS |
| Matches patterns | PASS |
| Spec-anchored outcome check (asserted values match spec) | PASS (PK-39/PK-40 derivation flagged) |
| Per-layer Coverage Expectation met (classification 1:1 ACs + edges; e2e happy+edge; storage round-trip) | PASS |
| Every test maps to a spec requirement - no unclaimed tests | PASS |
| Documented guidelines followed: `AGENTS.md` / `CLAUDE.md` (net10.0, Workspaces.MSBuild 5.6.0, no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults`, SyntheticSolution-only versioned fixture, LocalCorpus skip when clones absent) | PASS |

Diff `343a248^..7b064e2` stays on Analysis classification/extraction/pipeline, fixture SQL/config surface, Storage relation reconstitution (`mapping-role` facet axis; persistence facts in `IndexDerivedFacts`), mechanical `PK-` trait allowlists, and matching tests. No Domain descriptor table or `contracts/taxonomy-registry.json` byte changes (AD-013). No `Microsoft.Build.*` and no `MSBuildLocator`. Target framework `net10.0`; Roslyn package remains `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0.

`AnalyzePackageWriteTests.cs` dropped `Assert.True(result.Snapshot.Unresolved.IsEmpty)` because `SelectAllFrom` now emits an unresolved record. STOR-49 requires a schema-valid package and exit 0, not an empty unresolved set. PersistenceIntegrationTests asserts the SelectAllFrom record. Not a PK SPEC_DEVIATION.

`ComposabilityTests` and `FullClassifierPipelineTests` were updated so the 5A order includes PersistencePass and unresolved records may be `operates-on`. Required by PK-43 and the fixture SQL rewrite.

Spot-check (P1 store/object/field/operation): assertions name `OrdersDb`, `order_headers`/`order_status` ExplicitConfirmation, conventional OrderLines/Id/Amount CandidateLinks, shared PayOrder/Reprice update, SelectAllFrom unresolved, OrderRepository absence. Not trait-only.

---

## Edge Cases

- [x] DbContext-derived type with no DbSet and no data access naming it: no DataStore (PK-10, PK-13) — `PersistenceModelBuilderTests.cs:120`
- [x] ToTable with non-constant argument: conventional DbSet name (PK-16) — `PersistenceModelBuilderTests.cs:207-208`
- [x] HasColumnName with non-constant argument: CLR property name, ConventionalCandidate (PK-26) — `PersistenceModelBuilderTests.cs:560-561`
- [x] Same entity type exposed by two DbSet members: two unmerged DataObjects (PK-14, PK-20) — `PersistenceModelBuilderTests.cs:223-225`; `PersistenceEmitterTests.cs:187`
- [x] LINQ projection into an anonymous type: each reached property still a DataField (PK-22) — `PersistenceModelBuilderTests.cs:421`
- [x] Tracked assignment to a type that is not an exposed entity: no DataField and no update (PK-23, PK-42) — `PersistenceModelBuilderTests.cs:471`
- [x] Two DataObjects with the same store/form/schema/name/mapping state: SnapshotAccumulator deduplicates without corruption (PK-38) — `PersistenceEmitterTests.cs:164-167`
- [x] Persistence fact identity colliding with a structurally different fact: SnapshotAccumulator reports structural corruption (existing workstream 4 invariant) — `SnapshotAccumulatorTests.cs:42`

Independent Test extras that are stronger than the numbered ACs (GetOrder `read` + PlaceOrder `insert` + EXEC `execute` on the published package; coverage denominator equals DataAccess count) are covered at unit/builder/extraction layers. PersistenceIntegrationTests asserts the Success Criteria named in T23, not every Independent Test sentence. Not scored as uncovered ACs.

---

## Gate Check

- **Gate command**: `dotnet build` then per-csproj `dotnet test` (multi-csproj `dotnet test` hits MSB1008). CLI used VSTest `--filter "Category!=LocalCorpus"` (xUnit 2.9.3 + `xunit.runner.visualstudio`, not xUnit v3 MTP `--filter-not-trait`).
- **Result**: 1158 passed, 0 failed, 0 skipped among selected tests
- **Per-project passed counts**:
  - `Csharp2Md.Domain.Tests`: 545 passed
  - `Csharp2Md.Analysis.Tests`: 417 passed
  - `Csharp2Md.Storage.Tests`: 166 passed
  - `Csharp2Md.Cli.Tests`: 27 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Projection.Tests`: 3 passed
- **Test count before feature** (EBC, `Category!=LocalCorpus`): 1019
- **Test count after feature**: 1158
- **Delta**: +139 executed tests. Increase; no silent deletions.
- **Skipped tests**: `LocalCorpusAnalyzeTests` (2 theory cases, `[Trait("Category", "LocalCorpus")]`) excluded by the CLI filter for the gate. See LocalCorpus section for the post-gate run.
- **Failures**: none on the serial gate.

---

## Fix Plans

None. The PK-39/PK-40 derivation is recorded as a spec-precision gap, not an uncovered AC. Tests assert the specified UnresolvedRecord kind/cause for the interpolated-table and unread-keyword shapes.

---

## Requirement Traceability Update

Applied: PK-01..PK-53 status is Verified in `spec.md`.

| Requirement | Previous Status | New Status |
| ----------- | --------------- | ---------- |
| PK-01..PK-53 | Pending / Implementing | ✅ Verified |

---

## LocalCorpus

`fixtures/eShop` is absent. `fixtures/eShopOnContainers` exists as a directory (`ApiGateways`, `BuildingBlocks`, `Services`, `Web`, …) but does not contain `eShopOnContainers-ServicesAndWebApps.sln` (no `.sln`/`.slnx` under that tree).

Post-gate command: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category=LocalCorpus"`.

Both theory cases threw `$XunitDynamicSkip$… clone is not present at '<solution path>'`. This runner surfaces that token as a failed `InvalidOperationException` rather than an xUnit skip. Intended skip when the expected solution file is missing, matching `LocalCorpusAnalyzeTests.cs:17-20`. eShopOnContainers was not analyzed because the solution file the test requires is absent, not because analyze failed. Clones were not added to git. Not a persistence-knowledge product failure.

---

## Summary

**Overall**: Ready

**Spec-anchored check**: 53/53 ACs matched spec outcome | 1 spec-precision gap
**Sensor**: SKIPPED (standing user request)
**Gate**: 1158 passed

**What works**: Persistence evidence reaches the ledger. Acme.Orders yields one `OrdersDb` relational store, explicit `order_headers`/`order_status` maps-to, conventional OrderLines/Id/Amount candidates, unmerged SQL `Orders`, shared PayOrder/Reprice update, SelectAllFrom unresolved, no OrderRepository persistence records, coverage counts without a percentage, clone/retry determinism, secret absence, in-memory isolation, and Storage round-trip of persistence facts and relations.

**Issues found**: PK-39 vs PK-40 cannot be distinguished from payload alone; kind/cause is derived from whether the occurrence reached an object (`PersistenceModelBuilder.cs:84-88`). Fixture cases still assert the specified records.

**Next steps**: Feature is verified. Workstreams 5B/5D and workstream 8 wait on an explicit start.
