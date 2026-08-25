# Factual Storage Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and
Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the
full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**User-confirmed deviation from the standard flow:** the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by standing user request, as it was for
`symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`,
`knowledge-taxonomy-contract` and `engine-bootstrap`. No mutants are injected at any point. The Verifier's
spec-anchored outcome check, per-AC `file:line` evidence, and `validation.md` report still run as normal.

**Phase-end quality gate (user-confirmed, applies to every phase):** the last task of each phase - T5, T12,
T19, T26, T32, T39, T43, T50, T52 - additionally runs `dotnet-skills:slopwatch` over that phase's changes and
reports clean before the phase is considered complete. This is in addition to each task's own `Tools` list.

---

**Spec**: `.specs/features/factual-storage/spec.md`
**Context**: `.specs/features/factual-storage/context.md`
**Design**: `.specs/features/factual-storage/design.md`
**Status**: Approved

**Scope of this task list**: the whole feature. All 61 requirements `STOR-01` through `STOR-61` are broken
down here; nothing is deferred to a later pass. The approved design is the implementation contract.

**JsonSchemaExporter note (Execute):** use `System.Text.Json.Schema.JsonSchemaExporter.GetJsonSchemaAsNode`
with the source-generated `JsonTypeInfo` from `StorageJsonContext`, as `design.md` specifies. Some
documentation samples still show an instance `GetJsonSchema` API; do not copy those. Re-query net10 docs
at Execute. Do not add JsonSchema.Net or NJsonSchema.

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/Csharp2Md.Storage.Tests/Isolation/StorageIsolationTests.cs`,
> `InMemoryTransactionalStoreTests.cs`, `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisPublicSurfaceTests.cs`,
> `Storage/TransactionalStorePortTests.cs`, `Pipeline/CommitOnceTests.cs`, `Pipeline/NoFilesystemWriteTests.cs`,
> `tests/Csharp2Md.Cli.Tests/AnalyzeOptionSurfaceTests.cs`, `AnalyzeSuccessTests.cs`,
> `tests/Csharp2Md.Domain.Tests/Registry/RegistryDriftGateTests.cs`,
> `Surface/RequirementCoverageTests.cs`) and project guidelines. Guidelines found: `AGENTS.md` and `CLAUDE.md`
> (they route test quality to the `dotnet-test:*` skills as post-hoc gates and declare no coverage
> threshold), `Directory.Build.props:7` (`TreatWarningsAsErrors`), `.config/dotnet-tools.json` (slopwatch).
> No coverage-threshold tool config and no CI workflow exist in this repository, so strong defaults apply
> to the Coverage Expectation column. Trait IDs for new tests are `STOR-nn`. Existing `ENG-*` tests that
> this feature supersedes are rewritten in the task that replaces their contract, not deleted silently.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Project wiring (`*.csproj` references, EmbeddedResource, `launchSettings.json`) | none | Build gate only | `src/Csharp2Md.*/*.csproj`, `src/Csharp2Md.Cli/Properties/launchSettings.json` | build gate only |
| Wire DTOs and `JsonSerializerContext` | none | Build gate only - records with no branches; contract is the schema drift gate | `src/Csharp2Md.Storage/Wire/*.cs` | build gate only |
| Canonical JSON, DomainMapper, PackageValidator | unit | All branches; 1:1 to STOR-01..13, STOR-25..33, STOR-44, STOR-45; every listed abort/quarantine fixture | `tests/Csharp2Md.Storage.Tests/Wire/*Tests.cs`, `Mapping/*Tests.cs`, `Validation/*Tests.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Committed JSON Schema files | unit | STOR-01..08: one schema per registered fact type, observation kind, relation record and envelope; `schema_version` 1; drift gate names the differing file | `tests/Csharp2Md.Storage.Tests/Schema/*Tests.cs`, `contracts/json-schema/**/*.json` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| In-memory adapter and storage port | unit | All branches; 1:1 to STOR-15, STOR-24, STOR-59..61 and the in-memory side of STOR-06/10/16/44/45 | `tests/Csharp2Md.Storage.Tests/**/*Tests.cs`, `tests/Csharp2Md.Analysis.Tests/Storage/*Tests.cs`, `Pipeline/*Tests.cs` | matching `dotnet test tests/Csharp2Md.<X>.Tests/Csharp2Md.<X>.Tests.csproj` |
| Filesystem adapter | unit | All branches; 1:1 to STOR-14, STOR-16..23, STOR-31, STOR-39..46, STOR-50, STOR-56..59; temp directories only, never `src/` | `tests/Csharp2Md.Storage.Tests/Filesystem/*Tests.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Factual package reader | unit | 1:1 to STOR-34..38; Domain equality on the happy path; named rejection with no partial snapshot | `tests/Csharp2Md.Storage.Tests/Reading/*Tests.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Assembly isolation (Domain ref, no classifier, Analysis surface, CLI no Domain) | unit | 1:1 to STOR-11, STOR-12, STOR-35, STOR-53; each forbidden type or reference named in the failure | `tests/Csharp2Md.*.Tests/Isolation/*Tests.cs` | matching `dotnet test tests/Csharp2Md.<X>.Tests/Csharp2Md.<X>.Tests.csproj` |
| Analysis facade (snapshot, rejected-publication catch, Persistence stub) | unit | 1:1 to STOR-31, STOR-50 (stub Empty), ENG tests rewritten onto `Stage(FactualSnapshot)` | `tests/Csharp2Md.Analysis.Tests/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| CLI `analyze --output` | unit | 1:1 to STOR-47..55; exit codes 0/1/2; writes only under the requested root; forbidden leftover options stay absent | `tests/Csharp2Md.Cli.Tests/**/*Tests.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj` |
| Requirement traceability | unit | Every STOR-01..61 is carried by at least one `[Trait("Requirement", "STOR-nn")]` | `tests/Csharp2Md.Storage.Tests/Surface/RequirementCoverageTests.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Committed registry (`contracts/taxonomy-registry.json`) | none | Correctness is the existing Domain drift gate plus STOR-09 | `contracts/taxonomy-registry.json` | build gate only |

## Gate Check Commands

> Generated from the repository's own build and test entry points (`csharp2md.slnx`, `global.json`,
> `tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`). No CI workflow file exists. The test
> stack is xUnit 2.9.3 on `Microsoft.NET.Test.Sdk` (VSTest). Core.Tests is gone; `dotnet test csharp2md.slnx`
> is a valid green gate. No `.editorconfig` exists, so `dotnet format` enforces whitespace and import
> ordering only.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks whose tests live in one test project | `dotnet test tests/Csharp2Md.<X>.Tests/Csharp2Md.<X>.Tests.csproj` for the project the task names |
| Full | After tasks spanning more than one test project | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj` |
| Build | After phase completion and for project- or DTO-only tasks | `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then the Full command |
| Solution | After the port cut (T27 onward) and at feature close | `dotnet test csharp2md.slnx` |

Every task carrying the `build` gate is a phase-end task, so it also runs `dotnet-skills:slopwatch` over the
phase's changes per the protocol above. The tool is pinned locally in `.config/dotnet-tools.json`.

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a
phase execute in the listed order. Each phase diagram shows the intra-phase chain; `Depends on` in a task body
names its execution predecessor, and the first task of each phase depends on the last task of the phase before
it.

### Phase 1: Port payload and assembly wiring

Analysis and Storage reference Domain. The snapshot and rejected-publication types exist. Isolation tests
invert the bootstrap Domain ban. `Stage` still takes `StagedFragment` until Phase 5.

```
T1 → T2 → T3 → T4 → T5
```

### Phase 2: Wire DTOs and canonical bytes

JSON records for every Domain family and the package envelopes. No serializer context yet.

```
T6 → T7 → T8 → T9 → T10 → T11 → T12
```

### Phase 3: Serializer, schemas and mapper

Source-generated context, committed JSON Schemas, registry embed drift, DomainMapper round-trips.

```
T13 → T14 → T15 → T16 → T17 → T18 → T19
```

### Phase 4: Commit-time validator

Abort-class gates from fixture JSON, then quarantine versus remainder.

```
T20 → T21 → T22 → T23 → T24 → T25 → T26
```

### Phase 5: Port cut and in-memory last gate

`Stage(FactualSnapshot)` becomes the production input. In-memory commit maps, validates, and publishes.
Persistence stages Empty. The engine treats `PublicationRejectedException` as structural corruption.

```
T27 → T28 → T29 → T30 → T31 → T32
```

### Phase 6: Filesystem adapter

Production store: child directories, staging swap, clobber protection, layout, cleanup.

```
T33 → T34 → T35 → T36 → T37 → T38 → T39
```

### Phase 7: Factual package reader

Read-back through Domain `Create`. Analysis public surface does not grow a reader.

```
T40 → T41 → T42 → T43
```

### Phase 8: CLI `--output`

Required option, filesystem default adapter, empty package under the requested root, exit codes.

```
T44 → T45 → T46 → T47 → T48 → T49 → T50
```

### Phase 9: Traceability close

Every STOR-nn is carried by a trait. Solution-wide gate is green.

```
T51 → T52
```

---

## Task Breakdown

### Phase 1: Port payload and assembly wiring

#### T1: Analysis references Domain ✅

**What**: Add a `ProjectReference` from `Csharp2Md.Analysis` to `Csharp2Md.Domain` and remove `Csharp2Md.Domain`
from `AnalysisIsolationTests` forbidden project references (AD-016). Keep Storage, Projection and CLI forbidden.
**Where**: `src/Csharp2Md.Analysis/Csharp2Md.Analysis.csproj`
**Depends on**: None
**Reuses**: `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisIsolationTests.cs` member-data pattern
**Requirement**: STOR-11

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:

- [x] Analysis.csproj declares a project reference to Domain
- [x] `AnalysisCsproj_DeclaresNoProjectReferenceTo` no longer lists Domain and still names Storage, Projection and CLI
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): reference Domain for typed storage snapshots`

---

#### T2: Add FactualSnapshot ✅

**What**: Add `FactualSnapshot` on the Analysis storage surface with `Empty`, the six Domain arrays from
design.md, and `Merge` that concatenates arrays. Collision is not a merge error.
**Where**: `src/Csharp2Md.Analysis/Storage/FactualSnapshot.cs`
**Depends on**: T1
**Reuses**: Domain public types (`IFact`, `Observation`, `ConfirmedRelation`, `CandidateLink`,
`UnresolvedRecord`, `OpenFrontier`); existing record style in `StagedFragment.cs`
**Requirement**: STOR-10

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`, `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:csharp-nullable-reference-types`

**Done when**:

- [x] `Empty` has default empty arrays for every property
- [x] `Merge` concatenates; a test concatenates two snapshots that share an identity and does not throw
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): add factual snapshot as the staging payload`

---

#### T3: Add PublicationRejectedException ✅

**What**: Add `PublicationRejectedException(string gate, string detail)` with `Gate` and `Detail` on the
Analysis storage surface. Gates are the names in design.md (`schema`, `unregistered-kind`,
`identity-collision`, `content-hash`, `absolute-path`, `construction`, `io`, `session-state`,
`not-a-package`, `lock`).
**Where**: `src/Csharp2Md.Analysis/Storage/PublicationRejectedException.cs`
**Depends on**: T2
**Reuses**: nothing
**Requirement**: STOR-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] Constructor stores `Gate` and `Detail`; they are readable after throw/catch
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): name publication rejection by gate`

---

#### T4: Storage references Domain and embeds the registry ✅

**What**: Add a `ProjectReference` from `Csharp2Md.Storage` to `Csharp2Md.Domain`. Embed
`contracts/taxonomy-registry.json` as an `EmbeddedResource`. Invert `StorageIsolationTests`: Storage must
reference Domain; Storage must expose no classifier, promoter or extractor type. Drop the ENG-28
opaque-byte round-trip and the "does not expose Domain" assertions (superseded by AD-016).
**Where**: `src/Csharp2Md.Storage/Csharp2Md.Storage.csproj`
**Depends on**: T3
**Reuses**: `tests/Csharp2Md.Storage.Tests/Isolation/StorageIsolationTests.cs`;
`tests/Csharp2Md.Domain.Tests/Isolation/DomainIsolationTests.cs` name-scan pattern
**Requirement**: STOR-11, STOR-12

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:

- [x] Storage.csproj references Domain and embeds `contracts/taxonomy-registry.json`
- [x] Isolation tests assert the Domain reference and name any classifier/promoter/extractor type they find
- [x] Opaque-byte and Domain-ban tests are gone, not skipped
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): reference Domain and embed the taxonomy registry`

---

#### T5: Allowlist snapshot types on the Analysis surface ✅

**What**: Add `FactualSnapshot` and `PublicationRejectedException` to
`AnalysisPublicSurfaceTests` allowed public type names. Keep pass/classifier/adapter/stage names forbidden.
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisPublicSurfaceTests.cs`
**Depends on**: T4
**Reuses**: existing allowlist in that file
**Requirement**: STOR-35

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] The two new types are allowlisted; no other new public Analysis type appears
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Phase-end slopwatch is clean over Phase 1
- [x] Test count recorded (no silent deletions): Analysis.Tests 86 passed; solution 664 passed, 0 failed, 0 skipped

**Tests**: unit
**Gate**: build

**Commit**: `test(analysis): allowlist factual snapshot types on the public surface`

---

### Phase 2: Wire DTOs and canonical bytes

#### T6: CanonicalJson encoding ✅

**What**: Add `CanonicalJson.Write<T>` / `Read<T>` that emit and parse UTF-8 with no BOM, `\n` newlines and
indent 2. Snake_case and unmapped-member rejection land in T13 with the source-generated context; this task
locks the byte rules.
**Where**: `src/Csharp2Md.Storage/Wire/CanonicalJson.cs`
**Depends on**: T5
**Reuses**: `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyRegistryWriter.cs` (UTF-8 no BOM, `\n`, indent 2)
**Requirement**: STOR-44

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:serialization`, `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Written bytes have no UTF-8 BOM, use `\n` only, and indent with two spaces
- [x] Round-trip of a small DTO preserves values
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 19 passed, 0 failed (4 new STOR-44 tests)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): add canonical JSON byte encoding`

---

#### T7: Structural fact DTOs ✅

**What**: Add wire DTOs for Solution, Project, Document and Symbol, including `content_sha256` and identity
fields. Private Domain constructors stay unsourced; these records are the JSON shape.
**Where**: `src/Csharp2Md.Storage/Wire/StructuralFactDtos.cs`
**Depends on**: T6
**Reuses**: `src/Csharp2Md.Domain/Facts/Structural/StructuralFacts.cs` public properties
**Requirement**: STOR-01

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:serialization`

**Done when**:

- [x] One DTO per structural fact type with snake_case-ready property names
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release`
- [x] Test count recorded (no silent deletions) — no tests in this task; Release build 0 warnings, 0 errors

**Tests**: none
**Gate**: build

**Commit**: `feat(storage): add structural fact wire DTOs`

---

#### T8: Architecture fact DTOs ✅

**What**: Add wire DTOs for Component, DeploymentUnit, EntryPoint, BoundaryOperation and ExternalSystem.
**Where**: `src/Csharp2Md.Storage/Wire/ArchitectureFactDtos.cs`
**Depends on**: T7
**Reuses**: `src/Csharp2Md.Domain/Facts/Architecture/ComponentFacts.cs`,
`src/Csharp2Md.Domain/Facts/Architecture/BoundaryFacts.cs`
**Requirement**: STOR-01

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:serialization`

**Done when**:

- [x] One DTO per architecture fact type
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release`
- [x] Test count recorded (no silent deletions) — no tests in this task; Release build 0 warnings, 0 errors

**Tests**: none
**Gate**: build

**Commit**: `feat(storage): add architecture fact wire DTOs`

---

#### T9: Contract, persistence and configuration DTOs ✅

**What**: Add wire DTOs for Contract, ContractBinding, ContractRevision, DataStore, DataObject, DataField,
DataOperation and ConfigurationBinding.
**Where**: `src/Csharp2Md.Storage/Wire/DerivedFactDtos.cs`
**Depends on**: T8
**Reuses**: `src/Csharp2Md.Domain/Facts/Contracts/ContractFacts.cs`,
`src/Csharp2Md.Domain/Facts/Persistence/PersistenceFacts.cs`,
`src/Csharp2Md.Domain/Facts/Configuration/ConfigurationBinding.cs`
**Requirement**: STOR-01

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:serialization`

**Done when**:

- [x] One DTO per remaining fact type in STOR-01
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release`
- [x] Test count recorded (no silent deletions) — no tests in this task; Release build 0 warnings, 0 errors

**Tests**: none
**Gate**: build

**Commit**: `feat(storage): add contract persistence and configuration wire DTOs`

---

#### T10: Observation DTO ✅

**What**: Add the observation wire DTO covering identity, kind (`WireName`), payload, locator, evidence
method, diagnostic, document hash and extractor version.
**Where**: `src/Csharp2Md.Storage/Wire/ObservationDto.cs`
**Depends on**: T9
**Reuses**: `src/Csharp2Md.Domain/Observations/Observation.cs`;
`src/Csharp2Md.Domain/Registry/ObservationKindDescriptor.cs` `WireName`
**Requirement**: STOR-02

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:serialization`

**Done when**:

- [x] Kind is stored as the registry `WireName`, not the enum's ToString
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release`
- [x] Test count recorded (no silent deletions) — no tests in this task; Release build 0 warnings, 0 errors

**Tests**: none
**Gate**: build

**Commit**: `feat(storage): add observation wire DTO`

---

#### T11: Relation DTOs ✅

**What**: Add wire DTOs for ConfirmedRelation, CandidateLink, UnresolvedRecord and OpenFrontier. Confirmed
relation DTO includes evidence method and fields Domain `Create` needs (AD-015).
**Where**: `src/Csharp2Md.Storage/Wire/RelationDtos.cs`
**Depends on**: T10
**Reuses**: `src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs`, `CandidateLink.cs`, `UnresolvedRecord.cs`,
`OpenFrontier.cs`; `RelationDescriptor.WireName`
**Requirement**: STOR-03

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:serialization`

**Done when**:

- [x] One DTO per STOR-03 record; confirmed kind uses registry `WireName`
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release`
- [x] Test count recorded (no silent deletions) — no tests in this task; Release build 0 warnings, 0 errors

**Tests**: none
**Gate**: build

**Commit**: `feat(storage): add relation wire DTOs`

---

#### T12: Envelope DTOs ✅

**What**: Add wire DTOs for manifest, coverage, run_certification, diagnostics, quarantine and measurements
as design.md specifies. Manifest entries use relative forward-slash paths and roles `payload` | `manifest`.
**Where**: `src/Csharp2Md.Storage/Wire/EnvelopeDtos.cs`
**Depends on**: T11
**Reuses**: design.md Data Models section
**Requirement**: STOR-04

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:serialization`

**Done when**:

- [x] All six envelope DTOs exist; coverage metrics are the four named zeros
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes`
- [x] Phase-end slopwatch is clean over Phase 2
- [x] Test count recorded (no silent deletions) — 668 passed, 0 failed (Storage.Tests 19; 4 new STOR-44 tests from T6)

**Tests**: none
**Gate**: build

**Commit**: `feat(storage): add package envelope wire DTOs`

---

### Phase 3: Serializer, schemas and mapper

#### T13: Source-generated StorageJsonContext ✅

**What**: Add `StorageJsonContext` with `JsonSourceGenerationOptions`:
`PropertyNamingPolicy = SnakeCaseLower`, `UnmappedMemberHandling = Disallow`, indented, `\n`. Register every
wire DTO. Point `CanonicalJson` at this context.
**Where**: `src/Csharp2Md.Storage/Wire/StorageJsonContext.cs`
**Depends on**: T12
**Reuses**: T6 `CanonicalJson`; net10 `JsonSerializerContext`
**Requirement**: STOR-07

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:serialization`

**Done when**:

- [x] Every wire DTO is `[JsonSerializable]`
- [x] `CanonicalJson` serializes through the context; a test round-trips one DTO and rejects an unknown property
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 22 passed, 0 failed (was 19; +3 STOR-07)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): add source-generated JSON context for the wire`

---

#### T14: Emit committed JSON Schemas and a drift gate ✅

**What**: Add a Storage.Tests emitter (same home as `TaxonomyRegistryWriter`) that calls
`JsonSchemaExporter.GetJsonSchemaAsNode` on each DTO's `JsonTypeInfo`, writes UTF-8 no BOM / `\n` / indent 2,
injects `schema_version` 1, and commits files under `contracts/json-schema/`. Add a drift test that names the
differing file. Enumerate registered fact types, observation kinds and relation records against that
directory.
**Where**: `tests/Csharp2Md.Storage.Tests/Schema/JsonSchemaEmitter.cs`
**Depends on**: T13
**Reuses**: `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyRegistryWriter.cs`;
`TaxonomyTables.Default` fact types, `ObservationKindTable`, `RelationTable`
**Requirement**: STOR-01, STOR-02, STOR-03, STOR-04, STOR-07, STOR-08

**Tools**:

- MCP: `user-context7`
- Skill: `dotnet-skills:serialization`

**Done when**:

- [x] Committed schemas exist for all 17 fact types, 10 observation kinds, 4 relation records and 6 envelopes
- [x] Every schema file declares `schema_version` 1 and lives under `contracts/`
- [x] Drift gate fails by file name when an emitted schema differs
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 29 passed, 0 failed (was 22; +7 schema/drift)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): publish versioned JSON schemas for the factual wire`

---

#### T15: Embedded registry matches the committed file ✅

**What**: Assert the Storage embedded `taxonomy-registry.json` bytes equal
`contracts/taxonomy-registry.json`. Re-run the Domain registry drift gate so STOR-09 stays true.
**Where**: `tests/Csharp2Md.Storage.Tests/Schema/EmbeddedRegistryDriftTests.cs`
**Depends on**: T14
**Reuses**: `tests/Csharp2Md.Domain.Tests/Registry/RegistryDriftGateTests.cs`; T4 EmbeddedResource
**Requirement**: STOR-05, STOR-09

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Embed bytes equal the committed registry file; the failure names both paths
- [x] `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` still passes the registry drift gate
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 31 passed, 0 failed (was 29; +2); Domain.Tests 544 passed, 0 failed

**Tests**: unit
**Gate**: full

**Commit**: `test(storage): drift-gate the embedded taxonomy registry`

---

#### T16: Map an empty snapshot to envelopes ✅

**What**: Add `DomainMapper.ToWire` / `FromWire` for `FactualSnapshot.Empty`: envelopes with count 0, coverage
zeros, `run_certification.status = not_evaluated`, empty payload dictionaries. `FromWire` returns Empty
through Domain `Create` of nothing.
**Where**: `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`
**Depends on**: T15
**Reuses**: `FactualSnapshot.Empty`; envelope DTOs; `TaxonomyVersions.Initial`
**Requirement**: STOR-06, STOR-13

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:serialization`

**Done when**:

- [x] Empty ToWire has no payload records and every family count 0 on the manifest
- [x] FromWire of that document equals `FactualSnapshot.Empty` under Domain equality
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 35 passed, 0 failed (was 31; +4)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): map an empty factual snapshot to package envelopes`

---

#### T17: Round-trip every fact type through Domain Create ✅

**What**: Extend `DomainMapper` so each of the 17 fact types ToWire/FromWire equals the original under Domain
equality. Reconstruction calls public `Create` only. Add a Storage.Tests ProjectReference to Domain if
needed for fixtures.
**Where**: `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`
**Depends on**: T16
**Reuses**: Domain `*.Create` factories; Domain.Tests construction helpers where they exist
**Requirement**: STOR-01, STOR-10, STOR-13

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] One fixture per fact type round-trips under Domain equality
- [x] No test constructs a Domain fact except through `Create`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 52 passed, 0 failed (was 35; +17 fact round-trips)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): round-trip every fact type through Domain construction`

---

#### T18: Round-trip observations and relations ✅

**What**: Extend `DomainMapper` for all 10 observation kinds and ConfirmedRelation, CandidateLink,
UnresolvedRecord, OpenFrontier. Confirmed relations reconstruct through `ConfirmedRelation.Create` with
evidence method and materialized facts when the shape requires them (AD-015).
**Where**: `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`
**Depends on**: T17
**Reuses**: `ObservationKindDescriptor.WireName`; `RelationDescriptor.WireName`;
`tests/Csharp2Md.Domain.Tests/Relations/ConfirmedRelationTests.cs` fixtures
**Requirement**: STOR-02, STOR-03, STOR-10, STOR-13

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] One fixture per observation kind and each relation record round-trips under Domain equality
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 66 passed, 0 failed (was 52; +10 observation kinds, +4 relation records)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): round-trip observations and relations through Domain construction`

---

#### T19: Attach and verify content_sha256 ✅

**What**: When writing a payload record, set `content_sha256` to SHA-256 of the canonical JSON object with
that property omitted. `CanonicalJson` / mapper share that rule. A test mutates the hash and shows a mismatch
the validator will use in T22.
**Where**: `src/Csharp2Md.Storage/Wire/CanonicalJson.cs`
**Depends on**: T18
**Reuses**: T6 encoding; `SHA256` from `System.Security.Cryptography`
**Requirement**: STOR-28, STOR-42

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:serialization`, `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Hash is over the canonical object without `content_sha256`
- [x] Two writes of the same record produce identical hashes; a tampered hash does not match
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Phase-end slopwatch is clean over Phase 3
- [x] Test count recorded (no silent deletions) — Storage.Tests 68 passed, 0 failed (was 66; +2 content-hash); BUILD Release 0 warnings; `dotnet format --verify-no-changes`; solution 717 passed, 0 failed; slopwatch 0 issues

**Tests**: unit
**Gate**: build

**Commit**: `feat(storage): attach canonical content hashes on payload records`

---

### Phase 4: Commit-time validator

#### T20: Abort on schema failure ✅

**What**: Add `PackageValidator.Validate` that deserializes with `UnmappedMemberHandling.Disallow` and throws
`PublicationRejectedException` with gate `schema`, naming the artifact key. Tests drive fixture JSON, not a
production "stage raw bytes" API.
**Where**: `src/Csharp2Md.Storage/Validation/PackageValidator.cs`
**Depends on**: T19
**Reuses**: `PublicationRejectedException`; `StorageJsonContext`; design.md "STOR-25/26/27/28/29/30 tests call
this validator directly"
**Requirement**: STOR-25

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Unknown property or type-mismatch fixture aborts with gate `schema` and the artifact key in `Detail`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 70 passed, 0 failed (was 68; +2 schema abort)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): abort commit when a payload fails its schema`

---

#### T21: Abort on unregistered kind ✅

**What**: Validator rejects a fact type, observation kind or relation kind absent from the taxonomy registry
with gate `unregistered-kind`, naming the unregistered value.
**Where**: `src/Csharp2Md.Storage/Validation/PackageValidator.cs`
**Depends on**: T20
**Reuses**: `TaxonomyRegistry` / `TaxonomyTables.Default`
**Requirement**: STOR-26

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] One fixture per of fact type, observation kind and relation kind names the unregistered value
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 73 passed, 0 failed (was 70; +3 unregistered-kind)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): abort commit for kinds absent from the registry`

---

#### T22: Abort on identity collision and hash mismatch ✅

**What**: Validator aborts gate `identity-collision` when two facts in one document share one identity
string, naming the identity. Aborts gate `content-hash` when `content_sha256` does not match, naming the
identity.
**Where**: `src/Csharp2Md.Storage/Validation/PackageValidator.cs`
**Depends on**: T21
**Reuses**: T19 hash helper
**Requirement**: STOR-27, STOR-28

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Collision fixture names the identity; bad-hash fixture names the identity
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 75 passed, 0 failed (was 73; +2 collision/hash)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): abort commit on identity collision and content-hash mismatch`

---

#### T23: Abort on absolute filesystem paths ✅

**What**: Validator scans every JSON string; a rooted path (`/`, `\`, or `X:`) fails gate `absolute-path`,
naming the field.
**Where**: `src/Csharp2Md.Storage/Validation/PackageValidator.cs`
**Depends on**: T22
**Reuses**: Domain relative-path guards as the documented complement, not a substitute
**Requirement**: STOR-29

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Fixtures covering `/`, `\` and `C:` abort and name the field
- [x] A relative path in a Domain path field does not abort
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 79 passed, 0 failed (was 75; +4 absolute-path)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): abort commit when canonical payloads contain absolute paths`

---

#### T24: Abort on structural construction failure ✅

**What**: When `FromWire` cannot `Create` a Structural fact or an observation (including allowlist/secret
failure), validator aborts gate `construction`, naming the identity. Architecture/Contract/Persistence/
Configuration facts and confirmed relations are not abort-class here (T25).
**Where**: `src/Csharp2Md.Storage/Validation/PackageValidator.cs`
**Depends on**: T23
**Reuses**: Domain `Create` exceptions; `DomainMapper.FromWire`
**Requirement**: STOR-30

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] An out-of-allowlist structural literal aborts and is not stored as a fact
- [x] An invalid observation construction aborts similarly
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 81 passed, 0 failed (was 79; +2 construction abort)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): abort commit when structural facts or observations fail Create`

---

#### T25: Quarantine invalid derived records ✅

**What**: An Architecture, Contract, Persistence or Configuration fact or a confirmed relation that fails
schema or Domain construction is written to quarantine with a named diagnostic, omitted from canonical
payloads, and `run_certification.status = failed`. Remaining valid artifacts stay in the document.
**Where**: `src/Csharp2Md.Storage/Validation/PackageValidator.cs`
**Depends on**: T24
**Reuses**: quarantine envelope DTO; design.md abort vs quarantine split
**Requirement**: STOR-32

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] One invalid derived fact among valid structural facts yields quarantine + remaining facts
- [x] Certification is `failed`; the invalid record is absent from canonical payloads
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 82 passed, 0 failed (was 81; +1 quarantine)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): quarantine invalid derived facts and still keep the remainder`

---

#### T26: Candidates and unknowns are not abort-class ✅

**What**: A document with candidates, unresolved records or open frontiers and no abort-class failure
validates successfully. Certification stays `not_evaluated` when quarantine is empty.
**Where**: `src/Csharp2Md.Storage/Validation/PackageValidator.cs`
**Depends on**: T25
**Reuses**: T18 relation fixtures
**Requirement**: STOR-33

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Candidate-only and frontier-only fixtures validate without throwing
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Phase-end slopwatch is clean over Phase 4
- [x] Test count recorded (no silent deletions) — Storage.Tests 84 passed, 0 failed (was 82; +2 candidates/frontiers); BUILD Release 0 warnings; `dotnet format --verify-no-changes`; solution 733 passed, 0 failed; slopwatch 0 issues

**Tests**: unit
**Gate**: build

**Commit**: `feat(storage): accept candidates and frontiers as a valid package`

---

### Phase 5: Port cut and in-memory last gate

#### T27: Switch Stage to FactualSnapshot ✅

**What**: Replace `IStoreSession.Stage(StagedFragment)` with `Stage(FactualSnapshot)`. Merge staged snapshots
in session. Update `InMemoryTransactionalStore`, `PersistenceStub`, `CountingStore`, and every
`Stage(StagedFragment)` test call site so the solution compiles. `StagedFragment` remains the published
artifact view on `CommittedPublication`.
**Where**: `src/Csharp2Md.Analysis/Storage/ITransactionalStore.cs`
**Depends on**: T26
**Reuses**: `FactualSnapshot.Merge`; `tests/Csharp2Md.Analysis.Tests/Storage/TransactionalStorePortTests.cs`
**Requirement**: STOR-15

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] Port tests assert `Stage` takes `FactualSnapshot`
- [x] Existing Analysis and Storage tests compile; fragment-staging call sites are rewritten, not skipped
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Analysis.Tests 86 passed, 0 failed; Storage.Tests 84 passed, 0 failed (unchanged vs T26); Full 733 passed, 0 failed (Domain 544 + Analysis 86 + Storage 84 + Projection 3 + Cli 16)

**Tests**: unit
**Gate**: full

**Commit**: `feat(analysis): stage factual snapshots instead of opaque fragments`

---

#### T28: In-memory commit maps and validates ✅

**What**: `InMemoryTransactionalStore.Commit` runs DomainMapper + PackageValidator, fills
`ArtifactsInPublicationOrder` with canonical shard bytes then manifest (ordinal keys), omits empty family
shards, copies registry bytes. Creates no files (STOR-24). Empty and candidate-only snapshots commit.
Narrow the production-source System.IO scan so Wire/Mapping/Validation/InMemory still forbid `System.IO`;
the filesystem adapter is not in this task.
**Where**: `src/Csharp2Md.Storage/InMemoryTransactionalStore.cs`
**Depends on**: T27
**Reuses**: `PackageValidator`, `DomainMapper`, `CanonicalJson`; existing ENG-21 isolation tests rewritten
onto snapshots
**Requirement**: STOR-06, STOR-15, STOR-16, STOR-24, STOR-33, STOR-50

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Empty commit: omitted shards, manifest last, registry copy present, family counts 0
- [x] Candidate-only commit succeeds; in-memory run creates no files or directories
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 87 passed, 0 failed (was 84; +3 empty/candidate/no-files); Analysis.Tests 86 passed, 0 failed; Full 736 passed, 0 failed (Domain 544 + Analysis 86 + Storage 87 + Projection 3 + Cli 16)

**Tests**: unit
**Gate**: full

**Commit**: `feat(storage): commit in-memory snapshots through the last gate`

---

#### T29: Session-state and overlapping Open ✅

**What**: Second `Commit` or `Stage` after `Commit` throws `PublicationRejectedException` gate
`session-state`. Overlapping `Open` of the same solution key while a session has neither committed nor
aborted throws gate `lock`.
**Where**: `src/Csharp2Md.Storage/InMemoryTransactionalStore.cs`
**Depends on**: T28
**Reuses**: existing commit-once tests in `InMemoryTransactionalStoreTests.cs`
**Requirement**: STOR-59, STOR-60, STOR-61

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Double Commit, Stage-after-Commit, and overlapping Open are named rejections
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 89 passed, 0 failed (was 87; +2 stage-after-commit and overlapping Open)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): reject overlapping opens and post-commit session use`

---

#### T30: Persistence stub stages Empty ✅

**What**: `PersistenceStub` calls `context.Session.Stage(FactualSnapshot.Empty)` instead of staging a
manifest fragment. Default pipeline zeros stay zeros.
**Where**: `src/Csharp2Md.Analysis/Pipeline/StubStages.cs`
**Depends on**: T29
**Reuses**: `FactualSnapshot.Empty`; `tests/Csharp2Md.Analysis.Tests/Pipeline/DefaultPipelineZerosTests.cs`,
`PersistenceManifestTests.cs`
**Requirement**: STOR-50

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Stub commit of the default pipeline is a schema-valid empty publication (manifest last)
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Analysis.Tests 87 passed, 0 failed (was 86; +1 schema-valid empty package)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): stage an empty snapshot from the persistence stub`

---

#### T31: Engine treats publication rejection as structural corruption ✅

**What**: `AnalyzeSolutionAsync` wraps `Commit` in try/catch for `PublicationRejectedException`, then
`Abort`, `Unpublished`, `StructuralCorruption = true`. A store whose `Commit` throws that exception leaves
the prior publication byte-identical and publishes no new manifest.
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T30
**Reuses**: `tests/Csharp2Md.Analysis.Tests/Pipeline/StructuralCorruptionTests.cs`
**Requirement**: STOR-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Thrown `PublicationRejectedException` yields Unpublished + StructuralCorruption + Abort
- [x] Prior in-memory publication is unchanged; no new manifest artifact is stored
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Analysis.Tests 88 passed, 0 failed (was 87; +1 publication-rejection)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(analysis): mark unpublished when the last gate rejects publication`

---

#### T32: Staging order does not change canonical bytes ✅

**What**: Rewrite `StagingOrderTests` and the in-memory order test so two `Merge` orders of the same Domain
graph produce byte-identical canonical payload files (exclude measurements). Manifest still last.
**Where**: `tests/Csharp2Md.Analysis.Tests/Pipeline/StagingOrderTests.cs`
**Depends on**: T31
**Reuses**: T18 fixtures; `CanonicalJson` bytes
**Requirement**: STOR-44, STOR-45

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Two staging orders of the same facts and observations yield identical payload bytes
- [x] Measurements may differ; every other shard matches
- [x] Gate check passes: `dotnet test csharp2md.slnx`
- [x] Phase-end slopwatch is clean over Phase 5
- [x] Test count recorded (no silent deletions) — Analysis.Tests 88 passed, 0 failed; Storage.Tests 89 passed, 0 failed; BUILD Release 0 warnings; `dotnet format --verify-no-changes`; solution 740 passed, 0 failed (was 733; +7 across Phase 5); slopwatch 0 issues

**Tests**: unit
**Gate**: build

**Commit**: `test(storage): prove canonical payloads are order-independent`

---

### Phase 6: Filesystem adapter

#### T33: Filesystem empty commit under a hashed child ✅

**What**: Add `FilesystemTransactionalStore(string outputRoot)`. Child directory is `s-` plus the first 32
hex chars of SHA-256(UTF-8 canonical solution key), lowercase. Create a missing output root. Successful
empty commit writes the tree in design.md: registry copy, envelopes, omitted empty family shards, manifest
last inside the staging tree then swap. Writes only under `outputRoot`.
**Where**: `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs`
**Depends on**: T32
**Reuses**: in-memory session rules; `PackageValidator`; embedded registry bytes
**Requirement**: STOR-05, STOR-06, STOR-14, STOR-16, STOR-19, STOR-22, STOR-50

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-nullable-reference-types`

**Done when**:

- [x] N = 1 still uses a child directory, not the output root itself
- [x] Child name is not a display name and not an absolute path
- [x] Empty package is schema-valid: registry bytes match committed file, family counts 0, manifest last
- [x] Missing output root is created
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 91 passed, 0 failed (was 89; +2 empty filesystem commit)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): publish an empty package through the filesystem adapter`

---

#### T34: Refuse a file root and a non-package child ✅

**What**: If `outputRoot` is an existing file, refuse naming that path (`io` / `not-a-package` as specified).
If the solution child exists without `manifest.json`, refuse and leave the path byte-identical.
**Where**: `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs`
**Depends on**: T33
**Reuses**: T33 temp-directory test helpers
**Requirement**: STOR-20, STOR-21

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] File-as-root is named in the exception detail; no write occurs
- [x] Non-package child is unchanged after refusal
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 93 passed, 0 failed (was 91; +2 file-root and non-package refusal)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): refuse to clobber a file root or a non-package directory`

---

#### T35: Atomic replace and abort preserve the last package ✅

**What**: Implement staging directory + `.bak` swap from design.md. A second successful commit replaces the
child so no mix of old and new artifacts is readable. Abort or failed validate deletes staging, releases the
lock, and leaves the last successful package byte-identical; no new manifest is published.
**Where**: `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs`
**Depends on**: T34
**Reuses**: design.md Commit protocol steps 5–11
**Requirement**: STOR-17, STOR-18, STOR-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Commit twice: second tree fully replaces the first; no mixed shards
- [x] Forced validation failure after a successful commit: prior files unchanged
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 96 passed, 0 failed (was 93; +3 atomic replace, failed validate, abort preserve)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): replace packages atomically and preserve the last valid tree`

---

#### T36: I/O errors abort and preserve ✅

**What**: When create/write/replace fails (permission, simulated I/O), throw gate `io`, name the error, abort,
and leave the last valid package unchanged.
**Where**: `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs`
**Depends on**: T35
**Reuses**: T35 preserve assertions
**Requirement**: STOR-23

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A forced I/O failure during staging names `io` and keeps the prior package
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 97 passed, 0 failed (was 96; +1 staging I/O abort)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): abort filesystem publication on I/O failure`

---

#### T37: Exclusive lock on overlapping Open ✅

**What**: Overlapping `Open` of the same child uses an exclusive `FileStream` on `<child>.lock` and throws
gate `lock`, naming the root. Release on Commit and Abort.
**Where**: `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs`
**Depends on**: T36
**Reuses**: T29 in-memory lock semantics
**Requirement**: STOR-59

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Second Open of the same child while the first is live is a named `lock` rejection
- [x] After Commit or Abort, a new Open succeeds
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 99 passed, 0 failed (was 97; +2 exclusive FileStream lock)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): lock a solution child while a session is open`

---

#### T38: Compact layout invariants ✅

**What**: Assert the published tree: partition by fact family, observation `WireName`, confirmed relation
`WireName`; no directory named for a fact or observation identity; no catalogs, postings, Markdown, source
or `retrieval.md`; timestamps only in measurements; payload serialized once (no duplicated canonical
record bytes). Narrow the Storage System.IO scan to allow this adapter and the reader, still forbidding IO
in InMemory/Wire/Mapping.
**Where**: `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs`
**Depends on**: T37
**Reuses**: observation and relation `WireName` tables
**Requirement**: STOR-39, STOR-40, STOR-41, STOR-42, STOR-43, STOR-46

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] A multi-family commit matches the partition layout in design.md
- [x] Tests fail if an identity-named directory or a posting/Markdown/source file appears
- [x] Canonical payload files contain no timestamp fields
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 100 passed, 0 failed (was 99; +1 compact layout)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): partition shards without identity-named directories`

---

#### T39: Abort and cancel delete staging residue ✅

**What**: Abort or cancellation deletes `<child>.staging` and the lock, and does not leave partial artifacts
for that solution. The committed child is untouched.
**Where**: `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs`
**Depends on**: T38
**Reuses**: `tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineCancellationTests.cs` cancellation pattern
**Requirement**: STOR-58

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Abort mid-stage: no `.staging` directory remains; last package unchanged
- [x] Cancelled engine run using this adapter leaves no staging residue
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Phase-end slopwatch is clean over Phase 6
- [x] Test count recorded (no silent deletions) — Storage.Tests 101 passed, 0 failed (was 100; +1 abort staging); Analysis.Tests 89 passed, 0 failed (was 88; +1 filesystem cancel); BUILD Release 0 warnings; `dotnet format --verify-no-changes`; solution 753 passed, 0 failed (was 740; +13 across Phase 6); slopwatch 0 issues

**Tests**: unit
**Gate**: build

**Commit**: `feat(storage): delete leftover staging on abort and cancellation`

---

### Phase 7: Factual package reader

#### T40: Read a committed package into Domain types ✅

**What**: Add `FactualPackageReader.Read(string packageDirectory)` returning `PackageReadResult` with
snapshot, quarantine, coverage and certification. Happy path: Domain equality with the written graph.
**Where**: `src/Csharp2Md.Storage/FactualPackageReader.cs`
**Depends on**: T39
**Reuses**: `PackageValidator`, `DomainMapper`; T17/T18 fixtures written via the filesystem adapter
**Requirement**: STOR-10, STOR-34

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`, `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Read-back of a committed fixture package equals the original snapshot under Domain equality
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 133 passed, 0 failed (was 101; +32 committed-package read-back)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): read a committed package back into Domain types`

---

#### T41: Reject truncated packages and non-packages ✅

**What**: Missing manifest or abort-class gate failure: named rejection, no partial snapshot. A random
directory is `not-a-package` naming the path.
**Where**: `src/Csharp2Md.Storage/FactualPackageReader.cs`
**Depends on**: T40
**Reuses**: T20–T24 fixture kinds; T34 non-package path
**Requirement**: STOR-36, STOR-37

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Truncated package (manifest removed) names the gate and returns no snapshot
- [x] Random directory names the path; result has no snapshot
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 136 passed, 0 failed (was 133; +3 unreadable-package rejection)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): reject unreadable packages without a partial snapshot`

---

#### T42: Expose quarantine separately from the snapshot ✅

**What**: When the package contains quarantined records, `Read` returns the valid Domain snapshot and
exposes quarantine separately from confirmed facts and relations.
**Where**: `src/Csharp2Md.Storage/FactualPackageReader.cs`
**Depends on**: T41
**Reuses**: T25 quarantine fixture committed through the filesystem adapter
**Requirement**: STOR-38

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Snapshot omits the quarantined derived record; `Quarantine` contains it
- [x] Certification is `failed`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Storage.Tests 137 passed, 0 failed (was 136; +1 quarantine-separated read)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(storage): return quarantined records separately from the snapshot`

---

#### T43: Analysis surface does not include the reader ✅

**What**: Assert `FactualPackageReader` is not an exported Analysis type. Keep CLI free of a Domain
project reference (re-asserted in T47).
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisPublicSurfaceTests.cs`
**Depends on**: T42
**Reuses**: T5 allowlist
**Requirement**: STOR-35

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Public Analysis types do not include `FactualPackageReader` or `PackageReadResult`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Phase-end slopwatch is clean over Phase 7
- [x] Test count recorded (no silent deletions) — Analysis.Tests 90 passed, 0 failed (was 89; +1 reader-absent); Storage.Tests 137 passed, 0 failed; BUILD Release 0 warnings; `dotnet format --verify-no-changes`; Full 790 passed, 0 failed (Domain 544 + Analysis 90 + Storage 137 + Projection 3 + Cli 16; was 753; +37 across Phase 7); slopwatch 0 issues

**Tests**: unit
**Gate**: build

**Commit**: `test(analysis): keep the factual reader off the Analysis public surface`

---

### Phase 8: CLI `--output`

#### T44: Require --output ✅

**What**: `analyze` requires `--output <dir>` in addition to `--solution`. Missing `--output` exits 1 and
names `--output` on stderr. Update every existing analyze invocation (except this missing-option test) to
pass `--output` to a temp directory. ENG-45's prohibition of `--output` is removed; other names on that list
stay forbidden (full STOR-52 in T47).
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`
**Depends on**: T43
**Reuses**: `tests/Csharp2Md.Cli.Tests/AnalyzeInvocationErrorTests.cs`;
`AnalyzeOptionSurfaceTests.cs`; `AnalyzeCommandTreeTests.cs`
**Requirement**: STOR-47, STOR-48

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [x] `--output` is required; omitting it exits 1 naming `--output`
- [x] Product options are `--solution` and `--output`
- [x] Existing CLI tests pass with a temp `--output`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Cli.Tests 18 passed, 0 failed (was 16; +2 required `--output` surface and missing-option)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cli): require --output on analyze`

---

#### T45: Default engine uses the filesystem adapter ✅

**What**: When no engine is injected, construct `new AnalysisEngine(new FilesystemTransactionalStore(output))`.
Injected-engine tests still require `--output` so the surface does not lie; they ignore the path for
publication.
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`
**Depends on**: T44
**Reuses**: `FilesystemTransactionalStore`; existing `CreateRootCommand(IAnalysisEngine? engine)`
**Requirement**: STOR-14, STOR-47

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Production `CreateRootCommand()` without an engine writes through the filesystem adapter
- [x] Injected engine still requires `--output` and does not construct a Domain snapshot in CLI
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Cli.Tests 20 passed, 0 failed (was 18; +2 production write and injected-engine ignore)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cli): compose the filesystem store from --output`

---

#### T46: Write a schema-valid empty package only under --output ✅

**What**: Successful `analyze --solution <fixture> --output <dir>` exits 0, writes a schema-valid empty
package per solution under that root (registry copy, manifest, counts 0), and does not create, modify or
delete files outside that root. Reuse the working-tree hash from `NoFilesystemWriteTests`, excluding the
output directory.
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`
**Depends on**: T45
**Reuses**: `tests/Csharp2Md.Analysis.Tests/Pipeline/NoFilesystemWriteTests.cs`;
`tests/Csharp2Md.Cli.Tests/AnalyzeSuccessTests.cs`; `FactualPackageReader`
**Requirement**: STOR-49, STOR-50, STOR-51

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Built-tool invoke on the synthetic fixture exits 0 with a readable empty package under `--output`
- [x] Working-tree snapshot excluding the output root is unchanged
- [x] In-memory AnalysisEngine runs still write nothing (ENG-16 / STOR-24)
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Cli.Tests 21 passed, 0 failed (was 20; +1 schema-valid empty package under `--output`); Analysis.Tests 90 passed, 0 failed

**Tests**: unit
**Gate**: full

**Commit**: `feat(cli): write a schema-valid empty package under --output`

---

#### T47: Keep leftover options gone and CLI off Domain ✅

**What**: CLI still has no `--topic`, `--domain`, `--manifest`, `--trust`,
`--include-source-generators` or `--analysis-timeout`. CLI.csproj still has no Domain project reference.
**Where**: `tests/Csharp2Md.Cli.Tests/AnalyzeOptionSurfaceTests.cs`
**Depends on**: T46
**Reuses**: `tests/Csharp2Md.Cli.Tests/Isolation/CliIsolationTests.cs`
**Requirement**: STOR-52, STOR-53

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Removed-option list is those six names; `--output` is allowed
- [x] CliIsolationTests still asserts no Domain reference
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Cli.Tests 21 passed, 0 failed (unchanged; STOR-52/STOR-53 traits on existing assertions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(cli): keep markdown-era options gone and Domain off the CLI`

---

#### T48: Unknowns exit 0; structural abort exits 2 ✅

**What**: Unknowns/candidates/frontiers with a committed package exit 0. Structural unpublished for any
requested solution exits 2. Drive the structural path with an injected engine or store that throws
`PublicationRejectedException` (the stub pipeline cannot fail Empty).
**Where**: `tests/Csharp2Md.Cli.Tests/AnalyzeExitCodeTests.cs`
**Depends on**: T47
**Reuses**: existing ENG-42/ENG-43 tests; `PublicationRejectedException`
**Requirement**: STOR-54, STOR-55

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [x] Unknowns + committed → exit 0 and a package exists when using the filesystem adapter
- [x] Structural abort → exit 2
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [x] Test count recorded (no silent deletions) — Cli.Tests 23 passed, 0 failed (was 21; +2 unknowns+package and PublicationRejectedException → 2)

**Tests**: unit
**Gate**: quick

**Commit**: `test(cli): exit 0 for unknowns and 2 for structural publication abort`

---

#### T49: Point launchSettings at a gitignored output

**What**: Add `--output artifacts/analyze-out` (already gitignored via `artifacts/`) to
`launchSettings.json` commandLineArgs. Keep `--solution` against the synthetic fixture.
**Where**: `src/Csharp2Md.Cli/Properties/launchSettings.json`
**Depends on**: T48
**Reuses**: `.gitignore` `artifacts/`; existing launch profile
**Requirement**: STOR-47

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Profile args include `--output artifacts/analyze-out` and still start with `analyze --solution `
- [ ] `AnalyzeOptionSurfaceTests.LaunchSettings_*` accepts `--output` and still forbids the leftover names
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `chore(cli): default launchSettings --output to artifacts/analyze-out`

---

#### T50: Isolate multi-solution commit and abort

**What**: Two `--solution` values commit or abort independently under their own children. One solution's
`PublicationRejectedException` does not block the other from committing. CLI exit 2 if any unpublished.
**Where**: `tests/Csharp2Md.Cli.Tests/AnalyzeMultiSolutionTests.cs`
**Depends on**: T49
**Reuses**: `tests/Csharp2Md.Analysis.Tests/Pipeline/MultiSolutionIsolationTests.cs`; T31 catch path
**Requirement**: STOR-56, STOR-57

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Two solutions, second Commit throws: first child is a valid package, second child unchanged or absent
- [ ] Process exit code is 2
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`
- [ ] Phase-end slopwatch is clean over Phase 8
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `test(cli): isolate per-solution commit when one publication aborts`

---

### Phase 9: Traceability close

#### T51: Cover STOR-01 through STOR-61 with traits

**What**: Add Storage.Tests `RequirementCoverageTests` mirroring Domain's TAX coverage: every `STOR-01`
through `STOR-61` is carried by at least one `[Trait("Requirement", "STOR-nn")]` in the test assemblies this
feature owns (Storage, Analysis, CLI), and every carried STOR trait is well-formed and in range.
**Where**: `tests/Csharp2Md.Storage.Tests/Surface/RequirementCoverageTests.cs`
**Depends on**: T50
**Reuses**: `tests/Csharp2Md.Domain.Tests/Surface/RequirementCoverageTests.cs`
**Requirement**: STOR-01, STOR-02, STOR-03, STOR-04, STOR-05, STOR-06, STOR-07, STOR-08, STOR-09, STOR-10, STOR-11, STOR-12, STOR-13, STOR-14, STOR-15, STOR-16, STOR-17, STOR-18, STOR-19, STOR-20, STOR-21, STOR-22, STOR-23, STOR-24, STOR-25, STOR-26, STOR-27, STOR-28, STOR-29, STOR-30, STOR-31, STOR-32, STOR-33, STOR-34, STOR-35, STOR-36, STOR-37, STOR-38, STOR-39, STOR-40, STOR-41, STOR-42, STOR-43, STOR-44, STOR-45, STOR-46, STOR-47, STOR-48, STOR-49, STOR-50, STOR-51, STOR-52, STOR-53, STOR-54, STOR-55, STOR-56, STOR-57, STOR-58, STOR-59, STOR-60, STOR-61

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Uncovered STOR-nn list is empty; malformed IDs fail the format scanner
- [ ] Domain registry drift gate still passes (STOR-09)
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `test(storage): close STOR-01 through STOR-61 requirement traits`

---

#### T52: Green solution

**What**: `dotnet test csharp2md.slnx` passes. No new public Analysis types beyond the T5 allowlist. No
Storage classifier types. CLI still has no Domain reference.
**Where**: `tests/Csharp2Md.Storage.Tests/Isolation/StorageIsolationTests.cs`
**Depends on**: T51
**Reuses**: isolation tests from T4, T5, T43, T47
**Requirement**: STOR-11, STOR-12, STOR-35, STOR-53

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`

**Done when**:

- [ ] `dotnet test csharp2md.slnx` exits 0
- [ ] Isolation pins above still pass
- [ ] Phase-end slopwatch is clean over Phase 9
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `test(storage): keep the factual-storage solution green`

---

## Phase Execution Map

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 7 → Phase 8 → Phase 9

Phase 1:  T1 → T2 → T3 → T4 → T5
Phase 2:  T6 → T7 → T8 → T9 → T10 → T11 → T12
Phase 3:  T13 → T14 → T15 → T16 → T17 → T18 → T19
Phase 4:  T20 → T21 → T22 → T23 → T24 → T25 → T26
Phase 5:  T27 → T28 → T29 → T30 → T31 → T32
Phase 6:  T33 → T34 → T35 → T36 → T37 → T38 → T39
Phase 7:  T40 → T41 → T42 → T43
Phase 8:  T44 → T45 → T46 → T47 → T48 → T49 → T50
Phase 9:  T51 → T52
```

Execution is strictly sequential. Packing into ~7-task batches at Execute (whole phases, never split).
Phase 7 (4) does not pack with Phase 8 (7) because that would be 11 tasks. Phase 1 (5) does not pack with
Phase 2 (7) because that would be 12.

| Batch | Phases | Tasks |
| --- | --- | --- |
| 1 | Phase 1 | T1–T5 (5) |
| 2 | Phase 2 | T6–T12 (7) |
| 3 | Phase 3 | T13–T19 (7) |
| 4 | Phase 4 | T20–T26 (7) |
| 5 | Phase 5 | T27–T32 (6) |
| 6 | Phase 6 | T33–T39 (7) |
| 7 | Phase 7 | T40–T43 (4) |
| 8 | Phase 8 | T44–T50 (7) |
| 9 | Phase 9 | T51–T52 (2) |

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1 | 1 csproj + isolation assertion | ✅ Granular |
| T2 | 1 type | ✅ Granular |
| T3 | 1 exception type | ✅ Granular |
| T4 | 1 csproj + isolation invert | ✅ Granular |
| T5 | 1 allowlist test | ✅ Granular |
| T6 | 1 encoding helper | ✅ Granular |
| T7–T12 | 1 DTO file each | ✅ Granular |
| T13 | 1 serializer context | ✅ Granular |
| T14 | 1 emitter + committed schemas | ⚠️ emitter + artifacts, one drift gate |
| T15 | 1 drift test | ✅ Granular |
| T16–T19 | mapper/hash slices of one pipeline | ✅ Granular |
| T20–T26 | one validator method, one gate family each | ✅ Granular |
| T27 | 1 port signature + call-site compile fix | ⚠️ cohesive cut, many call sites |
| T28–T32 | in-memory last gate, one invariant each | ✅ Granular |
| T33–T39 | filesystem adapter, one protocol step each | ✅ Granular |
| T40–T43 | reader, one behavior each | ✅ Granular |
| T44–T50 | CLI, one behavior each | ✅ Granular |
| T51–T52 | coverage + solution pin | ✅ Granular |

**Granularity check**: T14 pairs the emitter with committed schema files because STOR-08 is a byte comparison
against those files. T27 is the Stage-signature cut; splitting it would leave a non-compiling tree.

---

## Diagram-Definition Cross-Check

Intra-phase `Depends on` is a linear predecessor chain matching each phase fence. Cross-phase: T6 depends on
T5, T13 on T12, T20 on T19, T27 on T26, T33 on T32, T40 on T39, T44 on T43, T51 on T50.

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | (start) | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | (cross-phase) | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T7 | T7 → T8 | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T9 | T9 → T10 | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T12 | (cross-phase) | ✅ Match |
| T14 | T13 | T13 → T14 | ✅ Match |
| T15 | T14 | T14 → T15 | ✅ Match |
| T16 | T15 | T15 → T16 | ✅ Match |
| T17 | T16 | T16 → T17 | ✅ Match |
| T18 | T17 | T17 → T18 | ✅ Match |
| T19 | T18 | T18 → T19 | ✅ Match |
| T20 | T19 | (cross-phase) | ✅ Match |
| T21 | T20 | T20 → T21 | ✅ Match |
| T22 | T21 | T21 → T22 | ✅ Match |
| T23 | T22 | T22 → T23 | ✅ Match |
| T24 | T23 | T23 → T24 | ✅ Match |
| T25 | T24 | T24 → T25 | ✅ Match |
| T26 | T25 | T25 → T26 | ✅ Match |
| T27 | T26 | (cross-phase) | ✅ Match |
| T28 | T27 | T27 → T28 | ✅ Match |
| T29 | T28 | T28 → T29 | ✅ Match |
| T30 | T29 | T29 → T30 | ✅ Match |
| T31 | T30 | T30 → T31 | ✅ Match |
| T32 | T31 | T31 → T32 | ✅ Match |
| T33 | T32 | (cross-phase) | ✅ Match |
| T34 | T33 | T33 → T34 | ✅ Match |
| T35 | T34 | T34 → T35 | ✅ Match |
| T36 | T35 | T35 → T36 | ✅ Match |
| T37 | T36 | T36 → T37 | ✅ Match |
| T38 | T37 | T37 → T38 | ✅ Match |
| T39 | T38 | T38 → T39 | ✅ Match |
| T40 | T39 | (cross-phase) | ✅ Match |
| T41 | T40 | T40 → T41 | ✅ Match |
| T42 | T41 | T41 → T42 | ✅ Match |
| T43 | T42 | T42 → T43 | ✅ Match |
| T44 | T43 | (cross-phase) | ✅ Match |
| T45 | T44 | T44 → T45 | ✅ Match |
| T46 | T45 | T45 → T46 | ✅ Match |
| T47 | T46 | T46 → T47 | ✅ Match |
| T48 | T47 | T47 → T48 | ✅ Match |
| T49 | T48 | T48 → T49 | ✅ Match |
| T50 | T49 | T49 → T50 | ✅ Match |
| T51 | T50 | (cross-phase) | ✅ Match |
| T52 | T51 | T51 → T52 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Assembly isolation + project wiring | unit | unit | ✅ OK |
| T2 | Analysis facade (snapshot) | unit | unit | ✅ OK |
| T3 | Analysis facade (exception) | unit | unit | ✅ OK |
| T4 | Assembly isolation + project wiring | unit | unit | ✅ OK |
| T5 | Assembly isolation | unit | unit | ✅ OK |
| T6 | Canonical JSON | unit | unit | ✅ OK |
| T7 | Wire DTOs | none | none | ✅ OK |
| T8 | Wire DTOs | none | none | ✅ OK |
| T9 | Wire DTOs | none | none | ✅ OK |
| T10 | Wire DTOs | none | none | ✅ OK |
| T11 | Wire DTOs | none | none | ✅ OK |
| T12 | Wire DTOs | none | none | ✅ OK |
| T13 | JsonSerializerContext + Canonical JSON | unit | unit | ✅ OK |
| T14 | Committed JSON Schema files | unit | unit | ✅ OK |
| T15 | Committed registry embed | unit | unit | ✅ OK |
| T16 | DomainMapper | unit | unit | ✅ OK |
| T17 | DomainMapper | unit | unit | ✅ OK |
| T18 | DomainMapper | unit | unit | ✅ OK |
| T19 | Canonical JSON | unit | unit | ✅ OK |
| T20 | PackageValidator | unit | unit | ✅ OK |
| T21 | PackageValidator | unit | unit | ✅ OK |
| T22 | PackageValidator | unit | unit | ✅ OK |
| T23 | PackageValidator | unit | unit | ✅ OK |
| T24 | PackageValidator | unit | unit | ✅ OK |
| T25 | PackageValidator | unit | unit | ✅ OK |
| T26 | PackageValidator | unit | unit | ✅ OK |
| T27 | Storage port | unit | unit | ✅ OK |
| T28 | In-memory adapter | unit | unit | ✅ OK |
| T29 | In-memory adapter | unit | unit | ✅ OK |
| T30 | Analysis facade / Persistence stub | unit | unit | ✅ OK |
| T31 | Analysis facade | unit | unit | ✅ OK |
| T32 | In-memory adapter / determinism | unit | unit | ✅ OK |
| T33 | Filesystem adapter | unit | unit | ✅ OK |
| T34 | Filesystem adapter | unit | unit | ✅ OK |
| T35 | Filesystem adapter | unit | unit | ✅ OK |
| T36 | Filesystem adapter | unit | unit | ✅ OK |
| T37 | Filesystem adapter | unit | unit | ✅ OK |
| T38 | Filesystem adapter | unit | unit | ✅ OK |
| T39 | Filesystem adapter | unit | unit | ✅ OK |
| T40 | Factual package reader | unit | unit | ✅ OK |
| T41 | Factual package reader | unit | unit | ✅ OK |
| T42 | Factual package reader | unit | unit | ✅ OK |
| T43 | Assembly isolation | unit | unit | ✅ OK |
| T44 | CLI `analyze --output` | unit | unit | ✅ OK |
| T45 | CLI `analyze --output` | unit | unit | ✅ OK |
| T46 | CLI `analyze --output` | unit | unit | ✅ OK |
| T47 | CLI + isolation | unit | unit | ✅ OK |
| T48 | CLI `analyze --output` | unit | unit | ✅ OK |
| T49 | Project wiring (`launchSettings.json`) | none | unit | ✅ OK |
| T50 | CLI multi-solution | unit | unit | ✅ OK |
| T51 | Requirement traceability | unit | unit | ✅ OK |
| T52 | Assembly isolation + solution green | unit | unit | ✅ OK |

T49 sets `Tests: unit` because `AnalyzeOptionSurfaceTests` already asserts launchSettings contents; the
matrix `none` row is for the JSON file itself, and the task's unit tests are that existing layer.

---
